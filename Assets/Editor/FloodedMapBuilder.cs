#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Random = System.Random;

// Flooded Grounds 에셋으로 게임 씬의 맵을 통째로 바꾸는 도구
// 메뉴: Tools > L4D 맵 > Flooded Grounds 맵으로 교체
//
// 하는 일 (한 번에)
//  1. 지금 게임 씬을 Assets/01.Scenes/Backup 에 복사해 둔다.
//  2. 기존 맵(CityMap, Map_Old)을 지운다. 게임 시스템(Player, 카메라, UI, GameManager, AIDirector, 스포너 등)은 그대로 둔다.
//  3. Flooded Grounds 데모 씬(Scene_A)의 지형/건물/소품/물/조명을 게임 씬으로 옮긴다. (원본 씬 파일은 저장하지 않는다)
//  4. Built-in 셰이더 머티리얼을 URP용으로 바꾼다. (원본 머티리얼은 Assets/L4DMap/Flooded/OriginalMaterials 에 백업)
//  5. FloodedMapLayout.json 의 설계대로 선형 경로를 만든다:
//     출발 → 남쪽 둑길 → 마을 남쪽 거리 → 동쪽 골목·판잣길 → 벽돌집(중간 안전가옥) → 북쪽 물가 → 마을 광장(대규모 전투)
//     → 저택 내부 통과 → 서쪽 마을 → 언덕 저택(피날레) → 북쪽 선착장 보트(탈출)
//     경로 밖으로 나가지 못하게 보이지 않는 벽(Ignore Raycast 레이어)과 원본 에셋 담장/폐차 바리케이드를 놓는다.
//  6. CityMapInfo(경로, 스폰 포인트, 안전가옥, 피날레, 탈출 지점)를 채워 AIDirector/보급품/동료 AI가 그대로 쓰게 한다.
//  7. NavMesh를 에디터에서 구워 에셋으로 저장한다. (실행할 때 다시 굽지 않는다)
//  8. NavMesh로 실제 경로 길이, 끊긴 곳, 경로 밖으로 새는 곳, 분홍 머티리얼을 검사하고 보고서를 남긴다.
public static class FloodedMapBuilder
{
    private const string Menu = "Tools/L4D 맵/";
    private const string SourceSceneGuid = "c0d944ca9defd20449bc336e50d75a08";
    private const string SourceScenePath = "Assets/Flooded_Grounds/Scenes/Scene_A.unity";
    private const string Folder = "Assets/L4DMap/Flooded";
    private const string LayoutPath = Folder + "/FloodedMapLayout.json";
    private const string MaterialBackupFolder = Folder + "/OriginalMaterials";
    private const string ConvertedFolder = Folder + "/ConvertedMaterials";
    private const string ReportPath = Folder + "/FloodedMapReport.txt";
    private const string NavMeshAssetPath = Folder + "/NavMesh-FloodedMap.asset";
    private const string BackupFolder = "Assets/01.Scenes/Backup";
    private const string RootName = "FloodedMap";
    private const string PreservedName = "_PreservedFromOldMap";
    private const int BoundaryLayer = 2; // Ignore Raycast: 총알/혈흔/시야 검사는 통과하고, 플레이어/좀비 이동만 막는다.
    private const float OldRouteFallback = 543.8f; // 기존 CityMap 진행 경로(38개 지점) 길이 - 씬에서 읽지 못할 때만 쓴다.

    private const string FencePrefab = "Assets/Flooded_Grounds/Prefabs/Buildings/Structures1/Struct_Fence1_Mid_B.prefab";
    private const string FenceAltPrefab = "Assets/Flooded_Grounds/Prefabs/Buildings/Structures1/Struct_Fence3_Mid_A.prefab";
    private const string BoardPrefab = "Assets/Flooded_Grounds/Prefabs/Buildings/Structures1/Struct_WoodBoard_A.prefab";
    private const string CarPrefabA = "Assets/Flooded_Grounds/Prefabs/Props/Prop_Car_A.prefab";
    private const string CarPrefabB = "Assets/Flooded_Grounds/Prefabs/Props/Prop_Car1_DM.prefab";
    private const string BoatPrefab = "Assets/Flooded_Grounds/Prefabs/Props/Prop_Boat_A_Bobbing.prefab";
    private const string BoatPrefabStill = "Assets/Flooded_Grounds/Prefabs/Props/Prop_Boat_A.prefab";

    [Serializable]
    private class Layout
    {
        public float[] route;
        public float[] start;
        public float[] startArea;
        public float[] midRoom;
        public float[] midCrates;
        public float[] midLight;
        public float[] finale;
        public float[] radio;
        public float[] extraction;
        public float[] boat;
        public float[] spawns;
        public float[] walls;
        public float[] guides;
        public float[] probes;
        public int version;
    }

    private struct Atmosphere
    {
        public bool Fog;
        public FogMode FogMode;
        public Color FogColor;
        public float FogDensity;
        public float FogStart;
        public float FogEnd;
        public AmbientMode AmbientMode;
        public Color AmbientSky;
        public Color AmbientEquator;
        public Color AmbientGround;
        public float AmbientIntensity;
        public Material Skybox;
        public float ReflectionIntensity;
        public DefaultReflectionMode ReflectionMode;
        public Texture CustomReflection;
        public Color SubtractiveShadowColor;
    }

    private sealed class PieceInfo
    {
        public GameObject Prefab;
        public float Length;
        public bool LongAxisIsX;
        public Vector3 PivotToCenter;  // 프리팹 원점에서 경계 상자 가운데까지 (회전 없는 상태)
        public float BottomOffset;     // 프리팹 원점에서 경계 상자 바닥까지 (y)
    }

    private static readonly List<string> Log = new List<string>();
    private static readonly List<string> Warnings = new List<string>();

    // ======================================================================
    // 메뉴
    // ======================================================================

    [MenuItem(Menu + "Flooded Grounds 맵으로 교체 (기존 맵 제거)", priority = 0)]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("Flooded 맵", "Play를 끄고 컴파일이 끝난 뒤 실행해 주세요.", "확인");
            return;
        }

        Layout layout = LoadLayout();
        if (layout == null)
        {
            EditorUtility.DisplayDialog("Flooded 맵", LayoutPath + " 파일을 읽지 못했습니다.\n받은 zip의 Assets 폴더를 다시 덮어써 주세요.", "확인");
            return;
        }

        string sourcePath = FindSourceScene();
        if (sourcePath == null)
        {
            EditorUtility.DisplayDialog("Flooded 맵", "Flooded Grounds 데모 씬(Assets/Flooded_Grounds/Scenes/Scene_A.unity)을 찾지 못했습니다.\nPackage Manager에서 Flooded Grounds를 전부 Import한 뒤 다시 실행해 주세요.", "확인");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene game = OpenGameScene();
        if (!game.IsValid())
        {
            return;
        }

        string question = "게임 씬: " + game.path + "\n\n"
                          + "1. 씬을 " + BackupFolder + " 에 백업합니다.\n"
                          + "2. 기존 맵(CityMap, Map_Old)을 지웁니다. 게임 시스템은 유지합니다.\n"
                          + "3. Flooded Grounds 지형·건물을 옮겨 와 긴 선형 맵을 만듭니다.\n"
                          + "4. 머티리얼을 URP로 바꿉니다. (원본은 백업)\n"
                          + "5. NavMesh를 굽고 경로를 검사합니다.\n\n"
                          + "몇 분 걸릴 수 있습니다. 진행할까요?";
        if (!EditorUtility.DisplayDialog("Flooded Grounds 맵으로 교체", question, "교체", "취소"))
        {
            return;
        }

        Log.Clear();
        Warnings.Clear();
        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            Progress("씬 백업", 0.02f);
            string backup = BackupScene(game);

            Progress("기존 맵 측정", 0.04f);
            float oldPolyline = MeasureOldMap(game, out float oldNavPath);

            Progress("기존 맵 제거", 0.06f);
            RemoveOldMap(game);

            GameObject root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, game);
            Transform environment = Child(root.transform, "Environment");

            Progress("Flooded Grounds 지형·건물 가져오기", 0.1f);
            Atmosphere atmosphere = ImportEnvironment(sourcePath, game, environment, out Light sun);

            Progress("머티리얼 URP 변환", 0.3f);
            ConvertMaterials(environment);

            Progress("지형 설정", 0.45f);
            SetupTerrain(environment);
            FixLayers(environment);
            DisableDemoExtras(environment);

            Progress("조명·안개", 0.48f);
            ApplyAtmosphere(atmosphere, sun, game);

            Progress("경로·안전가옥·피날레", 0.5f);
            Physics.SyncTransforms();
            CityMapInfo info = BuildGameplay(root.transform, layout);

            Progress("Player·스포너", 0.6f);
            MovePlayer(layout);
            AssignSpawner(info.SpawnPoints);

            Progress("NavMesh 굽는 중 (맵이 커서 몇 분 걸릴 수 있습니다)", 0.62f);
            BakeNavMesh(root, layout);

            Progress("검사", 0.92f);
            Verify(layout, info, oldPolyline, oldNavPath);

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            AssetDatabase.SaveAssets();
            Log.Add("✔ 씬 저장: " + game.path + "  (백업: " + backup + ")");
            Log.Add("소요 시간: " + watch.Elapsed.TotalSeconds.ToString("0") + "초");
            WriteReport(true);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Warnings.Add("❌ 중간에 오류가 났습니다: " + exception.Message + "\n   씬은 저장하지 않았습니다. 백업 씬(" + BackupFolder + ")에서 되돌리거나, 씬을 저장하지 말고 다시 여세요.");
            WriteReport(false);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem(Menu + "Flooded 맵 NavMesh 다시 굽기", priority = 1)]
    public static void Rebake()
    {
        CityMapInfo info = FindFloodedMap();
        Layout layout = LoadLayout();
        if (info == null || layout == null)
        {
            EditorUtility.DisplayDialog("Flooded 맵", "FloodedMap이 있는 게임 씬을 연 뒤 다시 실행해 주세요.", "확인");
            return;
        }

        Log.Clear();
        Warnings.Clear();
        try
        {
            Progress("NavMesh 굽는 중", 0.3f);
            Physics.SyncTransforms();
            BakeNavMesh(info.gameObject, layout);
            Progress("검사", 0.9f);
            Verify(layout, info, -1f, -1f);
            EditorSceneManager.MarkSceneDirty(info.gameObject.scene);
            EditorSceneManager.SaveScene(info.gameObject.scene);
            WriteReport(true);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem(Menu + "Flooded 맵 검사 (경로 길이·막힘·누수)", priority = 2)]
    public static void VerifyOnly()
    {
        CityMapInfo info = FindFloodedMap();
        Layout layout = LoadLayout();
        if (info == null || layout == null)
        {
            EditorUtility.DisplayDialog("Flooded 맵", "FloodedMap이 있는 게임 씬을 연 뒤 다시 실행해 주세요.", "확인");
            return;
        }

        Log.Clear();
        Warnings.Clear();
        Verify(layout, info, -1f, -1f);
        WriteReport(true);
    }

    [MenuItem(Menu + "Flooded 원본 머티리얼로 되돌리기", priority = 40)]
    public static void RestoreMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MaterialBackupFolder))
        {
            EditorUtility.DisplayDialog("Flooded 맵", "백업된 머티리얼이 없습니다.", "확인");
            return;
        }

        if (!EditorUtility.DisplayDialog("Flooded 맵", "Flooded Grounds 머티리얼을 변환 전(Built-in 셰이더) 상태로 되돌릴까요?\nURP에서는 다시 분홍색으로 보입니다.", "되돌리기", "취소"))
        {
            return;
        }

        int restored = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialBackupFolder }))
        {
            string backupPath = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(backupPath);
            int split = file.IndexOf('_');
            string originalGuid = split > 0 ? file.Substring(0, split) : file;
            string subName = split > 0 ? file.Substring(split + 1) : null;  // 프리팹 안에 든 머티리얼이면 이름
            string originalPath = AssetDatabase.GUIDToAssetPath(originalGuid);
            Material backup = AssetDatabase.LoadAssetAtPath<Material>(backupPath);
            Material original = null;
            if (!string.IsNullOrEmpty(originalPath))
            {
                original = subName == null
                    ? AssetDatabase.LoadAssetAtPath<Material>(originalPath)
                    : AssetDatabase.LoadAllAssetsAtPath(originalPath).OfType<Material>().FirstOrDefault(mat => Sanitize(mat.name) == subName);
            }

            if (backup == null || original == null)
            {
                continue;
            }

            string keepName = original.name;
            EditorUtility.CopySerialized(backup, original);
            original.name = keepName;
            EditorUtility.SetDirty(original);
            restored++;
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Flooded 맵", "머티리얼 " + restored + "개를 되돌렸습니다.", "확인");
    }

    [MenuItem(Menu + "Flooded 맵 오클루전 컬링 굽기 (선택)", priority = 41)]
    public static void BakeOcclusion()
    {
        if (FindFloodedMap() == null)
        {
            EditorUtility.DisplayDialog("Flooded 맵", "FloodedMap이 있는 게임 씬을 연 뒤 다시 실행해 주세요.", "확인");
            return;
        }

        if (!EditorUtility.DisplayDialog("Flooded 맵", "오클루전 컬링을 굽습니다. 맵이 커서 몇 분 이상 걸릴 수 있습니다.\n(건물 뒤에 가려진 물체를 그리지 않아 프레임이 오릅니다)", "굽기", "취소"))
        {
            return;
        }

        StaticOcclusionCulling.smallestOccluder = 3f;
        StaticOcclusionCulling.smallestHole = 0.3f;
        StaticOcclusionCulling.Compute();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Flooded 맵", "오클루전 컬링을 구웠습니다.", "확인");
    }

    // ======================================================================
    // 준비
    // ======================================================================

    private static Layout LoadLayout()
    {
        TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
        if (text == null)
        {
            return null;
        }

        Layout layout = JsonUtility.FromJson<Layout>(text.text);
        if (layout == null || layout.route == null || layout.route.Length < 6 || layout.walls == null || layout.start == null)
        {
            return null;
        }

        return layout;
    }

    private static string FindSourceScene()
    {
        string path = AssetDatabase.GUIDToAssetPath(SourceSceneGuid);
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            return path;
        }

        if (File.Exists(SourceScenePath))
        {
            return SourceScenePath;
        }

        return AssetDatabase.FindAssets("t:Scene Scene_A", new[] { "Assets/Flooded_Grounds" })
            .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
    }

    private static Scene OpenGameScene()
    {
        Scene active = SceneManager.GetActiveScene();
        if (active.IsValid() && SceneHasPlayer(active))
        {
            return active;
        }

        List<string> candidates = new List<string> { "Assets/01.Scenes/L4D.unity", "Assets/01.Scenes/GameScene.unity" };
        candidates.AddRange(AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !p.StartsWith(BackupFolder) && !p.StartsWith("Assets/Backup") && !p.StartsWith("Assets/Flooded_Grounds")));
        foreach (string path in candidates.Distinct())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (SceneHasPlayer(scene))
            {
                return scene;
            }
        }

        EditorUtility.DisplayDialog("Flooded 맵", "Player(PlayerMove)가 있는 게임 씬을 찾지 못했습니다. 게임 씬을 연 뒤 다시 실행해 주세요.", "확인");
        return default;
    }

    private static bool SceneHasPlayer(Scene scene)
    {
        return scene.GetRootGameObjects().Any(go => go.GetComponentInChildren<PlayerMove>(true) != null);
    }

    private static CityMapInfo FindFloodedMap()
    {
        return Object.FindObjectsByType<CityMapInfo>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(info => info.name == RootName);
    }

    private static string BackupScene(Scene game)
    {
        EnsureFolder(BackupFolder);
        string name = Path.GetFileNameWithoutExtension(game.path);
        string target = BackupFolder + "/" + name + "_before_FloodedMap_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
        if (!AssetDatabase.CopyAsset(game.path, target))
        {
            throw new IOException("씬 백업에 실패했습니다: " + target);
        }

        Log.Add("✔ 기존 씬 백업: " + target);
        return target;
    }

    // ======================================================================
    // 기존 맵
    // ======================================================================

    // 기존 CityMap의 진행 경로 길이 (지점 사이 직선 합) 와, 가능하면 NavMesh 실제 경로 길이
    private static float MeasureOldMap(Scene game, out float navLength)
    {
        navLength = -1f;
        CityMapInfo old = game.GetRootGameObjects().Select(go => go.GetComponentInChildren<CityMapInfo>(true))
            .FirstOrDefault(info => info != null && info.name != RootName);
        if (old == null || old.RoutePoints == null || old.RoutePoints.Length < 2)
        {
            Log.Add("· 기존 맵 경로를 씬에서 읽지 못해 설계 값 " + OldRouteFallback.ToString("0") + " m를 비교 기준으로 씁니다.");
            return OldRouteFallback;
        }

        float polyline = old.RouteLength;
        NavMeshSurface surface = old.GetComponent<NavMeshSurface>();
        if (surface != null && surface.navMeshData != null)
        {
            float sum = 0f;
            bool ok = true;
            NavMeshPath path = new NavMeshPath();
            for (int i = 0; i < old.RoutePoints.Length - 1 && ok; i++)
            {
                ok = PathLength(old.RoutePoints[i].position, old.RoutePoints[i + 1].position, path, out float length);
                sum += length;
            }

            if (ok)
            {
                navLength = sum;
            }
        }

        Log.Add("· 기존 맵 진행 경로: 지점 " + old.RoutePoints.Length + "개, 직선 합 " + polyline.ToString("0") + " m"
                + (navLength > 0f ? ", NavMesh 실제 경로 " + navLength.ToString("0") + " m" : " (기존 NavMesh는 실행 중에만 만들어져 에디터에서 실제 경로는 잴 수 없음)"));
        return polyline;
    }

    private static void RemoveOldMap(Scene game)
    {
        GameObject preserved = game.GetRootGameObjects().FirstOrDefault(go => go.name == PreservedName);
        List<string> removed = new List<string>();
        foreach (GameObject go in game.GetRootGameObjects())
        {
            bool isOldMap = go.name != PreservedName
                            && (go.name == "CityMap" || go.name == "Map_Old" || go.name == RootName || go.GetComponent<CityMapInfo>() != null);
            if (!isOldMap)
            {
                continue;
            }

            if (go.name != RootName)
            {
                // 게임 시스템이 섞여 있으면 먼저 꺼내 둔다.
                foreach (Transform system in EssentialChildren(go.transform))
                {
                    system.SetParent(null, true);
                    Warnings.Add("⚠ 기존 맵 안에 있던 게임 오브젝트 '" + system.name + "'를 씬 루트로 옮겨 보존했습니다.");
                }

                // 스폰 위치/이벤트 오브젝트 묶음은 지우지 않고 보관한다. (새 맵에서는 쓰지 않으므로 꺼 둔다)
                foreach (Transform group in go.transform.Cast<Transform>().ToArray())
                {
                    if (group.name == "SpawnPoints" || group.name == "Route")
                    {
                        if (preserved == null)
                        {
                            preserved = new GameObject(PreservedName);
                            SceneManager.MoveGameObjectToScene(preserved, game);
                            preserved.SetActive(false);
                        }

                        group.SetParent(preserved.transform, true);
                        group.name = "Old_" + group.name;
                    }
                }
            }

            removed.Add(go.name);
            Object.DestroyImmediate(go);
        }

        // 기존 씬에 따로 구워 둔 옛날 방식 NavMesh도 비운다.
        try
        {
            Type builder = Type.GetType("UnityEditor.AI.NavMeshBuilder, UnityEditor.AIModule") ?? Type.GetType("UnityEditor.AI.NavMeshBuilder, UnityEditor");
            MethodInfo clear = builder?.GetMethod("ClearAllNavMeshes", BindingFlags.Public | BindingFlags.Static);
            clear?.Invoke(null, null);
        }
        catch (Exception)
        {
            // 없는 버전이면 넘어간다.
        }

        NavMesh.RemoveAllNavMeshData();

        // 기존 맵용 해(Directional Light)는 끈다. (Flooded Grounds의 SUN을 쓴다)
        foreach (GameObject go in game.GetRootGameObjects())
        {
            Light light = go.GetComponent<Light>();
            if (light != null && light.type == LightType.Directional && go.GetComponentInChildren<Camera>(true) == null)
            {
                go.SetActive(false);
                Log.Add("· 기존 Directional Light '" + go.name + "'는 지우지 않고 꺼 두었습니다.");
            }
        }

        Log.Add(removed.Count > 0 ? "✔ 기존 맵 제거: " + string.Join(", ", removed) : "· 지울 기존 맵(CityMap, Map_Old)이 없었습니다.");
        if (preserved != null)
        {
            Log.Add("· 기존 맵의 스폰 위치·경로 오브젝트는 " + PreservedName + " (꺼짐)에 옮겨 보존했습니다. 새 맵은 새 스폰 포인트를 씁니다.");
        }
    }

    private static IEnumerable<Transform> EssentialChildren(Transform root)
    {
        List<Transform> found = new List<Transform>();
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component.transform == root)
            {
                continue;
            }

            bool essential = component is PlayerMove || component is GameManager || component is Camera || component is Canvas
                             || component is ZombieSpawner || component is AIDirector || component is CursorManager
                             || component is UnityEngine.EventSystems.EventSystem;
            if (essential && !found.Any(t => component.transform.IsChildOf(t)))
            {
                found.Add(component.transform);
            }
        }

        return found;
    }

    // ======================================================================
    // Flooded Grounds 가져오기
    // ======================================================================

    private static Atmosphere ImportEnvironment(string sourcePath, Scene game, Transform environment, out Light sun)
    {
        Scene source = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);
        Atmosphere atmosphere;
        try
        {
            SceneManager.SetActiveScene(source);
            atmosphere = CaptureAtmosphere();
            SceneManager.SetActiveScene(game);

            int moved = 0;
            List<string> skipped = new List<string>();
            foreach (GameObject go in source.GetRootGameObjects())
            {
                if (IsDemoPlayer(go))
                {
                    skipped.Add(go.name);
                    continue;
                }

                SceneManager.MoveGameObjectToScene(go, game);
                go.transform.SetParent(environment, true);
                moved++;
            }

            Log.Add("✔ Flooded Grounds(" + Path.GetFileName(sourcePath) + ")에서 오브젝트 " + moved + "개를 FloodedMap/Environment로 옮겼습니다."
                    + (skipped.Count > 0 ? " (데모용 카메라/플레이어 제외: " + string.Join(", ", skipped) + ")" : string.Empty));
        }
        finally
        {
            // 원본 데모 씬은 저장하지 않고 닫는다. (파일은 그대로)
            EditorSceneManager.CloseScene(source, true);
            SceneManager.SetActiveScene(game);
        }

        // 라이트맵은 옮겨 오지 않으므로 Baked 전용 조명은 실시간으로 바꾼다.
        int realtime = 0;
        foreach (Light light in environment.GetComponentsInChildren<Light>(true))
        {
            if (light.lightmapBakeType == LightmapBakeType.Baked)
            {
                light.lightmapBakeType = LightmapBakeType.Realtime;
                realtime++;
            }
        }

        if (realtime > 0) Log.Add("· Baked 전용 조명 " + realtime + "개를 실시간 조명으로 바꿨습니다. (라이트맵 없이도 보이게)");
        if (Lightmapping.lightingDataAsset != null)
        {
            Lightmapping.lightingDataAsset = null;
            Log.Add("· 기존 맵용 라이트맵 데이터 연결을 끊었습니다. (예전 맵 그림자가 새 맵에 붙지 않게)");
        }

        sun = environment.GetComponentsInChildren<Light>(true).Where(l => l.type == LightType.Directional)
            .OrderByDescending(l => l.name.ToUpperInvariant().Contains("SUN") ? 1 : 0).ThenByDescending(l => l.intensity).FirstOrDefault();
        return atmosphere;
    }

    private static bool IsDemoPlayer(GameObject go)
    {
        if (go.GetComponentsInChildren<Transform>(true).Length > 60)
        {
            return false; // 큰 묶음(환경)은 데모 플레이어가 아니다.
        }

        if (go.GetComponentInChildren<Camera>(true) != null || go.GetComponentInChildren<AudioListener>(true) != null
            || go.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null)
        {
            return true;
        }

        return go.name.StartsWith("FpsController") || go.GetComponentsInChildren<MonoBehaviour>(true)
            .Any(m => m != null && m.GetType().Name == "CharController_Motor");
    }

    private static Atmosphere CaptureAtmosphere()
    {
        Atmosphere a = new Atmosphere
        {
            Fog = RenderSettings.fog,
            FogMode = RenderSettings.fogMode,
            FogColor = RenderSettings.fogColor,
            FogDensity = RenderSettings.fogDensity,
            FogStart = RenderSettings.fogStartDistance,
            FogEnd = RenderSettings.fogEndDistance,
            AmbientMode = RenderSettings.ambientMode,
            AmbientSky = RenderSettings.ambientSkyColor,
            AmbientEquator = RenderSettings.ambientEquatorColor,
            AmbientGround = RenderSettings.ambientGroundColor,
            AmbientIntensity = RenderSettings.ambientIntensity,
            Skybox = RenderSettings.skybox,
            ReflectionIntensity = RenderSettings.reflectionIntensity,
            ReflectionMode = RenderSettings.defaultReflectionMode,
            SubtractiveShadowColor = RenderSettings.subtractiveShadowColor,
        };
        a.CustomReflection = GetStaticProperty<Texture>(typeof(RenderSettings), "customReflectionTexture")
                             ?? GetStaticProperty<Texture>(typeof(RenderSettings), "customReflection");
        return a;
    }

    // 원본 분위기(안개 낀 흐린 늪)를 살리고, 좀비물답게 조금 더 어둡고 안개를 짙게 한다.
    private static void ApplyAtmosphere(Atmosphere a, Light sun, Scene game)
    {
        SceneManager.SetActiveScene(game);
        RenderSettings.fog = true;
        RenderSettings.fogMode = a.FogMode;
        RenderSettings.fogColor = Color.Lerp(a.FogColor, new Color(0.36f, 0.39f, 0.4f), 0.35f);
        RenderSettings.fogDensity = Mathf.Max(0.0045f, a.FogDensity * 1.6f);
        RenderSettings.fogStartDistance = a.FogStart;
        RenderSettings.fogEndDistance = Mathf.Min(a.FogEnd > 0f ? a.FogEnd : 260f, 260f);
        // 하늘 기반 환경광은 조명 굽기 전에는 비어 있을 수 있어서, 원본 색으로 3색(Trilight) 환경광을 쓴다.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = a.AmbientSky * 1.35f;
        RenderSettings.ambientEquatorColor = a.AmbientEquator * 1.35f;
        RenderSettings.ambientGroundColor = a.AmbientGround * 1.35f;
        RenderSettings.ambientIntensity = 1f;
        if (a.Skybox != null)
        {
            RenderSettings.skybox = a.Skybox;
        }

        RenderSettings.reflectionIntensity = a.ReflectionIntensity;
        RenderSettings.subtractiveShadowColor = a.SubtractiveShadowColor;
        if (a.CustomReflection != null)
        {
            RenderSettings.defaultReflectionMode = a.ReflectionMode;
            if (!SetStaticProperty(typeof(RenderSettings), "customReflectionTexture", a.CustomReflection))
            {
                SetStaticProperty(typeof(RenderSettings), "customReflection", a.CustomReflection);
            }
        }

        DynamicGI.UpdateEnvironment();
        if (sun != null)
        {
            RenderSettings.sun = sun;
            sun.intensity *= 0.85f;
            sun.shadows = LightShadows.Soft;
            Log.Add("✔ 조명: Flooded Grounds의 해(" + sun.name + ") + 원본 안개/하늘/반사, 조금 더 어둡고 안개 짙게 조정");
        }
        else
        {
            Warnings.Add("⚠ Flooded Grounds 씬에서 해(Directional Light)를 찾지 못했습니다.");
        }
    }

    // ======================================================================
    // 머티리얼 (Built-in → URP)
    // ======================================================================

    private static Shader _lit;
    private static Shader _particles;
    private static Shader _topBlend;
    private static Shader _water;
    private static readonly Dictionary<Material, Material> Replacements = new Dictionary<Material, Material>();

    private static void ConvertMaterials(Transform environment)
    {
        _lit = Shader.Find("Universal Render Pipeline/Lit");
        _particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        _topBlend = UsableShader("L4D/Flooded TopBlend");
        _water = UsableShader("L4D/Flooded Water");
        Replacements.Clear();
        if (_lit == null)
        {
            Warnings.Add("❌ URP Lit 셰이더를 찾지 못했습니다. 프로젝트가 URP인지 확인하세요. 머티리얼 변환을 건너뜁니다.");
            return;
        }

        if (_topBlend == null)
        {
            Warnings.Add("⚠ L4D/Flooded TopBlend 셰이더를 쓸 수 없어 이끼 섞기 없이 URP Lit으로 변환합니다.");
        }

        if (_water == null)
        {
            Warnings.Add("⚠ L4D/Flooded Water 셰이더를 쓸 수 없어 물을 URP Lit(반투명)으로 변환합니다.");
        }

        EnsureFolder(MaterialBackupFolder);
        EnsureFolder(ConvertedFolder);

        // 씬의 렌더러 + 지형 나무/풀 프리팹의 머티리얼
        HashSet<Material> materials = new HashSet<Material>();
        Renderer[] renderers = environment.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null) materials.Add(material);
            }

            if (renderer is ParticleSystemRenderer particle && particle.trailMaterial != null)
            {
                materials.Add(particle.trailMaterial);
            }
        }

        foreach (Terrain terrain in environment.GetComponentsInChildren<Terrain>(true))
        {
            TerrainData data = terrain.terrainData;
            if (data == null) continue;
            foreach (TreePrototype tree in data.treePrototypes)
            {
                AddPrefabMaterials(tree.prefab, materials);
            }

            foreach (DetailPrototype detail in data.detailPrototypes)
            {
                if (detail.usePrototypeMesh) AddPrefabMaterials(detail.prototype, materials);
            }
        }

        // 나중에 놓을 바리케이드/보트 프리팹도 같이 변환한다.
        foreach (string prefabPath in new[] { FencePrefab, FenceAltPrefab, BoardPrefab, CarPrefabA, CarPrefabB, BoatPrefab, BoatPrefabStill })
        {
            AddPrefabMaterials(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), materials);
        }

        Dictionary<string, int> counts = new Dictionary<string, int>();
        foreach (Material material in materials)
        {
            string kind;
            try
            {
                kind = Convert(material);
            }
            catch (Exception exception)
            {
                kind = "변환 실패";
                Warnings.Add("⚠ 머티리얼 '" + material.name + "' 변환 실패: " + exception.Message);
            }

            counts[kind] = counts.TryGetValue(kind, out int n) ? n + 1 : 1;
        }

        // 지형 나무/풀 프리팹이 읽기 전용 머티리얼을 쓰면 복사본을 끼울 수 없어 경고만 남긴다.
        foreach (Terrain terrain in environment.GetComponentsInChildren<Terrain>(true))
        {
            if (terrain.terrainData == null) continue;
            foreach (TreePrototype tree in terrain.terrainData.treePrototypes)
            {
                if (tree.prefab != null && tree.prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Any(mat => mat != null && Replacements.ContainsKey(mat)))
                {
                    Warnings.Add("⚠ 지형 나무 '" + tree.prefab.name + "'의 머티리얼이 읽기 전용이라 바꾸지 못했습니다. (분홍색이면 나무 프리팹의 머티리얼을 Extract 후 다시 실행)");
                }
            }
        }

        // 고칠 수 없는(읽기 전용) 머티리얼은 변환한 복사본으로 바꿔 끼운다.
        int swapped = 0;
        if (Replacements.Count > 0)
        {
            foreach (Renderer renderer in renderers)
            {
                Material[] shared = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    if (shared[i] != null && Replacements.TryGetValue(shared[i], out Material copy))
                    {
                        shared[i] = copy;
                        changed = true;
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = shared;
                    swapped++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        Log.Add("✔ 머티리얼 " + materials.Count + "개 확인: " + string.Join(", ", counts.Select(kv => kv.Key + " " + kv.Value))
                + (swapped > 0 ? " / 읽기 전용 머티리얼 복사본으로 교체한 렌더러 " + swapped + "개" : string.Empty));
        Log.Add("· 변환 전 원본 머티리얼은 " + MaterialBackupFolder + " 에 백업 (메뉴: Flooded 원본 머티리얼로 되돌리기)");
    }

    private static void AddPrefabMaterials(GameObject prefab, HashSet<Material> materials)
    {
        if (prefab == null) return;
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null) materials.Add(material);
            }
        }
    }

    private static Shader UsableShader(string name)
    {
        Shader shader = Shader.Find(name);
        if (shader == null || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
        {
            return null;
        }

        return shader;
    }

    private static string Convert(Material material)
    {
        Shader shader = material.shader;
        string name = shader != null ? shader.name : string.Empty;
        if (name.StartsWith("Universal Render Pipeline/") || name.StartsWith("L4D/") || name.StartsWith("Shader Graphs/"))
        {
            return "이미 URP";
        }

        bool hiddenTree = name.StartsWith("Hidden/Nature/Tree");
        if (!hiddenTree && (name.Contains("Skybox") || name.StartsWith("Hidden/") || name.StartsWith("TextMeshPro") || name.StartsWith("UI/")))
        {
            return "그대로(하늘/UI)";
        }

        string path = AssetDatabase.GetAssetPath(material);
        bool builtIn = string.IsNullOrEmpty(path) || path.StartsWith("Resources/") || path.StartsWith("Library/");
        bool readOnly = builtIn || (material.hideFlags & HideFlags.NotEditable) != 0
                        || (AssetDatabase.IsSubAsset(material) && AssetImporter.GetAtPath(path) is ModelImporter);
        Material target = material;
        if (readOnly)
        {
            target = new Material(material) { name = material.name + "_URP" };
        }
        else
        {
            Backup(material, path);
        }

        string kind = ConvertInto(target, name);
        if (readOnly)
        {
            string copyPath = AssetDatabase.GenerateUniqueAssetPath(ConvertedFolder + "/" + Sanitize(material.name) + "_URP.mat");
            AssetDatabase.CreateAsset(target, copyPath);
            Replacements[material] = target;
            kind += "(복사본)";
        }
        else
        {
            EditorUtility.SetDirty(material);
        }

        return kind;
    }

    private static void Backup(Material material, string path)
    {
        string guid = AssetDatabase.AssetPathToGUID(path);
        if (AssetDatabase.IsSubAsset(material))
        {
            guid = guid + "_" + Sanitize(material.name);
        }

        string backupPath = MaterialBackupFolder + "/" + guid + ".mat";
        if (File.Exists(backupPath))
        {
            return;
        }

        if (AssetDatabase.IsMainAsset(material) && path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
        {
            AssetDatabase.CopyAsset(path, backupPath);
        }
        else
        {
            AssetDatabase.CreateAsset(new Material(material), backupPath);
        }
    }

    private static string ConvertInto(Material m, string shaderName)
    {
        // 변환 전에 원본 값을 모두 읽어 둔다.
        Dictionary<string, Texture> textures = new Dictionary<string, Texture>();
        Dictionary<string, Vector2> scales = new Dictionary<string, Vector2>();
        Dictionary<string, Vector2> offsets = new Dictionary<string, Vector2>();
        foreach (string property in m.GetTexturePropertyNames())
        {
            Texture texture = m.GetTexture(property);
            if (texture == null) continue;
            textures[property] = texture;
            scales[property] = m.GetTextureScale(property);
            offsets[property] = m.GetTextureOffset(property);
        }

        Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
        float mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : 0f;
        float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
        float metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
        float glossiness = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : (m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0.3f);
        float glossScale = m.HasProperty("_GlossMapScale") ? m.GetFloat("_GlossMapScale") : 1f;
        float smoothChannel = m.HasProperty("_SmoothnessTextureChannel") ? m.GetFloat("_SmoothnessTextureChannel") : 0f;
        float bumpScale = m.HasProperty("_BumpScale") ? m.GetFloat("_BumpScale") : 1f;
        float occlusion = m.HasProperty("_OcclusionStrength") ? m.GetFloat("_OcclusionStrength") : 1f;
        Color emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
        bool emissive = m.IsKeywordEnabled("_EMISSION") && (emission.maxColorComponent > 0.01f);
        bool hadTint = m.HasProperty("_TintColor");
        Color tint = hadTint ? m.GetColor("_TintColor") : Color.white;
        string materialName = m.name.ToLowerInvariant();
        bool foliage = materialName.Contains("leaf") || materialName.Contains("leaves") || materialName.Contains("branch")
                       || materialName.Contains("bush") || materialName.Contains("grass") || materialName.Contains("fol")
                       || materialName.Contains("flower") || materialName.Contains("ivy") || materialName.Contains("vine");
        string lower = shaderName.ToLowerInvariant();

        // ---------- 파티클 ----------
        if (lower.Contains("particle"))
        {
            Shader shader = _particles != null ? _particles : _lit;
            m.shader = shader;
            Texture main = Find(textures, "_MainTex", "_BaseMap");
            SetTex(m, "_BaseMap", main, textures, scales, offsets, "_MainTex");
            Color baseColor = hadTint ? tint * 2f : color;
            SetColor(m, "_BaseColor", baseColor);
            bool additive = lower.Contains("additive");
            MakeTransparent(m, additive ? 2 : 0);
            ApplyUrpKeywords(m);
            return additive ? "파티클(가산)" : "파티클";
        }

        // ---------- 물 ----------
        if (lower.Contains("water"))
        {
            Texture normal = FindByKind(textures, TextureKind.Normal, false) ?? textures.Values.FirstOrDefault();
            if (_water != null)
            {
                m.shader = _water;
                m.renderQueue = -1;
                if (normal != null) m.SetTexture("_NormalMap", normal);
                return "물(전용)";
            }

            m.shader = _lit;
            SetColor(m, "_BaseColor", new Color(0.18f, 0.22f, 0.2f, 0.78f));
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }

            SetFloat(m, "_Smoothness", 0.92f);
            MakeTransparent(m, 0);
            ApplyUrpKeywords(m);
            return "물(Lit)";
        }

        bool topBlend = lower.Contains("topblend");
        bool triplanar = lower.Contains("triplanar");
        bool treeCreator = lower.Contains("tree creator") || lower.Contains("treecreator") || lower.Contains("nature/");
        bool leaves = treeCreator && (lower.Contains("leaves") || lower.Contains("leaf"));

        // ---------- 이끼 섞기 / 삼면 투영 (전용 셰이더) ----------
        if ((topBlend || triplanar) && _topBlend != null)
        {
            Texture albedo = FindByKind(textures, TextureKind.Albedo, false);
            Texture normal = FindByKind(textures, TextureKind.Normal, false);
            Texture smooth = FindByKind(textures, TextureKind.Smoothness, false);
            Texture topAlbedo = FindByKind(textures, TextureKind.Albedo, true);
            Texture topNormal = FindByKind(textures, TextureKind.Normal, true);
            string albedoProperty = KeyOf(textures, albedo);
            m.shader = _topBlend;
            if (albedo != null)
            {
                m.SetTexture("_BaseMap", albedo);
                m.SetTextureScale("_BaseMap", scales[albedoProperty]);
                m.SetTextureOffset("_BaseMap", offsets[albedoProperty]);
                m.SetTextureScale("_BumpMap", scales[albedoProperty]);
                m.SetTextureOffset("_BumpMap", offsets[albedoProperty]);
                m.SetTextureScale("_SmoothMap", scales[albedoProperty]);
            }

            if (normal != null) m.SetTexture("_BumpMap", normal);
            if (smooth != null)
            {
                m.SetTexture("_SmoothMap", smooth);
                m.SetFloat("_SmoothFromAlbedo", 0f);
            }
            else
            {
                // Flooded Grounds 텍스처 이름 규칙: *_AS = Albedo + Smoothness(A)
                m.SetFloat("_SmoothFromAlbedo", albedo != null && albedo.name.EndsWith("_AS") ? 1f : 0f);
            }

            m.SetColor("_BaseColor", new Color(color.r, color.g, color.b, 1f));
            m.SetFloat("_Smoothness", Mathf.Clamp01(glossiness > 0f ? Mathf.Max(glossiness, 0.35f) : 0.5f));
            m.SetFloat("_BumpScale", bumpScale > 0f ? bumpScale : 1f);
            m.SetFloat("_Metallic", 0f);
            if (topAlbedo != null)
            {
                string topProperty = KeyOf(textures, topAlbedo);
                m.SetTexture("_TopMap", topAlbedo);
                m.SetTextureScale("_TopMap", scales[topProperty]);
                m.SetTextureOffset("_TopMap", offsets[topProperty]);
                m.SetTextureScale("_TopBumpMap", scales[topProperty]);
                if (topNormal != null) m.SetTexture("_TopBumpMap", topNormal);
                m.SetFloat("_TopAmount", 1f);
            }
            else
            {
                m.SetFloat("_TopAmount", 0f);
            }

            m.SetFloat("_Triplanar", triplanar ? 1f : 0f);
            m.SetFloat("_Cull", 2f);
            m.renderQueue = -1;
            return triplanar ? "삼면 투영(전용)" : (topAlbedo != null ? "이끼 섞기(전용)" : "이끼 없음(전용)");
        }

        // ---------- 나머지: URP Lit ----------
        {
            Texture albedo = Find(textures, "_MainTex", "_BaseMap", "_Albedo", "_Diffuse") ?? FindByKind(textures, TextureKind.Albedo, false);
            Texture normal = Find(textures, "_BumpMap", "_NormalMap") ?? (treeCreator ? null : FindByKind(textures, TextureKind.Normal, false));
            Texture metallicMap = Find(textures, "_MetallicGlossMap");
            Texture specMap = Find(textures, "_SpecGlossMap");
            Texture occlusionMap = Find(textures, "_OcclusionMap");
            Texture emissionMap = Find(textures, "_EmissionMap");
            string albedoProperty = KeyOf(textures, albedo);
            bool specularSetup = lower.Contains("specular");

            m.shader = _lit;
            if (albedo != null)
            {
                m.SetTexture("_BaseMap", albedo);
                m.SetTexture("_MainTex", albedo);
                m.SetTextureScale("_BaseMap", scales[albedoProperty]);
                m.SetTextureOffset("_BaseMap", offsets[albedoProperty]);
            }

            SetColor(m, "_BaseColor", color);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                SetFloat(m, "_BumpScale", bumpScale > 0f ? bumpScale : 1f);
            }

            SetFloat(m, "_WorkflowMode", specularSetup ? 0f : 1f);
            if (specularSetup) m.EnableKeyword("_SPECULAR_SETUP");
            SetFloat(m, "_Metallic", metallic);
            if (metallicMap != null) m.SetTexture("_MetallicGlossMap", metallicMap);
            if (specMap != null) m.SetTexture("_SpecGlossMap", specMap);
            SetFloat(m, "_Smoothness", metallicMap != null || specMap != null || smoothChannel > 0.5f ? glossScale : glossiness);
            SetFloat(m, "_SmoothnessTextureChannel", smoothChannel);
            if (albedo != null && albedo.name.EndsWith("_AS") && metallicMap == null && specMap == null)
            {
                // Flooded Grounds: 바탕 텍스처 알파 = 매끄러움
                SetFloat(m, "_SmoothnessTextureChannel", 1f);
                SetFloat(m, "_Smoothness", Mathf.Clamp01(glossScale > 0f ? glossScale : 1f));
            }

            if (occlusionMap != null)
            {
                m.SetTexture("_OcclusionMap", occlusionMap);
                SetFloat(m, "_OcclusionStrength", occlusion);
            }

            if (emissive)
            {
                if (emissionMap != null) m.SetTexture("_EmissionMap", emissionMap);
                SetColor(m, "_EmissionColor", emission);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            // 투명 방식: Standard의 _Mode (0 불투명, 1 컷아웃, 2 페이드, 3 투명), 나무 잎은 컷아웃
            bool cutout = mode > 0.5f && mode < 1.5f || leaves || (foliage && albedo != null && albedo.name.Contains("_AS") == false && mode < 1.5f && treeCreator);
            if (leaves) cutout = true;
            if (cutout)
            {
                SetFloat(m, "_AlphaClip", 1f);
                SetFloat(m, "_Cutoff", leaves ? Mathf.Clamp(cutoff, 0.25f, 0.6f) : cutoff);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }

            if (mode > 1.5f)
            {
                MakeTransparent(m, mode > 2.5f ? 1 : 0);
            }

            if (cutout && (foliage || leaves))
            {
                SetFloat(m, "_Cull", 0f); // 잎은 양면
            }

            ApplyUrpKeywords(m);
            if (treeCreator) return "나무(Lit)";
            if (topBlend || triplanar) return "이끼 섞기→Lit";
            return lower.StartsWith("standard") ? "Standard→Lit" : "기타→Lit";
        }
    }

    private enum TextureKind { Albedo, Normal, Smoothness, Mask }

    // 원본 셰이더의 텍스처 칸 이름(예: _TopTex, _BumpMap2)과 텍스처 파일 이름(예: *_N, *_AS)으로 용도를 짐작한다.
    private static Texture FindByKind(Dictionary<string, Texture> textures, TextureKind kind, bool top)
    {
        foreach (KeyValuePair<string, Texture> pair in textures)
        {
            string property = pair.Key.ToLowerInvariant();
            string file = pair.Value.name.ToLowerInvariant();
            bool isTop = property.Contains("top") || property.Contains("moss") || property.Contains("2") || property.Contains("detail") || property.Contains("blend") && !property.Contains("mask");
            if (isTop != top) continue;
            if (Classify(property, file) == kind) return pair.Value;
        }

        return null;
    }

    private static TextureKind Classify(string property, string file)
    {
        if (property.Contains("bump") || property.Contains("normal") || file.EndsWith("_n") || file.EndsWith("_normal") || file.EndsWith("_nrm") || file.EndsWith("_nm"))
            return TextureKind.Normal;
        if (property.Contains("mask") || property.Contains("noise") || file.Contains("noise") || file.EndsWith("_mask"))
            return TextureKind.Mask;
        if (property.Contains("spec") || property.Contains("metal") || property.Contains("gloss") || property.Contains("rough") || property.Contains("smooth")
            || file.EndsWith("_ms") || file.EndsWith("_s") || file.EndsWith("_sg") || file.EndsWith("_spec"))
            return TextureKind.Smoothness;
        return TextureKind.Albedo;
    }

    private static Texture Find(Dictionary<string, Texture> textures, params string[] names)
    {
        foreach (string name in names)
        {
            if (textures.TryGetValue(name, out Texture texture)) return texture;
        }

        return null;
    }

    private static string KeyOf(Dictionary<string, Texture> textures, Texture texture)
    {
        return texture == null ? null : textures.First(pair => pair.Value == texture).Key;
    }

    private static void SetTex(Material m, string property, Texture texture, Dictionary<string, Texture> textures,
        Dictionary<string, Vector2> scales, Dictionary<string, Vector2> offsets, string source)
    {
        if (texture == null || !m.HasProperty(property)) return;
        m.SetTexture(property, texture);
        string key = KeyOf(textures, texture) ?? source;
        if (scales.ContainsKey(key))
        {
            m.SetTextureScale(property, scales[key]);
            m.SetTextureOffset(property, offsets[key]);
        }
    }

    private static void SetColor(Material m, string property, Color value)
    {
        if (m.HasProperty(property)) m.SetColor(property, value);
    }

    private static void SetFloat(Material m, string property, float value)
    {
        if (m.HasProperty(property)) m.SetFloat(property, value);
    }

    // blend: 0 알파, 1 프리멀티플라이, 2 가산
    private static void MakeTransparent(Material m, int blend)
    {
        SetFloat(m, "_Surface", 1f);
        SetFloat(m, "_Blend", blend);
        SetFloat(m, "_ZWrite", 0f);
        SetFloat(m, "_SrcBlend", blend == 1 ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
        SetFloat(m, "_DstBlend", blend == 2 ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (blend == 1) m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
    }

    // URP 에디터의 키워드 정리 함수를 쓸 수 있으면 쓴다. (없으면 위에서 켠 키워드 그대로)
    private static void ApplyUrpKeywords(Material m)
    {
        try
        {
            Type baseGui = Type.GetType("UnityEditor.BaseShaderGUI, Unity.RenderPipelines.Universal.Editor");
            MethodInfo method = baseGui?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(info => info.Name == "SetMaterialKeywords" && info.GetParameters().Length == 3);
            if (method != null)
            {
                Delegate shading = null;
                if (m.shader != null && m.shader.name == "Universal Render Pipeline/Lit")
                {
                    Type litGui = Type.GetType("UnityEditor.Rendering.Universal.ShaderGUI.LitGUI, Unity.RenderPipelines.Universal.Editor");
                    MethodInfo litKeywords = litGui?.GetMethod("SetMaterialKeywords", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Material) }, null);
                    if (litKeywords != null) shading = Delegate.CreateDelegate(typeof(Action<Material>), litKeywords);
                }

                method.Invoke(null, new object[] { m, shading, null });
            }
        }
        catch (Exception)
        {
            // 키워드는 이미 수동으로 켜 두었다.
        }

        if (m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") != null) m.EnableKeyword("_NORMALMAP");
        if (m.HasProperty("_MetallicGlossMap") && m.GetTexture("_MetallicGlossMap") != null) m.EnableKeyword("_METALLICSPECGLOSSMAP");
        if (m.HasProperty("_SpecGlossMap") && m.GetTexture("_SpecGlossMap") != null && m.HasProperty("_WorkflowMode") && m.GetFloat("_WorkflowMode") < 0.5f)
        {
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.EnableKeyword("_SPECULAR_SETUP");
        }
        if (m.HasProperty("_OcclusionMap") && m.GetTexture("_OcclusionMap") != null) m.EnableKeyword("_OCCLUSIONMAP");
        if (m.HasProperty("_SmoothnessTextureChannel") && m.GetFloat("_SmoothnessTextureChannel") > 0.5f) m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        if (m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f) m.EnableKeyword("_ALPHATEST_ON");
    }

    // ======================================================================
    // 지형 / 레이어 / 데모 전용 오브젝트
    // ======================================================================

    private static void SetupTerrain(Transform environment)
    {
        Shader terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        foreach (Terrain terrain in environment.GetComponentsInChildren<Terrain>(true))
        {
            if (terrainShader != null)
            {
                string path = Folder + "/Terrain_URP.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(terrainShader) { name = "Terrain_URP" };
                    AssetDatabase.CreateAsset(material, path);
                }

                terrain.materialTemplate = material;
            }

            // URP에서는 Tree Creator 나무 빌보드가 깨지므로 빌보드 거리를 멀리 둔다.
            terrain.treeBillboardDistance = Mathf.Max(terrain.treeBillboardDistance, 1200f);
            terrain.drawInstanced = true;
            TerrainData data = terrain.terrainData;
            Log.Add("✔ 지형 '" + terrain.name + "': URP Terrain/Lit 머티리얼, 크기 " + (data != null ? data.size.ToString("0") : "?")
                    + ", 나무 " + (data != null ? data.treeInstanceCount : 0) + "그루");
        }
    }

    // 에셋 오브젝트가 우연히 Player/Zombie 레이어를 쓰면 총알·NavMesh가 꼬이므로 Default로 돌린다.
    private static void FixLayers(Transform environment)
    {
        int fixedCount = 0;
        foreach (Transform t in environment.GetComponentsInChildren<Transform>(true))
        {
            string layer = LayerMask.LayerToName(t.gameObject.layer);
            if (layer == "Player" || layer == "Zombie")
            {
                t.gameObject.layer = 0;
                fixedCount++;
            }
        }

        if (fixedCount > 0)
        {
            Log.Add("· Player/Zombie 레이어를 쓰던 에셋 오브젝트 " + fixedCount + "개를 Default 레이어로 바꿨습니다.");
        }
    }

    // 하늘을 떠다니는 UFO(데모 장식)는 좀비 맵에 어울리지 않아 끈다. (지우지 않음)
    private static void DisableDemoExtras(Transform environment)
    {
        int count = 0;
        foreach (Transform t in environment.GetComponentsInChildren<Transform>(true))
        {
            if (t.parent == environment && t.name.StartsWith("Prop_FlyingSaucer_Animated"))
            {
                t.gameObject.SetActive(false);
                count++;
            }
        }

        if (count > 0)
        {
            Log.Add("· 하늘을 나는 UFO 장식 " + count + "개는 꺼 두었습니다. (Environment 안에 그대로 있음)");
        }
    }

    // ======================================================================
    // 경로 / 안전가옥 / 피날레 / 벽
    // ======================================================================

    private static CityMapInfo BuildGameplay(Transform root, Layout layout)
    {
        Random random = new Random(20261030);
        Transform route = Child(root, "Route");
        Transform spawnRoot = Child(root, "SpawnPoints");
        Transform boundary = Child(root, "Boundary");
        Transform barricades = Child(root, "Barricades");
        Transform guides = Child(root, "Guides");
        Material wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/L4DMap/Materials/Wood.mat");
        Material military = AssetDatabase.LoadAssetAtPath<Material>("Assets/L4DMap/Materials/Military.mat");
        Material lampRed = AssetDatabase.LoadAssetAtPath<Material>("Assets/L4DMap/Materials/LampRed.mat");

        // 진행 경로
        List<Transform> routePoints = new List<Transform>();
        for (int i = 0; i + 2 < layout.route.Length; i += 3)
        {
            routePoints.Add(Point(route, "Route_" + (i / 3).ToString("000"), V(layout.route, i)));
        }

        // 좀비 스폰 포인트
        List<Transform> spawns = new List<Transform>();
        for (int i = 0; i + 2 < layout.spawns.Length; i += 3)
        {
            spawns.Add(Point(spawnRoot, "SpawnPoint_" + (i / 3).ToString("000"), V(layout.spawns, i)));
        }

        // 출발
        Transform start = Point(root, "StartPoint", V(layout.start, 0));
        start.rotation = Quaternion.Euler(0f, layout.start[3], 0f);
        BoxCollider startArea = Area(root, "StartSafeRoom", layout.startArea);
        Light startLight = AddLight(startArea.transform, "StartLight", V(layout.start, 0) + Vector3.up * 3f, new Color(1f, 0.8f, 0.55f), 2.2f, 12f);
        startLight.shadows = LightShadows.None;

        // 중간 안전가옥 (벽돌집 2층 방): 안에서는 좀비가 새로 나오지 않는다.
        BoxCollider midRoom = Area(root, "MidSafeRoom", layout.midRoom);
        Point(midRoom.transform, "MidAmmoCrate", V(layout.midCrates, 0));
        Point(midRoom.transform, "MidMedKitBox", V(layout.midCrates, 3));
        Light midLight = AddLight(midRoom.transform, "SafeRoomLight", V(layout.midLight, 0), new Color(1f, 0.78f, 0.5f), 2.4f, 9f);
        midLight.shadows = LightShadows.Soft;

        // 피날레 (언덕 저택 앞마당) + 무전기 + 탈출 보트
        BoxCollider finale = Area(root, "FinalePlaza", layout.finale);
        Transform radioRoot = Child(root, "RadioStation");
        Vector3 radioPosition = Ground(V(layout.radio, 0), null);
        Box("RadioTable", radioRoot, radioPosition + new Vector3(0f, 0.45f, 0f), new Vector3(1.4f, 0.9f, 0.8f), wood, true);
        Box("Radio", radioRoot, radioPosition + new Vector3(0f, 1.05f, 0f), new Vector3(0.6f, 0.3f, 0.35f), military, false);
        Light radioLight = AddLight(radioRoot, "RadioLight", radioPosition + new Vector3(0f, 1.4f, 0f), new Color(0.3f, 1f, 0.4f), 1.4f, 5f);
        AddFlicker(radioLight, MapFlickerLight.FlickerMode.Siren, 1f);

        Transform extraction = Point(root, "ExtractionPoint", V(layout.extraction, 0));
        GameObject boatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoatPrefab) ?? AssetDatabase.LoadAssetAtPath<GameObject>(BoatPrefabStill);
        if (boatPrefab != null)
        {
            GameObject boat = (GameObject)PrefabUtility.InstantiatePrefab(boatPrefab, extraction);
            boat.name = "RescueBoat";
            boat.transform.position = V(layout.boat, 0);
            boat.transform.rotation = Quaternion.Euler(0f, layout.boat[3], 0f);
        }
        else
        {
            Warnings.Add("⚠ 보트 프리팹(Prop_Boat_A)을 찾지 못해 탈출 지점에 보트 모델을 놓지 못했습니다.");
        }

        Light dockLight = AddLight(extraction, "DockLight", V(layout.extraction, 0) + Vector3.up * 3f, new Color(1f, 0.85f, 0.6f), 1.8f, 10f);
        dockLight.shadows = LightShadows.None;

        // 보이지 않는 벽 + 원본 에셋 바리케이드
        BuildWalls(layout, boundary, barricades, random, out int wallCount, out int pieces, out int cars, out int boards);

        // 길 안내: 꺾이는 곳마다 붉은 조명탄
        int flares = 0;
        for (int i = 0; i + 2 < layout.guides.Length; i += 3)
        {
            Vector3 p = Ground(V(layout.guides, i), barricades);
            Transform flare = Box("Flare_" + (i / 3).ToString("00"), guides, p + new Vector3(0f, 0.08f, 0f), new Vector3(0.12f, 0.12f, 0.35f), lampRed, false).transform;
            flare.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            Light light = AddLight(flare, "FlareLight", p + Vector3.up * 0.6f, new Color(1f, 0.25f, 0.12f), 2.2f, 9f);
            AddFlicker(light, MapFlickerLight.FlickerMode.Fire, 1.3f);
            flares++;
        }

        CityMapInfo info = root.gameObject.AddComponent<CityMapInfo>();
        SerializedObject so = new SerializedObject(info);
        so.FindProperty("_startPoint").objectReferenceValue = start;
        so.FindProperty("_startSafeRoomArea").objectReferenceValue = startArea;
        SetArray(so.FindProperty("_routePoints"), routePoints);
        SetArray(so.FindProperty("_spawnPoints"), spawns);
        SetArray(so.FindProperty("_midSafeRooms"), new List<Object> { midRoom });
        so.FindProperty("_finaleArea").objectReferenceValue = finale;
        so.FindProperty("_extractionPoint").objectReferenceValue = extraction;
        SetBool(so, "_bakedNavigation", true);
        SetBool(so, "_keepSceneAtmosphere", true);
        SetString(so, "_textStart", "물에 잠긴 마을을 지나 북서쪽 언덕 저택의 무전기로 가라");
        SetString(so, "_textAfterMidRoom", "마을 광장을 지나 저택을 통과해 북서쪽 언덕으로 가라");
        SetString(so, "_textRadioFar", "언덕 저택 앞 무전기로 가라");
        SetString(so, "_textHoldOut", "구조 보트가 올 때까지 버텨라!");
        SetString(so, "_textTimer", "보트 도착까지 {0}초");
        SetString(so, "_textEscape", "보트가 왔다! 북쪽 선착장으로 달려라!");
        SetString(so, "_textArrive", "구조 보트 도착!");
        so.ApplyModifiedPropertiesWithoutUndo();

        Log.Add("✔ 진행 경로 지점 " + routePoints.Count + "개, 좀비 스폰 포인트 " + spawns.Count + "개 (피날레용 포함)");
        Log.Add("✔ 출발 지점, 중간 안전가옥(벽돌집), 피날레 광장(언덕 저택 앞), 무전기, 탈출 보트(북쪽 선착장) 배치");
        Log.Add("✔ 경계: 보이지 않는 벽 " + wallCount + "개 (Ignore Raycast 레이어 - 총알은 통과, 이동만 막음)");
        Log.Add("✔ 바리케이드: 원본 에셋 담장 " + pieces + "개, 폐차 " + cars + "대, 문 막는 판자 " + boards + "개 / 길 안내 조명탄 " + flares + "개");
        return info;
    }

    private static void BuildWalls(Layout layout, Transform boundary, Transform barricades, Random random,
        out int wallCount, out int pieces, out int cars, out int boards)
    {
        wallCount = 0;
        pieces = 0;
        cars = 0;
        boards = 0;
        PieceInfo fence = Piece(FencePrefab);
        PieceInfo fenceAlt = Piece(FenceAltPrefab);
        PieceInfo board = Piece(BoardPrefab);
        PieceInfo carA = Piece(CarPrefabA);
        PieceInfo carB = Piece(CarPrefabB);
        if (fence == null)
        {
            Warnings.Add("⚠ 담장 프리팹(" + FencePrefab + ")을 찾지 못해 보이는 바리케이드 없이 보이지 않는 벽만 놓았습니다.");
        }

        for (int i = 0; i + 8 < layout.walls.Length; i += 9)
        {
            Vector3 a = new Vector3(layout.walls[i], 0f, layout.walls[i + 1]);
            Vector3 b = new Vector3(layout.walls[i + 2], 0f, layout.walls[i + 3]);
            float y0 = layout.walls[i + 4];
            float y1 = layout.walls[i + 5];
            int visual = Mathf.RoundToInt(layout.walls[i + 7]);
            float side = layout.walls[i + 8]; // +1: 왼쪽 법선 방향이 경로 바깥, -1: 반대, 0: 양쪽 다 경로
            Vector3 direction = b - a;
            float length = direction.magnitude;
            Vector3 forward = length > 0.01f ? direction / length : Vector3.forward;

            GameObject wall = new GameObject("Wall_" + (i / 9).ToString("000"));
            wall.layer = BoundaryLayer;
            wall.transform.SetParent(boundary, false);
            wall.transform.position = new Vector3((a.x + b.x) * 0.5f, (y0 + y1) * 0.5f, (a.z + b.z) * 0.5f);
            wall.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.6f, Mathf.Max(2f, y1 - y0), length + 1.2f);
            wallCount++;

            if (visual == 1 && fence != null)
            {
                Transform group = new GameObject("Barricade_" + (i / 9).ToString("000")).transform;
                // 담장 두께만큼 경로 바깥쪽으로 밀어 놓아 길을 좁히지 않는다.
                Vector3 outward = new Vector3(-forward.z, 0f, forward.x) * side * 0.5f;
                group.SetParent(barricades, false);
                float used = 0f;
                while (used < length - 0.4f)
                {
                    float left = length - used;
                    PieceInfo info = fence;
                    if (fenceAlt != null && random.NextDouble() < 0.18) info = fenceAlt;
                    bool car = left > 9f && random.NextDouble() < 0.14 && (carA != null || carB != null);
                    if (car)
                    {
                        info = random.NextDouble() < 0.5 && carA != null ? carA : (carB ?? carA);
                    }

                    float pieceLength = Mathf.Min(info.Length, Mathf.Max(left, info.Length * 0.6f));
                    Vector3 center = a + forward * (used + pieceLength * 0.5f) + outward;
                    bool flip = random.NextDouble() < 0.5;
                    float jitter = car ? (float)(random.NextDouble() * 16.0 - 8.0) : 0f;
                    PlacePiece(info, group, center, forward, flip, jitter, y0, y1, barricades);
                    used += pieceLength;
                    if (car) cars++; else pieces++;
                }
            }
            else if (visual == 2 && board != null)
            {
                Transform group = new GameObject("DoorBoards_" + (i / 9).ToString("000")).transform;
                group.SetParent(barricades, false);
                int count = Mathf.Max(1, Mathf.CeilToInt((length + 0.6f) / (board.Length * 0.85f)));
                for (int k = 0; k < count; k++)
                {
                    float t = count == 1 ? 0.5f : (float)k / (count - 1);
                    Vector3 center = Vector3.Lerp(a - forward * 0.3f, b + forward * 0.3f, t);
                    PlacePiece(board, group, center, forward, random.NextDouble() < 0.5, (float)(random.NextDouble() * 10.0 - 5.0), y0, y1, barricades);
                    boards++;
                }
            }
        }
    }

    private static readonly Dictionary<string, PieceInfo> Pieces = new Dictionary<string, PieceInfo>();

    // 프리팹의 긴 축과 크기를 한 번 재 둔다.
    private static PieceInfo Piece(string path)
    {
        if (Pieces.TryGetValue(path, out PieceInfo cached)) return cached;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        PieceInfo info = null;
        if (prefab != null)
        {
            GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Renderer[] renderers = temp.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                bool x = bounds.size.x >= bounds.size.z;
                info = new PieceInfo
                {
                    Prefab = prefab,
                    LongAxisIsX = x,
                    Length = Mathf.Max(0.5f, x ? bounds.size.x : bounds.size.z),
                    PivotToCenter = new Vector3(bounds.center.x, 0f, bounds.center.z),
                    BottomOffset = bounds.min.y,
                };
            }

            Object.DestroyImmediate(temp);
        }

        Pieces[path] = info;
        return info;
    }

    private static void PlacePiece(PieceInfo info, Transform parent, Vector3 center, Vector3 forward, bool flip, float yawJitter,
        float y0, float y1, Transform ignore)
    {
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, (info.LongAxisIsX ? -90f : 0f) + (flip ? 180f : 0f) + yawJitter, 0f);
        // 벽 데이터: y0 = 가장 낮은 땅 - 2.5, y1 = 가장 높은 땅 + 12
        float ground = GroundHeight(center, y0 - 2f, y1 - 9f, ignore, out bool hit);
        if (!hit) ground = y0 + 2.5f;
        GameObject piece = (GameObject)PrefabUtility.InstantiatePrefab(info.Prefab, parent);
        Vector3 offset = rotation * info.PivotToCenter;
        piece.transform.SetPositionAndRotation(new Vector3(center.x - offset.x, ground - info.BottomOffset - 0.12f, center.z - offset.z), rotation);
    }

    // 위에서 아래로 쏴서 땅 높이를 찾는다. (바리케이드/경계벽/트리거는 무시)
    // top: 쏘기 시작할 높이, bottom: 이보다 아래는 찾지 않는다.
    private static float GroundHeight(Vector3 p, float bottom, float top, Transform ignore, out bool found)
    {
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, top, p.z), Vector3.down, Mathf.Max(1f, top - bottom), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits.OrderBy(h => h.distance))
        {
            if (ignore != null && h.transform.IsChildOf(ignore)) continue;
            found = true;
            return h.point.y;
        }

        found = false;
        return bottom;
    }

    private static Vector3 Ground(Vector3 p, Transform ignore)
    {
        float y = GroundHeight(p, p.y - 4f, p.y + 2.5f, ignore, out bool found);
        return found ? new Vector3(p.x, y, p.z) : p;
    }

    // ======================================================================
    // Player / 스포너
    // ======================================================================

    private static void MovePlayer(Layout layout)
    {
        PlayerMove player = Object.FindAnyObjectByType<PlayerMove>();
        if (player == null)
        {
            Warnings.Add("⚠ Player(PlayerMove)를 찾지 못했습니다.");
            return;
        }

        Vector3 start = Ground(V(layout.start, 0), null);
        CharacterController controller = player.GetComponent<CharacterController>();
        float lift = controller != null ? controller.height * 0.5f - controller.center.y + controller.skinWidth + 0.05f : 1f;
        player.transform.position = start + Vector3.up * lift;
        player.transform.rotation = Quaternion.Euler(0f, layout.start[3], 0f);
        Camera camera = Camera.main;
        if (camera != null && !camera.transform.IsChildOf(player.transform))
        {
            camera.transform.rotation = Quaternion.identity;
        }

        EditorUtility.SetDirty(player.transform);
        Log.Add("✔ Player를 출발 지점(남쪽 둑길 끝, " + start.ToString("0") + ")으로 옮겼습니다. 동료 AI는 시작할 때 Player 옆에 생깁니다.");
    }

    private static void AssignSpawner(Transform[] spawnPoints)
    {
        ZombieSpawner spawner = Object.FindAnyObjectByType<ZombieSpawner>();
        if (spawner == null)
        {
            Log.Add("· ZombieSpawner가 없어 스폰 포인트 연결은 건너뜁니다. (AIDirector가 CityMapInfo를 씁니다)");
            return;
        }

        SerializedObject so = new SerializedObject(spawner);
        SerializedProperty property = so.FindProperty("_spawnPoints");
        if (property != null)
        {
            SetArray(property, spawnPoints.Cast<Object>().ToList());
            so.ApplyModifiedPropertiesWithoutUndo();
            Log.Add("✔ ZombieSpawner의 스폰 포인트를 새 맵의 " + spawnPoints.Length + "곳으로 바꿨습니다. (평소에는 AIDirector가 대신 스폰)");
        }
    }

    // ======================================================================
    // NavMesh
    // ======================================================================

    private static void BakeNavMesh(GameObject root, Layout layout)
    {
        foreach (NavMeshSurface other in Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (other.gameObject != root && other.gameObject.scene == root.scene && !other.transform.IsChildOf(root.transform))
            {
                other.enabled = false;
                Warnings.Add("⚠ 다른 NavMeshSurface('" + other.name + "')를 껐습니다.");
            }
        }

        NavMeshSurface surface = root.GetComponent<NavMeshSurface>();
        if (surface == null) surface = root.AddComponent<NavMeshSurface>();
        Bounds bounds = LayoutBounds(layout);
        surface.collectObjects = CollectObjects.Volume;
        surface.center = root.transform.InverseTransformPoint(bounds.center);
        surface.size = bounds.size;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        int excluded = 0;
        foreach (string layer in new[] { "Player", "Zombie" })
        {
            int index = LayerMask.NameToLayer(layer);
            if (index >= 0) excluded |= 1 << index;
        }

        surface.layerMask = ~excluded;
        surface.ignoreNavMeshAgent = true;
        surface.ignoreNavMeshObstacle = true;
        if (NavMesh.GetSettingsCount() > 0) surface.agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID;
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(surface.agentTypeID);
        surface.overrideVoxelSize = true;
        surface.voxelSize = Mathf.Clamp(settings.agentRadius / 3f, 0.08f, 0.17f);
        surface.overrideTileSize = true;
        surface.tileSize = 256;

        NavMesh.RemoveAllNavMeshData();
        Physics.SyncTransforms();
        Stopwatch watch = Stopwatch.StartNew();
        surface.BuildNavMesh();
        NavMeshData data = surface.navMeshData;
        if (data == null)
        {
            throw new InvalidOperationException("NavMesh 굽기에 실패했습니다.");
        }

        EnsureFolder(Folder);
        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAssetPath) != null && AssetDatabase.GetAssetPath(data) != NavMeshAssetPath)
        {
            AssetDatabase.DeleteAsset(NavMeshAssetPath);
        }

        if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(data)))
        {
            AssetDatabase.CreateAsset(data, NavMeshAssetPath);
        }

        surface.navMeshData = data;
        EditorUtility.SetDirty(surface);
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        Log.Add("✔ NavMesh 굽기: 범위 " + bounds.size.ToString("0") + ", 복셀 " + surface.voxelSize.ToString("0.###") + " m, 삼각형 "
                + (triangulation.indices != null ? triangulation.indices.Length / 3 : 0) + "개, " + watch.Elapsed.TotalSeconds.ToString("0") + "초 → " + NavMeshAssetPath);
    }

    private static Bounds LayoutBounds(Layout layout)
    {
        Bounds bounds = new Bounds(V(layout.start, 0), Vector3.one);
        for (int i = 0; i + 2 < layout.route.Length; i += 3) bounds.Encapsulate(V(layout.route, i));
        for (int i = 0; i + 8 < layout.walls.Length; i += 9)
        {
            bounds.Encapsulate(new Vector3(layout.walls[i], layout.walls[i + 4], layout.walls[i + 1]));
            bounds.Encapsulate(new Vector3(layout.walls[i + 2], layout.walls[i + 5], layout.walls[i + 3]));
        }

        bounds.Encapsulate(V(layout.extraction, 0));
        bounds.Expand(new Vector3(30f, 20f, 30f));
        return bounds;
    }

    // ======================================================================
    // 검사
    // ======================================================================

    private static void Verify(Layout layout, CityMapInfo info, float oldPolyline, float oldNavPath)
    {
        NavMeshPath path = new NavMeshPath();
        Transform[] route = info.RoutePoints;

        // 1. 진행 경로를 따라 끝까지 걸어갈 수 있는가 (실제 NavMesh 경로 길이)
        float routeLength = 0f;
        List<int> broken = new List<int>();
        for (int i = 0; i < route.Length - 1; i++)
        {
            if (PathLength(route[i].position, route[i + 1].position, path, out float length))
            {
                routeLength += length;
            }
            else
            {
                broken.Add(i);
            }
        }

        // 2. 출발 → 무전기 → 탈출 지점 최단 거리 (지름길이 있으면 짧게 나온다)
        Transform radio = info.transform.Find("RadioStation/Radio");
        Vector3 startPosition = info.StartPoint.position;
        bool toRadio = PathLength(startPosition, radio != null ? radio.position : route[route.Length - 1].position, path, out float shortestToRadio);
        bool toBoat = PathLength(radio != null ? radio.position : route[route.Length - 1].position, info.ExtractionPoint.position, path, out float radioToBoat);

        // 3. 경로 밖으로 새는 곳 (벽 바깥 지점까지 걸어갈 수 있으면 누수)
        int probes = 0;
        List<string> leaks = new List<string>();
        for (int i = 0; layout.probes != null && i + 2 < layout.probes.Length; i += 3)
        {
            Vector3 probe = V(layout.probes, i);
            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 1.2f, NavMesh.AllAreas)) continue;
            probes++;
            if (PathLength(startPosition, hit.position, path, out _))
            {
                leaks.Add(hit.position.ToString("0"));
            }
        }

        // 4. 스폰 포인트가 NavMesh 위이고 플레이어 쪽으로 올 수 있는가
        int badSpawns = 0;
        foreach (Transform spawn in info.SpawnPoints)
        {
            if (!PathLength(spawn.position, startPosition, path, out _)) badSpawns++;
        }

        // 5. 분홍(지원 안 되는) 머티리얼
        int pink = 0;
        HashSet<string> pinkNames = new HashSet<string>();
        {
            foreach (Renderer renderer in info.transform.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    Shader shader = material.shader;
                    bool bad = shader == null || !shader.isSupported || shader.name == "Hidden/InternalErrorShader" || ShaderUtil.ShaderHasError(shader)
                               || !(shader.name.StartsWith("Universal Render Pipeline/") || shader.name.StartsWith("L4D/") || shader.name.Contains("Skybox") || shader.name.StartsWith("Shader Graphs/"));
                    if (bad)
                    {
                        pink++;
                        pinkNames.Add(material.name + " (" + (shader != null ? shader.name : "null") + ")");
                    }
                }
            }
        }

        Log.Add("");
        Log.Add("■ 검사 결과 (NavMesh 기준)");
        Log.Add("  - 진행 경로(지점 " + route.Length + "개)를 따라 걸은 실제 길이: " + routeLength.ToString("0") + " m"
                + (broken.Count == 0 ? " / 끊긴 곳 없음" : " / ❌ 끊긴 구간 " + broken.Count + "곳: " + string.Join(", ", broken.Take(12).Select(i => "Route_" + i.ToString("000") + "→" + (i + 1).ToString("000")))));
        Log.Add("  - 출발 → 무전기 최단 경로: " + (toRadio ? shortestToRadio.ToString("0") + " m" : "❌ 갈 수 없음")
                + " / 무전기 → 탈출 보트: " + (toBoat ? radioToBoat.ToString("0") + " m" : "❌ 갈 수 없음"));
        if (oldPolyline > 0f)
        {
            float shortest = toRadio && toBoat ? shortestToRadio + radioToBoat : routeLength;
            Log.Add("  - 기존 맵 진행 경로 " + oldPolyline.ToString("0") + " m" + (oldNavPath > 0f ? " (NavMesh " + oldNavPath.ToString("0") + " m)" : string.Empty)
                    + " → 새 맵 최단 경로 " + shortest.ToString("0") + " m (" + (shortest >= oldPolyline ? "✔ 더 김, " + (shortest / oldPolyline).ToString("0.0") + "배" : "❌ 더 짧음") + ")");
        }

        Log.Add("  - 경계 밖 검사 지점 " + probes + "곳 중 출발점에서 걸어갈 수 있는 곳: " + (leaks.Count == 0 ? "0곳 ✔" : "❌ " + leaks.Count + "곳 " + string.Join(" ", leaks.Take(10))));
        Log.Add("  - 좀비 스폰 포인트 " + info.SpawnPoints.Length + "곳 중 플레이어까지 길이 없는 곳: " + badSpawns + "곳");
        Log.Add("  - URP에서 그릴 수 없는(분홍) 머티리얼 사용: " + (pink == 0 ? "0개 ✔" : "❌ " + pink + "개: " + string.Join(", ", pinkNames.Take(10))));

        if (broken.Count > 0) Warnings.Add("❌ 진행 경로가 NavMesh에서 " + broken.Count + "곳 끊겼습니다. (보고서의 Route 번호 참고)");
        if (leaks.Count > 0) Warnings.Add("⚠ 경계 밖으로 걸어갈 수 있는 곳이 " + leaks.Count + "곳 있습니다. (보고서 좌표 참고)");
        if (pink > 0) Warnings.Add("⚠ 분홍색으로 보일 머티리얼이 " + pink + "개 있습니다.");
    }

    private static bool PathLength(Vector3 from, Vector3 to, NavMeshPath path, out float length)
    {
        length = 0f;
        if (!NavMesh.SamplePosition(from, out NavMeshHit a, 3f, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out NavMeshHit b, 3f, NavMesh.AllAreas))
        {
            return false;
        }

        if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        Vector3[] corners = path.corners;
        for (int i = 0; i < corners.Length - 1; i++) length += Vector3.Distance(corners[i], corners[i + 1]);
        return true;
    }

    // ======================================================================
    // 보고서
    // ======================================================================

    private static void WriteReport(bool success)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("Flooded Grounds 맵 교체 보고서 - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        text.AppendLine(success ? "결과: 완료" : "결과: 실패 (씬 저장 안 함)");
        text.AppendLine();
        foreach (string line in Log) text.AppendLine(line);
        if (Warnings.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("■ 확인이 필요한 항목");
            foreach (string line in Warnings) text.AppendLine("  " + line);
        }

        text.AppendLine();
        text.AppendLine("■ 구역 순서");
        text.AppendLine("  A 출발(남쪽 둑길 끝) → B 남쪽 둑길·물가 길 → B2 마을 남쪽 거리·동쪽 골목 → 동쪽 판잣길 → 벽돌집(중간 안전가옥)");
        text.AppendLine("  → 북쪽 물가 길 → D 마을 광장(대규모 전투) → C 저택 내부 통과(북문→남문) → E 서쪽 마을 골목·정원 옆길");
        text.AppendLine("  → F 언덕 저택(피날레: 무전기로 구조 요청, 버티기) → 북쪽 선착장 보트(탈출)");

        try
        {
            EnsureFolder(Folder);
            File.WriteAllText(ReportPath, text.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ReportPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[Flooded 맵] 보고서 저장 실패: " + exception.Message);
        }

        Debug.Log("[Flooded 맵]\n" + text);
        string summary = string.Join("\n", Log.Where(l => l.StartsWith("  -")).Take(8));
        string warn = Warnings.Count > 0 ? "\n\n확인 필요:\n" + string.Join("\n", Warnings.Take(6)) : string.Empty;
        EditorUtility.DisplayDialog(success ? "Flooded 맵 완료" : "Flooded 맵 실패",
            (success ? "맵 교체를 마쳤습니다. ▶ Play로 확인해 보세요.\n\n" : string.Empty) + summary + warn + "\n\n전체 보고서: " + ReportPath, "확인");
    }

    // ======================================================================
    // 도우미
    // ======================================================================

    private static void Progress(string message, float value)
    {
        EditorUtility.DisplayProgressBar("Flooded Grounds 맵으로 교체", message, value);
    }

    private static Vector3 V(float[] values, int index)
    {
        return new Vector3(values[index], values[index + 1], values[index + 2]);
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Transform Point(Transform parent, string name, Vector3 position)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        return go.transform;
    }

    // 범위 상자 (트리거, Ignore Raycast 레이어 - 총알/좀비 길찾기에 영향 없음)
    private static BoxCollider Area(Transform parent, string name, float[] box)
    {
        GameObject go = new GameObject(name);
        go.layer = BoundaryLayer;
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(box[0], box[1], box[2]);
        BoxCollider collider = go.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(box[3], box[4], box[5]);
        go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        return collider;
    }

    private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = size;
        if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static Light AddLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        return light;
    }

    private static void AddFlicker(Light light, MapFlickerLight.FlickerMode mode, float speed)
    {
        MapFlickerLight flicker = light.gameObject.AddComponent<MapFlickerLight>();
        flicker.Setup(mode, speed);
    }

    private static void SetArray<T>(SerializedProperty property, List<T> values) where T : Object
    {
        if (property == null) return;
        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.boolValue = value;
    }

    private static void SetString(SerializedObject so, string name, string value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.stringValue = value;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Replace(' ', '_');
    }

    private static T GetStaticProperty<T>(Type type, string name) where T : class
    {
        PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
        return property != null ? property.GetValue(null) as T : null;
    }

    private static bool SetStaticProperty(Type type, string name, object value)
    {
        PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
        if (property == null || !property.CanWrite) return false;
        try
        {
            property.SetValue(null, value);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
#endif
