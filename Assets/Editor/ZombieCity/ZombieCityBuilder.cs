#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// 레프트 4 데드 느낌의 도시 맵을 지금 맵(Map)의 남쪽에 이어 붙이는 에디터 메뉴. (v2: Kenney 무료 모델 사용)
//   Tools > Zombie City > Build City (extend current map)
// 출발(기존 맵) → 골목 → 큰길(버려진 차, 가로등, 막힌 사거리) → 바리케이드로 막힌 길을 주차장으로 우회
// → 마지막 거리 → 도착 안전가옥(들어가면 탈출 성공).
// 플레이 영역은 보이지 않는 높은 벽으로 둘러싸여 있어서 좀비와 플레이어가 건물 사이로 빠져나가지 못한다.
// 모델: Kenney City Kit (Commercial, Roads), Car Kit — CC0 (www.kenney.nl)
public static class ZombieCityBuilder
{
    private const string RootName = "ZombieCity";
    private const string MaterialFolder = "Assets/ZombieCity/Materials";
    private const string KenneyRoot = "Assets/ZombieCity/Kenney/";
    private const string BackgroundName = "FreeModels_Background";
    private const float GateWidth = 4.5f;
    private const float WallHeight = 40f;   // 2단 점프로도 넘지 못하는 높이
    private const float CityScale = 8f;      // Kenney 도시 키트 1칸 = 8m
    private const float CarScale = 1.6f;     // Kenney 자동차 키트 1단위 = 1.6m

    private enum Kit
    {
        Commercial,
        Roads,
        Cars,
    }

    private static System.Random _rng;
    private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();
    private static readonly Dictionary<string, GameObject> ModelCache = new Dictionary<string, GameObject>();
    private static readonly Dictionary<string, Bounds> BoundsCache = new Dictionary<string, Bounds>();
    private static readonly Dictionary<Kit, float> UnitCache = new Dictionary<Kit, float>();
    private static Shader _litShader;

    private static readonly string[] FrontBuildings =
    {
        "building-a", "building-b", "building-c", "building-d", "building-e", "building-f", "building-g",
        "building-h", "building-i", "building-j", "building-k", "building-l", "building-m", "building-n",
    };

    private static readonly string[] SkylineBuildings =
    {
        "building-skyscraper-a", "building-skyscraper-b", "building-skyscraper-c", "building-skyscraper-d",
        "building-skyscraper-e", "building-l", "building-m", "building-n",
    };

    // ===================================================================== 메뉴

    [MenuItem("Tools/Zombie City/Build City (extend current map)", false, 1)]
    public static void BuildMenu()
    {
        if (CanEdit() == false)
        {
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(KenneyRoot + "Roads/road-straight.fbx") == null)
        {
            Debug.LogError("[Zombie City] Kenney 모델이 없습니다. 패키지 전체(Assets/ZombieCity/Kenney 폴더 포함)를 Import하세요.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject old = FindRoot(scene);
        if (old != null && EditorUtility.DisplayDialog("Zombie City",
                "이미 만든 도시가 있습니다. 지우고 새로 만들까요?", "새로 만들기", "취소") == false)
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build Zombie City");
        try
        {
            if (old != null)
            {
                RemoveCity(old);
            }

            GameObject root = BuildCity(scene);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log("[Zombie City] 도시를 만들었습니다. Ctrl+S로 씬을 저장하고 Play 하세요. " +
                      "골목 → 큰길 → (바리케이드) 오른쪽 주차장으로 우회 → 안전가옥 순서입니다. (F키: 손전등)");
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
    }

    [MenuItem("Tools/Zombie City/Fix Kenney Import Settings", false, 10)]
    public static void FixImportMenu()
    {
        if (CanEdit() == false)
        {
            return;
        }

        int fixedCount = PrepareTextures() + PrepareModels();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Zombie City] Kenney 텍스처/모델 Import 설정 {fixedCount}개를 고쳤습니다. 이제 Build City를 다시 실행하세요.");
    }

    [MenuItem("Tools/Zombie City/Add Colliders to Background Models", false, 20)]
    public static void BackgroundCollidersMenu()
    {
        if (CanEdit() == false)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        int count = AddBackgroundColliders(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[Zombie City] 배경 모델 {count}개에 Collider를 붙였습니다.");
    }

    [MenuItem("Tools/Zombie City/Apply Night Atmosphere", false, 21)]
    public static void AtmosphereMenu()
    {
        if (CanEdit() == false)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject root = FindRoot(scene);
        ApplyAtmosphere(root != null ? root.GetComponent<ZombieCityInfo>() : null);
        EnsureFlashlight();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[Zombie City] 밤 + 안개 분위기를 적용했습니다.");
    }

    [MenuItem("Tools/Zombie City/Remove City", false, 40)]
    public static void RemoveMenu()
    {
        if (CanEdit() == false)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject root = FindRoot(scene);
        if (root == null)
        {
            Debug.Log("[Zombie City] 지울 도시가 없습니다.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Remove Zombie City");
        RemoveCity(root);
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[Zombie City] 도시를 지우고 원래 벽과 조명 설정을 되돌렸습니다.");
    }

    // ===================================================================== 전체 흐름

    private static GameObject BuildCity(Scene scene)
    {
        List<Transform> all = SceneTransforms(scene);
        Transform map = all.Find(t => t != null && t.name == "Map");
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        Vector3 playerPosition = playerObject != null ? playerObject.transform.position : Vector3.zero;

        Bounds arena = ArenaBounds(map, playerPosition);
        float floorY = FloorY(all, arena);

        _rng = new System.Random(20261026);
        MaterialCache.Clear();
        ModelCache.Clear();
        BoundsCache.Clear();
        UnitCache.Clear();
        _litShader = null;
        PrepareTextures();
        PrepareModels();

        // 도시의 기준점: 기존 맵 남쪽 끝 가운데. 도시 안에서 +Z가 남쪽(월드 -Z)으로 뻗어 나간다.
        GameObject rootObject = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(rootObject, scene);
        Transform root = rootObject.transform;
        root.SetPositionAndRotation(new Vector3(arena.center.x, floorY, arena.min.z), Quaternion.Euler(0f, 180f, 0f));
        ZombieCityInfo info = rootObject.AddComponent<ZombieCityInfo>();

        BuildGround(root);
        BuildBoundary(root);
        BuildBuildings(root);
        BuildAlleyProps(root);
        BuildStreetProps(root);
        BuildBarricade(root);
        BuildParkingLot(root);
        BuildSafeRoom(root);
        MarkStatic(root);

        Undo.RegisterCreatedObjectUndo(rootObject, "Build Zombie City");

        int cut = CutGate(map, arena, arena.center.x, info);
        if (cut == 0)
        {
            Debug.Log("[Zombie City] 남쪽에서 자를 벽을 찾지 못했습니다. 기존 맵 남쪽이 막혀 있다면 출입구(가운데 폭 4.5m)를 직접 열어 주세요.");
        }

        int colliders = AddBackgroundColliders(scene);
        if (colliders > 0)
        {
            Debug.Log($"[Zombie City] 배경 모델 {colliders}개에 Collider를 붙였습니다.");
        }

        ApplyAtmosphere(info);
        EnsureFlashlight();
        return rootObject;
    }

    // ===================================================================== 구역 (도시 기준 좌표, 단위 m)
    // x: 좌우(큰길 가운데 0), z: 기존 맵에서 멀어지는 방향, y: 바닥 0
    //  골목      : x -3~3,      z 0~32
    //  큰길      : x -7.5~7.5,  z 32~160 (차도 -4~4, 인도 양쪽 3.5m)
    //  막힌 사거리: z 64~72, 양옆 x ±14에서 막힘
    //  바리케이드 : z 112 (큰길 완전 차단)
    //  주차장 우회: x 7.5~30.5, z 96~128 (가운데 x 7.5~16, z 104~120은 건물 블록)
    //  안전가옥  : x -6~6,      z 160~174

    private static void BuildGround(Transform root)
    {
        Transform g = Group("Ground", root, Vector3.zero, 0f);
        Box("Base", g, new Vector3(6f, -0.25f, 88f), new Vector3(96f, 0.5f, 184f), "Asphalt", true);

        // 큰길 차도 (Kenney 도로 타일은 X 방향으로 뻗어 있어서 90도 돌린다)
        for (int k = 0; k < 16; k++)
        {
            float z = 36f + 8f * k;
            string tile = k == 4 ? "road-crossroad" : (k == 0 || k == 13) ? "road-crossing" : "road-straight";
            Place(Kit.Roads, tile, g, new Vector3(0f, 0f, z), 90f, CityScale, Vector3.one, true, false, "K_Roads");

            if (k != 4)
            {
                Place(Kit.Roads, "tile-low", g, new Vector3(-5.75f, 0f, z), 0f, CityScale, new Vector3(3.5f / 8f, 1f, 1f), true, false, "K_Roads");
                Place(Kit.Roads, "tile-low", g, new Vector3(5.75f, 0f, z), 0f, CityScale, new Vector3(3.5f / 8f, 1f, 1f), true, false, "K_Roads");
            }
        }

        // 막힌 사거리의 옆길
        foreach (float x in new[] { -16f, -8f, 8f, 16f })
        {
            Place(Kit.Roads, "road-straight", g, new Vector3(x, 0f, 68f), 0f, CityScale, Vector3.one, true, false, "K_Roads");
        }

        // 골목 바닥
        for (int i = 0; i < 4; i++)
        {
            Place(Kit.Roads, "tile-low", g, new Vector3(0f, 0f, 4f + 8f * i), 0f, CityScale, new Vector3(6f / 8f, 1f, 1f), true, false, "K_Roads");
        }

        // 주차장 바닥 + 주차선
        foreach (float x in new[] { 11.5f, 19.5f, 27.5f })
        {
            foreach (float z in new[] { 100f, 108f, 116f, 124f })
            {
                Place(Kit.Roads, "tile-low", g, new Vector3(x, 0f, z), 0f, CityScale, Vector3.one, true, false, "K_Roads");
            }
        }

        for (float z = 99.5f; z <= 125f; z += 3.2f)
        {
            Box("ParkingLine", g, new Vector3(27.4f, 0.17f, z), new Vector3(4.6f, 0.01f, 0.12f), "LineWhite", false);
        }

        // 핏자국
        for (int i = 0; i < 24; i++)
        {
            bool alley = i < 5;
            float x = alley ? Range(-2.4f, 2.4f) : Range(-3.2f, 3.2f);
            float z = alley ? Range(2f, 30f) : Range(34f, 158f);
            float size = Range(0.6f, 2f);
            GameObject blood = Cylinder("Blood", g, new Vector3(x, alley ? 0.17f : 0.09f, z), size, 0.01f, "Blood", false, Vector3.zero);
            blood.transform.localScale = new Vector3(size, 0.004f, size * Range(0.5f, 1.4f));
            blood.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
        }
    }

    // 플레이 영역 둘레의 보이지 않는 높은 벽
    private static void BuildBoundary(Transform root)
    {
        Transform b = Group("Boundary (invisible)", root, Vector3.zero, 0f);

        // 골목
        Wall(b, -3.25f, 16f, 0.5f, 32f);
        Wall(b, 3.25f, 16f, 0.5f, 32f);
        Wall(b, -5.25f, 31.75f, 4.5f, 0.5f);
        Wall(b, 5.25f, 31.75f, 4.5f, 0.5f);

        // 큰길 양옆
        Wall(b, -7.75f, 48f, 0.5f, 32f);
        Wall(b, -7.75f, 116f, 0.5f, 88f);
        Wall(b, 7.75f, 48f, 0.5f, 32f);
        Wall(b, 7.75f, 84f, 0.5f, 24f);
        Wall(b, 7.75f, 144f, 0.5f, 32f);

        // 막힌 사거리 옆길
        foreach (float side in new[] { -1f, 1f })
        {
            Wall(b, side * 10.75f, 63.75f, 6.5f, 0.5f);
            Wall(b, side * 10.75f, 72.25f, 6.5f, 0.5f);
            Wall(b, side * 14.25f, 68f, 0.5f, 8f);
        }

        // 바리케이드 (큰길 완전 차단)
        Wall(b, 0f, 112f, 15.5f, 1f);

        // 주차장
        Wall(b, 11.75f, 112f, 8.5f, 16f);    // 가운데 건물 블록
        Wall(b, 19f, 95.75f, 23f, 0.5f);
        Wall(b, 30.75f, 112f, 0.5f, 32f);
        Wall(b, 19f, 128.25f, 23f, 0.5f);

        // 길 끝 (안전가옥 양옆)
        Wall(b, -6.75f, 160.25f, 1.5f, 0.5f);
        Wall(b, 6.75f, 160.25f, 1.5f, 0.5f);
    }

    private static void BuildBuildings(Transform root)
    {
        Transform g = Group("Buildings", root, Vector3.zero, 0f);
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;

        // 골목 양옆
        BuildingRow(g, new Vector3(-3.5f, 0f, 0f), forward, 32f, Vector3.right, 12f, FrontBuildings, true);
        BuildingRow(g, new Vector3(3.5f, 0f, 0f), forward, 32f, Vector3.left, 12f, FrontBuildings, true);

        // 큰길 왼쪽
        BuildingRow(g, new Vector3(-8f, 0f, 32f), forward, 32f, Vector3.right, 16f, FrontBuildings, true);
        BuildingRow(g, new Vector3(-8f, 0f, 72f), forward, 88f, Vector3.right, 16f, FrontBuildings, true);

        // 큰길 오른쪽 (주차장 쪽은 건물 깊이를 제한)
        BuildingRow(g, new Vector3(8f, 0f, 32f), forward, 32f, Vector3.left, 16f, FrontBuildings, true);
        BuildingRow(g, new Vector3(8f, 0f, 72f), forward, 24f, Vector3.left, 8f, FrontBuildings, true);
        BuildingRow(g, new Vector3(8f, 0f, 104f), forward, 16f, Vector3.left, 7.8f, FrontBuildings, true);
        BuildingRow(g, new Vector3(8f, 0f, 128f), forward, 32f, Vector3.left, 8f, FrontBuildings, true);

        // 막힌 옆길 끝
        BuildingRow(g, new Vector3(-20.5f, 0f, 63.5f), forward, 9f, Vector3.right, 12f, FrontBuildings, true);
        BuildingRow(g, new Vector3(20.5f, 0f, 63.5f), forward, 9f, Vector3.left, 12f, FrontBuildings, true);

        // 주차장 둘레
        BuildingRow(g, new Vector3(31f, 0f, 96f), forward, 32f, Vector3.left, 14f, FrontBuildings, true);
        BuildingRow(g, new Vector3(16.5f, 0f, 95.5f), right, 14f, Vector3.forward, 14f, FrontBuildings, true);
        BuildingRow(g, new Vector3(16.5f, 0f, 128.5f), right, 14f, Vector3.back, 14f, FrontBuildings, true);

        // 길 끝 (안전가옥 양옆)
        BuildingRow(g, new Vector3(-30.5f, 0f, 160.5f), right, 24.5f, Vector3.back, 16f, FrontBuildings, true);
        BuildingRow(g, new Vector3(6.5f, 0f, 160.5f), right, 24f, Vector3.back, 16f, FrontBuildings, true);

        // 멀리 보이는 높은 건물들 (분위기용)
        BuildingRow(g, new Vector3(-34f, 0f, 20f), forward, 150f, Vector3.right, 20f, SkylineBuildings, false);
        BuildingRow(g, new Vector3(46f, 0f, 20f), forward, 150f, Vector3.left, 20f, SkylineBuildings, false);
        BuildingRow(g, new Vector3(-32f, 0f, 180f), right, 64f, Vector3.back, 20f, SkylineBuildings, false);
    }

    private static void BuildAlleyProps(Transform root)
    {
        Transform a = Group("AlleyProps", root, Vector3.zero, 0f);
        Place(Kit.Roads, "dumpster", a, new Vector3(2.05f, 0.16f, 8f), 0f, 6f, Vector3.one, true, false, "K_Roads");
        Place(Kit.Roads, "dumpster", a, new Vector3(-2.05f, 0.16f, 21f), 180f, 6f, Vector3.one, true, false, "K_Roads");
        Place(Kit.Cars, "box", a, new Vector3(-2.3f, 0.16f, 12f), 15f, CarScale, Vector3.one, true, false, "K_Cars");
        Place(Kit.Cars, "box", a, new Vector3(-2.2f, 0.16f, 13.3f), -10f, CarScale, Vector3.one, true, false, "K_Cars");
        Place(Kit.Cars, "box", a, new Vector3(-2.25f, 1.3f, 12.5f), 40f, CarScale, Vector3.one, true, false, "K_Cars");
        Place(Kit.Roads, "construction-fence", a, new Vector3(-1.35f, 0.16f, 26f), 90f, CityScale, Vector3.one, true, false, "K_Roads");
        Place(Kit.Cars, "cone", a, new Vector3(1.2f, 0.16f, 25.6f), 0f, CarScale, Vector3.one, false, false, "K_Cars");
        Place(Kit.Cars, "debris-tire", a, new Vector3(-2.2f, 0.16f, 29.5f), 30f, CarScale, Vector3.one, false, false, "K_Cars");
        Place(Kit.Cars, "debris-door", a, new Vector3(2.2f, 0.16f, 16f), 80f, CarScale, Vector3.one, false, false, "K_Cars");

        WallLamp(a, new Vector3(2.95f, 4.2f, 6f), new Vector3(-1f, 0f, 0f), FlickerLight.FlickerMode.Broken);
        WallLamp(a, new Vector3(-2.95f, 4.2f, 18f), new Vector3(1f, 0f, 0f), FlickerLight.FlickerMode.Subtle);
        WallLamp(a, new Vector3(2.95f, 4.2f, 29f), new Vector3(-1f, 0f, 0f), FlickerLight.FlickerMode.Broken);
    }

    private static void BuildStreetProps(Transform root)
    {
        Transform s = Group("StreetProps", root, Vector3.zero, 0f);

        // 가로등 (인도 위, 팔은 차도 쪽)
        StreetLight(s, new Vector3(-6.9f, 0.16f, 42f), Vector3.right, LampState.On);
        StreetLight(s, new Vector3(6.9f, 0.16f, 56f), Vector3.left, LampState.Flicker);
        StreetLight(s, new Vector3(-6.9f, 0.16f, 80f), Vector3.right, LampState.Flicker);
        StreetLight(s, new Vector3(6.9f, 0.16f, 88f), Vector3.left, LampState.On);
        StreetLight(s, new Vector3(-6.9f, 0.16f, 104f), Vector3.right, LampState.Dead);
        StreetLight(s, new Vector3(6.9f, 0.16f, 138f), Vector3.left, LampState.On);
        StreetLight(s, new Vector3(-6.9f, 0.16f, 146f), Vector3.right, LampState.Flicker);

        // 사거리 신호등, 표지판
        foreach (Vector3 p in new[] { new Vector3(-6.9f, 0.16f, 63.2f), new Vector3(6.9f, 0.16f, 72.8f) })
        {
            Place(Kit.Roads, "traffic-light", s, p, p.x < 0f ? 90f : -90f, CityScale, Vector3.one, true, false, "K_Roads");
        }

        Place(Kit.Roads, "road-sign-stop", s, new Vector3(6.9f, 0.16f, 61.5f), -90f, CityScale, Vector3.one, true, false, "K_Roads");

        // 막힌 옆길 (x ±13)
        foreach (float side in new[] { -1f, 1f })
        {
            for (int i = 0; i < 4; i++)
            {
                Place(Kit.Roads, "construction-barrier", s, new Vector3(side * 12.6f, 0.08f, 65.2f + i * 1.9f), 0f, CityScale, Vector3.one, true, false, "K_Roads");
            }

            Place(Kit.Cars, side < 0f ? "garbage-truck" : "delivery", s, new Vector3(side * 17f, 0.08f, 68f), side < 0f ? 15f : 160f, CarScale, Vector3.one, true, false, "K_Cars");
        }

        // 버려진 차들 (항상 지나갈 틈이 남도록)
        Car(s, "sedan", new Vector3(-2.2f, 0.08f, 44f), 12f, false);
        Car(s, "suv", new Vector3(2.4f, 0.08f, 50f), 178f, false);
        Car(s, "taxi", new Vector3(-1.4f, 0.08f, 58f), 72f, false);
        Car(s, "police", new Vector3(2.2f, 0.08f, 75.5f), 200f, false);
        Car(s, "van", new Vector3(-2.1f, 0.08f, 82f), 168f, false);
        Car(s, "sedan-sports", new Vector3(2.0f, 0.08f, 90f), -20f, true);
        Car(s, "hatchback-sports", new Vector3(-2.4f, 0.08f, 100f), 5f, false);
        Car(s, "sedan", new Vector3(2.0f, 0.08f, 134f), -8f, true);
        Car(s, "suv-luxury", new Vector3(-2.3f, 0.08f, 142f), 160f, false);
        Car(s, "ambulance", new Vector3(1.4f, 0.08f, 151f), 60f, false);
        Car(s, "delivery-flat", new Vector3(-3.2f, 0.08f, 155.5f), 185f, false);

        // 경찰차, 구급차 경광등
        Siren(s, new Vector3(2.2f, 2.3f, 75.5f), new Color(0.25f, 0.4f, 1f));
        Siren(s, new Vector3(1.4f, 3.1f, 151f), new Color(1f, 0.2f, 0.15f));

        // 잔해
        string[] debris = { "debris-tire", "debris-door", "debris-bumper", "debris-plate-a", "debris-door-window" };
        for (int i = 0; i < 14; i++)
        {
            Vector3 p = new Vector3(Range(-3.3f, 3.3f), 0.08f, Range(36f, 158f));
            if (p.z > 108f && p.z < 116f) continue;
            Place(Kit.Cars, Pick(debris), s, p, Range(0f, 360f), CarScale, Vector3.one, false, false, "K_Cars");
        }

        for (int i = 0; i < 6; i++)
        {
            Place(Kit.Cars, "cone", s, new Vector3(Range(-3.4f, 3.4f), 0.08f, Range(36f, 158f)), Range(0f, 360f), CarScale, Vector3.one, false, false, "K_Cars");
        }

        Place(Kit.Roads, "dumpster", s, new Vector3(-6.4f, 0.16f, 54f), 0f, 6f, Vector3.one, true, false, "K_Roads");
        Place(Kit.Roads, "dumpster", s, new Vector3(6.4f, 0.16f, 146f), 180f, 6f, Vector3.one, true, false, "K_Roads");
        Place(Kit.Cars, "box", s, new Vector3(6.6f, 0.16f, 40f), 20f, CarScale, Vector3.one, true, false, "K_Cars");
        Place(Kit.Cars, "box", s, new Vector3(-6.6f, 0.16f, 126f), -15f, CarScale, Vector3.one, true, false, "K_Cars");
    }

    // 큰길을 막는 바리케이드 (z 112)
    private static void BuildBarricade(Transform root)
    {
        Transform b = Group("Barricade", root, Vector3.zero, 0f);
        Place(Kit.Cars, "firetruck", b, new Vector3(-3.2f, 0.08f, 111f), 90f, CarScale, Vector3.one, true, false, "K_Cars");
        Place(Kit.Cars, "garbage-truck", b, new Vector3(3.5f, 0.08f, 113f), -80f, CarScale, Vector3.one, true, false, "K_Cars");

        for (int i = 0; i < 8; i++)
        {
            Place(Kit.Roads, "construction-barrier", b, new Vector3(-6.6f + i * 1.9f, 0.08f, 109.4f), 90f, CityScale, Vector3.one, true, false, "K_Roads");
        }

        Place(Kit.Roads, "road-sign-warning", b, new Vector3(5.6f, 0.16f, 108.2f), 180f, CityScale, Vector3.one, true, false, "K_Roads");
        foreach (float x in new[] { -6.8f, 6.8f })
        {
            Place(Kit.Roads, "construction-light", b, new Vector3(x, 0.16f, 108.6f), 0f, CityScale, Vector3.one, true, false, "K_Roads");
            Light warn = AddLight(b, "WarningLight", new Vector3(x, 2.1f, 108.6f), LightType.Point, new Color(1f, 0.55f, 0.1f), 1.4f, 7f, 0f, Vector3.zero);
            AddFlicker(warn, FlickerLight.FlickerMode.Broken, null);
        }

        for (int i = 0; i < 5; i++)
        {
            Place(Kit.Cars, "cone", b, new Vector3(-5f + i * 2.4f, 0.08f, 107.6f), Range(0f, 360f), CarScale, Vector3.one, false, false, "K_Cars");
        }
    }

    // 바리케이드를 돌아가는 주차장 (x 7.5~30.5, z 96~128, 가운데 블록 x 7.5~16, z 104~120)
    private static void BuildParkingLot(Transform root)
    {
        Transform p = Group("ParkingLot", root, Vector3.zero, 0f);

        string[] parked = { "sedan", "suv", "van", "taxi", "hatchback-sports", "truck", "suv-luxury" };
        foreach (float z in new[] { 101.1f, 107.5f, 113.9f, 120.3f, 126.4f })
        {
            Car(p, Pick(parked), new Vector3(27.6f, 0.16f, z), _rng.NextDouble() < 0.5 ? 88f : 272f, false);
        }

        Car(p, "sedan", new Vector3(21f, 0.16f, 113f), 30f, true);
        Place(Kit.Roads, "dumpster", p, new Vector3(17.6f, 0.16f, 106f), 90f, 6f, Vector3.one, true, false, "K_Roads");
        Place(Kit.Roads, "construction-fence", p, new Vector3(19.5f, 0.16f, 99.6f), 0f, CityScale, Vector3.one, true, false, "K_Roads");
        Place(Kit.Roads, "construction-fence", p, new Vector3(19.5f, 0.16f, 124.4f), 0f, CityScale, Vector3.one, true, false, "K_Roads");
        Place(Kit.Cars, "box", p, new Vector3(17.4f, 0.16f, 117.5f), 10f, CarScale, Vector3.one, true, false, "K_Cars");
        Place(Kit.Cars, "box", p, new Vector3(17.6f, 0.16f, 118.8f), -20f, CarScale, Vector3.one, true, false, "K_Cars");
        Place(Kit.Roads, "electricity-pole-single", p, new Vector3(24f, 0.16f, 97f), 0f, CityScale, Vector3.one, true, false, "K_Roads");

        StreetLight(p, new Vector3(16.9f, 0.16f, 97.2f), Vector3.right, LampState.Flicker);
        StreetLight(p, new Vector3(16.9f, 0.16f, 126.8f), Vector3.right, LampState.On);
    }

    // 도착 안전가옥 (x -6~6, z 160~174)
    private static void BuildSafeRoom(Transform root)
    {
        Transform sr = Group("SafeRoom", root, Vector3.zero, 0f);
        const float h = 3.6f;
        const float t = 0.3f;
        const float front = 160f;
        const float back = 174f;
        float mid = (front + back) * 0.5f;

        WallAlongX(sr, "SafeRoom_Front", front + 0.15f, -6f, 6f, h, t, "Concrete", new[] { new Vector2(-1.2f, 1.2f) }, 2.4f);
        WallAlongX(sr, "SafeRoom_Back", back - 0.15f, -6f, 6f, h, t, "Concrete", null, 0f);
        WallAlongZ(sr, "SafeRoom_Left", -5.85f, front, back, h, t, "Concrete", null, 0f);
        WallAlongZ(sr, "SafeRoom_Right", 5.85f, front, back, h, t, "Concrete", null, 0f);
        Box("Roof", sr, new Vector3(0f, h + 0.15f, mid), new Vector3(12f, 0.3f, back - front), "RoofDark", true);
        Box("Floor", sr, new Vector3(0f, 0.17f, mid), new Vector3(11.4f, 0.02f, back - front - 0.6f), "Tile", true);

        // 빨간 철문 (열린 채로) + 문 위 빨간 등 + 간판
        Box("SafeDoor", sr, new Vector3(1.25f, 1.2f, front - 1.15f), new Vector3(0.08f, 2.35f, 2.3f), "SafeDoor", true);
        GameObject lamp = Box("DoorLamp", sr, new Vector3(0f, 2.75f, front - 0.05f), new Vector3(0.6f, 0.22f, 0.15f), "LampRed", false);
        Light red = AddLight(sr, "DoorLight", new Vector3(0f, 2.7f, front - 0.7f), LightType.Point, new Color(1f, 0.15f, 0.1f), 1.6f, 6f, 0f, Vector3.zero);
        AddFlicker(red, FlickerLight.FlickerMode.Subtle, lamp.GetComponent<Renderer>());
        Box("SignPlate", sr, new Vector3(0f, 3.25f, front - 0.07f), new Vector3(3.2f, 0.55f, 0.06f), "MetalDark", false);
        Sign(sr, "SAFE ROOM", new Vector3(0f, 3.25f, front - 0.12f), 0f, new Color(1f, 0.2f, 0.15f), 0.07f);

        // 안: 초록 조명, 간이침대, 탄약 상자
        AddLight(sr, "RoomLight", new Vector3(0f, h - 0.3f, mid), LightType.Point, new Color(0.55f, 1f, 0.6f), 1.8f, 11f, 0f, Vector3.zero);
        Box("RoomLamp", sr, new Vector3(0f, h - 0.1f, mid), new Vector3(0.3f, 0.1f, 1.4f), "LampGreen", false);
        Box("Cot", sr, new Vector3(-4.6f, 0.5f, mid - 3f), new Vector3(0.9f, 0.3f, 2.1f), "Cot", true);
        Box("Cot", sr, new Vector3(-4.6f, 0.5f, mid + 2f), new Vector3(0.9f, 0.3f, 2.1f), "Cot", true);
        Box("Table", sr, new Vector3(3.8f, 0.6f, mid + 4f), new Vector3(2f, 0.9f, 1f), "Wood", true);
        Place(Kit.Cars, "box", sr, new Vector3(4.4f, 0.18f, mid - 3f), 10f, CarScale, Vector3.one, true, false, "K_Cars");
        Place(Kit.Cars, "box", sr, new Vector3(4.6f, 0.18f, mid - 1.6f), -5f, CarScale, Vector3.one, true, false, "K_Cars");

        // 들어오면 탈출 성공
        GameObject goal = new GameObject("SafeRoomGoal");
        goal.transform.SetParent(sr, false);
        goal.transform.localPosition = new Vector3(0f, 1.6f, mid + 0.5f);
        BoxCollider area = goal.AddComponent<BoxCollider>();
        area.isTrigger = true;
        area.size = new Vector3(10.6f, 3f, back - front - 1.4f);
        goal.AddComponent<SafeRoomGoal>();
    }

    // ===================================================================== 건물 줄 배치

    // start에서 direction 방향으로 length만큼, 건물 정면이 facing 쪽을 보도록 빈틈없이 세운다.
    private static void BuildingRow(Transform parent, Vector3 start, Vector3 direction, float length, Vector3 facing,
        float maxDepth, string[] pool, bool colliders)
    {
        float unit = CityScale / Unit(Kit.Commercial);
        float cursor = 0f;
        int guard = 0;
        while (cursor < length - 0.6f && guard++ < 60)
        {
            float remaining = length - cursor;
            string chosen = null;
            Bounds chosenBounds = new Bounds();
            for (int attempt = 0; attempt < 8; attempt++)
            {
                string candidate = Pick(pool);
                if (MeasureModel(Kit.Commercial, candidate, out Bounds b) == false)
                {
                    continue;
                }

                float width = b.size.x * unit;
                float depth = b.size.z * unit;
                if (width <= remaining + 0.05f && depth <= maxDepth)
                {
                    chosen = candidate;
                    chosenBounds = b;
                    break;
                }
            }

            string material = Pick("K_Commercial", "K_CommercialA", "K_CommercialB");
            float used;
            if (chosen != null)
            {
                used = chosenBounds.size.x * unit;
                Vector3 front = start + direction * (cursor + used * 0.5f);
                Place(Kit.Commercial, chosen, parent, front, Yaw(facing), CityScale, Vector3.one, colliders, true, material);
            }
            else
            {
                // 남은 자리는 납작한 건물을 늘려서 채운다.
                string filler = Pick("low-detail-building-wide-a", "low-detail-building-wide-b");
                if (MeasureModel(Kit.Commercial, filler, out Bounds f) == false)
                {
                    break;
                }

                used = remaining;
                float depth = Mathf.Min(maxDepth, 8f);
                Vector3 stretch = new Vector3(used / (f.size.x * unit), 1f, depth / (f.size.z * unit));
                Vector3 front = start + direction * (cursor + used * 0.5f);
                Place(Kit.Commercial, filler, parent, front, Yaw(facing), CityScale, stretch, colliders, true, material);
            }

            cursor += used + 0.25f;
        }
    }

    private static float Yaw(Vector3 facing)
    {
        return Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
    }

    // ===================================================================== 소품

    private static void Car(Transform parent, string model, Vector3 position, float yaw, bool burning)
    {
        Transform car = Place(Kit.Cars, model, parent, position, yaw, CarScale, Vector3.one, true, false, burning ? "Charred" : "K_Cars");
        if (car != null && burning)
        {
            Fire(parent, position + new Vector3(0f, 1.8f, 0f), 2.6f);
        }
    }

    private static void Siren(Transform parent, Vector3 position, Color color)
    {
        Light light = AddLight(parent, "Siren", position, LightType.Point, color, 1.6f, 9f, 0f, Vector3.zero);
        AddFlicker(light, FlickerLight.FlickerMode.Broken, null);
    }

    private enum LampState
    {
        On,
        Flicker,
        Dead,
    }

    // Kenney 가로등 + 머리 부분에서 아래를 비추는 빛
    private static void StreetLight(Transform parent, Vector3 position, Vector3 armDirection, LampState state)
    {
        if (MeasureModel(Kit.Roads, "light-curved", out Bounds b) == false)
        {
            return;
        }

        // 팔이 모델의 +Z 쪽인지 -Z 쪽인지 확인해서 차도 쪽으로 돌린다.
        float armSign = b.center.z < 0f ? -1f : 1f;
        float yaw = Mathf.Atan2(armDirection.x * armSign, armDirection.z * armSign) * Mathf.Rad2Deg;
        Transform lamp = Place(Kit.Roads, "light-curved", parent, position, yaw, CityScale, Vector3.one, false, false, "K_Roads");
        if (lamp == null)
        {
            return;
        }

        // 기둥에만 Collider를 붙인다. (팔 아래로는 지나갈 수 있게)
        float pole = Mathf.Max(0.0001f, b.size.x);
        BoxCollider post = lamp.gameObject.AddComponent<BoxCollider>();
        post.center = new Vector3(b.center.x, b.center.y, armSign < 0f ? b.max.z - pole * 0.5f : b.min.z + pole * 0.5f);
        post.size = new Vector3(pole, b.size.y, pole);

        if (state == LampState.Dead)
        {
            return;
        }

        Vector3 headLocal = new Vector3(b.center.x, b.max.y - b.size.y * 0.04f,
            armSign < 0f ? b.min.z + b.size.z * 0.12f : b.max.z - b.size.z * 0.12f);
        Vector3 head = parent.InverseTransformPoint(lamp.TransformPoint(headLocal));
        Light light = AddLight(parent, "StreetLight", head - Vector3.up * 0.15f, LightType.Spot,
            new Color(1f, 0.8f, 0.55f), 4f, 16f, 105f, new Vector3(90f, 0f, 0f));
        if (state == LampState.Flicker)
        {
            AddFlicker(light, FlickerLight.FlickerMode.Broken, null);
        }
    }

    private static void WallLamp(Transform parent, Vector3 position, Vector3 facing, FlickerLight.FlickerMode mode)
    {
        Transform l = Group("WallLamp", parent, position, 0f);
        Box("Bracket", l, facing * 0.2f, FaceSize(new Vector3(Mathf.Abs(facing.z), 0f, Mathf.Abs(facing.x)), facing, 0.1f, 0.1f, 0.4f), "MetalDark", false);
        GameObject head = Box("LampHead", l, facing * 0.4f - Vector3.up * 0.05f, new Vector3(0.35f, 0.12f, 0.35f), "LampWarm", false);
        Vector3 direction = new Vector3(facing.x * 0.6f, -1f, facing.z * 0.6f);
        Light light = AddLight(l, "Light", facing * 0.45f - Vector3.up * 0.15f, LightType.Spot,
            new Color(1f, 0.78f, 0.5f), 2.6f, 11f, 110f, Quaternion.LookRotation(direction).eulerAngles);
        AddFlicker(light, mode, head.GetComponent<Renderer>());
    }

    private static void Fire(Transform parent, Vector3 position, float intensity)
    {
        Light light = AddLight(parent, "FireLight", position, LightType.Point, new Color(1f, 0.5f, 0.15f), intensity, 9f, 0f, Vector3.zero);
        AddFlicker(light, FlickerLight.FlickerMode.Fire, null);
    }

    // 보이지 않는 높은 벽 (BoxCollider만 있음)
    private static void Wall(Transform parent, float x, float z, float sizeX, float sizeZ)
    {
        GameObject wall = new GameObject("Wall");
        wall.transform.SetParent(parent, false);
        BoxCollider collider = wall.AddComponent<BoxCollider>();
        collider.center = new Vector3(x, WallHeight * 0.5f - 0.5f, z);
        collider.size = new Vector3(sizeX, WallHeight + 1f, sizeZ);
    }

    // ===================================================================== Kenney 모델

    private static string KitFolder(Kit kit)
    {
        switch (kit)
        {
            case Kit.Commercial: return KenneyRoot + "Commercial/";
            case Kit.Roads: return KenneyRoot + "Roads/";
            default: return KenneyRoot + "Cars/";
        }
    }

    private static GameObject LoadModel(Kit kit, string name)
    {
        string path = KitFolder(kit) + name + ".fbx";
        if (ModelCache.TryGetValue(path, out GameObject cached))
        {
            return cached;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null)
        {
            Debug.LogWarning("[Zombie City] 모델을 찾지 못해 건너뜁니다: " + path);
        }

        ModelCache[path] = model;
        return model;
    }

    // 모델을 기본 크기(배율 1)로 놓았을 때의 범위
    private static bool MeasureModel(Kit kit, string name, out Bounds bounds)
    {
        string key = kit + "/" + name;
        if (BoundsCache.TryGetValue(key, out bounds))
        {
            return true;
        }

        GameObject model = LoadModel(kit, name);
        if (model == null)
        {
            return false;
        }

        GameObject wrapper = new GameObject("Measure");
        try
        {
            PrefabUtility.InstantiatePrefab(model, wrapper.transform);
            if (LocalMeshBounds(wrapper.transform, out bounds) == false)
            {
                return false;
            }
        }
        finally
        {
            Object.DestroyImmediate(wrapper);
        }

        BoundsCache[key] = bounds;
        return true;
    }

    // Kenney 1단위가 Unity에 몇 단위로 들어왔는지 (FBX 단위 변환과 상관없이 맞추려고 직접 잰다)
    private static float Unit(Kit kit)
    {
        if (UnitCache.TryGetValue(kit, out float unit))
        {
            return unit;
        }

        string reference;
        float expected;
        int axis;
        switch (kit)
        {
            case Kit.Commercial: reference = "building-a"; expected = 1.293f; axis = 1; break;
            case Kit.Roads: reference = "road-straight"; expected = 1f; axis = 0; break;
            default: reference = "sedan"; expected = 2.55f; axis = 2; break;
        }

        unit = 1f;
        if (MeasureModel(kit, reference, out Bounds b) && b.size[axis] > 0.0001f)
        {
            unit = b.size[axis] / expected;
        }

        UnitCache[kit] = unit;
        return unit;
    }

    // 모델을 감싼 빈 오브젝트를 만들어 놓는다. 감싼 오브젝트만 움직이므로 원래 모델의 회전/크기는 그대로다.
    // anchorFront = true면 position이 모델 정면(+Z) 바닥 가운데, false면 바닥 가운데.
    private static Transform Place(Kit kit, string name, Transform parent, Vector3 position, float yaw,
        float metersPerUnit, Vector3 stretch, bool collider, bool anchorFront, string material)
    {
        GameObject model = LoadModel(kit, name);
        if (model == null || MeasureModel(kit, name, out Bounds b) == false)
        {
            return null;
        }

        GameObject wrapper = new GameObject(name);
        wrapper.transform.SetParent(parent, false);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, wrapper.transform);

        float s = metersPerUnit / Unit(kit);
        Vector3 scale = new Vector3(s * stretch.x, s * stretch.y, s * stretch.z);
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 anchor = new Vector3(b.center.x, b.min.y, anchorFront ? b.max.z : b.center.z);
        wrapper.transform.localRotation = rotation;
        wrapper.transform.localScale = scale;
        wrapper.transform.localPosition = position - rotation * Vector3.Scale(scale, anchor);

        Material mat = Mat(material);
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] slots = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
            for (int i = 0; i < slots.Length; i++) slots[i] = mat;
            renderer.sharedMaterials = slots;
        }

        if (collider)
        {
            BoxCollider box = wrapper.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
        }

        return wrapper.transform;
    }

    // 색 팔레트 텍스처는 일반 2D 텍스처로, 밉맵 없이, 가장자리 반복 없이 쓴다. (멀리서 색이 섞이지 않게)
    // 큐브맵 등으로 잘못 들어와 있으면 여기서 바로잡는다.
    private static int PrepareTextures()
    {
        string[] textures =
        {
            KitFolder(Kit.Commercial) + "Textures/colormap.png",
            KitFolder(Kit.Commercial) + "Textures/variation-a.png",
            KitFolder(Kit.Commercial) + "Textures/variation-b.png",
            KitFolder(Kit.Roads) + "Textures/colormap.png",
            KitFolder(Kit.Cars) + "Textures/colormap.png",
        };

        int changed = 0;
        foreach (string path in textures)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            bool ok = importer.textureType == TextureImporterType.Default
                      && importer.textureShape == TextureImporterShape.Texture2D
                      && importer.mipmapEnabled == false
                      && importer.wrapMode == TextureWrapMode.Clamp;
            if (ok)
            {
                continue;
            }

            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            changed++;
        }

        return changed;
    }

    // Kenney 모델은 재질을 따로 만들어 입히므로, 모델 안의 재질은 가져오지 않는다. 크기는 파일 단위 그대로.
    private static int PrepareModels()
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { KenneyRoot.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                continue;
            }

            bool ok = importer.materialImportMode == ModelImporterMaterialImportMode.None
                      && Mathf.Approximately(importer.globalScale, 1f)
                      && importer.useFileScale
                      && importer.importCameras == false
                      && importer.importLights == false
                      && importer.animationType == ModelImporterAnimationType.None
                      && importer.importAnimation == false;
            if (ok)
            {
                continue;
            }

            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.SaveAndReimport();
            changed++;
        }

        return changed;
    }

    // ===================================================================== 기본 도형

    private static Transform Group(string name, Transform parent, Vector3 position, float yaw)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return go.transform;
    }

    private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, string material, bool collider, float yaw = 0f)
    {
        GameObject box = Primitive(PrimitiveType.Cube, name, parent, center, size, material, collider);
        box.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return box;
    }

    private static GameObject Cylinder(string name, Transform parent, Vector3 center, float diameter, float height,
        string material, bool collider, Vector3 euler)
    {
        GameObject cylinder = Primitive(PrimitiveType.Cylinder, name, parent, center, new Vector3(diameter, height * 0.5f, diameter), material, collider);
        cylinder.transform.localRotation = Quaternion.Euler(euler);
        return cylinder;
    }

    private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 center, Vector3 scale,
        string material, bool collider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = Mat(material);
        if (collider == false)
        {
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        return go;
    }

    private static Vector3 FaceSize(Vector3 along, Vector3 normal, float w, float h, float d)
    {
        return new Vector3(Mathf.Abs(along.x) * w + Mathf.Abs(normal.x) * d, h, Mathf.Abs(along.z) * w + Mathf.Abs(normal.z) * d);
    }

    // x 방향으로 뻗은 벽 (doors: x 구간)
    private static void WallAlongX(Transform parent, string name, float z, float x0, float x1, float height,
        float thickness, string material, Vector2[] doors, float doorHeight)
    {
        float cursor = x0;
        if (doors != null)
        {
            foreach (Vector2 door in doors)
            {
                if (door.x > cursor + 0.01f)
                {
                    WallPiece(parent, name, new Vector3((cursor + door.x) * 0.5f, height * 0.5f, z), new Vector3(door.x - cursor, height, thickness), material);
                }

                float lintel = height - doorHeight;
                WallPiece(parent, name + "_Lintel", new Vector3((door.x + door.y) * 0.5f, doorHeight + lintel * 0.5f, z),
                    new Vector3(door.y - door.x, lintel, thickness), material);
                cursor = door.y;
            }
        }

        if (x1 > cursor + 0.01f)
        {
            WallPiece(parent, name, new Vector3((cursor + x1) * 0.5f, height * 0.5f, z), new Vector3(x1 - cursor, height, thickness), material);
        }
    }

    // z 방향으로 뻗은 벽 (doors: z 구간)
    private static void WallAlongZ(Transform parent, string name, float x, float z0, float z1, float height,
        float thickness, string material, Vector2[] doors, float doorHeight)
    {
        float cursor = z0;
        if (doors != null)
        {
            foreach (Vector2 door in doors)
            {
                if (door.x > cursor + 0.01f)
                {
                    WallPiece(parent, name, new Vector3(x, height * 0.5f, (cursor + door.x) * 0.5f), new Vector3(thickness, height, door.x - cursor), material);
                }

                float lintel = height - doorHeight;
                WallPiece(parent, name + "_Lintel", new Vector3(x, doorHeight + lintel * 0.5f, (door.x + door.y) * 0.5f),
                    new Vector3(thickness, lintel, door.y - door.x), material);
                cursor = door.y;
            }
        }

        if (z1 > cursor + 0.01f)
        {
            WallPiece(parent, name, new Vector3(x, height * 0.5f, (cursor + z1) * 0.5f), new Vector3(thickness, height, z1 - cursor), material);
        }
    }

    private static void WallPiece(Transform parent, string name, Vector3 center, Vector3 size, string material)
    {
        if (size.x <= 0.01f || size.y <= 0.01f || size.z <= 0.01f)
        {
            return;
        }

        GameObject wall = Box(name, parent, center, size, material, true);
        ExtendCollider(wall);
    }

    // 보이는 상자는 그대로 두고, Collider만 WallHeight 높이까지 늘린다.
    private static void ExtendCollider(GameObject box)
    {
        BoxCollider collider = box.GetComponent<BoxCollider>();
        if (collider == null)
        {
            return;
        }

        float height = Mathf.Max(0.01f, box.transform.localScale.y);
        float bottom = box.transform.localPosition.y - height * 0.5f;
        float total = Mathf.Max(1f, (WallHeight - bottom) / height);
        collider.size = new Vector3(1f, total, 1f);
        collider.center = new Vector3(0f, -0.5f + total * 0.5f, 0f);
    }

    private static bool AddFitCollider(GameObject target, bool recordUndo)
    {
        if (target.GetComponentInChildren<Collider>(true) != null)
        {
            return false;
        }

        if (LocalMeshBounds(target.transform, out Bounds bounds) == false)
        {
            return false;
        }

        BoxCollider collider = recordUndo ? Undo.AddComponent<BoxCollider>(target) : target.AddComponent<BoxCollider>();
        collider.center = bounds.center;
        collider.size = bounds.size;
        return true;
    }

    private static bool LocalMeshBounds(Transform root, out Bounds result)
    {
        result = new Bounds();
        bool found = false;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
            {
                continue;
            }

            Bounds b = filter.sharedMesh.bounds;
            Matrix4x4 matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = b.center + Vector3.Scale(b.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                point = matrix.MultiplyPoint3x4(point);
                if (found)
                {
                    result.Encapsulate(point);
                }
                else
                {
                    result = new Bounds(point, Vector3.zero);
                    found = true;
                }
            }
        }

        return found;
    }

    private static Light AddLight(Transform parent, string name, Vector3 position, LightType type, Color color,
        float intensity, float range, float spotAngle, Vector3 euler)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(euler);
        Light light = go.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        if (type == LightType.Spot)
        {
            light.spotAngle = spotAngle;
            light.innerSpotAngle = spotAngle * 0.5f;
        }

        light.shadows = LightShadows.None;
        return light;
    }

    private static void AddFlicker(Light light, FlickerLight.FlickerMode mode, Renderer emissive)
    {
        FlickerLight flicker = light.gameObject.AddComponent<FlickerLight>();
        flicker.Setup(mode, emissive);
    }

    private static void Sign(Transform parent, string text, Vector3 position, float yaw, Color color, float characterSize)
    {
        Font font = LoadBuiltinFont();
        if (font == null)
        {
            return;
        }

        GameObject go = new GameObject("Text_" + text);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        TextMesh mesh = go.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.font = font;
        mesh.fontSize = 64;
        mesh.characterSize = characterSize;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = color;
        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = go.AddComponent<MeshRenderer>();
        }

        renderer.sharedMaterial = font.material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    private static Font LoadBuiltinFont()
    {
        try
        {
#if UNITY_2022_2_OR_NEWER
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void MarkStatic(Transform root)
    {
        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            GameObject go = renderer.gameObject;
            if (go.name == "LampHead" || go.name == "DoorLamp" || go.GetComponent<TextMesh>() != null)
            {
                continue; // 깜빡이는 발광체와 글자는 정적 배칭에서 뺀다.
            }

            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }
    }

    // ===================================================================== 재질

    private static Material Mat(string name)
    {
        if (MaterialCache.TryGetValue(name, out Material cached) && cached != null)
        {
            return cached;
        }

        MaterialSpec(name, out Color color, out float smoothness, out float metallic, out Color emission, out string texture);
        EnsureFolder(MaterialFolder);
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = LitShader();
        bool created = material == null;
        if (created)
        {
            material = new Material(shader) { name = name };
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        Texture2D map = string.IsNullOrEmpty(texture) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", map);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", map);
        if (material.HasProperty("_BaseColorMap")) material.SetTexture("_BaseColorMap", map);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);

        if (emission.maxColorComponent > 0f)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
            if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        if (created)
        {
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            EditorUtility.SetDirty(material);
        }

        MaterialCache[name] = material;
        return material;
    }

    private static void MaterialSpec(string name, out Color color, out float smoothness, out float metallic,
        out Color emission, out string texture)
    {
        smoothness = 0.15f;
        metallic = 0f;
        emission = Color.black;
        texture = null;
        switch (name)
        {
            // Kenney 모델 (색 팔레트 텍스처 + 밤 분위기에 맞게 살짝 어둡게)
            case "K_Commercial": color = new Color(0.82f, 0.82f, 0.85f); texture = KitFolder(Kit.Commercial) + "Textures/colormap.png"; break;
            case "K_CommercialA": color = new Color(0.82f, 0.82f, 0.85f); texture = KitFolder(Kit.Commercial) + "Textures/variation-a.png"; break;
            case "K_CommercialB": color = new Color(0.82f, 0.82f, 0.85f); texture = KitFolder(Kit.Commercial) + "Textures/variation-b.png"; break;
            case "K_Roads": color = new Color(0.72f, 0.72f, 0.76f); smoothness = 0.25f; texture = KitFolder(Kit.Roads) + "Textures/colormap.png"; break;
            case "K_Cars": color = new Color(0.85f, 0.85f, 0.85f); smoothness = 0.45f; texture = KitFolder(Kit.Cars) + "Textures/colormap.png"; break;

            case "Asphalt": color = new Color(0.09f, 0.09f, 0.1f); smoothness = 0.3f; break;
            case "Concrete": color = new Color(0.42f, 0.41f, 0.39f); break;
            case "RoofDark": color = new Color(0.12f, 0.12f, 0.13f); break;
            case "Tile": color = new Color(0.4f, 0.4f, 0.38f); smoothness = 0.5f; break;
            case "MetalDark": color = new Color(0.13f, 0.13f, 0.14f); metallic = 0.6f; smoothness = 0.4f; break;
            case "LampWarm": color = new Color(0.9f, 0.8f, 0.6f); emission = new Color(2.5f, 2f, 1.3f); break;
            case "LampRed": color = new Color(0.5f, 0.05f, 0.05f); emission = new Color(2.5f, 0.15f, 0.1f); break;
            case "LampGreen": color = new Color(0.1f, 0.5f, 0.15f); emission = new Color(0.2f, 2.2f, 0.5f); break;
            case "Charred": color = new Color(0.07f, 0.065f, 0.06f); smoothness = 0.05f; break;
            case "LineWhite": color = new Color(0.6f, 0.6f, 0.58f); break;
            case "Blood": color = new Color(0.18f, 0.01f, 0.01f); smoothness = 0.6f; break;
            case "Wood": color = new Color(0.33f, 0.22f, 0.12f); break;
            case "SafeDoor": color = new Color(0.45f, 0.04f, 0.03f); metallic = 0.4f; break;
            case "Cot": color = new Color(0.25f, 0.3f, 0.2f); break;
            default: color = new Color(0.5f, 0.5f, 0.5f); break;
        }
    }

    private static Shader LitShader()
    {
        if (_litShader != null)
        {
            return _litShader;
        }

        RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
        if (pipeline != null)
        {
            string type = pipeline.GetType().Name;
            if (type.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0) _litShader = Shader.Find("Universal Render Pipeline/Lit");
            else if (type.IndexOf("HDRender", StringComparison.OrdinalIgnoreCase) >= 0) _litShader = Shader.Find("HDRP/Lit");
        }

        if (_litShader == null)
        {
            _litShader = Shader.Find("Standard");
        }

        if (_litShader == null)
        {
            throw new InvalidOperationException("Lit 셰이더를 찾지 못했습니다.");
        }

        return _litShader;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        int slash = folder.LastIndexOf('/');
        string parent = folder.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
    }

    // ===================================================================== 기존 맵 처리

    private static Bounds ArenaBounds(Transform map, Vector3 playerPosition)
    {
        Bounds bounds = new Bounds(playerPosition, new Vector3(24f, 2f, 24f));
        bool found = false;
        if (map != null)
        {
            foreach (Collider collider in map.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || collider.bounds.size.y < 0.5f)
                {
                    continue; // 바닥은 제외하고 벽으로 범위를 잡는다.
                }

                if (found) bounds.Encapsulate(collider.bounds);
                else { bounds = collider.bounds; found = true; }
            }

            if (found == false)
            {
                foreach (Renderer renderer in map.GetComponentsInChildren<Renderer>())
                {
                    if (renderer.bounds.size.y < 0.5f) continue;
                    if (found) bounds.Encapsulate(renderer.bounds);
                    else { bounds = renderer.bounds; found = true; }
                }
            }
        }

        return bounds;
    }

    private static float FloorY(List<Transform> all, Bounds arena)
    {
        foreach (Transform t in all)
        {
            if (t == null || t.name != "Ground" || t.GetComponentInParent<ZombieCityInfo>() != null)
            {
                continue;
            }

            Collider collider = t.GetComponent<Collider>();
            if (collider != null) return collider.bounds.max.y;
            Renderer renderer = t.GetComponent<Renderer>();
            if (renderer != null) return renderer.bounds.max.y;
            return t.position.y;
        }

        return arena.min.y;
    }

    // 기존 맵 남쪽 벽 가운데를 잘라 출입구를 만든다. (원래 벽은 끄고, 양쪽 조각을 새로 만든다)
    private static int CutGate(Transform map, Bounds arena, float gateX, ZombieCityInfo info)
    {
        if (map == null)
        {
            return 0;
        }

        float gateMin = gateX - GateWidth * 0.5f;
        float gateMax = gateX + GateWidth * 0.5f;
        int cut = 0;

        foreach (Collider collider in map.GetComponentsInChildren<Collider>())
        {
            if (collider.isTrigger)
            {
                continue;
            }

            Bounds b = collider.bounds;
            if (b.size.y < 0.5f || b.max.z > arena.min.z + 3f || b.max.x <= gateMin || b.min.x >= gateMax)
            {
                continue;
            }

            if (b.size.z > 4f || TrySplitWall(collider.gameObject, b, gateMin, gateMax, info) == false)
            {
                Debug.LogWarning($"[Zombie City] '{collider.name}'을(를) 자동으로 자르지 못했습니다. 출입구 자리(가운데 폭 {GateWidth}m)를 직접 열어 주세요.", collider);
                continue;
            }

            cut++;
        }

        return cut;
    }

    private static bool TrySplitWall(GameObject wall, Bounds b, float gateMin, float gateMax, ZombieCityInfo info)
    {
        Transform t = wall.transform;
        if (t.childCount > 0)
        {
            return false;
        }

        Vector3[] axes = { t.right, t.up, t.forward };
        int axis = -1;
        for (int i = 0; i < 3; i++)
        {
            if (Mathf.Abs(axes[i].x) > 0.99f) axis = i;
        }

        if (axis < 0)
        {
            return false;
        }

        CreateWallPiece(wall, b, b.min.x, gateMin, axis, "_GateLeft", info);
        CreateWallPiece(wall, b, gateMax, b.max.x, axis, "_GateRight", info);

        Undo.RecordObject(wall, "Open gate");
        wall.SetActive(false);
        info.DisabledWalls.Add(wall);
        return true;
    }

    private static void CreateWallPiece(GameObject wall, Bounds b, float from, float to, int axis, string suffix, ZombieCityInfo info)
    {
        if (to - from < 0.05f)
        {
            return;
        }

        GameObject piece = Object.Instantiate(wall, wall.transform.parent);
        piece.name = wall.name + suffix;
        Transform p = piece.transform;
        Vector3 scale = wall.transform.localScale;
        scale[axis] *= (to - from) / b.size.x;
        p.localScale = scale;
        p.rotation = wall.transform.rotation;
        Vector3 pivotOffset = wall.transform.position - b.center;
        p.position = new Vector3((from + to) * 0.5f, b.center.y, b.center.z) + pivotOffset;
        Undo.RegisterCreatedObjectUndo(piece, "Open gate");
        info.WallPieces.Add(piece);
    }

    private static int AddBackgroundColliders(Scene scene)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != BackgroundName)
            {
                continue;
            }

            foreach (Transform child in root.transform)
            {
                if (AddFitCollider(child.gameObject, true))
                {
                    count++;
                }
            }
        }

        return count;
    }

    // ===================================================================== 분위기 (밤 + 안개)

    private static void ApplyAtmosphere(ZombieCityInfo info)
    {
        Color fog = new Color(0.05f, 0.06f, 0.075f);

        if (info != null && info.AtmosphereSaved == false)
        {
            info.AtmosphereSaved = true;
            info.Fog = RenderSettings.fog;
            info.FogMode = RenderSettings.fogMode;
            info.FogDensity = RenderSettings.fogDensity;
            info.FogColor = RenderSettings.fogColor;
            info.AmbientMode = RenderSettings.ambientMode;
            info.AmbientLight = RenderSettings.ambientLight;
            info.Skybox = RenderSettings.skybox;
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.028f;
        RenderSettings.fogColor = fog;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.11f, 0.12f, 0.15f);
        RenderSettings.skybox = null;

        foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional)
            {
                continue;
            }

            if (info != null && info.SunLights.Contains(light) == false)
            {
                info.SunLights.Add(light);
                info.SunIntensities.Add(light.intensity);
                info.SunColors.Add(light.color);
            }

            Undo.RecordObject(light, "Night light");
            light.intensity = 0.15f;
            light.color = new Color(0.55f, 0.65f, 0.9f);
        }

        Camera camera = Camera.main;
        if (camera != null)
        {
            if (info != null && info.ViewCamera == null)
            {
                info.ViewCamera = camera;
                info.ClearFlags = camera.clearFlags;
                info.BackgroundColor = camera.backgroundColor;
            }

            Undo.RecordObject(camera, "Night sky");
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = fog;
        }
    }

    private static void RestoreAtmosphere(ZombieCityInfo info)
    {
        if (info == null || info.AtmosphereSaved == false)
        {
            return;
        }

        RenderSettings.fog = info.Fog;
        RenderSettings.fogMode = info.FogMode;
        RenderSettings.fogDensity = info.FogDensity;
        RenderSettings.fogColor = info.FogColor;
        RenderSettings.ambientMode = info.AmbientMode;
        RenderSettings.ambientLight = info.AmbientLight;
        RenderSettings.skybox = info.Skybox;

        for (int i = 0; i < info.SunLights.Count; i++)
        {
            Light light = info.SunLights[i];
            if (light == null) continue;
            Undo.RecordObject(light, "Restore light");
            light.intensity = info.SunIntensities[i];
            light.color = info.SunColors[i];
        }

        if (info.ViewCamera != null)
        {
            Undo.RecordObject(info.ViewCamera, "Restore camera");
            info.ViewCamera.clearFlags = info.ClearFlags;
            info.ViewCamera.backgroundColor = info.BackgroundColor;
        }
    }

    private static void EnsureFlashlight()
    {
        Camera camera = Camera.main;
        if (camera != null && camera.GetComponent<PlayerFlashlight>() == null)
        {
            Undo.AddComponent<PlayerFlashlight>(camera.gameObject);
        }
    }

    // ===================================================================== 공통

    private static void RemoveCity(GameObject root)
    {
        ZombieCityInfo info = root.GetComponent<ZombieCityInfo>();
        if (info != null)
        {
            foreach (GameObject piece in info.WallPieces)
            {
                if (piece != null) Undo.DestroyObjectImmediate(piece);
            }

            foreach (GameObject wall in info.DisabledWalls)
            {
                if (wall == null) continue;
                Undo.RecordObject(wall, "Restore wall");
                wall.SetActive(true);
            }

            RestoreAtmosphere(info);
        }

        Undo.DestroyObjectImmediate(root);
    }

    private static GameObject FindRoot(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == RootName && root.GetComponent<ZombieCityInfo>() != null)
            {
                return root;
            }
        }

        return null;
    }

    private static List<Transform> SceneTransforms(Scene scene)
    {
        List<Transform> all = new List<Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            all.AddRange(root.GetComponentsInChildren<Transform>(true));
        }

        return all;
    }

    private static bool CanEdit()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Debug.LogWarning("[Zombie City] Play 모드를 끄고 컴파일이 끝난 뒤 실행하세요.");
            return false;
        }

        return true;
    }

    private static float Range(float min, float max)
    {
        return min + (float)_rng.NextDouble() * (max - min);
    }

    private static string Pick(params string[] options)
    {
        return options[_rng.Next(options.Length)];
    }
}
#endif
