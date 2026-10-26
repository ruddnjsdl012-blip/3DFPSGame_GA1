#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// 레프트 4 데드 느낌의 도시 맵을 지금 맵(Map)의 남쪽에 이어 붙이는 에디터 메뉴.
//   Tools > Zombie City > Build City (extend current map)
// 출발(기존 맵) → 골목 → 큰길(버려진 차, 버스, 가로등) → 바리케이드로 막힌 길을 상점 안으로 우회
// → 뒷마당 → 마지막 거리 → 도착 안전가옥(들어가면 탈출 성공).
// 모든 건물/벽/차에는 Collider가 있어서 좀비와 플레이어가 통과하지 못한다.
// 만든 뒤에는 씬에서 자유롭게 옮기거나 지울 수 있고, Remove City 메뉴로 통째로 되돌릴 수 있다.
public static class ZombieCityBuilder
{
    private const string RootName = "ZombieCity";
    private const string MaterialFolder = "Assets/ZombieCity/Materials";
    private const string ModelFolder = "Assets/FreeModels_ImportFixed_20261026/Prefabs/";
    private const string BackgroundName = "FreeModels_Background";
    private const float GateWidth = 4.5f;
    // 2단 점프로 건물 위나 바리케이드를 넘어가지 못하도록, 벽의 Collider는 보이는 것보다 높게 세운다.
    private const float WallColliderHeight = 40f;

    [Flags]
    private enum Face
    {
        None = 0,
        PosX = 1,
        NegX = 2,
        PosZ = 4,
        NegZ = 8,
    }

    private static System.Random _rng;
    private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();
    private static Shader _litShader;

    // ===================================================================== 메뉴

    [MenuItem("Tools/Zombie City/Build City (extend current map)", false, 1)]
    public static void BuildMenu()
    {
        if (CanEdit() == false)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject old = FindRoot(scene);
        if (old != null && EditorUtility.DisplayDialog("Zombie City",
                "이미 만든 도시가 있습니다. 지우고 다시 만들까요?", "다시 만들기", "취소") == false)
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
                      "남쪽 출입구 → 골목 → 큰길 → 상점 → 안전가옥 순서로 이동합니다. (F키: 손전등)");
        }
        catch
        {
            Undo.RevertAllDownToGroup(group);
            throw;
        }
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
        Debug.Log($"[Zombie City] 배경 모델 {count}개에 Collider를 붙였습니다. 이제 좀비와 플레이어가 통과하지 못합니다.");
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
        _litShader = null;

        // 도시의 기준점: 기존 맵 남쪽 끝 가운데. 도시 안에서 +Z가 남쪽(월드 -Z)으로 뻗어 나간다.
        GameObject rootObject = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(rootObject, scene);
        Transform root = rootObject.transform;
        root.SetPositionAndRotation(new Vector3(arena.center.x, floorY, arena.min.z), Quaternion.Euler(0f, 180f, 0f));
        ZombieCityInfo info = rootObject.AddComponent<ZombieCityInfo>();

        BuildGround(root);
        BuildAlley(root);
        BuildMainStreet(root);
        BuildStore(root);
        BuildBackLot(root);
        BuildBarricade(root);
        BuildFinalStreet(root);
        BuildSafeRoom(root);
        MarkStatic(root);

        Undo.RegisterCreatedObjectUndo(rootObject, "Build Zombie City");

        // 기존 맵 남쪽 벽에 출입구를 낸다.
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

    // ===================================================================== 구역별 생성 (도시 기준 좌표, 단위 m)
    // x: 좌우(길 가운데 0), z: 기존 맵에서 멀어지는 방향, y: 바닥 0

    private static void BuildGround(Transform root)
    {
        Transform g = Group("Ground", root, Vector3.zero, 0f);
        Box("Asphalt", g, new Vector3(0f, -0.24f, 72f), new Vector3(52f, 0.5f, 146f), "Asphalt", true);
        Box("AlleyConcrete", g, new Vector3(0f, 0.012f, 17f), new Vector3(7f, 0.01f, 34f), "ConcreteDark", false);
        Box("Sidewalk_L", g, new Vector3(-8.5f, 0.075f, 81f), new Vector3(3f, 0.15f, 94f), "Sidewalk", true);
        Box("Sidewalk_R", g, new Vector3(8.5f, 0.075f, 81f), new Vector3(3f, 0.15f, 94f), "Sidewalk", true);

        for (float z = 41f; z < 126f; z += 6f)
        {
            if (z > 84f && z < 92f) continue; // 바리케이드 자리
            Box("LaneMark", g, new Vector3(0f, 0.02f, z), new Vector3(0.18f, 0.012f, 2.6f), "LineYellow", false);
        }

        for (float x = -6f; x <= 6.01f; x += 1.2f)
        {
            Box("Crosswalk", g, new Vector3(x, 0.02f, 37f), new Vector3(0.6f, 0.012f, 3f), "LineWhite", false);
        }

        Cylinder("Manhole", g, new Vector3(-2.5f, 0.02f, 52f), 0.9f, 0.01f, "MetalDark", false, Vector3.zero);
        Cylinder("Manhole", g, new Vector3(3f, 0.02f, 110f), 0.9f, 0.01f, "MetalDark", false, Vector3.zero);

        // 핏자국
        for (int i = 0; i < 26; i++)
        {
            bool alley = i < 6;
            float x = alley ? Range(-2.8f, 2.8f) : Range(-6.5f, 6.5f);
            float z = alley ? Range(2f, 32f) : Range(36f, 126f);
            float size = Range(0.6f, 2.2f);
            GameObject blood = Cylinder("Blood", g, new Vector3(x, 0.024f, z), size, 0.008f, "Blood", false, Vector3.zero);
            blood.transform.localScale = new Vector3(size, 0.004f, size * Range(0.5f, 1.4f));
            blood.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
        }
    }

    private static void BuildAlley(Transform root)
    {
        Transform a = Group("Alley", root, Vector3.zero, 0f);
        Building(a, "AlleyBuilding_L1", -16f, -3.5f, 0f, 16f, 10f, "Brick", Face.PosX, Face.None);
        Building(a, "AlleyBuilding_L2", -16f, -3.5f, 16f, 34f, 14f, "PlasterDark", Face.PosX | Face.PosZ, Face.None);
        Building(a, "AlleyBuilding_R1", 3.5f, 16f, 0f, 20f, 12f, "BrickDark", Face.NegX, Face.None);
        Building(a, "AlleyBuilding_R2", 3.5f, 16f, 20f, 34f, 9f, "Plaster", Face.NegX | Face.PosZ, Face.None);

        Dumpster(a, new Vector3(2.85f, 0f, 8f), 0f);
        Dumpster(a, new Vector3(-2.85f, 0f, 21f), 180f);
        TrashBags(a, new Vector3(2.7f, 0f, 10.4f));
        TrashBags(a, new Vector3(-2.7f, 0f, 18.6f));
        TrashBags(a, new Vector3(-2.6f, 0f, 4f));

        Prefab("crate", a, new Vector3(-2.7f, 0f, 12f), 15f);
        Prefab("crate", a, new Vector3(-2.6f, 0f, 13.3f), -10f);
        Prefab("barrel", a, new Vector3(2.8f, 0f, 15.5f), 0f);
        Prefab("planks", a, new Vector3(-2.5f, 0f, 29.5f), 80f);

        // 골목을 반쯤 막은 철망 (오른쪽으로 지나갈 수 있음)
        FenceAlongX(a, -3.5f, 0.4f, 27f);

        WallLamp(a, new Vector3(3.45f, 4.2f, 6f), new Vector3(-1f, 0f, 0f), FlickerLight.FlickerMode.Broken);
        WallLamp(a, new Vector3(-3.45f, 4.2f, 19f), new Vector3(1f, 0f, 0f), FlickerLight.FlickerMode.Subtle);
        WallLamp(a, new Vector3(3.45f, 4.2f, 30f), new Vector3(-1f, 0f, 0f), FlickerLight.FlickerMode.Broken);
    }

    private static void BuildMainStreet(Transform root)
    {
        Transform s = Group("MainStreet", root, Vector3.zero, 0f);

        // 왼쪽 건물들 (z 34 ~ 128, 길 쪽 면은 x = -10)
        float[] leftZ = { 34f, 50f, 62f, 80f, 96f, 112f, 128f };
        float[] leftH = { 12f, 18f, 10f, 22f, 14f, 9f };
        string[] leftMat = { "Brick", "PlasterDark", "BrickDark", "Plaster", "Concrete", "Brick" };
        for (int i = 0; i < leftH.Length; i++)
        {
            Building(s, $"Building_L{i + 1}", -24f, -10f, leftZ[i], leftZ[i + 1], leftH[i], leftMat[i], Face.PosX, Face.PosX);
        }

        // 오른쪽 건물들 (상점 z 72~96, 뒷마당 z 96~104는 따로 만든다)
        Building(s, "Building_R1", 10f, 24f, 34f, 52f, 15f, "Concrete", Face.NegX, Face.NegX);
        Building(s, "Building_R2", 10f, 24f, 52f, 72f, 11f, "BrickDark", Face.NegX, Face.NegX);
        Building(s, "Building_R3", 10f, 24f, 104f, 116f, 16f, "Plaster", Face.NegX | Face.NegZ, Face.NegX);
        Building(s, "Building_R4", 10f, 24f, 116f, 128f, 12f, "Brick", Face.NegX, Face.NegX);

        // 버려진 차들 (가운데에 항상 지나갈 길이 남도록 배치)
        Car(s, new Vector3(-4f, 0f, 42f), 10f, "CarRed", false);
        Car(s, new Vector3(4.2f, 0f, 47f), 175f, "CarGray", false);
        Bus(s, new Vector3(-3.6f, 0f, 58f), 18f);
        Car(s, new Vector3(4.6f, 0f, 64f), -15f, "Charred", true);
        Car(s, new Vector3(0.5f, 0f, 74f), 95f, "CarBlue", false);
        Car(s, new Vector3(-5f, 0f, 80f), 182f, "CarWhite", false);
        PoliceCar(s, new Vector3(4.8f, 0f, 81.5f), 28f);

        Hydrant(s, new Vector3(-7.4f, 0f, 47f));
        Hydrant(s, new Vector3(7.4f, 0f, 60f));
        TrashBags(s, new Vector3(-9.2f, 0.15f, 55f));
        TrashBags(s, new Vector3(9.2f, 0.15f, 44f));
        Prefab("barrel", s, new Vector3(-8.8f, 0.15f, 70f), 0f);
        Prefab("crate", s, new Vector3(9f, 0.15f, 57f), 30f);

        StreetLamp(s, -7.6f, 42f, 1f, LampState.On);
        StreetLamp(s, 7.6f, 51f, -1f, LampState.Flicker);
        StreetLamp(s, -7.6f, 61f, 1f, LampState.Dead);
        StreetLamp(s, 7.6f, 69f, -1f, LampState.On);
        StreetLamp(s, -7.6f, 79f, 1f, LampState.Flicker);
    }

    // 큰길이 막혀 있어서 통과해야 하는 상점 (x 10~24, z 72~96)
    private static void BuildStore(Transform root)
    {
        Transform st = Group("Store", root, Vector3.zero, 0f);
        const float h = 5f;
        const float t = 0.3f;

        Box("Floor", st, new Vector3(17f, 0.02f, 84f), new Vector3(13.4f, 0.02f, 23.4f), "Tile", false);
        WallAlongZ(st, "Wall_Front", 10.15f, 72f, 96f, h, t, "Plaster", new[] { new Vector2(76f, 79.5f) }, 2.6f);
        WallAlongZ(st, "Wall_Back", 23.85f, 72f, 96f, h, t, "Plaster", null, 0f);
        WallAlongX(st, "Wall_North", 72.15f, 10f, 24f, h, t, "Plaster", null, 0f);
        WallAlongX(st, "Wall_South", 95.85f, 10f, 24f, h, t, "Plaster", new[] { new Vector2(18f, 21f) }, 2.4f);
        Box("Roof", st, new Vector3(17f, h + 0.15f, 84f), new Vector3(14f, 0.3f, 24f), "RoofDark", true);
        Box("Parapet", st, new Vector3(17f, h + 0.45f, 84f), new Vector3(14.3f, 0.3f, 24.3f), "RoofDark", false);

        // 길 쪽 외관: 진열창, 간판
        Box("DisplayWindow", st, new Vector3(9.96f, 1.5f, 87.5f), new Vector3(0.08f, 2.2f, 14f), "GlassDark", false);
        Box("DisplayWindow", st, new Vector3(9.96f, 1.5f, 74f), new Vector3(0.08f, 2.2f, 3f), "GlassDark", false);
        GameObject sign = Box("StoreSign", st, new Vector3(9.9f, 3.7f, 77.75f), new Vector3(0.15f, 0.8f, 5.5f), "SignRed", false);
        Sign(st, "MART", new Vector3(9.8f, 3.7f, 77.75f), 90f, new Color(1f, 0.9f, 0.85f), 0.09f);
        Light signLight = AddLight(st, "SignLight", new Vector3(9.2f, 3.4f, 77.75f), LightType.Point, new Color(1f, 0.25f, 0.2f), 1.2f, 6f, 0f, Vector3.zero);
        AddFlicker(signLight, FlickerLight.FlickerMode.Broken, sign.GetComponent<Renderer>());

        // 안: 진열대, 계산대, 쓰러진 물건
        foreach (float x in new[] { 14f, 17f, 20f })
        {
            Box("Shelf", st, new Vector3(x, 0.95f, 82.5f), new Vector3(0.7f, 1.9f, 15f), "Metal", true);
            for (int i = 0; i < 6; i++)
            {
                Box("Goods", st, new Vector3(x + Range(-0.2f, 0.2f), 1.95f, Range(75.5f, 89.5f)),
                    new Vector3(0.4f, Range(0.15f, 0.4f), Range(0.4f, 1.2f)), Pick("AwningRed", "AwningBlue", "SignDead", "Wood"), false);
            }
        }

        Box("Counter", st, new Vector3(11.9f, 0.5f, 83f), new Vector3(0.8f, 1f, 4f), "Wood", true);
        Box("Register", st, new Vector3(11.9f, 1.15f, 82.2f), new Vector3(0.4f, 0.3f, 0.5f), "MetalDark", false);
        Box("FallenShelf", st, new Vector3(22.3f, 0.35f, 79f), new Vector3(1.9f, 0.7f, 0.7f), "Metal", true, 12f);
        TrashBags(st, new Vector3(22.5f, 0f, 92f));
        Prefab("crate", st, new Vector3(15.5f, 0f, 93.5f), 20f);

        // 형광등 (하나는 고장)
        StoreLight(st, new Vector3(13f, h - 0.1f, 78f), FlickerLight.FlickerMode.Broken);
        StoreLight(st, new Vector3(18.5f, h - 0.1f, 84f), FlickerLight.FlickerMode.Subtle);
        StoreLight(st, new Vector3(18.5f, h - 0.1f, 92f), FlickerLight.FlickerMode.Broken);
    }

    // 상점 뒷문으로 나오면 바리케이드 너머 큰길로 이어지는 뒷마당 (x 10~24, z 96~104)
    private static void BuildBackLot(Transform root)
    {
        Transform lot = Group("BackLot", root, Vector3.zero, 0f);
        GameObject wall = Box("Wall_East", lot, new Vector3(24.25f, 1.5f, 100f), new Vector3(0.5f, 3f, 8f), "BrickDark", true);
        ExtendCollider(wall);

        Dumpster(lot, new Vector3(23f, 0f, 101.5f), 90f);
        TrashBags(lot, new Vector3(22.8f, 0f, 98f));
        Prefab("crate", lot, new Vector3(12f, 0f, 102.6f), 5f);
        GameObject barrel = Prefab("barrel", lot, new Vector3(15f, 0f, 101.5f), 0f);
        if (barrel != null)
        {
            Fire(lot, new Vector3(15f, 1.4f, 101.5f), 2f);
        }

        WallLamp(lot, new Vector3(16f, 3.8f, 96.05f), new Vector3(0f, 0f, 1f), FlickerLight.FlickerMode.Broken);
    }

    // 큰길을 완전히 막는 바리케이드 (z 88) → 상점으로 돌아가야 한다.
    private static void BuildBarricade(Transform root)
    {
        Transform b = Group("Barricade", root, Vector3.zero, 0f);

        // 보이지 않는 높은 벽: 차 위로 점프해서 넘어가지 못하게 한다.
        GameObject blocker = new GameObject("InvisibleBlocker");
        blocker.transform.SetParent(b, false);
        BoxCollider block = blocker.AddComponent<BoxCollider>();
        block.center = new Vector3(0f, WallColliderHeight * 0.5f, 88f);
        block.size = new Vector3(20.6f, WallColliderHeight, 1.2f);

        Truck(b, new Vector3(-4.6f, 0f, 88f), 90f);
        for (int i = 0; i < 3; i++)
        {
            float x = 2.2f + i * 2.9f;
            JerseyBarrier(b, new Vector3(x, 0f, 87.7f), 90f);
            Box("Sandbags", b, new Vector3(x, 1.08f, 87.7f), new Vector3(2.8f, 0.45f, 0.75f), "Sandbag", false);
            Box("Sandbags", b, new Vector3(x + 0.2f, 1.45f, 87.7f), new Vector3(2.2f, 0.35f, 0.65f), "Sandbag", false);
        }

        // 철조망
        for (float x = 0.8f; x < 9.8f; x += 1.2f)
        {
            Cylinder("BarbedWire", b, new Vector3(x, 1.9f, 87.7f), 0.35f, 1.2f, "Fence", false, new Vector3(0f, 0f, 90f));
        }

        Car(b, new Vector3(6f, 0f, 91f), 35f, "CarBlack", false);
        Prefab("container_a", b, new Vector3(-4.5f, 0f, 92.6f), 90f);
        Prefab("planks", b, new Vector3(8.6f, 0.15f, 86.4f), 10f);
        GameObject barrel = Prefab("barrel", b, new Vector3(1.2f, 0f, 85.9f), 0f);
        if (barrel != null)
        {
            Fire(b, new Vector3(1.2f, 1.4f, 85.9f), 2.2f);
        }
    }

    private static void BuildFinalStreet(Transform root)
    {
        Transform s = Group("FinalStreet", root, Vector3.zero, 0f);

        Car(s, new Vector3(3.5f, 0f, 100f), -10f, "CarGray", false);
        Car(s, new Vector3(-4.5f, 0f, 108f), 160f, "CarRed", false);
        Van(s, new Vector3(-4.6f, 0f, 117f), 172f);
        Car(s, new Vector3(1.4f, 0f, 118.5f), 62f, "CarBlue", false);
        JerseyBarrier(s, new Vector3(5.3f, 0f, 123f), 70f);
        JerseyBarrier(s, new Vector3(-1.8f, 0f, 124.5f), 100f);
        TrashBags(s, new Vector3(9.1f, 0.15f, 110f));
        Prefab("crate", s, new Vector3(-9f, 0.15f, 101f), 10f);
        Prefab("barrel", s, new Vector3(-8.9f, 0.15f, 123f), 0f);
        Hydrant(s, new Vector3(7.4f, 0f, 115f));

        StreetLamp(s, 7.6f, 98f, -1f, LampState.Flicker);
        StreetLamp(s, -7.6f, 104f, 1f, LampState.On);
        StreetLamp(s, 7.6f, 112f, -1f, LampState.Dead);
        StreetLamp(s, -7.6f, 121f, 1f, LampState.Flicker);
    }

    // 도착 안전가옥 (x -6~6, z 128~144) + 양옆 건물로 길 끝을 막는다.
    private static void BuildSafeRoom(Transform root)
    {
        Transform sr = Group("SafeRoom", root, Vector3.zero, 0f);
        Building(sr, "Building_End_L", -24f, -6f, 128f, 144f, 11f, "BrickDark", Face.NegZ, Face.NegZ);
        Building(sr, "Building_End_R", 6f, 24f, 128f, 144f, 13f, "Plaster", Face.NegZ, Face.NegZ);

        const float h = 3.6f;
        const float t = 0.3f;
        WallAlongX(sr, "SafeRoom_Front", 128.15f, -6f, 6f, h, t, "Concrete", new[] { new Vector2(-1.2f, 1.2f) }, 2.4f);
        WallAlongX(sr, "SafeRoom_Back", 143.85f, -6f, 6f, h, t, "Concrete", null, 0f);
        WallAlongZ(sr, "SafeRoom_Left", -5.85f, 128f, 144f, h, t, "Concrete", null, 0f);
        WallAlongZ(sr, "SafeRoom_Right", 5.85f, 128f, 144f, h, t, "Concrete", null, 0f);
        Box("Roof", sr, new Vector3(0f, h + 0.15f, 136f), new Vector3(12f, 0.3f, 16f), "RoofDark", true);
        Box("Floor", sr, new Vector3(0f, 0.02f, 136f), new Vector3(11.4f, 0.02f, 15.4f), "Tile", false);

        // 빨간 철문 (열린 채로) + 문 위 빨간 등 + 간판
        Box("SafeDoor", sr, new Vector3(1.25f, 1.2f, 126.95f), new Vector3(0.08f, 2.35f, 2.3f), "SafeDoor", true);
        GameObject lamp = Box("DoorLamp", sr, new Vector3(0f, 2.75f, 127.95f), new Vector3(0.6f, 0.22f, 0.15f), "LampRed", false);
        Light red = AddLight(sr, "DoorLight", new Vector3(0f, 2.7f, 127.3f), LightType.Point, new Color(1f, 0.15f, 0.1f), 1.6f, 6f, 0f, Vector3.zero);
        AddFlicker(red, FlickerLight.FlickerMode.Subtle, lamp.GetComponent<Renderer>());
        Box("SignPlate", sr, new Vector3(0f, 3.25f, 127.93f), new Vector3(3.2f, 0.55f, 0.06f), "MetalDark", false);
        Sign(sr, "SAFE ROOM", new Vector3(0f, 3.25f, 127.88f), 0f, new Color(1f, 0.2f, 0.15f), 0.07f);

        // 안: 초록 조명, 간이침대, 탄약 상자
        AddLight(sr, "RoomLight", new Vector3(0f, h - 0.3f, 136f), LightType.Point, new Color(0.55f, 1f, 0.6f), 1.8f, 11f, 0f, Vector3.zero);
        Box("RoomLamp", sr, new Vector3(0f, h - 0.1f, 136f), new Vector3(0.3f, 0.1f, 1.4f), "LampGreen", false);
        Box("Cot", sr, new Vector3(-4.6f, 0.35f, 133f), new Vector3(0.9f, 0.3f, 2.1f), "Cot", true);
        Box("Cot", sr, new Vector3(-4.6f, 0.35f, 138f), new Vector3(0.9f, 0.3f, 2.1f), "Cot", true);
        Box("Table", sr, new Vector3(3.8f, 0.45f, 140f), new Vector3(2f, 0.9f, 1f), "Wood", true);
        Prefab("crate", sr, new Vector3(4.4f, 0f, 133f), 10f);
        Prefab("crate", sr, new Vector3(4.6f, 0f, 134.3f), -5f);

        // 들어오면 탈출 성공
        GameObject goal = new GameObject("SafeRoomGoal");
        goal.transform.SetParent(sr, false);
        goal.transform.localPosition = new Vector3(0f, 1.5f, 136.5f);
        BoxCollider area = goal.AddComponent<BoxCollider>();
        area.isTrigger = true;
        area.size = new Vector3(10.6f, 3f, 13.6f);
        goal.AddComponent<SafeRoomGoal>();
    }

    // ===================================================================== 큰 구조물

    private static void Building(Transform parent, string name, float x0, float x1, float z0, float z1,
        float height, string wallMaterial, Face faces, Face storefront)
    {
        Transform g = Group(name, parent, Vector3.zero, 0f);
        GameObject body = Box("Body", g, new Vector3((x0 + x1) * 0.5f, height * 0.5f, (z0 + z1) * 0.5f),
            new Vector3(x1 - x0, height, z1 - z0), wallMaterial, true);
        ExtendCollider(body);
        Box("Parapet", g, new Vector3((x0 + x1) * 0.5f, height + 0.25f, (z0 + z1) * 0.5f),
            new Vector3(x1 - x0 + 0.3f, 0.5f, z1 - z0 + 0.3f), "RoofDark", false);

        foreach (Face face in new[] { Face.PosX, Face.NegX, Face.PosZ, Face.NegZ })
        {
            if ((faces & face) == 0)
            {
                continue;
            }

            FaceFrame(face, x0, x1, z0, z1, out Vector3 origin, out Vector3 along, out float length, out Vector3 normal);
            bool shop = (storefront & face) != 0;
            Windows(g, origin, along, length, normal, height, shop ? 4.4f : 1.6f);
            if (shop)
            {
                Storefront(g, origin, along, length, normal);
            }

            FacadeDetails(g, origin, along, length, normal, height);
        }
    }

    private static void FaceFrame(Face face, float x0, float x1, float z0, float z1,
        out Vector3 origin, out Vector3 along, out float length, out Vector3 normal)
    {
        switch (face)
        {
            case Face.PosX:
                origin = new Vector3(x1, 0f, z0); along = Vector3.forward; length = z1 - z0; normal = Vector3.right;
                break;
            case Face.NegX:
                origin = new Vector3(x0, 0f, z0); along = Vector3.forward; length = z1 - z0; normal = Vector3.left;
                break;
            case Face.PosZ:
                origin = new Vector3(x0, 0f, z1); along = Vector3.right; length = x1 - x0; normal = Vector3.forward;
                break;
            default:
                origin = new Vector3(x0, 0f, z0); along = Vector3.right; length = x1 - x0; normal = Vector3.back;
                break;
        }
    }

    // 벽면에 붙는 판 모양 크기 (가로 w, 세로 h, 두께 d)
    private static Vector3 FaceSize(Vector3 along, Vector3 normal, float w, float h, float d)
    {
        return new Vector3(Mathf.Abs(along.x) * w + Mathf.Abs(normal.x) * d, h, Mathf.Abs(along.z) * w + Mathf.Abs(normal.z) * d);
    }

    private static void Windows(Transform g, Vector3 origin, Vector3 along, float length, Vector3 normal, float height, float firstY)
    {
        int columns = Mathf.Max(1, Mathf.FloorToInt((length - 1.5f) / 3f));
        float spacing = length / columns;
        for (float y = firstY; y + 1.6f < height - 0.4f; y += 3.3f)
        {
            for (int i = 0; i < columns; i++)
            {
                Vector3 position = origin + along * (spacing * (i + 0.5f)) + normal * 0.04f + Vector3.up * (y + 0.8f);
                double roll = _rng.NextDouble();
                string material = roll < 0.12 ? "WindowLit" : roll < 0.22 ? "Boarded" : "WindowDark";
                Box("Window", g, position, FaceSize(along, normal, 1.2f, 1.6f, 0.08f), material, false);
            }
        }
    }

    private static void Storefront(Transform g, Vector3 origin, Vector3 along, float length, Vector3 normal)
    {
        Vector3 middle = origin + along * (length * 0.5f);
        Box("ShopGlass", g, middle + normal * 0.05f + Vector3.up * 1.45f,
            FaceSize(along, normal, length - 3f, 2.3f, 0.08f), Pick("GlassDark", "GlassDark", "Boarded"), false);

        float doorAt = length * Range(0.2f, 0.8f);
        Box("Door", g, origin + along * doorAt + normal * 0.08f + Vector3.up * 1.15f,
            FaceSize(along, normal, 1.2f, 2.3f, 0.12f), "DoorDark", false);

        if (_rng.NextDouble() < 0.6)
        {
            Box("Awning", g, middle + normal * 0.8f + Vector3.up * 3.0f,
                FaceSize(along, normal, length - 2f, 0.12f, 1.6f), Pick("AwningRed", "AwningGreen", "AwningBlue"), false);
        }

        if (_rng.NextDouble() < 0.7)
        {
            Box("ShopSign", g, middle + normal * 0.1f + Vector3.up * 3.7f,
                FaceSize(along, normal, Mathf.Min(6f, length - 3f), 0.7f, 0.12f), Pick("SignRed", "SignCyan", "SignYellow", "SignDead", "SignDead"), false);
        }
    }

    private static void FacadeDetails(Transform g, Vector3 origin, Vector3 along, float length, Vector3 normal, float height)
    {
        // 배수관
        Box("DrainPipe", g, origin + along * (length * Range(0.05f, 0.95f)) + normal * 0.12f + Vector3.up * (height * 0.5f),
            FaceSize(along, normal, 0.16f, height, 0.16f), "MetalDark", false);

        // 에어컨 실외기
        if (height > 7f)
        {
            int count = 1 + _rng.Next(2);
            for (int i = 0; i < count; i++)
            {
                Box("ACUnit", g, origin + along * (length * Range(0.1f, 0.9f)) + normal * 0.35f + Vector3.up * Range(5f, height - 2f),
                    FaceSize(along, normal, 0.9f, 0.6f, 0.6f), "Metal", false);
            }
        }
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

    // 보이는 상자는 그대로 두고, Collider만 WallColliderHeight 높이까지 늘린다.
    private static void ExtendCollider(GameObject box)
    {
        BoxCollider collider = box.GetComponent<BoxCollider>();
        if (collider == null)
        {
            return;
        }

        float height = Mathf.Max(0.01f, box.transform.localScale.y);
        float bottom = box.transform.localPosition.y - height * 0.5f;
        float total = Mathf.Max(1f, (WallColliderHeight - bottom) / height);
        collider.size = new Vector3(1f, total, 1f);
        collider.center = new Vector3(0f, -0.5f + total * 0.5f, 0f);
    }

    // ===================================================================== 소품

    private static void Car(Transform parent, Vector3 position, float yaw, string paint, bool burning)
    {
        Transform c = Group(burning ? "BurningCar" : "Car", parent, position, yaw);
        BoxCollider collider = c.gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0.8f, 0f);
        collider.size = new Vector3(1.9f, 1.6f, 4.4f);

        Box("Body", c, new Vector3(0f, 0.62f, 0f), new Vector3(1.85f, 0.7f, 4.4f), paint, false);
        Box("Cabin", c, new Vector3(0f, 1.22f, -0.25f), new Vector3(1.6f, 0.55f, 2.2f), burning ? "Charred" : "GlassDark", false);
        Box("Roof", c, new Vector3(0f, 1.5f, -0.25f), new Vector3(1.5f, 0.06f, 1.9f), paint, false);
        foreach (float x in new[] { -0.6f, 0.6f })
        {
            Box("Headlight", c, new Vector3(x, 0.68f, 2.21f), new Vector3(0.35f, 0.14f, 0.04f), burning ? "Charred" : "LightDead", false);
            Box("Taillight", c, new Vector3(x, 0.72f, -2.21f), new Vector3(0.3f, 0.12f, 0.04f), burning ? "Charred" : "AwningRed", false);
        }

        Wheels(c, 0.85f, 1.35f, 0.34f);

        if (burning)
        {
            Fire(c, new Vector3(0f, 1.6f, 0.6f), 2.6f);
        }
    }

    private static void PoliceCar(Transform parent, Vector3 position, float yaw)
    {
        Car(parent, position, yaw, "CarBlack", false);
        Transform car = parent.GetChild(parent.childCount - 1);
        Box("Door_L", car, new Vector3(-0.93f, 0.7f, -0.2f), new Vector3(0.02f, 0.55f, 1.8f), "CarWhite", false);
        Box("Door_R", car, new Vector3(0.93f, 0.7f, -0.2f), new Vector3(0.02f, 0.55f, 1.8f), "CarWhite", false);
        Box("LightBar_Red", car, new Vector3(-0.35f, 1.6f, -0.2f), new Vector3(0.6f, 0.12f, 0.25f), "LampRed", false);
        GameObject blue = Box("LightBar_Blue", car, new Vector3(0.35f, 1.6f, -0.2f), new Vector3(0.6f, 0.12f, 0.25f), "LampBlue", false);
        Light light = AddLight(car, "Siren", new Vector3(0.35f, 1.9f, -0.2f), LightType.Point, new Color(0.25f, 0.4f, 1f), 1.5f, 8f, 0f, Vector3.zero);
        AddFlicker(light, FlickerLight.FlickerMode.Broken, blue.GetComponent<Renderer>());
    }

    private static void Bus(Transform parent, Vector3 position, float yaw)
    {
        Transform b = Group("Bus", parent, position, yaw);
        BoxCollider collider = b.gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 1.6f, 0f);
        collider.size = new Vector3(2.6f, 3.2f, 11f);

        Box("Body", b, new Vector3(0f, 1.65f, 0f), new Vector3(2.55f, 2.7f, 11f), "BusYellow", false);
        Box("Windows_L", b, new Vector3(-1.29f, 2.1f, 0.3f), new Vector3(0.04f, 0.9f, 9f), "GlassDark", false);
        Box("Windows_R", b, new Vector3(1.29f, 2.1f, 0.3f), new Vector3(0.04f, 0.9f, 9f), "GlassDark", false);
        Box("Windshield", b, new Vector3(0f, 2.1f, 5.51f), new Vector3(2.2f, 1.1f, 0.04f), "GlassDark", false);
        Box("Bumper", b, new Vector3(0f, 0.45f, 5.55f), new Vector3(2.5f, 0.25f, 0.12f), "MetalDark", false);
        Wheels(b, 1.15f, 3.7f, 0.5f);
    }

    private static void Truck(Transform parent, Vector3 position, float yaw)
    {
        Transform t = Group("Truck", parent, position, yaw);
        BoxCollider cargo = t.gameObject.AddComponent<BoxCollider>();
        cargo.center = new Vector3(0f, 1.75f, -1.2f);
        cargo.size = new Vector3(2.5f, 3.5f, 7f);
        BoxCollider cab = t.gameObject.AddComponent<BoxCollider>();
        cab.center = new Vector3(0f, 1.3f, 3.35f);
        cab.size = new Vector3(2.4f, 2.6f, 2.3f);

        Box("Cargo", t, new Vector3(0f, 1.95f, -1.2f), new Vector3(2.5f, 3f, 7f), "CarWhite", false);
        Box("Chassis", t, new Vector3(0f, 0.55f, 0.4f), new Vector3(2.2f, 0.4f, 9.8f), "MetalDark", false);
        Box("Cab", t, new Vector3(0f, 1.45f, 3.35f), new Vector3(2.4f, 2.2f, 2.2f), "CarRed", false);
        Box("Windshield", t, new Vector3(0f, 1.95f, 4.46f), new Vector3(2.1f, 0.9f, 0.04f), "GlassDark", false);
        Wheels(t, 1.05f, 3.3f, 0.48f);
        Wheels(t, 1.05f, -3.6f, 0.48f);
    }

    private static void Van(Transform parent, Vector3 position, float yaw)
    {
        Transform v = Group("Van", parent, position, yaw);
        BoxCollider collider = v.gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 1.2f, 0f);
        collider.size = new Vector3(2.1f, 2.4f, 5.2f);

        Box("Body", v, new Vector3(0f, 1.3f, 0f), new Vector3(2.05f, 2.1f, 5.2f), "CarWhite", false);
        Box("Stripe_L", v, new Vector3(-1.04f, 1.2f, 0f), new Vector3(0.02f, 0.3f, 5f), "AwningRed", false);
        Box("Stripe_R", v, new Vector3(1.04f, 1.2f, 0f), new Vector3(0.02f, 0.3f, 5f), "AwningRed", false);
        Box("Windshield", v, new Vector3(0f, 1.8f, 2.61f), new Vector3(1.8f, 0.7f, 0.04f), "GlassDark", false);
        Wheels(v, 0.95f, 1.8f, 0.38f);
    }

    private static void Wheels(Transform car, float halfWidth, float halfLength, float radius)
    {
        foreach (float x in new[] { -halfWidth, halfWidth })
        {
            foreach (float z in new[] { -halfLength, halfLength })
            {
                Cylinder("Wheel", car, new Vector3(x, radius, z), radius * 2f, 0.26f, "Tire", false, new Vector3(0f, 0f, 90f));
            }
        }
    }

    private static void JerseyBarrier(Transform parent, Vector3 position, float yaw)
    {
        Transform j = Group("JerseyBarrier", parent, position, yaw);
        Box("Base", j, new Vector3(0f, 0.42f, 0f), new Vector3(0.6f, 0.85f, 2.9f), "Concrete", true);
        Box("Top", j, new Vector3(0f, 0.95f, 0f), new Vector3(0.3f, 0.2f, 2.9f), "Concrete", false);
    }

    private static void Dumpster(Transform parent, Vector3 position, float yaw)
    {
        Transform d = Group("Dumpster", parent, position, yaw);
        Box("Body", d, new Vector3(0f, 0.6f, 0f), new Vector3(1.1f, 1.2f, 1.9f), "DumpsterGreen", true);
        Box("Lid", d, new Vector3(0f, 1.24f, 0f), new Vector3(1.15f, 0.08f, 1.95f), "DumpsterGreen", false);
    }

    private static void TrashBags(Transform parent, Vector3 center)
    {
        int count = 3 + _rng.Next(3);
        for (int i = 0; i < count; i++)
        {
            float size = Range(0.45f, 0.75f);
            GameObject bag = Primitive(PrimitiveType.Sphere, "TrashBag", parent,
                center + new Vector3(Range(-0.5f, 0.5f), size * 0.35f, Range(-0.6f, 0.6f)), new Vector3(size, size * 0.75f, size), "TrashBag", false);
            bag.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
        }
    }

    private static void Hydrant(Transform parent, Vector3 position)
    {
        Cylinder("Hydrant", parent, position + new Vector3(0f, 0.5f, 0f), 0.3f, 0.7f, "Hydrant", true, Vector3.zero);
        Primitive(PrimitiveType.Sphere, "HydrantTop", parent, position + new Vector3(0f, 0.88f, 0f), new Vector3(0.3f, 0.2f, 0.3f), "Hydrant", false);
    }

    private static void FenceAlongX(Transform parent, float x0, float x1, float z)
    {
        Transform f = Group("ChainLinkFence", parent, Vector3.zero, 0f);
        Box("Panel", f, new Vector3((x0 + x1) * 0.5f, 1.1f, z), new Vector3(x1 - x0, 2.2f, 0.05f), "Fence", true);
        for (float x = x0; x <= x1 + 0.01f; x += Mathf.Max(0.5f, (x1 - x0) / 2f))
        {
            Cylinder("Post", f, new Vector3(x, 1.15f, z), 0.08f, 2.3f, "MetalDark", false, Vector3.zero);
        }
    }

    private enum LampState
    {
        On,
        Flicker,
        Dead,
    }

    private static void StreetLamp(Transform parent, float x, float z, float armDirection, LampState state)
    {
        Transform l = Group("StreetLamp", parent, new Vector3(x, 0f, z), 0f);
        Cylinder("Post", l, new Vector3(0f, 3f, 0f), 0.16f, 6f, "MetalDark", true, Vector3.zero);
        Box("Arm", l, new Vector3(armDirection * 0.8f, 5.95f, 0f), new Vector3(1.6f, 0.1f, 0.1f), "MetalDark", false);
        GameObject head = Box("LampHead", l, new Vector3(armDirection * 1.5f, 5.85f, 0f), new Vector3(0.6f, 0.15f, 0.35f),
            state == LampState.Dead ? "LampOff" : "LampWarm", false);

        if (state == LampState.Dead)
        {
            return;
        }

        Light light = AddLight(l, "Light", new Vector3(armDirection * 1.5f, 5.6f, 0f), LightType.Spot,
            new Color(1f, 0.8f, 0.55f), 3.5f, 15f, 100f, new Vector3(90f, 0f, 0f));
        if (state == LampState.Flicker)
        {
            AddFlicker(light, FlickerLight.FlickerMode.Broken, head.GetComponent<Renderer>());
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

    private static void StoreLight(Transform parent, Vector3 position, FlickerLight.FlickerMode mode)
    {
        GameObject fixture = Box("Fluorescent", parent, position, new Vector3(0.3f, 0.08f, 2.4f), "LampCold", false);
        Light light = AddLight(parent, "StoreLight", position - Vector3.up * 0.25f, LightType.Point,
            new Color(0.85f, 0.95f, 1f), 1.6f, 9f, 0f, Vector3.zero);
        AddFlicker(light, mode, fixture.GetComponent<Renderer>());
    }

    private static void Fire(Transform parent, Vector3 position, float intensity)
    {
        Light light = AddLight(parent, "FireLight", position, LightType.Point, new Color(1f, 0.5f, 0.15f), intensity, 9f, 0f, Vector3.zero);
        AddFlicker(light, FlickerLight.FlickerMode.Fire, null);
    }

    // ===================================================================== 기본 도형 / 모델

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

    // 무료 모델 프리팹을 놓고, 모양에 맞는 BoxCollider를 붙인다. (프리팹이 없으면 건너뜀)
    private static GameObject Prefab(string id, Transform parent, Vector3 position, float yaw)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + id + ".prefab");
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.localPosition = position;
        instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        AddFitCollider(instance, false);
        return instance;
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
            // 깜빡이는 발광체와 글자는 정적 배칭에서 뺀다.
            if (go.name == "LampHead" || go.name == "Fluorescent" || go.name == "StoreSign" || go.name == "DoorLamp"
                || go.name.StartsWith("LightBar") || go.GetComponent<TextMesh>() != null)
            {
                continue;
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

        MaterialSpec(name, out Color color, out float smoothness, out float metallic, out Color emission);
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

    private static void MaterialSpec(string name, out Color color, out float smoothness, out float metallic, out Color emission)
    {
        smoothness = 0.15f;
        metallic = 0f;
        emission = Color.black;
        switch (name)
        {
            case "Asphalt": color = new Color(0.09f, 0.09f, 0.1f); smoothness = 0.3f; break;
            case "ConcreteDark": color = new Color(0.2f, 0.2f, 0.2f); break;
            case "Concrete": color = new Color(0.42f, 0.41f, 0.39f); break;
            case "Sidewalk": color = new Color(0.36f, 0.35f, 0.33f); break;
            case "Brick": color = new Color(0.38f, 0.17f, 0.12f); break;
            case "BrickDark": color = new Color(0.24f, 0.11f, 0.09f); break;
            case "Plaster": color = new Color(0.52f, 0.49f, 0.43f); break;
            case "PlasterDark": color = new Color(0.3f, 0.31f, 0.33f); break;
            case "RoofDark": color = new Color(0.12f, 0.12f, 0.13f); break;
            case "WindowDark": color = new Color(0.03f, 0.035f, 0.045f); smoothness = 0.9f; break;
            case "WindowLit": color = new Color(0.25f, 0.18f, 0.1f); emission = new Color(1.2f, 0.8f, 0.4f); break;
            case "Boarded": color = new Color(0.33f, 0.23f, 0.13f); break;
            case "GlassDark": color = new Color(0.02f, 0.03f, 0.035f); smoothness = 0.92f; break;
            case "DoorDark": color = new Color(0.08f, 0.08f, 0.09f); metallic = 0.4f; break;
            case "AwningRed": color = new Color(0.4f, 0.07f, 0.06f); break;
            case "AwningGreen": color = new Color(0.08f, 0.25f, 0.12f); break;
            case "AwningBlue": color = new Color(0.08f, 0.12f, 0.3f); break;
            case "SignRed": color = new Color(0.3f, 0.02f, 0.02f); emission = new Color(1.6f, 0.1f, 0.1f); break;
            case "SignCyan": color = new Color(0.02f, 0.2f, 0.25f); emission = new Color(0.1f, 1.2f, 1.4f); break;
            case "SignYellow": color = new Color(0.3f, 0.25f, 0.02f); emission = new Color(1.5f, 1.1f, 0.2f); break;
            case "SignDead": color = new Color(0.1f, 0.1f, 0.1f); break;
            case "MetalDark": color = new Color(0.13f, 0.13f, 0.14f); metallic = 0.6f; smoothness = 0.4f; break;
            case "Metal": color = new Color(0.35f, 0.36f, 0.37f); metallic = 0.5f; smoothness = 0.35f; break;
            case "LampWarm": color = new Color(0.9f, 0.8f, 0.6f); emission = new Color(2.5f, 2f, 1.3f); break;
            case "LampCold": color = new Color(0.85f, 0.9f, 0.95f); emission = new Color(1.8f, 2f, 2.2f); break;
            case "LampOff": color = new Color(0.15f, 0.15f, 0.15f); break;
            case "LampRed": color = new Color(0.5f, 0.05f, 0.05f); emission = new Color(2.5f, 0.15f, 0.1f); break;
            case "LampBlue": color = new Color(0.05f, 0.1f, 0.5f); emission = new Color(0.15f, 0.35f, 2.5f); break;
            case "LampGreen": color = new Color(0.1f, 0.5f, 0.15f); emission = new Color(0.2f, 2.2f, 0.5f); break;
            case "CarRed": color = new Color(0.35f, 0.06f, 0.05f); smoothness = 0.55f; metallic = 0.3f; break;
            case "CarBlue": color = new Color(0.07f, 0.12f, 0.28f); smoothness = 0.55f; metallic = 0.3f; break;
            case "CarGray": color = new Color(0.3f, 0.31f, 0.32f); smoothness = 0.55f; metallic = 0.3f; break;
            case "CarWhite": color = new Color(0.62f, 0.62f, 0.6f); smoothness = 0.5f; metallic = 0.2f; break;
            case "CarBlack": color = new Color(0.04f, 0.04f, 0.045f); smoothness = 0.6f; metallic = 0.3f; break;
            case "BusYellow": color = new Color(0.55f, 0.4f, 0.05f); smoothness = 0.45f; break;
            case "Charred": color = new Color(0.05f, 0.045f, 0.04f); smoothness = 0.05f; break;
            case "Tire": color = new Color(0.03f, 0.03f, 0.03f); break;
            case "LightDead": color = new Color(0.6f, 0.6f, 0.55f); smoothness = 0.8f; break;
            case "LineYellow": color = new Color(0.55f, 0.45f, 0.1f); break;
            case "LineWhite": color = new Color(0.6f, 0.6f, 0.58f); break;
            case "Blood": color = new Color(0.18f, 0.01f, 0.01f); smoothness = 0.6f; break;
            case "DumpsterGreen": color = new Color(0.1f, 0.22f, 0.13f); metallic = 0.3f; break;
            case "TrashBag": color = new Color(0.02f, 0.02f, 0.025f); smoothness = 0.7f; break;
            case "Fence": color = new Color(0.25f, 0.26f, 0.27f); metallic = 0.5f; break;
            case "Wood": color = new Color(0.33f, 0.22f, 0.12f); break;
            case "Tile": color = new Color(0.4f, 0.4f, 0.38f); smoothness = 0.5f; break;
            case "Sandbag": color = new Color(0.4f, 0.36f, 0.26f); break;
            case "SafeDoor": color = new Color(0.45f, 0.04f, 0.03f); metallic = 0.4f; break;
            case "Hydrant": color = new Color(0.5f, 0.06f, 0.05f); break;
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
                continue; // 바닥이거나, 남쪽 끝이 아니거나, 출입구 자리를 막지 않는다.
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

        // 월드 X 방향과 나란한 로컬 축을 찾는다. (회전이 0/90/180/270도인 상자만 자를 수 있다)
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
        RenderSettings.fogDensity = 0.032f;
        RenderSettings.fogColor = fog;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.09f, 0.1f, 0.13f);
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
            light.intensity = 0.12f;
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
