#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using Object = UnityEngine.Object;

// 대용량 FBX/텍스처를 전송하지 않고 로컬 맵의 배치/높이/경로 정보를 ZIP으로 추출한다.
public sealed class L4DMapInfoExporter : EditorWindow
{
    private const string Folder = "Assets/Flooded_Grounds";
    [SerializeField] private SceneAsset _mapScene;
    [SerializeField] private bool _buildNavigation = true;
    private string _status;

    [Serializable] private sealed class AssetRef
    {
        public string path;
        public string guid;
    }
    [Serializable] private sealed class Geometry
    {
        public string hierarchy;
        public string type;
        public bool active;
        public Vector3 position;
        public Vector3 center;
        public Vector3 size;
        public AssetRef mesh;
        public AssetRef[] materials;
        public string[] shaders;
        public bool waterCandidate;
    }
    [Serializable] private sealed class HeightGrid
    {
        public string hierarchy;
        public AssetRef data;
        public Vector3 position;
        public Vector3 size;
        public int samples = 129;
        public float[] heights;
        public int treeCount;
    }
    [Serializable] private sealed class ScriptEntry
    {
        public string hierarchy;
        public string type;
        public AssetRef script;
    }
    [Serializable] private sealed class Report
    {
        public string schema = "L4D-local-map-report-1";
        public string unityVersion;
        public string scenePath;
        public string sceneGuid;
        public string sceneSha256;
        public string sceneFileFormat;
        public string sceneFile;
        public string pipelineType;
        public AssetRef pipeline;
        public string[] originalScenes;
        public string[] roots;
        public int navigationVertices;
        public int navigationIndices;
        public int navigationAreas;
        public float navigationVoxelSize;
        public float agentRadius;
        public float agentHeight;
        public bool navigationBuilt;
        public Geometry[] renderers;
        public Geometry[] colliders;
        public HeightGrid[] terrain;
        public ScriptEntry[] scripts;
        public string[] warnings;
    }

    [MenuItem("Tools/L4D 맵/새 맵 정보 추출", priority = 30)]
    public static void ShowWindow()
    {
        L4DMapInfoExporter window = GetWindow<L4DMapInfoExporter>("새 맵 정보 추출 v3");
        window.minSize = new Vector2(560f, 300f);
        if (window._mapScene == null) window.FindMapScene();
        window.Show();
    }

    private void FindMapScene()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) { _status = "Flooded_Grounds 폴더를 찾지 못했습니다. 에셋을 Import한 뒤 다시 실행하세요."; return; }
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { Folder });
        string best = null;
        long largest = -1;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string full = FullPath(path);
            long length = File.Exists(full) ? new FileInfo(full).Length : 0;
            if (length <= largest) continue;
            largest = length; best = path;
        }
        _mapScene = best != null ? AssetDatabase.LoadAssetAtPath<SceneAsset>(best) : null;
        _status = best != null ? "폴더에서 가장 큰 씬을 선택했습니다. 실제 맵 데모 씬인지 확인하세요."
            : "폴더에 데모 씬이 없습니다. Package Manager에서 맵의 Scenes/Models/Materials/Textures를 로컬 프로젝트에 Import하세요.";
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("대용량 맵을 올리지 않고 연결 정보만 보내기", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("모델·텍스처는 네 PC에 그대로 두고, 작은 정보 ZIP만 만듭니다. 원본 씬·기존 맵·에셋은 저장하거나 삭제하지 않습니다. (추출기 v2)", MessageType.Info);
        _mapScene = (SceneAsset)EditorGUILayout.ObjectField("Flooded Grounds 데모 씬", _mapScene, typeof(SceneAsset), false);
        if (GUILayout.Button("데모 씬 다시 찾기")) FindMapScene();
        _buildNavigation = EditorGUILayout.ToggleLeft("이동 경로 분석 포함 (처음에는 시간이 걸릴 수 있음)", _buildNavigation);
        if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, _mapScene != null ? MessageType.Info : MessageType.Warning);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("실행 전: Play 종료 → 열려 있는 씬 저장(Ctrl+S)", EditorStyles.wordWrappedLabel);
        using (new EditorGUI.DisabledScope(_mapScene == null || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("작은 정보 ZIP 만들기", GUILayout.Height(36f))) Export();
        }
    }

    private void Export()
    {
        string scenePath = AssetDatabase.GetAssetPath(_mapScene);
        if (string.IsNullOrEmpty(scenePath) || !scenePath.StartsWith(Folder + "/", StringComparison.Ordinal)
            || !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        { EditorUtility.DisplayDialog("새 맵 정보", "Flooded_Grounds 폴더 안의 실제 데모 씬을 선택하세요.", "확인"); return; }
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene open = SceneManager.GetSceneAt(i);
            if (open.isLoaded && (open.isDirty || string.IsNullOrEmpty(open.path)))
            { EditorUtility.DisplayDialog("씬 저장 필요", "열려 있는 씬을 먼저 저장(Ctrl+S)하고 다시 실행하세요. 저장하지 않은 작업이 사라지지 않게 여기서 중단합니다.", "확인"); return; }
        }
        if (setup.Length == 0) return;
        string output = EditorUtility.SaveFilePanel("맵 정보 ZIP 저장", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "FloodedGrounds_MapInfo", "zip");
        if (string.IsNullOrEmpty(output)) return;
        string fullOutput = Path.GetFullPath(output);
        string assets = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (fullOutput.StartsWith(assets, StringComparison.OrdinalIgnoreCase))
        { EditorUtility.DisplayDialog("저장 위치", "ZIP은 Assets 폴더 밖, 예를 들어 바탕화면에 저장하세요.", "확인"); return; }
        GameObject temporary = null;
        NavMeshData builtData = null;
        bool switched = false;
        bool completed = false;
        List<string> warnings = new List<string>();
        try
        {
            Progress("맵 씬을 여는 중", 0.05f);
            string sourceHash = HashFile(FullPath(scenePath));
            string[] originalScenes = new string[setup.Length];
            for (int i = 0; i < setup.Length; i++) originalScenes[i] = setup[i].path;
            switched = true;
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            Report report = new Report { unityVersion = Application.unityVersion, scenePath = scenePath,
                sceneGuid = AssetDatabase.AssetPathToGUID(scenePath), sceneSha256 = sourceHash,
                originalScenes = originalScenes, pipeline = Ref(GraphicsSettings.currentRenderPipeline),
                pipelineType = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.GetType().FullName : "Built-in" };
            List<Geometry> renderers = new List<Geometry>();
            List<Geometry> colliders = new List<Geometry>();
            List<HeightGrid> terrain = new List<HeightGrid>();
            List<ScriptEntry> scripts = new List<ScriptEntry>();
            report.roots = new string[roots.Length];
            for (int i = 0; i < roots.Length; i++)
            {
                Progress("배치·지형 정보 읽는 중", 0.1f + 0.35f * i / Mathf.Max(1, roots.Length));
                GameObject root = roots[i]; report.roots[i] = root.name;
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    // 오브젝트 하나가 이상해도 전체 추출은 멈추지 않고 경고로만 남긴다.
                    try { renderers.Add(ReadRenderer(renderer)); }
                    catch (Exception error) { warnings.Add("Renderer 건너뜀: " + Hierarchy(renderer.transform) + " (" + error.GetType().Name + ")"); }
                }
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                {
                    try
                    {
                        colliders.Add(new Geometry { hierarchy = Hierarchy(collider.transform), type = collider.GetType().Name,
                            active = collider.enabled && collider.gameObject.activeInHierarchy, position = collider.transform.position,
                            center = collider.bounds.center, size = collider.bounds.size,
                            mesh = Ref(collider is MeshCollider mesh ? mesh.sharedMesh : null) });
                    }
                    catch (Exception error) { warnings.Add("Collider 건너뜀: " + Hierarchy(collider.transform) + " (" + error.GetType().Name + ")"); }
                }
                foreach (Terrain ground in root.GetComponentsInChildren<Terrain>(true))
                {
                    if (ground.terrainData == null) continue;
                    HeightGrid grid = new HeightGrid { hierarchy = Hierarchy(ground.transform), data = Ref(ground.terrainData),
                        position = ground.transform.position, size = ground.terrainData.size, treeCount = ground.terrainData.treeInstanceCount };
                    grid.heights = new float[grid.samples * grid.samples];
                    for (int z = 0; z < grid.samples; z++)
                        for (int x = 0; x < grid.samples; x++)
                            grid.heights[z * grid.samples + x] = ground.terrainData.GetInterpolatedHeight((float)x / (grid.samples - 1), (float)z / (grid.samples - 1));
                    terrain.Add(grid);
                }
                foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (script == null) { warnings.Add("Missing script: " + Hierarchy(root.transform)); continue; }
                    try { scripts.Add(new ScriptEntry { hierarchy = Hierarchy(script.transform), type = script.GetType().FullName, script = Ref(MonoScript.FromMonoBehaviour(script)) }); }
                    catch (Exception error) { warnings.Add("Script 건너뜀: " + Hierarchy(script.transform) + " (" + error.GetType().Name + ")"); }
                }
            }
            report.renderers = renderers.ToArray(); report.colliders = colliders.ToArray();
            report.terrain = terrain.ToArray(); report.scripts = scripts.ToArray();
            NavMeshTriangulation navigation = default;
            if (_buildNavigation)
            {
                Progress("이동 경로 분석 중 — 큰 맵은 잠시 걸릴 수 있습니다", 0.5f);
                try
                {
                    foreach (GameObject root in roots)
                        foreach (NavMeshSurface existing in root.GetComponentsInChildren<NavMeshSurface>(true)) existing.enabled = false;
                    NavMesh.RemoveAllNavMeshData();
                    temporary = new GameObject("L4D_MapReport_TEMP");
                    SceneManager.MoveGameObjectToScene(temporary, scene);
                    NavMeshSurface surface = temporary.AddComponent<NavMeshSurface>();
                    surface.collectObjects = CollectObjects.All;
                    surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                    surface.layerMask = ~0;
                    if (NavMesh.GetSettingsCount() > 0) surface.agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID;
                    surface.overrideVoxelSize = true; surface.voxelSize = 0.25f;
                    surface.overrideTileSize = true; surface.tileSize = 128;
                    Physics.SyncTransforms();
                    surface.BuildNavMesh();
                    builtData = surface.navMeshData;
                    navigation = NavMesh.CalculateTriangulation();
                    NavMeshBuildSettings settings = surface.GetBuildSettings();
                    report.agentRadius = settings.agentRadius; report.agentHeight = settings.agentHeight;
                    report.navigationVoxelSize = surface.voxelSize;
                    report.navigationVertices = navigation.vertices != null ? navigation.vertices.Length : 0;
                    report.navigationIndices = navigation.indices != null ? navigation.indices.Length : 0;
                    report.navigationAreas = navigation.areas != null ? navigation.areas.Length : 0;
                    report.navigationBuilt = builtData != null && report.navigationVertices > 0;
                    if (!report.navigationBuilt) warnings.Add("NavMesh가 비어 있습니다. 지형·바닥 충돌체를 확인해야 합니다.");
                }
                catch (Exception error) { warnings.Add("NavMesh 분석: " + error.GetType().Name + " " + error.Message); }
            }
            else warnings.Add("이동 경로 분석을 끈 상태입니다.");
            report.warnings = warnings.ToArray();
            Progress("작은 ZIP 저장 중 (모델·텍스처 원본은 제외)", 0.9f);
            WriteZip(fullOutput, report, navigation);
            if (HashFile(FullPath(scenePath)) != sourceHash) throw new IOException("분석 중 원본 씬 파일이 바뀌었습니다. 원본을 확인하세요.");
            completed = true;
        }
        catch (OperationCanceledException) { _status = "추출을 취소했습니다."; }
        catch (Exception error)
        {
            _status = "추출 실패 (v2): " + error.Message + "\n\n" + FirstFrames(error);
            Debug.LogException(error);
            EditorUtility.DisplayDialog("맵 정보 추출 실패", _status, "확인");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (temporary != null) Object.DestroyImmediate(temporary);
            if (builtData != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(builtData))) Object.DestroyImmediate(builtData);
            if (switched)
            {
                // 분석 때문에 생긴 메모리 변경을 저장하지 않고 닫은 다음 원래 씬 구성을 다시 연다.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
        if (completed)
        {
            _status = "완료: " + new FileInfo(fullOutput).Length.ToString("N0") + " 바이트. 생성된 ZIP만 채팅에 첨부하세요.";
            EditorUtility.RevealInFinder(fullOutput);
            EditorUtility.DisplayDialog("맵 정보 추출 완료", _status, "확인");
        }
    }

    // 실패 위치를 알 수 있게 스택의 앞부분만 보여 준다.
    private static string FirstFrames(Exception error)
    {
        string trace = error.StackTrace ?? "";
        string[] lines = trace.Split('\n');
        return string.Join("\n", lines, 0, Mathf.Min(4, lines.Length)).Trim();
    }

    private static Geometry ReadRenderer(Renderer renderer)
    {
        Material[] materials = renderer.sharedMaterials;
        AssetRef[] refs = new AssetRef[materials.Length]; string[] shaders = new string[materials.Length];
        bool water = false;
        for (int i = 0; i < materials.Length; i++)
        {
            refs[i] = Ref(materials[i]);
            shaders[i] = materials[i] != null && materials[i].shader != null ? materials[i].shader.name : "";
            if (shaders[i].IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0) water = true;
        }
        if (renderer.name.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0 && renderer.bounds.size.y < 2f) water = true;
        Mesh mesh = null;
        if (renderer is SkinnedMeshRenderer skin) mesh = skin.sharedMesh;
        else if (renderer.TryGetComponent(out MeshFilter filter)) mesh = filter.sharedMesh;  // 파티클·라인 렌더러에는 MeshFilter가 없다
        return new Geometry { hierarchy = Hierarchy(renderer.transform), type = renderer.GetType().Name,
            active = renderer.enabled && renderer.gameObject.activeInHierarchy, position = renderer.transform.position,
            center = renderer.bounds.center, size = renderer.bounds.size, mesh = Ref(mesh), materials = refs, shaders = shaders, waterCandidate = water };
    }

    private static AssetRef Ref(Object asset)
    {
        string path = asset != null ? AssetDatabase.GetAssetPath(asset) : "";
        return new AssetRef { path = path, guid = string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path) };
    }

    private static string Hierarchy(Transform item)
    {
        string path = item.name + "#" + item.GetSiblingIndex();
        while (item.parent != null) { item = item.parent; path = item.name + "#" + item.GetSiblingIndex() + "/" + path; }
        return path;
    }

    private static string FullPath(string assetPath)
    {
        return Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);
    }

    private static string HashFile(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream file = File.OpenRead(path))
        { return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant(); }
    }

    private static void Progress(string title, float value)
    {
        if (EditorUtility.DisplayCancelableProgressBar("L4D 새 맵 정보 추출", title, value)) throw new OperationCanceledException();
    }

    private static void WriteText(ZipArchive zip, string name, string text)
    {
        using (Stream stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open())
        using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(text);
    }

    private static string SceneEntryName(string format)
    {
        return format == "yaml" ? "environment.unity.txt" : "environment.unity.bytes";
    }

    private static string ReadSceneFormat(string path)
    {
        byte[] prefix = new byte[32];
        using (FileStream stream = File.OpenRead(path))
        {
            int read = stream.Read(prefix, 0, prefix.Length);
            string header = Encoding.UTF8.GetString(prefix, 0, read).TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
            return header.StartsWith("%YAML", StringComparison.Ordinal) || header.StartsWith("--- !u!", StringComparison.Ordinal) ? "yaml" : "binary";
        }
    }

    private static void WriteZip(string output, Report report, NavMeshTriangulation nav)
    {
        string partial = output + ".partial";
        try
        {
            report.sceneFileFormat = ReadSceneFormat(FullPath(report.scenePath));
            report.sceneFile = SceneEntryName(report.sceneFileFormat);
            using (FileStream file = File.Create(partial))
            using (ZipArchive zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                WriteText(zip, "map-info.json", JsonUtility.ToJson(report, false));
                StringBuilder assets = new StringBuilder("guid\tpath\n");
                foreach (string guid in AssetDatabase.FindAssets("", new[] { Folder }))
                    assets.Append(guid).Append('\t').Append(AssetDatabase.GUIDToAssetPath(guid)).Append('\n');
                WriteText(zip, "asset-list.tsv", assets.ToString());
                using (Stream input = File.OpenRead(FullPath(report.scenePath)))
                using (Stream entry = zip.CreateEntry(report.sceneFile, CompressionLevel.Optimal).Open()) input.CopyTo(entry);
                string meta = FullPath(report.scenePath) + ".meta";
                if (File.Exists(meta)) WriteText(zip, "environment.unity.meta.txt", File.ReadAllText(meta));
                if (report.navigationBuilt)
                {
                    using (Stream stream = zip.CreateEntry("navmesh.bin", CompressionLevel.Optimal).Open())
                    using (BinaryWriter writer = new BinaryWriter(stream))
                    { WriteNavigation(writer, nav); }
                }
                WriteText(zip, "READ_ME.txt", "이 ZIP만 채팅에 첨부하세요. 큰 FBX/텍스처 파일은 포함되어 있지 않습니다.\nExporter version: 3. CompressionLevel type alias resolves CS0104.\nScene: " + report.sceneFile + " (" + report.sceneFileFormat + "). Binary scenes are read by Unity; world geometry/terrain/navigation are in map-info.json and navmesh.bin. No scene format conversion is required.\nNavMesh binary: ASCII L4DNAV1 + 3 little-endian Int32 counts + float32 XYZ vertices + Int32 indices + Int32 areas.\n지형 높이 배열은 z행 우선이며 지형 오브젝트의 position.y를 더하면 월드 높이가 됩니다.\n");
            }
            using (FileStream file = File.OpenRead(partial))
            using (ZipArchive zip = new ZipArchive(file, ZipArchiveMode.Read))
            { if (zip.GetEntry("map-info.json") == null || zip.GetEntry(report.sceneFile) == null) throw new InvalidDataException("정보 ZIP 검증에 실패했습니다."); }
            if (File.Exists(output)) File.Delete(output);
            File.Move(partial, output);
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }

    private static void WriteNavigation(BinaryWriter writer, NavMeshTriangulation nav)
    {
        writer.Write(Encoding.ASCII.GetBytes("L4DNAV1"));
        writer.Write(nav.vertices.Length); writer.Write(nav.indices.Length); writer.Write(nav.areas.Length);
        foreach (Vector3 vertex in nav.vertices) { writer.Write(vertex.x); writer.Write(vertex.y); writer.Write(vertex.z); }
        foreach (int index in nav.indices) writer.Write(index);
        foreach (int area in nav.areas) writer.Write(area);
    }
}
#endif
