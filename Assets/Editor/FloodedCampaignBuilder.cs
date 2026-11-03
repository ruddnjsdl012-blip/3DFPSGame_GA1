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

// Flooded Grounds 에셋으로 L4D2 / World War Z / Back 4 Blood 느낌의 긴 선형 캠페인 맵(약 2 km)을 만드는 도구
// 메뉴: Tools > L4D 맵 > 플러디드 캠페인 맵 만들기 (기존 맵 삭제)
//
// 맵 설계(지형 높이, 길, 건물/소품 위치, 경계, 스폰, 조명)는 Assets/L4DCampaign/Data 의 파일에 들어 있고,
// 이 도구는 그 설계대로 씬을 만든다. 설계는 오프라인에서 콜라이더 기준으로 "출발 → 무전기 → 탈출"이 이어지는지 검사한 것이다.
//
// 구역 순서 (약 2 km, 15~20분)
//   1 늪지 둑길(출발 오두막) → 2 섬 → 3 물가 마을 → 4 농장 → 5 온실 통과 → 6 공장 지대 → 7 공장 마당(전투)
//   → 8 뒷골목 → 9 강변 도로 → 10 철교 → 11 강변 → 중간 안전가옥(오두막) → 12 읍내 → 13 교회 광장(전투) → 14 뒷길
//   → 15 읍내 서쪽 → 16 저택 통과 → 17 정원(피날레: 무전기, 버티기) → 18 선착장 끝 보트(탈출)
//
// 최적화
//   - 조립된 건물(Pref_*)의 조각 수백 개를 머티리얼별로 합친 메시 1개 + LODGroup(멀리서 안 그림) + 합친 MeshCollider 1개로 만든다.
//   - 지형 나무는 URP Lit 머티리얼을 쓰는 가벼운 나무 프리팹으로 바꿔 GPU 인스턴싱으로 그린다. (Tree Creator 셰이더는 URP에서 분홍)
//   - 맵 오브젝트는 Static(배칭/오클루전), 카메라 먼 거리 320 m + 안개, 나무 거리 240 m.
//   - NavMesh는 에디터에서 구워 에셋으로 저장 (실행할 때 굽지 않음). 오클루전 컬링은 선택해서 굽는다.
public static class FloodedCampaignBuilder
{
    private const string Menu = "Tools/L4D 맵/";
    private const string Folder = "Assets/L4DCampaign";
    private const string DataFolder = Folder + "/Data";
    private const string GeneratedFolder = Folder + "/Generated";
    private const string BuildingFolder = GeneratedFolder + "/Buildings";
    private const string MeshFolder = GeneratedFolder + "/Meshes";
    private const string TreeFolder = GeneratedFolder + "/Trees";
    private const string LayoutPath = DataFolder + "/CampaignLayout.json";
    private const string HeightsPath = DataFolder + "/CampaignHeights.bytes";
    private const string SplatPath = DataFolder + "/CampaignSplat.bytes";
    private const string TerrainDataPath = GeneratedFolder + "/CampaignTerrain.asset";
    private const string TerrainMaterialPath = GeneratedFolder + "/CampaignTerrain_URP.mat";
    private const string NavMeshAssetPath = GeneratedFolder + "/NavMesh-Campaign.asset";
    private const string ReportPath = Folder + "/CampaignReport.txt";
    private const string BackupFolder = "Assets/01.Scenes/Backup";
    private const string BackupPath = BackupFolder + "/L4D_before_campaign.unity";
    private const string GameScenePath = "Assets/01.Scenes/L4D.unity";
    private const string PreassembledScene = "Assets/Flooded_Grounds/Scenes/PreAsembeld_Buildings.unity";
    private const string TreePrefabFolder = "Assets/Flooded_Grounds/Prefabs/Nature/Trees";
    private const string WaterMaterialPath = "Assets/Flooded_Grounds/Content/Materials/BGR_Water.mat";
    private const string SkyMaterialPath = "Assets/Flooded_Grounds/Content/Materials/BGR_Sky1.mat";
    private const string DrumPrefabPath = "Assets/03.Prefabs/Environment/Drum.prefab";
    private const string RootName = "FloodedCampaign";
    private const int BoundaryLayer = 2; // Ignore Raycast: 총알/혈흔/시야 검사는 통과하고, 플레이어/좀비 이동만 막는다.
    private const float TerrainSize = 1024f;
    private const float TerrainHeight = 64f;
    private const int HeightRes = 1025;
    private const int AlphaRes = 1024;
    private const float FarClip = 320f;

    private static readonly string[] TerrainLayerPaths =
    {
        "Assets/_TerrainAutoUpgrade/layer_GR_Moss1_ASGR_Moss1_N3d1cca0d6d0e9938.terrainlayer",
        "Assets/_TerrainAutoUpgrade/layer_GR_Dirt1_ASGR_Dirt1_N3d1cca0d6d0e9938.terrainlayer",
        "Assets/_TerrainAutoUpgrade/layer_GR_Asphalt1_ASGR_Asphalt1_Nd261c09ae55ba1e3.terrainlayer",
    };

    // 예전에 만든 맵/도구 (새 맵이 완성된 뒤에 지운다). 게임 시스템 스크립트와 GameScene은 건드리지 않는다.
    private static readonly string[] OldAssets =
    {
        "Assets/Backup_Map",
        "Assets/Backup_15_16",
        "Assets/L4D_LinearCampaignGenerated",
        "Assets/L4DMap/Kenney",
        "Assets/L4DMap/Materials/K_Cars.mat",
        "Assets/L4DMap/Materials/K_Commercial.mat",
        "Assets/L4DMap/Materials/K_CommercialA.mat",
        "Assets/L4DMap/Materials/K_CommercialB.mat",
        "Assets/L4DMap/Materials/K_Roads.mat",
        "Assets/L4DMap/Flooded/OriginalMaterials",
        "Assets/L4DMap/Flooded/ConvertedMaterials",
        "Assets/L4DMap/Flooded/FloodedMapLayout.json",
        "Assets/L4DMap/Flooded/FloodedMapReport.txt",
        "Assets/L4DMap/Flooded/NavMesh-FloodedMap.asset",
        "Assets/L4DMap/Flooded/Terrain_URP.mat",
        "Assets/01.Scenes/L4D/NavMesh-CityMap.asset",
        "Assets/Editor/L4DLinearRebuild",
        "Assets/Editor/CityMapBuilder.cs",
        "Assets/Editor/CityMapBuilder.Layout.cs",
        "Assets/Editor/FloodedMapBuilder.cs",
        "Assets/Editor/L4DMapInfoExporter.cs",
        "Assets/02.Scripts/Map/L4DRouteClarity.cs",
        "Assets/L4D_LinearRebuild_Readme.txt",
        "Assets/L4D_MapInfo_Readme.txt",
        "Assets/L4D_RouteClarity_Layout.png",
        "Assets/L4D_RouteClarity_Readme.txt",
    };

    [Serializable]
    private class Placement
    {
        public string k;   // "tpl" = 조립 건물(PreAsembeld 씬의 Pref_*), "prefab" = 프리팹
        public string r;   // 건물 이름 또는 프리팹 경로
        public float[] p;  // 루트 위치 (월드)
        public float yaw;
        public float s = 1f;
        public string tag;
    }

    [Serializable]
    private class Layout
    {
        public int version;
        public float[] startArea;   // 가운데 xyz, 크기 xyz
        public float[] start;       // xyz, 바라보는 방향(도)
        public float[] midRoom;
        public float[] midCrates;
        public float[] midLight;
        public float[] startLight;
        public float[] radio;
        public float[] finale;
        public float[] extraction;
        public float[] route;       // xyz 반복 (10 m 간격)
        public float[] spawns;      // xyz 반복
        public float[] walls;       // x0 z0 x1 z1 아래 위 법선x 법선z (법선 = 길 바깥쪽)
        public float[] probes;      // 벽 바깥 3 m 검사 지점 xyz
        public float[] flares;      // 길 안내 조명탄 xyz
        public float[] lights;      // x y z r g b 세기 범위 깜빡임(0 없음 1 불 2 사이렌)
        public float[] drums;       // 폭발 드럼통 xyz
        public Placement[] placements;
        public float[] trees;       // x y z 종류 크기
        public string[] treeProtos;
        public float water;
        public float routeLength;
    }

    private struct Part
    {
        public MeshRenderer Renderer;
        public Mesh Mesh;
        public int LodIndex;   // -1 = LOD 없음 (모든 단계에 포함)
        public int LodCount;
    }

    private sealed class MeshBuffer
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector4> Tangents = new List<Vector4>();
        public readonly List<Vector2> Uv0 = new List<Vector2>();
        public readonly List<Vector2> Uv1 = new List<Vector2>();
        public readonly List<Color> Colors = new List<Color>();
        public readonly List<Material> Materials = new List<Material>();
        public readonly List<List<int>> Indices = new List<List<int>>();
        public bool HasColors;
        public bool HasUv1;
    }

    private static readonly List<string> Log = new List<string>();
    private static readonly List<string> Warnings = new List<string>();
    private static readonly Dictionary<string, GameObject> Templates = new Dictionary<string, GameObject>();
    private static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>();
    private static readonly Dictionary<GameObject, float> BottomCache = new Dictionary<GameObject, float>();

    // ======================================================================
    // 메뉴
    // ======================================================================

    [MenuItem(Menu + "플러디드 캠페인 맵 만들기 (기존 맵 삭제)", priority = 0)]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("캠페인 맵", "Play를 끄고 컴파일이 끝난 뒤 실행해 주세요.", "확인");
            return;
        }

        Layout layout = LoadLayout(out string error);
        if (layout == null)
        {
            EditorUtility.DisplayDialog("캠페인 맵", error + "\n받은 zip의 Assets 폴더를 다시 덮어써 주세요.", "확인");
            return;
        }

        if (!File.Exists(PreassembledScene))
        {
            EditorUtility.DisplayDialog("캠페인 맵", "Flooded Grounds의 " + PreassembledScene + " 씬을 찾지 못했습니다.\nFlooded Grounds 에셋이 프로젝트에 있어야 합니다.", "확인");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        string question = "게임 씬: " + GameScenePath + "\n\n"
                          + "1. 지금 씬을 " + BackupPath + " 하나로 백업합니다.\n"
                          + "2. 씬의 예전 맵을 지우고 (Player·카메라·UI·GameManager·AIDirector 등은 유지)\n"
                          + "   약 2 km 선형 캠페인 맵을 새로 만듭니다.\n"
                          + "3. NavMesh를 굽고 경로·누수·스폰을 검사해 보고서를 남깁니다.\n"
                          + "4. 다 되면 예전 맵 파일(Backup_Map, Backup_15_16, 예전 맵 도구/생성물, Kenney 도시 에셋 등)을 지웁니다.\n\n"
                          + "오클루전 컬링까지 구우면 몇 분 더 걸리지만 프레임이 더 잘 나옵니다.";
        int choice = EditorUtility.DisplayDialogComplex("플러디드 캠페인 맵 만들기", question, "만들기 + 오클루전 굽기", "취소", "만들기만");
        if (choice == 1)
        {
            return;
        }

        bool occlusion = choice == 0;
        Log.Clear();
        Warnings.Clear();
        Templates.Clear();
        PrefabCache.Clear();
        BottomCache.Clear();
        Stopwatch watch = Stopwatch.StartNew();
        bool success = false;
        try
        {
            Progress("게임 씬 열기", 0.01f);
            Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            if (!game.GetRootGameObjects().Any(go => go.GetComponentInChildren<PlayerMove>(true) != null))
            {
                throw new InvalidOperationException(GameScenePath + " 에 Player(PlayerMove)가 없습니다.");
            }

            Progress("씬 백업", 0.02f);
            BackupScene(game);

            Progress("예전 맵 지우기", 0.04f);
            RemoveOldMap(game);

            EnsureFolder(BuildingFolder);
            EnsureFolder(MeshFolder);
            EnsureFolder(TreeFolder);

            Progress("건물 합치기 (조각 → 메시 1개 + LOD + 콜라이더 1개)", 0.06f);
            BuildTemplates(layout);

            GameObject root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, game);
            SetStatic(root, false);

            Progress("지형", 0.3f);
            BuildTerrain(layout, root.transform);

            Progress("물", 0.4f);
            BuildWater(layout, root.transform);

            Progress("건물·소품 배치", 0.45f);
            PlaceObjects(layout, root.transform);

            Progress("조명·안개", 0.55f);
            Light sun = ApplyAtmosphere(root.transform);

            Physics.SyncTransforms();
            Progress("경로·안전가옥·피날레·경계", 0.58f);
            CityMapInfo info = BuildGameplay(layout, root.transform);

            Progress("Player·스포너·카메라", 0.62f);
            MovePlayer(layout);
            AssignSystems(info);

            Progress("NavMesh 굽는 중 (맵이 커서 1~3분 걸릴 수 있습니다)", 0.65f);
            BakeNavMesh(root, layout);

            Progress("검사", 0.88f);
            Verify(layout, info);

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            AssetDatabase.SaveAssets();
            Log.Add("✔ 씬 저장: " + game.path);

            if (occlusion)
            {
                Progress("오클루전 컬링 굽는 중", 0.92f);
                ComputeOcclusion();
                EditorSceneManager.SaveScene(game);
            }

            Log.Add("소요 시간: " + watch.Elapsed.TotalSeconds.ToString("0") + "초");
            success = true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Warnings.Add("❌ 중간에 오류가 났습니다: " + exception.Message + "\n   씬은 저장하지 않았습니다. 씬을 저장하지 말고 다시 열거나 백업(" + BackupPath + ")을 쓰세요.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (success)
        {
            // 예전 맵 파일은 새 맵이 저장된 다음에만 지운다. (스크립트를 지우면 다시 컴파일되므로 맨 마지막)
            DeleteOldAssets();
        }

        WriteReport(success);
    }

    [MenuItem(Menu + "캠페인 맵 NavMesh 다시 굽기 + 검사", priority = 1)]
    public static void Rebake()
    {
        CityMapInfo info = FindCampaign();
        Layout layout = LoadLayout(out string error);
        if (info == null || layout == null)
        {
            EditorUtility.DisplayDialog("캠페인 맵", info == null ? "FloodedCampaign이 있는 L4D 씬을 연 뒤 다시 실행해 주세요." : error, "확인");
            return;
        }

        Log.Clear();
        Warnings.Clear();
        bool success = false;
        try
        {
            Progress("NavMesh 굽는 중", 0.3f);
            Physics.SyncTransforms();
            BakeNavMesh(info.gameObject, layout);
            Progress("검사", 0.9f);
            Verify(layout, info);
            EditorSceneManager.MarkSceneDirty(info.gameObject.scene);
            EditorSceneManager.SaveScene(info.gameObject.scene);
            success = true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Warnings.Add("❌ 오류: " + exception.Message);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        WriteReport(success);
    }

    // 철교 양 끝과 지형 사이에 틈(남쪽 약 2 m, 북쪽 약 5 m)이 있어 NavMesh가 끊겼다. (플레이어는 점프로 건넜지만 좀비/동료는 못 건넘)
    // 고친 높이 데이터(CampaignHeights.bytes)에서 다리 주변만 지형에 다시 써 넣고 NavMesh를 다시 굽는다. (맵 전체를 다시 만들 필요 없음)
    [MenuItem(Menu + "캠페인 맵 철교 끊김 고치기 (지형 + NavMesh)", priority = 3)]
    public static void FixBridge()
    {
        CityMapInfo info = FindCampaign();
        Layout layout = LoadLayout(out string error);
        Terrain terrain = info != null ? info.GetComponentInChildren<Terrain>() : null;
        if (info == null || layout == null || terrain == null)
        {
            EditorUtility.DisplayDialog("캠페인 맵", info == null ? "FloodedCampaign이 있는 L4D 씬을 연 뒤 다시 실행해 주세요." : error ?? "지형이 없습니다.", "확인");
            return;
        }

        Log.Clear();
        Warnings.Clear();
        bool success = false;
        try
        {
            Progress("철교 양 끝 지형 고치기", 0.1f);
            byte[] bytes = File.ReadAllBytes(HeightsPath);
            const int x0 = 868;
            const int z0 = 552;
            const int width = 44;
            const int depth = 108;
            float[,] heights = new float[depth, width];
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = ((z0 + z) * HeightRes + x0 + x) * 2;
                    heights[z, x] = (bytes[i] | (bytes[i + 1] << 8)) / 65535f;
                }
            }

            terrain.terrainData.SetHeights(x0, z0, heights);
            EditorUtility.SetDirty(terrain.terrainData);
            AssetDatabase.SaveAssets();
            Log.Add("✔ 철교 남쪽(z 565~579)과 북쪽(z 632~646) 끝을 다리 상판 높이(15 m)까지 메워 틈을 없앴습니다.");
            Progress("NavMesh 굽는 중", 0.3f);
            Physics.SyncTransforms();
            BakeNavMesh(info.gameObject, layout);
            Progress("검사", 0.9f);
            Verify(layout, info);
            EditorSceneManager.MarkSceneDirty(info.gameObject.scene);
            EditorSceneManager.SaveScene(info.gameObject.scene);
            success = true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Warnings.Add("❌ 오류: " + exception.Message);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        WriteReport(success);
    }

    [MenuItem(Menu + "캠페인 맵 검사 (경로 길이·막힘·누수)", priority = 2)]
    public static void VerifyOnly()
    {
        CityMapInfo info = FindCampaign();
        Layout layout = LoadLayout(out string error);
        if (info == null || layout == null)
        {
            EditorUtility.DisplayDialog("캠페인 맵", info == null ? "FloodedCampaign이 있는 L4D 씬을 연 뒤 다시 실행해 주세요." : error, "확인");
            return;
        }

        Log.Clear();
        Warnings.Clear();
        Verify(layout, info);
        WriteReport(true);
    }

    [MenuItem(Menu + "캠페인 맵 오클루전 컬링 굽기", priority = 3)]
    public static void BakeOcclusion()
    {
        if (FindCampaign() == null)
        {
            EditorUtility.DisplayDialog("캠페인 맵", "FloodedCampaign이 있는 L4D 씬을 연 뒤 다시 실행해 주세요.", "확인");
            return;
        }

        if (!EditorUtility.DisplayDialog("캠페인 맵", "오클루전 컬링을 굽습니다. 몇 분 걸릴 수 있습니다.\n(건물 뒤에 가려진 물체를 그리지 않아 프레임이 오릅니다)", "굽기", "취소"))
        {
            return;
        }

        try
        {
            Progress("오클루전 컬링 굽는 중", 0.5f);
            ComputeOcclusion();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        EditorUtility.DisplayDialog("캠페인 맵", "오클루전 컬링을 구웠습니다.", "확인");
    }

    // ======================================================================
    // 준비 / 예전 맵
    // ======================================================================

    private static Layout LoadLayout(out string error)
    {
        error = null;
        if (!File.Exists(LayoutPath) || !File.Exists(HeightsPath) || !File.Exists(SplatPath))
        {
            error = DataFolder + " 의 설계 파일(CampaignLayout.json, CampaignHeights.bytes, CampaignSplat.bytes)을 찾지 못했습니다.";
            return null;
        }

        try
        {
            Layout layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
            if (layout == null || layout.route == null || layout.route.Length < 6 || layout.placements == null || layout.walls == null)
            {
                error = LayoutPath + " 내용이 비어 있습니다.";
                return null;
            }

            return layout;
        }
        catch (Exception exception)
        {
            error = LayoutPath + " 를 읽지 못했습니다: " + exception.Message;
            return null;
        }
    }

    private static CityMapInfo FindCampaign()
    {
        return Object.FindObjectsByType<CityMapInfo>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(info => info.name == RootName);
    }

    private static void BackupScene(Scene game)
    {
        EnsureFolder(BackupFolder);
        if (File.Exists(BackupPath) && game.GetRootGameObjects().Any(go => go.name == RootName))
        {
            // 다시 만들 때는 캠페인 맵 이전의 백업을 그대로 둔다.
            Log.Add("· 백업 유지: " + BackupPath + " (캠페인 맵을 만들기 전 씬)");
            return;
        }

        if (File.Exists(BackupPath))
        {
            AssetDatabase.DeleteAsset(BackupPath);
        }

        if (!AssetDatabase.CopyAsset(game.path, BackupPath))
        {
            throw new IOException("씬 백업에 실패했습니다: " + BackupPath);
        }

        Log.Add("✔ 지금 씬 백업 1개: " + BackupPath);
    }

    private static bool IsSystem(Component component)
    {
        return component is PlayerMove || component is GameManager || component is Camera || component is Canvas
               || component is ZombieSpawner || component is AIDirector || component is CursorManager
               || component is UnityEngine.EventSystems.EventSystem || component is Volume;
    }

    private static void RemoveOldMap(Scene game)
    {
        List<string> removed = new List<string>();
        string[] mapNames = { RootName, "FloodedMap", "CityMap", "Map_Old", "_PreservedFromOldMap" };
        foreach (GameObject go in game.GetRootGameObjects())
        {
            bool isMap = go.GetComponent<CityMapInfo>() != null || mapNames.Contains(go.name);
            if (!isMap)
            {
                if (go.GetComponentsInChildren<Component>(true).Any(c => c != null && IsSystem(c)))
                {
                    continue; // 게임 시스템 (Player, 카메라, UI, GameManager, AIDirector, 스포너, 볼륨 등)
                }

                if (go.GetComponentInChildren<Zombie>(true) != null)
                {
                    continue; // 씬에 직접 놓인 좀비는 지우지 않고 나중에 새 맵의 길 위로 옮긴다.
                }
            }

            // 맵 묶음 안에 게임 시스템이 섞여 있으면 먼저 꺼내 둔다.
            foreach (Transform system in SystemChildren(go.transform))
            {
                system.SetParent(null, true);
                Warnings.Add("⚠ 예전 맵 안에 있던 게임 오브젝트 '" + system.name + "'를 씬 루트로 옮겨 보존했습니다.");
            }

            removed.Add(go.name);
            Object.DestroyImmediate(go);
        }

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
        if (Lightmapping.lightingDataAsset != null)
        {
            Lightmapping.lightingDataAsset = null;
        }

        Log.Add(removed.Count > 0 ? "✔ 씬의 예전 맵 지움: " + string.Join(", ", removed) : "· 씬에 지울 예전 맵이 없었습니다.");
    }

    private static List<Transform> SystemChildren(Transform root)
    {
        List<Transform> found = new List<Transform>();
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component.transform == root || !IsSystem(component)) continue;
            if (!found.Any(t => component.transform.IsChildOf(t)))
            {
                found.Add(component.transform);
            }
        }

        return found;
    }

    private static void DeleteOldAssets()
    {
        List<string> targets = OldAssets.Where(p => AssetDatabase.IsValidFolder(p) || File.Exists(p)).ToList();
        if (AssetDatabase.IsValidFolder(BackupFolder))
        {
            foreach (string guid in AssetDatabase.FindAssets(string.Empty, new[] { BackupFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path != BackupPath && !AssetDatabase.IsValidFolder(path) && !targets.Contains(path)) targets.Add(path);
            }
        }

        if (targets.Count == 0)
        {
            Log.Add("· 지울 예전 맵 파일이 없었습니다.");
            return;
        }

        List<string> failed = new List<string>();
        AssetDatabase.DeleteAssets(targets.ToArray(), failed);
        AssetDatabase.Refresh();
        Log.Add("✔ 예전 맵 파일 " + (targets.Count - failed.Count) + "개(폴더 포함) 삭제: " + string.Join(", ", targets.Except(failed).Select(Path.GetFileName)));
        Log.Add("  · 남긴 것: 백업 씬 1개(" + BackupPath + "), GameScene, Flooded Grounds 원본 에셋, L4DMap/Flooded/Shaders(머티리얼이 씀), L4DMap/Materials");
        if (failed.Count > 0)
        {
            Warnings.Add("⚠ 지우지 못한 파일: " + string.Join(", ", failed));
        }
    }

    // ======================================================================
    // 건물: PreAsembeld 씬의 조립 건물을 메시 1개 + LOD + 콜라이더 1개로 합친다.
    // ======================================================================

    private static void BuildTemplates(Layout layout)
    {
        HashSet<string> names = new HashSet<string>(layout.placements.Where(p => p.k == "tpl").Select(p => p.r));
        Scene source = EditorSceneManager.OpenScene(PreassembledScene, OpenSceneMode.Additive);
        int parts = 0;
        int drawCalls = 0;
        int fallbacks = 0;
        try
        {
            Dictionary<string, GameObject> roots = new Dictionary<string, GameObject>();
            foreach (GameObject go in source.GetRootGameObjects())
            {
                if (!roots.ContainsKey(go.name)) roots[go.name] = go;
            }

            int index = 0;
            foreach (string name in names)
            {
                Progress("건물 합치기: " + name, 0.06f + 0.22f * index++ / Mathf.Max(1, names.Count));
                if (!roots.TryGetValue(name, out GameObject group))
                {
                    Warnings.Add("⚠ 조립 건물 '" + name + "'을(를) " + PreassembledScene + " 에서 찾지 못했습니다.");
                    continue;
                }

                GameObject template;
                try
                {
                    template = CombineBuilding(group, name, out int partCount, out int calls);
                    parts += partCount;
                    drawCalls += calls;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[캠페인 맵] 건물 합치기 실패, 원본 조각 복사로 대신합니다: " + name + " - " + exception.Message);
                    template = CopyBuilding(group, name);
                    fallbacks++;
                }

                string path = BuildingFolder + "/" + name + ".prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, path);
                Object.DestroyImmediate(template);
                Templates[name] = prefab;
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(source, true);
        }

        AssetDatabase.SaveAssets();
        Log.Add("✔ 조립 건물 " + Templates.Count + "종: 조각 " + parts + "개 → 머티리얼별 메시 " + drawCalls + "개 (LODGroup + 합친 콜라이더) → " + BuildingFolder);
        if (fallbacks > 0)
        {
            Warnings.Add("⚠ 건물 " + fallbacks + "종은 메시를 읽지 못해 원본 조각을 그대로 복사했습니다. (보이는 것과 길은 같고, 그리기 횟수만 많음)");
        }
    }

    private static GameObject CombineBuilding(GameObject group, string name, out int partCount, out int drawCalls)
    {
        Transform root = group.transform;
        Matrix4x4 toLocal = root.worldToLocalMatrix;
        List<Part> list = new List<Part>();
        foreach (MeshRenderer renderer in group.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (!renderer.enabled) continue;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            int lodIndex = -1;
            int lodCount = 1;
            LODGroup lodGroup = renderer.GetComponentInParent<LODGroup>();
            if (lodGroup != null && lodGroup.transform.IsChildOf(root))
            {
                LOD[] lods = lodGroup.GetLODs();
                for (int i = 0; i < lods.Length && lodIndex < 0; i++)
                {
                    if (lods[i].renderers != null && lods[i].renderers.Contains(renderer)) lodIndex = i;
                }

                if (lodIndex < 0) continue; // LODGroup 아래인데 어느 단계에도 없으면 원래도 안 그려진다.
                lodCount = lods.Length;
            }

            list.Add(new Part { Renderer = renderer, Mesh = filter.sharedMesh, LodIndex = lodIndex, LodCount = lodCount });
        }

        int levels = Mathf.Clamp(list.Count == 0 ? 1 : list.Max(p => p.LodIndex >= 0 ? p.LodCount : 1), 1, 3);
        GameObject template = new GameObject(name);
        try
        {
            return FillTemplate(template, list, levels, group, toLocal, name, out drawCalls, out partCount);
        }
        catch
        {
            Object.DestroyImmediate(template);
            throw;
        }
    }

    private static GameObject FillTemplate(GameObject template, List<Part> list, int levels, GameObject group, Matrix4x4 toLocal, string name, out int drawCalls, out int partCount)
    {
        List<Mesh> meshes = new List<Mesh>();
        List<LOD> lodList = new List<LOD>();
        float[] transitions = levels == 1 ? new[] { 0.01f } : levels == 2 ? new[] { 0.12f, 0.01f } : new[] { 0.3f, 0.1f, 0.01f };
        drawCalls = 0;
        for (int level = 0; level < levels; level++)
        {
            MeshBuffer buffer = new MeshBuffer();
            foreach (Part part in list)
            {
                bool include = part.LodIndex < 0 || part.LodIndex == Mathf.Min(level, part.LodCount - 1);
                if (include) Append(buffer, part.Mesh, part.Renderer.sharedMaterials, toLocal * part.Renderer.transform.localToWorldMatrix);
            }

            if (buffer.Vertices.Count == 0) continue;
            Mesh mesh = ToMesh(buffer, name + "_LOD" + level);
            meshes.Add(mesh);
            GameObject child = new GameObject("LOD" + level);
            child.transform.SetParent(template.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = buffer.Materials.ToArray();
            renderer.shadowCastingMode = level == levels - 1 && levels > 1 ? ShadowCastingMode.Off : ShadowCastingMode.On;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            lodList.Add(new LOD(transitions[Mathf.Min(lodList.Count, transitions.Length - 1)], new Renderer[] { renderer }));
            drawCalls += buffer.Materials.Count;
        }

        if (lodList.Count > 0)
        {
            // 단계 수가 줄었으면 마지막 단계의 전환 값을 가장 작게 맞춘다.
            LOD[] lods = lodList.ToArray();
            lods[lods.Length - 1].screenRelativeTransitionHeight = 0.01f;
            LODGroup lodGroup = template.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.None;
            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
        }

        // 콜라이더: 상자 + 메시 콜라이더를 메시 하나로 합친다. (LOD마다 같은 콜라이더가 있으면 한 번만)
        Mesh collision = CombineColliders(group, toLocal, name + "_Collider");
        if (collision != null)
        {
            meshes.Add(collision);
            GameObject child = new GameObject("Collider");
            child.transform.SetParent(template.transform, false);
            MeshCollider collider = child.AddComponent<MeshCollider>();
            collider.sharedMesh = collision;
        }

        SaveMeshes(meshes, MeshFolder + "/" + name + "_Meshes.asset");
        SetStatic(template, true);
        partCount = list.Count;
        return template;
    }

    private static void Append(MeshBuffer buffer, Mesh mesh, Material[] materials, Matrix4x4 matrix)
    {
        Vector3[] vertices = mesh.vertices;
        if (vertices == null || vertices.Length == 0) return;
        Vector3[] normals = mesh.normals;
        Vector4[] tangents = mesh.tangents;
        Vector2[] uv0 = mesh.uv;
        Vector2[] uv1 = mesh.uv2;
        Color[] colors = mesh.colors;
        bool mirrored = matrix.determinant < 0f;
        Matrix4x4 normalMatrix = matrix.inverse.transpose;
        int baseVertex = -1;
        int subMeshes = Mathf.Min(mesh.subMeshCount, materials.Length);
        for (int s = 0; s < subMeshes; s++)
        {
            Material material = materials[s];
            if (material == null || mesh.GetTopology(s) != MeshTopology.Triangles) continue;
            int[] triangles = mesh.GetTriangles(s);
            if (triangles.Length == 0) continue;
            if (baseVertex < 0)
            {
                baseVertex = buffer.Vertices.Count;
                if (colors != null && colors.Length == vertices.Length && !buffer.HasColors)
                {
                    buffer.HasColors = true;
                    while (buffer.Colors.Count < buffer.Vertices.Count) buffer.Colors.Add(Color.white);
                }

                if (uv1 != null && uv1.Length == vertices.Length && !buffer.HasUv1)
                {
                    buffer.HasUv1 = true;
                    while (buffer.Uv1.Count < buffer.Vertices.Count) buffer.Uv1.Add(Vector2.zero);
                }

                for (int v = 0; v < vertices.Length; v++)
                {
                    buffer.Vertices.Add(matrix.MultiplyPoint3x4(vertices[v]));
                    Vector3 n = normals != null && normals.Length == vertices.Length ? normalMatrix.MultiplyVector(normals[v]).normalized : Vector3.up;
                    buffer.Normals.Add(n);
                    if (tangents != null && tangents.Length == vertices.Length)
                    {
                        Vector3 t = matrix.MultiplyVector(tangents[v]).normalized;
                        buffer.Tangents.Add(new Vector4(t.x, t.y, t.z, tangents[v].w * (mirrored ? -1f : 1f)));
                    }
                    else
                    {
                        buffer.Tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                    }

                    buffer.Uv0.Add(uv0 != null && uv0.Length == vertices.Length ? uv0[v] : Vector2.zero);
                    if (buffer.HasUv1) buffer.Uv1.Add(uv1 != null && uv1.Length == vertices.Length ? uv1[v] : Vector2.zero);
                    if (buffer.HasColors) buffer.Colors.Add(colors != null && colors.Length == vertices.Length ? colors[v] : Color.white);
                }
            }

            int slot = buffer.Materials.IndexOf(material);
            if (slot < 0)
            {
                slot = buffer.Materials.Count;
                buffer.Materials.Add(material);
                buffer.Indices.Add(new List<int>());
            }

            List<int> indices = buffer.Indices[slot];
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                // 거울 배치(음수 스케일)된 조각은 삼각형 감기 방향을 뒤집어야 앞면이 보인다.
                indices.Add(baseVertex + triangles[i]);
                indices.Add(baseVertex + triangles[mirrored ? i + 2 : i + 1]);
                indices.Add(baseVertex + triangles[mirrored ? i + 1 : i + 2]);
            }
        }
    }

    private static Mesh ToMesh(MeshBuffer buffer, string name)
    {
        Mesh mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(buffer.Vertices);
        mesh.SetNormals(buffer.Normals);
        mesh.SetTangents(buffer.Tangents);
        mesh.SetUVs(0, buffer.Uv0);
        if (buffer.HasUv1) mesh.SetUVs(1, buffer.Uv1);
        if (buffer.HasColors) mesh.SetColors(buffer.Colors);
        mesh.subMeshCount = buffer.Indices.Count;
        for (int i = 0; i < buffer.Indices.Count; i++) mesh.SetTriangles(buffer.Indices[i], i, false);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CombineColliders(GameObject group, Matrix4x4 toLocal, string name)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();
        HashSet<string> seen = new HashSet<string>();
        foreach (Collider collider in group.GetComponentsInChildren<Collider>(false))
        {
            if (!collider.enabled || collider.isTrigger) continue;
            Matrix4x4 matrix = toLocal * collider.transform.localToWorldMatrix;
            if (collider is MeshCollider meshCollider)
            {
                Mesh mesh = meshCollider.sharedMesh;
                if (mesh == null) continue;
                string key = mesh.GetInstanceID() + "|" + Key(matrix);
                if (!seen.Add(key)) continue;
                Vector3[] source = mesh.vertices;
                int start = vertices.Count;
                bool flip = matrix.determinant < 0f;
                foreach (Vector3 v in source) vertices.Add(matrix.MultiplyPoint3x4(v));
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                    int[] triangles = mesh.GetTriangles(s);
                    for (int t = 0; t + 2 < triangles.Length; t += 3)
                    {
                        // 거울 배치된 조각은 감기 방향을 뒤집어야 면이 바깥을 본다. (뒷면은 레이캐스트에 안 맞음)
                        indices.Add(start + triangles[t]);
                        indices.Add(start + triangles[flip ? t + 2 : t + 1]);
                        indices.Add(start + triangles[flip ? t + 1 : t + 2]);
                    }
                }

                continue;
            }

            Vector3 center;
            Vector3 size;
            if (collider is BoxCollider box)
            {
                center = box.center;
                size = box.size;
            }
            else if (collider is SphereCollider sphere)
            {
                center = sphere.center;
                size = Vector3.one * sphere.radius * 2f;
            }
            else if (collider is CapsuleCollider capsule)
            {
                center = capsule.center;
                size = new Vector3(capsule.radius * 2f, capsule.radius * 2f, capsule.radius * 2f);
                size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2f);
            }
            else
            {
                continue;
            }

            string boxKey = "box|" + Key(matrix) + center + size;
            if (!seen.Add(boxKey)) continue;
            AddBox(vertices, indices, matrix, center, size);
        }

        if (indices.Count == 0) return null;
        Mesh result = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        result.SetVertices(vertices);
        result.SetTriangles(indices, 0);
        result.RecalculateBounds();
        result.RecalculateNormals();
        return result;
    }

    private static void AddBox(List<Vector3> vertices, List<int> indices, Matrix4x4 matrix, Vector3 center, Vector3 size)
    {
        int start = vertices.Count;
        Vector3 h = size * 0.5f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
            vertices.Add(matrix.MultiplyPoint3x4(center + corner));
        }

        int[] faces =
        {
            0, 2, 3, 0, 3, 1, // -z
            4, 5, 7, 4, 7, 6, // +z
            0, 4, 6, 0, 6, 2, // -x
            1, 3, 7, 1, 7, 5, // +x
            0, 1, 5, 0, 5, 4, // -y
            2, 6, 7, 2, 7, 3, // +y
        };
        bool flip = matrix.determinant < 0f;
        for (int i = 0; i < faces.Length; i += 3)
        {
            indices.Add(start + faces[i]);
            indices.Add(start + faces[flip ? i + 2 : i + 1]);
            indices.Add(start + faces[flip ? i + 1 : i + 2]);
        }
    }

    private static string Key(Matrix4x4 m)
    {
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < 16; i++) text.Append(Mathf.Round(m[i] * 1000f)).Append(',');
        return text.ToString();
    }

    // 합치기에 실패한 건물은 원본 조각을 그대로 복사한다. (프리팹 연결은 끊긴 일반 오브젝트)
    private static GameObject CopyBuilding(GameObject group, string name)
    {
        GameObject copy = Object.Instantiate(group);
        copy.name = name;
        SceneManager.MoveGameObjectToScene(copy, SceneManager.GetActiveScene());
        copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        copy.transform.localScale = Vector3.one;
        SetStatic(copy, true);
        return copy;
    }

    private static void SaveMeshes(List<Mesh> meshes, string path)
    {
        if (meshes.Count == 0) return;
        if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(meshes[0], path);
        for (int i = 1; i < meshes.Count; i++) AssetDatabase.AddObjectToAsset(meshes[i], path);
    }

    // ======================================================================
    // 지형 / 물 / 나무
    // ======================================================================

    private static void BuildTerrain(Layout layout, Transform root)
    {
        byte[] heightBytes = File.ReadAllBytes(HeightsPath);
        byte[] splatBytes = File.ReadAllBytes(SplatPath);
        if (heightBytes.Length != HeightRes * HeightRes * 2 || splatBytes.Length != HeightRes * HeightRes * 3)
        {
            throw new InvalidDataException("지형 데이터 크기가 맞지 않습니다. (" + heightBytes.Length + ", " + splatBytes.Length + ")");
        }

        TerrainData data = new TerrainData { name = "CampaignTerrain" };
        data.heightmapResolution = HeightRes;
        data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
        float[,] heights = new float[HeightRes, HeightRes];
        for (int z = 0; z < HeightRes; z++)
        {
            for (int x = 0; x < HeightRes; x++)
            {
                int i = (z * HeightRes + x) * 2;
                heights[z, x] = (heightBytes[i] | (heightBytes[i + 1] << 8)) / 65535f;
            }
        }

        data.SetHeights(0, 0, heights);

        List<TerrainLayer> layers = new List<TerrainLayer>();
        foreach (string path in TerrainLayerPaths)
        {
            TerrainLayer terrainLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (terrainLayer == null)
            {
                Warnings.Add("⚠ 지형 레이어를 찾지 못했습니다: " + path);
                terrainLayer = new TerrainLayer { tileSize = new Vector2(8f, 8f) };
            }

            layers.Add(terrainLayer);
        }

        data.alphamapResolution = AlphaRes;
        data.terrainLayers = layers.ToArray();
        data.baseMapResolution = 1024;
        float[,,] alpha = new float[AlphaRes, AlphaRes, 3];
        for (int z = 0; z < AlphaRes; z++)
        {
            for (int x = 0; x < AlphaRes; x++)
            {
                int i = (z * HeightRes + x) * 3;
                float a = splatBytes[i];
                float b = splatBytes[i + 1];
                float c = splatBytes[i + 2];
                float sum = Mathf.Max(1f, a + b + c);
                alpha[z, x, 0] = a / sum;
                alpha[z, x, 1] = b / sum;
                alpha[z, x, 2] = c / sum;
            }
        }

        data.SetAlphamaps(0, 0, alpha);

        // 나무: Tree Creator 나무를 URP Lit 나무 프리팹으로 바꿔 쓴다.
        List<TreePrototype> prototypes = new List<TreePrototype>();
        List<int> remap = new List<int>();
        foreach (string protoName in layout.treeProtos ?? new string[0])
        {
            GameObject tree = ConvertTree(protoName);
            remap.Add(tree != null ? prototypes.Count : -1);
            if (tree != null) prototypes.Add(new TreePrototype { prefab = tree, bendFactor = 0f });
        }

        data.treePrototypes = prototypes.ToArray();
        List<TreeInstance> trees = new List<TreeInstance>();
        Random random = new Random(21);
        for (int i = 0; layout.trees != null && i + 4 < layout.trees.Length; i += 5)
        {
            int proto = (int)layout.trees[i + 3];
            if (proto < 0 || proto >= remap.Count || remap[proto] < 0) continue;
            float scale = layout.trees[i + 4];
            byte tint = (byte)(215 + random.Next(40));
            trees.Add(new TreeInstance
            {
                position = new Vector3(layout.trees[i] / TerrainSize, layout.trees[i + 1] / TerrainHeight, layout.trees[i + 2] / TerrainSize),
                prototypeIndex = remap[proto],
                widthScale = scale,
                heightScale = scale * (0.9f + (float)random.NextDouble() * 0.2f),
                rotation = (float)(random.NextDouble() * Math.PI * 2.0),
                color = new Color32(tint, tint, tint, 255),
                lightmapColor = new Color32(255, 255, 255, 255),
            });
        }

        data.SetTreeInstances(trees.ToArray(), true);

        if (AssetDatabase.LoadMainAssetAtPath(TerrainDataPath) != null) AssetDatabase.DeleteAsset(TerrainDataPath);
        AssetDatabase.CreateAsset(data, TerrainDataPath);
        foreach (TerrainLayer terrainLayer in layers)
        {
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(terrainLayer))) AssetDatabase.AddObjectToAsset(terrainLayer, TerrainDataPath);
        }

        GameObject go = Terrain.CreateTerrainGameObject(data);
        go.name = "Terrain";
        go.transform.SetParent(root, false);
        go.transform.position = Vector3.zero;
        Terrain terrain = go.GetComponent<Terrain>();
        Material material = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
        Shader terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (material == null && terrainShader != null)
        {
            material = new Material(terrainShader) { name = "CampaignTerrain_URP" };
            AssetDatabase.CreateAsset(material, TerrainMaterialPath);
        }

        if (material != null) terrain.materialTemplate = material;
        terrain.heightmapPixelError = 4f;
        terrain.basemapDistance = 220f;
        terrain.drawInstanced = true;
        terrain.treeDistance = 240f;
        terrain.treeBillboardDistance = 2000f; // URP Lit 나무는 빌보드가 없으므로 끝까지 메시로 그린다. (나무 거리 240 m에서 끊김)
        terrain.treeCrossFadeLength = 0f;
        terrain.treeMaximumFullLODCount = 5000;
        terrain.detailObjectDistance = 0f;
        terrain.shadowCastingMode = ShadowCastingMode.On;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        Log.Add("✔ 지형: 1024 × 1024 m, 높이 64 m, 레이어 3종(이끼·진흙·아스팔트), 나무 " + trees.Count + "그루 (URP 나무 " + prototypes.Count + "종, 240 m까지)");
    }

    private static GameObject ConvertTree(string protoName)
    {
        string sourcePath = TreePrefabFolder + "/" + protoName + ".prefab";
        string targetPath = TreeFolder + "/" + protoName + "_URP.prefab";
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(sourcePath);
        Mesh sourceMesh = assets.OfType<Mesh>().OrderByDescending(m => m.vertexCount).FirstOrDefault();
        if (sourceMesh == null)
        {
            Warnings.Add("⚠ 나무 메시를 찾지 못했습니다: " + sourcePath);
            return null;
        }

        Material[] sourceMaterials = assets.OfType<Material>().ToArray();
        Material barkSource = sourceMaterials.FirstOrDefault(m => m.name.Contains("Bark")) ?? sourceMaterials.FirstOrDefault();
        Material leafSource = sourceMaterials.FirstOrDefault(m => m.name.Contains("Leaf")) ?? barkSource;
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Material bark = TreeMaterial(lit, barkSource, protoName + "_Bark", false);
        Material leaf = TreeMaterial(lit, leafSource, protoName + "_Leaves", true);

        Mesh mesh = Object.Instantiate(sourceMesh);
        mesh.name = protoName + "_Mesh";
        string meshPath = TreeFolder + "/" + protoName + "_Mesh.asset";
        if (AssetDatabase.LoadMainAssetAtPath(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);

        GameObject go = new GameObject(protoName + "_URP");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        // Tree Creator 메시는 0번 = 줄기(불투명), 1번 = 잎(알파 컷)
        renderer.sharedMaterials = mesh.subMeshCount > 1 ? new[] { bark, leaf } : new[] { bark };
        renderer.shadowCastingMode = ShadowCastingMode.On;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, targetPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static Material TreeMaterial(Shader lit, Material source, string name, bool leaves)
    {
        string path = TreeFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(lit) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = lit;
        Texture texture = source != null && source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
        if (texture != null)
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTexture("_MainTex", texture);
        }

        Color tint = leaves ? new Color(0.78f, 0.82f, 0.72f) : new Color(0.82f, 0.8f, 0.78f);
        material.SetColor("_BaseColor", tint);
        material.SetColor("_Color", tint);
        material.SetFloat("_Smoothness", leaves ? 0.12f : 0.05f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_AlphaClip", leaves ? 1f : 0f);
        material.SetFloat("_Cutoff", 0.35f);
        material.SetFloat("_Cull", leaves ? 0f : 2f);
        if (leaves)
        {
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            material.SetOverrideTag("RenderType", "TransparentCutout");
        }
        else
        {
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Geometry;
            material.SetOverrideTag("RenderType", "Opaque");
        }

        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    // 물: 지형이 수면보다 낮은 곳에만 16 m 칸을 깔고, 128 m 덩어리로 나눠 화면 밖 덩어리는 그리지 않게 한다.
    private static void BuildWater(Layout layout, Transform root)
    {
        Material water = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
        if (water == null)
        {
            Warnings.Add("⚠ 물 머티리얼(" + WaterMaterialPath + ")을 찾지 못했습니다.");
            return;
        }

        Terrain terrain = root.GetComponentInChildren<Terrain>();
        float level = layout.water > 0f ? layout.water : 10.8f;
        Transform parent = new GameObject("Water").transform;
        parent.SetParent(root, false);
        const float chunk = 128f;
        const float cell = 16f;
        int chunks = 0;
        List<Mesh> meshes = new List<Mesh>();
        for (float cz = 0f; cz < TerrainSize; cz += chunk)
        {
            for (float cx = 0f; cx < TerrainSize; cx += chunk)
            {
                List<Vector3> vertices = new List<Vector3>();
                List<int> indices = new List<int>();
                for (float z = cz; z < cz + chunk; z += cell)
                {
                    for (float x = cx; x < cx + chunk; x += cell)
                    {
                        float low = float.MaxValue;
                        for (int sz = 0; sz <= 4; sz++)
                        {
                            for (int sx = 0; sx <= 4; sx++)
                            {
                                low = Mathf.Min(low, terrain.SampleHeight(new Vector3(x + sx * cell / 4f, 0f, z + sz * cell / 4f)));
                            }
                        }

                        if (low > level + 0.05f) continue;
                        int start = vertices.Count;
                        vertices.Add(new Vector3(x, level, z));
                        vertices.Add(new Vector3(x, level, z + cell));
                        vertices.Add(new Vector3(x + cell, level, z + cell));
                        vertices.Add(new Vector3(x + cell, level, z));
                        indices.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                    }
                }

                if (indices.Count == 0) continue;
                Mesh mesh = new Mesh { name = "Water_" + (int)cx + "_" + (int)cz, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(indices, 0);
                mesh.SetNormals(vertices.Select(v => Vector3.up).ToList());
                mesh.SetUVs(0, vertices.Select(v => new Vector2(v.x / cell, v.z / cell)).ToList());
                mesh.RecalculateBounds();
                meshes.Add(mesh);
                GameObject go = new GameObject(mesh.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = water;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                chunks++;
            }
        }

        SaveMeshes(meshes, GeneratedFolder + "/Water_Meshes.asset");
        Log.Add("✔ 물: 수면 높이 " + level.ToString("0.0") + " m, 덩어리 " + chunks + "개 (지형이 물보다 낮은 곳만)");
    }

    // ======================================================================
    // 건물 / 소품
    // ======================================================================

    private static void PlaceObjects(Layout layout, Transform root)
    {
        Transform buildings = Child(root, "Buildings");
        Transform props = Child(root, "Props");
        Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
        int placedBuildings = 0;
        int placedProps = 0;
        int missing = 0;
        foreach (Placement placement in layout.placements)
        {
            if (placement.p == null || placement.p.Length < 3) continue;
            GameObject prefab;
            Transform parent;
            if (placement.k == "tpl")
            {
                Templates.TryGetValue(placement.r, out prefab);
                parent = buildings;
            }
            else
            {
                prefab = LoadPrefab(placement.r);
                string tag = string.IsNullOrEmpty(placement.tag) ? "misc" : placement.tag;
                if (!groups.TryGetValue(tag, out parent))
                {
                    parent = Child(props, tag);
                    groups[tag] = parent;
                }
            }

            if (prefab == null)
            {
                missing++;
                continue;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetPositionAndRotation(new Vector3(placement.p[0], placement.p[1], placement.p[2]), Quaternion.Euler(0f, placement.yaw, 0f));
            instance.transform.localScale = Vector3.one * (placement.s > 0f ? placement.s : 1f);
            FixLayers(instance);
            if (placement.k == "tpl")
            {
                if (!string.IsNullOrEmpty(placement.tag) && placement.tag != "street") instance.name = placement.r + " (" + placement.tag + ")";
                placedBuildings++;
            }
            else
            {
                // 흔들리는 보트(애니메이션)는 Static이면 안 움직인다.
                bool moving = placement.r.Contains("Bobbing") || instance.GetComponentInChildren<Animator>(true) != null || instance.GetComponentInChildren<Animation>(true) != null
                              || instance.GetComponentInChildren<Rigidbody>(true) != null;
                if (!moving) SetStatic(instance, placement.tag == "landmark" || placement.tag == "skyline" || placement.tag == "bridge");
                placedProps++;
            }
        }

        Log.Add("✔ 배치: 건물 " + placedBuildings + "채, 소품/담장/폐차/선착장 등 " + placedProps + "개");
        if (missing > 0) Warnings.Add("⚠ 프리팹을 찾지 못해 " + missing + "개를 놓지 못했습니다. (Flooded Grounds 에셋이 다 있는지 확인)");
    }

    private static GameObject LoadPrefab(string path)
    {
        if (PrefabCache.TryGetValue(path, out GameObject cached)) return cached;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) Warnings.Add("⚠ 프리팹 없음: " + path);
        PrefabCache[path] = prefab;
        return prefab;
    }

    // 에셋 오브젝트가 우연히 Player/Zombie 레이어를 쓰면 총알·NavMesh가 꼬이므로 Default로 돌린다.
    private static void FixLayers(GameObject go)
    {
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
        {
            string layer = LayerMask.LayerToName(t.gameObject.layer);
            if (layer == "Player" || layer == "Zombie") t.gameObject.layer = 0;
        }
    }

    private static void SetStatic(GameObject go, bool occluder)
    {
        StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
        if (occluder) flags |= StaticEditorFlags.OccluderStatic;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }
    }

    // ======================================================================
    // 조명 / 안개 (Flooded Grounds 데모의 흐린 늪 분위기 + 조금 더 어둡고 안개 짙게)
    // ======================================================================

    private static Light ApplyAtmosphere(Transform root)
    {
        GameObject sunObject = new GameObject("Sun");
        sunObject.transform.SetParent(root, false);
        sunObject.transform.rotation = new Quaternion(0.15957603f, 0.6595761f, -0.14622451f, 0.71980083f);
        Light sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.9647059f, 0.97964936f, 1f);
        sun.intensity = 0.85f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        sun.lightmapBakeType = LightmapBakeType.Realtime;
        RenderSettings.sun = sun;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Color.Lerp(new Color(0.49181086f, 0.5666567f, 0.6117647f), new Color(0.36f, 0.39f, 0.4f), 0.35f);
        RenderSettings.fogStartDistance = 18f;
        RenderSettings.fogEndDistance = 230f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.368254f, 0.38306883f, 0.40000004f) * 1.35f;
        RenderSettings.ambientEquatorColor = new Color(0.23579474f, 0.26282853f, 0.28235295f) * 1.35f;
        RenderSettings.ambientGroundColor = new Color(0.09411766f, 0.08297215f, 0.068111464f) * 1.35f;
        RenderSettings.ambientIntensity = 1f;
        Material sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        if (sky != null) RenderSettings.skybox = sky;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 0.6f;
        DynamicGI.UpdateEnvironment();
        Log.Add("✔ 조명: Flooded Grounds 해 방향 + 흐린 하늘, 안개 18~230 m, 3색 환경광");
        return sun;
    }

    // ======================================================================
    // 게임플레이: 경로 / 스폰 / 안전가옥 / 피날레 / 탈출 / 경계 / 길 안내
    // ======================================================================

    private static CityMapInfo BuildGameplay(Layout layout, Transform root)
    {
        Random random = new Random(20261102);
        Transform route = Child(root, "Route");
        Transform spawnRoot = Child(root, "SpawnPoints");
        Transform boundary = Child(root, "Boundary");
        Transform guides = Child(root, "Guides");
        Transform lights = Child(root, "Lights");
        Transform hazards = Child(root, "Hazards");
        Material wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/L4DMap/Materials/Wood.mat");
        Material military = AssetDatabase.LoadAssetAtPath<Material>("Assets/L4DMap/Materials/Military.mat");
        Material lampRed = AssetDatabase.LoadAssetAtPath<Material>("Assets/L4DMap/Materials/LampRed.mat");

        List<Transform> routePoints = new List<Transform>();
        for (int i = 0; i + 2 < layout.route.Length; i += 3)
        {
            routePoints.Add(Point(route, "Route_" + (i / 3).ToString("000"), V(layout.route, i)));
        }

        List<Transform> spawns = new List<Transform>();
        for (int i = 0; layout.spawns != null && i + 2 < layout.spawns.Length; i += 3)
        {
            spawns.Add(Point(spawnRoot, "SpawnPoint_" + (i / 3).ToString("000"), V(layout.spawns, i)));
        }

        // 출발 안전가옥 (늪지 끝 오두막)
        Transform start = Point(root, "StartPoint", V(layout.start, 0));
        start.rotation = Quaternion.Euler(0f, layout.start[3], 0f);
        BoxCollider startArea = Area(root, "StartSafeRoom", layout.startArea);
        Vector3 forward = start.forward;
        Vector3 right = start.right;
        Point(startArea.transform, "AmmoCrate", V(layout.start, 0) - forward * 1.6f + right * 1.1f);
        Point(startArea.transform, "MedKitBox", V(layout.start, 0) - forward * 1.6f - right * 1.1f);

        // 중간 안전가옥 (강변 오두막): 안에서는 좀비가 새로 나오지 않는다.
        BoxCollider midRoom = Area(root, "MidSafeRoom", layout.midRoom);
        Point(midRoom.transform, "MidAmmoCrate", V(layout.midCrates, 0));
        Point(midRoom.transform, "MidMedKitBox", V(layout.midCrates, 3));

        // 피날레 (저택 뒤 정원) + 무전기
        BoxCollider finale = Area(root, "FinaleGarden", layout.finale);
        Transform radioRoot = Child(root, "RadioStation");
        Vector3 radioPosition = GroundFromAbove(V(layout.radio, 0), 6f);
        Box("RadioTable", radioRoot, radioPosition + new Vector3(0f, 0.45f, 0f), new Vector3(1.4f, 0.9f, 0.8f), wood, true);
        Box("Radio", radioRoot, radioPosition + new Vector3(0f, 1.05f, 0f), new Vector3(0.6f, 0.3f, 0.35f), military, false);

        // 탈출: 선착장 끝 (구조 보트는 배치 데이터에 들어 있다)
        Transform extraction = Point(root, "ExtractionPoint", V(layout.extraction, 0));

        // 보이지 않는 벽 (길 경계선 위, 바깥쪽으로 0.25 m 밀어 길을 좁히지 않는다)
        int wallCount = 0;
        for (int i = 0; i + 7 < layout.walls.Length; i += 8)
        {
            Vector3 a = new Vector3(layout.walls[i], 0f, layout.walls[i + 1]);
            Vector3 b = new Vector3(layout.walls[i + 2], 0f, layout.walls[i + 3]);
            float y0 = layout.walls[i + 4];
            float y1 = layout.walls[i + 5];
            Vector3 normal = new Vector3(layout.walls[i + 6], 0f, layout.walls[i + 7]);
            Vector3 direction = b - a;
            float length = direction.magnitude;
            if (length < 0.01f) continue;
            GameObject wall = new GameObject("Wall_" + (i / 8).ToString("0000"));
            wall.layer = BoundaryLayer;
            wall.transform.SetParent(boundary, false);
            wall.transform.position = (a + b) * 0.5f + normal * 0.25f + Vector3.up * ((y0 + y1) * 0.5f);
            wall.transform.rotation = Quaternion.LookRotation(direction / length, Vector3.up);
            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.5f, Mathf.Max(2f, y1 - y0), length + 0.5f);
            GameObjectUtility.SetStaticEditorFlags(wall, 0);
            wallCount++;
        }

        // 길 안내: 꺾이는 곳마다 붉은 조명탄 (L4D처럼 불빛을 따라가면 된다)
        int flares = 0;
        for (int i = 0; layout.flares != null && i + 2 < layout.flares.Length; i += 3)
        {
            Vector3 p = Ground(V(layout.flares, i));
            Transform flare = Box("Flare_" + (i / 3).ToString("00"), guides, p + new Vector3(0f, 0.08f, 0f), new Vector3(0.12f, 0.12f, 0.35f), lampRed, false).transform;
            flare.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            Light light = AddLight(flare, "FlareLight", p + Vector3.up * 0.6f, new Color(1f, 0.25f, 0.12f), 2.4f, 10f);
            AddFlicker(light, MapFlickerLight.FlickerMode.Fire, 1.3f);
            flares++;
        }

        // 안전가옥·저택·온실 실내등, 드럼통 불, 무전기 신호등, 선착장 등
        int lightCount = 0;
        for (int i = 0; layout.lights != null && i + 8 < layout.lights.Length; i += 9)
        {
            Light light = AddLight(lights, "Light_" + (i / 9).ToString("00"), V(layout.lights, i),
                new Color(layout.lights[i + 3], layout.lights[i + 4], layout.lights[i + 5]), layout.lights[i + 6], layout.lights[i + 7]);
            int mode = Mathf.RoundToInt(layout.lights[i + 8]);
            if (mode == 1) AddFlicker(light, MapFlickerLight.FlickerMode.Fire, 1f);
            if (mode == 2) AddFlicker(light, MapFlickerLight.FlickerMode.Siren, 1f);
            lightCount++;
        }

        // 폭발 드럼통 (게임 프리팹)
        int drums = 0;
        GameObject drumPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DrumPrefabPath);
        if (drumPrefab != null)
        {
            float bottom = BottomOffset(drumPrefab);
            for (int i = 0; layout.drums != null && i + 2 < layout.drums.Length; i += 3)
            {
                Vector3 p = Ground(V(layout.drums, i));
                GameObject drum = (GameObject)PrefabUtility.InstantiatePrefab(drumPrefab, hazards);
                drum.transform.SetPositionAndRotation(new Vector3(p.x, p.y - bottom + 0.01f, p.z), Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
                drums++;
            }
        }
        else
        {
            Warnings.Add("⚠ 드럼통 프리팹(" + DrumPrefabPath + ")을 찾지 못했습니다.");
        }

        CityMapInfo info = root.gameObject.AddComponent<CityMapInfo>();
        SerializedObject so = new SerializedObject(info);
        so.FindProperty("_startPoint").objectReferenceValue = start;
        so.FindProperty("_startSafeRoomArea").objectReferenceValue = startArea;
        SetArray(so.FindProperty("_routePoints"), routePoints);
        SetArray(so.FindProperty("_spawnPoints"), spawns);
        SetArray(so.FindProperty("_midSafeRooms"), new List<BoxCollider> { midRoom });
        so.FindProperty("_finaleArea").objectReferenceValue = finale;
        so.FindProperty("_extractionPoint").objectReferenceValue = extraction;
        SetBool(so, "_bakedNavigation", true);
        SetBool(so, "_keepSceneAtmosphere", true);
        SetString(so, "_textStart", "붉은 조명탄을 따라 늪지 둑길을 지나 마을로 가라");
        SetString(so, "_textAfterMidRoom", "읍내 교회 광장과 저택을 지나 정원의 무전기로 가라");
        SetString(so, "_textRadioFar", "저택 뒤 정원 가운데의 무전기로 가라");
        SetString(so, "_textHoldOut", "구조 보트가 올 때까지 버텨라!");
        SetString(so, "_textTimer", "보트 도착까지 {0}초");
        SetString(so, "_textEscape", "보트가 왔다! 선착장 끝으로 달려라!");
        SetString(so, "_textArrive", "구조 보트 도착!");
        so.ApplyModifiedPropertiesWithoutUndo();

        Log.Add("✔ 진행 경로 지점 " + routePoints.Count + "개 (설계 길이 " + layout.routeLength.ToString("0") + " m), 좀비 스폰 포인트 " + spawns.Count + "개");
        Log.Add("✔ 출발 오두막(안전가옥) → 중간 안전가옥(강변 오두막) → 피날레 정원(무전기) → 선착장 끝 보트(탈출)");
        Log.Add("✔ 경계: 보이지 않는 벽 " + wallCount + "개 (Ignore Raycast 레이어 - 총알은 통과, 이동만 막음)");
        Log.Add("✔ 길 안내 조명탄 " + flares + "개, 조명 " + lightCount + "개, 폭발 드럼통 " + drums + "개");
        return info;
    }

    private static float BottomOffset(GameObject prefab)
    {
        if (BottomCache.TryGetValue(prefab, out float cached)) return cached;
        GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        float bottom = 0f;
        Renderer[] renderers = temp.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (renderers.Length > 0) bottom = renderers.Min(r => r.bounds.min.y);
        Object.DestroyImmediate(temp);
        BottomCache[prefab] = bottom;
        return bottom;
    }

    // 주어진 높이 근처에서 아래로 쏴 바닥을 찾는다. (경계벽/트리거 무시)
    private static Vector3 Ground(Vector3 p)
    {
        return GroundFromAbove(p, 2f);
    }

    private static Vector3 GroundFromAbove(Vector3 p, float above)
    {
        Vector3 origin = new Vector3(p.x, p.y + above, p.z);
        int mask = ~(1 << BoundaryLayer);
        foreach (string layer in new[] { "Player", "Zombie" })
        {
            int index = LayerMask.NameToLayer(layer);
            if (index >= 0) mask &= ~(1 << index);
        }

        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, above + 4f, mask, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return p;
        float best = hits.OrderBy(h => h.distance).First().point.y;
        return new Vector3(p.x, best, p.z);
    }

    // ======================================================================
    // Player / 시스템 연결
    // ======================================================================

    private static void MovePlayer(Layout layout)
    {
        PlayerMove player = Object.FindAnyObjectByType<PlayerMove>();
        if (player == null)
        {
            Warnings.Add("⚠ Player(PlayerMove)를 찾지 못했습니다.");
            return;
        }

        Vector3 start = Ground(V(layout.start, 0));
        CharacterController controller = player.GetComponent<CharacterController>();
        float lift = controller != null ? controller.height * 0.5f - controller.center.y + controller.skinWidth + 0.05f : 1f;
        player.transform.position = start + Vector3.up * lift;
        player.transform.rotation = Quaternion.Euler(0f, layout.start[3], 0f);
        EditorUtility.SetDirty(player.transform);
        Log.Add("✔ Player를 출발 오두막 안(" + start.ToString("0") + ")으로 옮겼습니다. 동료 AI는 시작할 때 Player 옆에 생깁니다.");
    }

    private static void AssignSystems(CityMapInfo info)
    {
        ZombieSpawner spawner = Object.FindAnyObjectByType<ZombieSpawner>();
        if (spawner != null)
        {
            SerializedObject so = new SerializedObject(spawner);
            SetArray(so.FindProperty("_spawnPoints"), info.SpawnPoints.ToList());
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        AIDirector director = Object.FindAnyObjectByType<AIDirector>();
        if (director != null)
        {
            SerializedObject so = new SerializedObject(director);
            SerializedProperty map = so.FindProperty("_map");
            if (map != null)
            {
                map.objectReferenceValue = info;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // 씬에 직접 놓여 있던 좀비는 새 맵의 길 위(출발 후 약 120 m)로 옮긴다.
        int moved = 0;
        Transform[] route = info.RoutePoints;
        foreach (GameObject go in info.gameObject.scene.GetRootGameObjects())
        {
            if (go.GetComponent<Zombie>() == null || route.Length < 14) continue;
            go.transform.position = route[12 + moved % 2].position + Vector3.up * 0.1f;
            if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            moved++;
        }

        // 카메라: 안개 끝 너머는 그리지 않는다. (먼 거리 320 m)
        int cameras = 0;
        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera.orthographic || camera.targetTexture != null) continue;
            camera.farClipPlane = FarClip;
            camera.useOcclusionCulling = true;
            EditorUtility.SetDirty(camera);
            cameras++;
        }

        Log.Add("✔ ZombieSpawner·AIDirector를 새 맵에 연결, 카메라 " + cameras + "개 먼 거리 " + FarClip.ToString("0") + " m" + (moved > 0 ? ", 씬에 있던 좀비 " + moved + "마리를 길 위로 옮김" : string.Empty));
    }

    // ======================================================================
    // NavMesh
    // ======================================================================

    private static void BakeNavMesh(GameObject root, Layout layout)
    {
        foreach (NavMeshSurface other in Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (other.gameObject != root && other.gameObject.scene == root.scene) other.enabled = false;
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
        surface.voxelSize = Mathf.Clamp(settings.agentRadius / 3f, 0.09f, 0.17f);
        surface.overrideTileSize = true;
        surface.tileSize = 256;

        NavMesh.RemoveAllNavMeshData();
        Physics.SyncTransforms();
        Stopwatch watch = Stopwatch.StartNew();
        surface.BuildNavMesh();
        NavMeshData data = surface.navMeshData;
        if (data == null) throw new InvalidOperationException("NavMesh 굽기에 실패했습니다.");

        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAssetPath) != null && AssetDatabase.GetAssetPath(data) != NavMeshAssetPath)
        {
            AssetDatabase.DeleteAsset(NavMeshAssetPath);
        }

        if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(data))) AssetDatabase.CreateAsset(data, NavMeshAssetPath);
        surface.navMeshData = data;
        EditorUtility.SetDirty(surface);
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        Log.Add("✔ NavMesh 굽기: 범위 " + bounds.size.ToString("0") + ", 복셀 " + surface.voxelSize.ToString("0.###") + " m, 삼각형 "
                + (triangulation.indices != null ? triangulation.indices.Length / 3 : 0) + "개, " + watch.Elapsed.TotalSeconds.ToString("0") + "초 → " + NavMeshAssetPath);
    }

    private static Bounds LayoutBounds(Layout layout)
    {
        Bounds bounds = new Bounds(V(layout.start, 0), Vector3.one);
        for (int i = 0; i + 7 < layout.walls.Length; i += 8)
        {
            bounds.Encapsulate(new Vector3(layout.walls[i], layout.walls[i + 4], layout.walls[i + 1]));
            bounds.Encapsulate(new Vector3(layout.walls[i + 2], layout.walls[i + 5], layout.walls[i + 3]));
        }

        bounds.Encapsulate(V(layout.extraction, 0));
        bounds.Expand(new Vector3(8f, 4f, 8f));
        return bounds;
    }

    // ======================================================================
    // 검사
    // ======================================================================

    private static void Verify(Layout layout, CityMapInfo info)
    {
        NavMeshPath path = new NavMeshPath();
        Transform[] route = info.RoutePoints;

        // 1. 진행 경로를 따라 끝까지 걸어갈 수 있는가
        float routeLength = 0f;
        List<int> broken = new List<int>();
        for (int i = 0; i < route.Length - 1; i++)
        {
            if (PathLength(route[i].position, route[i + 1].position, path, out float length)) routeLength += length;
            else broken.Add(i);
        }

        // 2. 출발 → 무전기 → 탈출 최단 거리 (지름길이 있으면 짧게 나온다)
        Transform radio = info.transform.Find("RadioStation/Radio");
        Vector3 startPosition = info.StartPoint.position;
        Vector3 radioPosition = radio != null ? radio.position : route[route.Length - 1].position;
        bool toRadio = PathLength(startPosition, radioPosition, path, out float shortestToRadio);
        bool toBoat = PathLength(radioPosition, info.ExtractionPoint.position, path, out float radioToBoat);
        bool toMid = info.MidSafeRooms != null && info.MidSafeRooms.Length > 0 && info.MidSafeRooms[0] != null
                     && PathLength(startPosition, info.MidSafeRooms[0].bounds.center - Vector3.up * (info.MidSafeRooms[0].bounds.extents.y - 0.2f), path, out _);

        // 3. 경로 밖으로 새는 곳 (벽 바깥 지점까지 걸어갈 수 있으면 누수)
        int probes = 0;
        List<string> leaks = new List<string>();
        for (int i = 0; layout.probes != null && i + 2 < layout.probes.Length; i += 3)
        {
            Vector3 probe = V(layout.probes, i);
            if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 1.0f, NavMesh.AllAreas)) continue;
            if (Mathf.Abs(hit.position.x - probe.x) > 0.8f || Mathf.Abs(hit.position.z - probe.z) > 0.8f) continue;
            probes++;
            if (PathLength(startPosition, hit.position, path, out _)) leaks.Add(hit.position.ToString("0"));
        }

        // 4. 스폰 포인트에서 플레이어 쪽으로 올 수 있는가
        int badSpawns = 0;
        List<string> badSpawnNames = new List<string>();
        foreach (Transform spawn in info.SpawnPoints)
        {
            if (PathLength(spawn.position, startPosition, path, out _)) continue;
            badSpawns++;
            badSpawnNames.Add(spawn.name);
        }

        // 5. 분홍(URP에서 못 그리는) 머티리얼
        int pink = 0;
        HashSet<string> pinkNames = new HashSet<string>();
        List<Material> materials = info.transform.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToList();
        Terrain terrain = info.GetComponentInChildren<Terrain>();
        if (terrain != null)
        {
            if (terrain.materialTemplate != null) materials.Add(terrain.materialTemplate);
            foreach (TreePrototype tree in terrain.terrainData.treePrototypes)
            {
                if (tree.prefab != null) materials.AddRange(tree.prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials));
            }
        }

        foreach (Material material in materials.Where(m => m != null).Distinct())
        {
            Shader shader = material.shader;
            bool bad = shader == null || !shader.isSupported || shader.name == "Hidden/InternalErrorShader" || ShaderUtil.ShaderHasError(shader)
                       || !(shader.name.StartsWith("Universal Render Pipeline/") || shader.name.StartsWith("L4D/") || shader.name.StartsWith("Shader Graphs/"));
            if (!bad) continue;
            pink++;
            pinkNames.Add(material.name + " (" + (shader != null ? shader.name : "null") + ")");
        }

        // 6. 그리기 부담 (대략)
        int renderers = info.transform.GetComponentsInChildren<Renderer>(true).Length;
        int lodGroups = info.transform.GetComponentsInChildren<LODGroup>(true).Length;

        Log.Add("");
        Log.Add("■ 검사 결과 (NavMesh 기준)");
        Log.Add("  - 진행 경로(지점 " + route.Length + "개)를 따라 걸은 실제 길이: " + routeLength.ToString("0") + " m"
                + (broken.Count == 0 ? " / 끊긴 곳 없음 ✔" : " / ❌ 끊긴 구간 " + broken.Count + "곳: " + string.Join(", ", broken.Take(12).Select(i => "Route_" + i.ToString("000") + "→" + (i + 1).ToString("000") + " " + route[i].position.ToString("0")))));
        Log.Add("  - 출발 → 무전기 최단 경로: " + (toRadio ? shortestToRadio.ToString("0") + " m ✔" : "❌ 갈 수 없음")
                + " / 무전기 → 탈출 보트: " + (toBoat ? radioToBoat.ToString("0") + " m ✔" : "❌ 갈 수 없음")
                + " / 중간 안전가옥: " + (toMid ? "들어갈 수 있음 ✔" : "❌ 길 없음"));
        Log.Add("  - 경계 밖 검사 지점 " + probes + "곳 중 출발점에서 걸어갈 수 있는 곳: " + (leaks.Count == 0 ? "0곳 ✔" : "❌ " + leaks.Count + "곳 " + string.Join(" ", leaks.Take(10))));
        Log.Add("  - 좀비 스폰 포인트 " + info.SpawnPoints.Length + "곳 중 플레이어까지 길이 없는 곳: " + (badSpawns == 0 ? "0곳 ✔" : badSpawns + "곳 (" + string.Join(", ", badSpawnNames.Take(8)) + ")"));
        Log.Add("  - URP에서 그릴 수 없는(분홍) 머티리얼: " + (pink == 0 ? "0개 ✔" : "❌ " + pink + "개: " + string.Join(", ", pinkNames.Take(10))));
        Log.Add("  - 맵 렌더러 " + renderers + "개, LODGroup " + lodGroups + "개 (건물은 머티리얼별로 합쳐짐)");

        if (broken.Count > 0) Warnings.Add("❌ 진행 경로가 NavMesh에서 " + broken.Count + "곳 끊겼습니다. (보고서의 Route 번호·좌표 참고)");
        if (!toRadio || !toBoat) Warnings.Add("❌ 출발 → 무전기 → 탈출 중 이어지지 않는 구간이 있습니다.");
        if (leaks.Count > 0) Warnings.Add("⚠ 경계 밖으로 걸어갈 수 있는 곳이 " + leaks.Count + "곳 있습니다. (보고서 좌표 참고)");
        if (badSpawns > info.SpawnPoints.Length / 10) Warnings.Add("⚠ 길이 없는 스폰 포인트가 " + badSpawns + "곳 있습니다. (AIDirector는 길 있는 곳만 고르지만 확인 필요)");
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

    private static void ComputeOcclusion()
    {
        StaticOcclusionCulling.smallestOccluder = 4f;
        StaticOcclusionCulling.smallestHole = 0.4f;
        StaticOcclusionCulling.backfaceThreshold = 100f;
        StaticOcclusionCulling.Compute();
        Log.Add("✔ 오클루전 컬링 굽기 완료 (건물 뒤에 가려진 물체는 그리지 않음)");
    }

    // ======================================================================
    // 보고서
    // ======================================================================

    private static void WriteReport(bool success)
    {
        StringBuilder text = new StringBuilder();
        text.AppendLine("플러디드 캠페인 맵 보고서 - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
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
        text.AppendLine("■ 구역 순서 (약 2 km)");
        text.AppendLine("  출발 오두막 → 늪지 둑길 → 섬 → 물가 마을 → 농장 → 온실 통과 → 공장 지대 → 공장 마당(전투) → 뒷골목");
        text.AppendLine("  → 강변 도로 → 철교 → 강변 → 중간 안전가옥(오두막) → 읍내 → 교회 광장(전투) → 뒷길 → 읍내 서쪽");
        text.AppendLine("  → 저택 통과 → 정원(피날레: 무전기 [E], 버티기) → 선착장 끝 보트(탈출)");

        try
        {
            EnsureFolder(Folder);
            File.WriteAllText(ReportPath, text.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ReportPath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[캠페인 맵] 보고서 저장 실패: " + exception.Message);
        }

        Debug.Log("[캠페인 맵]\n" + text);
        string summary = string.Join("\n", Log.Where(l => l.StartsWith("  -")).Take(8));
        string warn = Warnings.Count > 0 ? "\n\n확인 필요:\n" + string.Join("\n", Warnings.Take(6)) : string.Empty;
        EditorUtility.DisplayDialog(success ? "캠페인 맵 완료" : "캠페인 맵 실패",
            (success ? "맵을 만들었습니다. ▶ Play로 확인해 보세요.\n\n" : string.Empty) + summary + warn + "\n\n전체 보고서: " + ReportPath, "확인");
    }

    // ======================================================================
    // 도우미
    // ======================================================================

    private static void Progress(string message, float value)
    {
        EditorUtility.DisplayProgressBar("플러디드 캠페인 맵 만들기", message, value);
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

    // 범위 상자 (트리거, Ignore Raycast 레이어 - 총알/길찾기에 영향 없음)
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
        light.lightmapBakeType = LightmapBakeType.Realtime;
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
}
#endif
