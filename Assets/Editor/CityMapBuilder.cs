using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// L4D / World War Z 느낌의 도시 맵을 GameScene에 만들어 주는 도구. (v2: 예전 맵의 약 4배 - 길이 2배, 폭 2배)
// 메뉴: Tools > L4D 맵 > 도시 맵 만들기
//
//  출발 안전가옥 → 골목 미로 → 큰길(막힌 사거리, 바리케이드) → 공원 → 중간 안전가옥
//  → 주차타워(1층 → 경사로 → 옥상) → 다리 → 고가도로(차가 막힌 고속도로) → 내려가는 경사로 → 피날레 광장
//
//  - 배치(바닥, 보이지 않는 벽, 건물 줄, 소품, 스폰 포인트, 경로)는 CityMapBuilder.Layout.cs에 있다.
//    (tools/layout.py로 위에서 본 그림과 함께 만든 데이터)
//  - 원래 Map과 맵 소품(드럼통, 상자, 씬에 놓은 좀비 등)은 지우지 않고 Map_Old 아래로 옮겨 꺼 둔다.
//  - Player를 출발 안전가옥으로 옮기고, ZombieSpawner의 스폰 포인트를 새 맵의 숨은 자리로 바꾼다.
//  - CityMap에 NavMesh Surface를 붙여 굽는다. (14장의 AI Navigation 패키지)
//  - 바꾸기 전의 씬은 Assets/Backup_Map에 복사해 둔다.
// 모델: Kenney City Kit (Commercial, Roads), Car Kit - CC0 (www.kenney.nl)
public static partial class CityMapBuilder
{
    private const string Menu = "Tools/L4D 맵/";
    private const string RootName = "CityMap";
    private const string OldRootName = "Map_Old";
    private const string KenneyRoot = "Assets/L4DMap/Kenney/";
    private const string MaterialFolder = "Assets/L4DMap/Materials";
    private const string BackupRoot = "Assets/Backup_Map";
    private const float CityScale = 8f;   // Kenney 도시 키트 1칸 = 8m
    private const float CarScale = 1.6f;  // Kenney 자동차 키트 1단위 = 1.6m
    private const float WallHeight = 40f; // 2단 점프로도 넘지 못하는 보이지 않는 벽 높이

    private enum Kit
    {
        Commercial,
        Roads,
        Cars,
    }

    private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();
    private static readonly Dictionary<string, GameObject> ModelCache = new Dictionary<string, GameObject>();
    private static readonly Dictionary<string, Bounds> BoundsCache = new Dictionary<string, Bounds>();
    private static readonly Dictionary<Kit, float> UnitCache = new Dictionary<Kit, float>();
    private static readonly List<string> Log = new List<string>();
    private static System.Random _rng;

    private static readonly string[] Buildings =
    {
        "building-a", "building-b", "building-c", "building-d", "building-e", "building-f", "building-g",
        "building-h", "building-i", "building-j", "building-k", "building-l", "building-m", "building-n",
    };

    private static readonly string[] LowBuildings =
    {
        "low-detail-building-a", "low-detail-building-b", "low-detail-building-c", "low-detail-building-d",
        "low-detail-building-e", "low-detail-building-f", "low-detail-building-g", "low-detail-building-h",
    };

    private static readonly string[] Skyscrapers =
    {
        "building-skyscraper-a", "building-skyscraper-b", "building-skyscraper-c", "building-skyscraper-d", "building-skyscraper-e",
    };

    // ===================== 메뉴 =====================

    [MenuItem(Menu + "도시 맵 만들기", priority = 0)]
    public static void BuildMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("L4D 맵", "플레이를 멈춘 뒤에 실행해 주세요.", "확인");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(KenneyRoot + "Roads/road-straight.fbx") == null)
        {
            EditorUtility.DisplayDialog("L4D 맵", "Assets/L4DMap/Kenney 폴더(맵 모델)가 없습니다.\nzip 안의 Assets 폴더를 전부 복사했는지 확인해 주세요.", "확인");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() || !OpenSceneWithPlayer())
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject existing = scene.GetRootGameObjects().FirstOrDefault(go => go.name == RootName);
        if (existing != null && !EditorUtility.DisplayDialog("L4D 맵", "이미 만든 도시 맵이 있습니다. 지우고 새로 만들까요?", "새로 만들기", "취소"))
        {
            return;
        }

        Log.Clear();
        try
        {
            Backup(scene.path);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            Build(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Log.Add("✔ " + scene.path + " 저장");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Log.Add("⚠ 오류: " + exception.Message + " (Console 확인)");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        string body = "도시 맵을 만들었습니다. ▶ Play로 확인해 보세요.\n\n" + string.Join("\n", Log);
        Debug.Log("[L4D 맵] " + body);
        EditorUtility.DisplayDialog("L4D 맵", body.Length > 1800 ? body.Substring(0, 1800) + "\n... (Console 참고)" : body, "확인");
    }

    [MenuItem(Menu + "원래 맵으로 되돌리기", priority = 20)]
    public static void RestoreMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject city = scene.GetRootGameObjects().FirstOrDefault(go => go.name == RootName);
        GameObject old = scene.GetRootGameObjects().FirstOrDefault(go => go.name == OldRootName);
        if (city == null && old == null)
        {
            EditorUtility.DisplayDialog("L4D 맵", "이 씬에는 도시 맵이 없습니다.", "확인");
            return;
        }

        if (!EditorUtility.DisplayDialog("L4D 맵", "도시 맵을 지우고 원래 맵을 다시 켤까요?\n(Player 위치와 스폰 포인트는 Assets/Backup_Map의 씬으로 되돌릴 수 있습니다)", "되돌리기", "취소"))
        {
            return;
        }

        if (city != null)
        {
            Object.DestroyImmediate(city);
        }

        if (old != null)
        {
            old.SetActive(true);
            foreach (Transform child in old.transform.Cast<Transform>().ToList())
            {
                child.SetParent(null, true);
            }

            Object.DestroyImmediate(old);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("L4D 맵", "원래 맵을 다시 켰습니다. NavMesh는 Map의 NavMesh Surface에서 Bake를 한 번 눌러 주세요.", "확인");
    }

    // ===================== 전체 흐름 =====================
    // 좌표 (단위 m, y 0 = 바닥, +z = 앞으로 나아가는 방향, 고가 높이 y 6)
    //  출발 안전가옥 : x -4~4,    z -4~6
    //  골목 미로     : x -20~20,  z 6~58   (4m 골목, 막다른 길)
    //  큰길          : x -8~8,    z 58~150 (막힌 사거리 z 92~104, z 150에서 바리케이드)
    //  공원 입구     : x 8~20,    z 122~138 → 공원 x 20~64, z 110~188 (분수, 나무, 산울타리)
    //  중간 안전가옥 : x 44~54,   z 188~198 (남쪽 문 → 북쪽 문)
    //  주차타워      : x 24~58,   z 200~244 (1층) → 경사로 x 58~64 → 옥상 y 6
    //  다리          : x -4~24,   z 236~244 (y 6)
    //  고가도로      : x -20~-4,  z 200~340 (y 6) → 내려가는 경사로 z 340~372
    //  피날레 광장   : x -40~40,  z 372~432 (헬기장 z 418, 무전기 z 404)

    private static void Build(Scene scene)
    {
        _rng = new System.Random(20261028);
        _breakablesSearched = false;
        MaterialCache.Clear();
        ModelCache.Clear();
        BoundsCache.Clear();
        UnitCache.Clear();

        Progress("모델 설정", 0.05f);
        int textures = PrepareTextures();
        int models = PrepareModels();
        if (textures + models > 0)
        {
            Log.Add("✔ Kenney 모델/텍스처 가져오기 설정 " + (textures + models) + "개 정리");
        }

        Progress("기존 맵 보관", 0.1f);
        StoreOldMap(scene);

        GameObject rootObject = new GameObject(RootName);
        SceneManager.MoveGameObjectToScene(rootObject, scene);
        Transform root = rootObject.transform;
        CityMapInfo info = rootObject.AddComponent<CityMapInfo>();

        Progress("바닥과 길", 0.2f);
        BuildGeneratedFloors(Group("Ground", root));
        Progress("보이지 않는 경계 벽", 0.25f);
        BuildGeneratedWalls(Group("Boundary", root));
        Progress("고가도로와 주차타워", 0.3f);
        BuildGeneratedDecks(Group("Elevated", root));
        Progress("안전가옥", 0.35f);
        BoxCollider startArea = BuildStartSafeRoom(root, out Transform startPoint);
        BoxCollider midArea = BuildMidSafeRoom(root);
        Progress("건물", 0.45f);
        BuildGeneratedBuildings(Group("Buildings", root));
        Progress("소품", 0.6f);
        BuildGeneratedProps(Group("Props", root));
        Progress("피날레 광장", 0.7f);
        BuildFinaleMarkers(root, out BoxCollider finaleArea, out Transform extraction);
        Progress("스폰 포인트", 0.75f);
        Transform[] spawnPoints = Points(root, "SpawnPoints", "SpawnPoint_", GeneratedSpawnPoints);
        Transform[] route = Points(root, "Route", "Route_", GeneratedRoute);
        MarkStatic(root);

        SetRef(info, "_startPoint", startPoint);
        SetRef(info, "_startSafeRoomArea", startArea);
        SetRef(info, "_finaleArea", finaleArea);
        SetRef(info, "_extractionPoint", extraction);
        SetArray(info, "_spawnPoints", spawnPoints);
        SetArray(info, "_routePoints", route);
        SetArray(info, "_midSafeRooms", new Object[] { midArea });
        Log.Add("✔ 맵 v2 (약 4배): 안전가옥 → 골목 미로 → 큰길 → 공원 → 중간 안전가옥 → 주차타워 → 고가도로 → 피날레 광장");

        Progress("Player와 스포너", 0.8f);
        MovePlayer(startPoint);
        AssignSpawner(spawnPoints);

        Progress("분위기", 0.85f);
        ApplyAtmosphere();

        // 콜라이더 없는 모델 보강, 드럼통/상자 NavMesh Obstacle, NavMesh 굽기 (좀비 끼임 고치기와 같은 처리)
        Progress("NavMesh 굽기", 0.9f);
        Log.AddRange(ZombieStuckFix.PrepareAndBake(info));
    }

    private static void Progress(string text, float value)
    {
        EditorUtility.DisplayProgressBar("L4D 맵 만들기", text, value);
    }

    // ===================== 기존 맵 보관 =====================

    // 원래 Map과 콜라이더가 있는 맵 소품(드럼통, 상자, 벽, 씬에 놓은 좀비 등)을 Map_Old 아래로 옮겨 끈다.
    private static void StoreOldMap(Scene scene)
    {
        GameObject old = scene.GetRootGameObjects().FirstOrDefault(go => go.name == OldRootName);
        if (old == null)
        {
            old = new GameObject(OldRootName);
            SceneManager.MoveGameObjectToScene(old, scene);
        }

        List<string> moved = new List<string>();
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go == old || go.name == RootName || IsEssential(go))
            {
                continue;
            }

            bool isMap = go.name == "Map" || go.name == "Ground";
            bool hasCollider = go.GetComponentInChildren<Collider>(true) != null;
            bool isZombie = go.GetComponentInChildren<Zombie>(true) != null;
            if (isMap || hasCollider || isZombie)
            {
                go.transform.SetParent(old.transform, true);
                moved.Add(go.name);
            }
        }

        old.SetActive(false);
        if (moved.Count > 0)
        {
            Log.Add("✔ 원래 맵은 지우지 않고 Map_Old 아래로 옮겨 꺼 두었습니다: " + string.Join(", ", moved.Distinct().Take(12)) + (moved.Count > 12 ? " 외" : string.Empty));
        }
    }

    // 게임에 꼭 필요한 오브젝트는 그대로 둔다.
    private static bool IsEssential(GameObject go)
    {
        return go.GetComponentInChildren<PlayerMove>(true) != null
               || go.GetComponentInChildren<GameManager>(true) != null
               || go.GetComponentInChildren<Camera>(true) != null
               || go.GetComponentInChildren<Canvas>(true) != null
               || go.GetComponentInChildren<ZombieSpawner>(true) != null
               || go.GetComponentInChildren<CursorManager>(true) != null
               || go.GetComponentInChildren<Light>(true) != null && go.GetComponentInChildren<Collider>(true) == null
               || go.GetComponent<UnityEngine.EventSystems.EventSystem>() != null;
    }

    // ===================== 보이지 않는 벽 =====================

    // 가운데 (cx, cz), 바닥 높이 y0에서 h만큼 올라가는 벽
    private static void Wall(Transform parent, float cx, float y0, float cz, float sx, float h, float sz)
    {
        GameObject wall = new GameObject("Wall");
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = new Vector3(cx, y0 + h * 0.5f, cz);
        BoxCollider collider = wall.AddComponent<BoxCollider>();
        collider.size = new Vector3(sx, h, sz);
    }

    // ===================== 고가 / 경사로 =====================

    // 윗면이 DeckY인 판 (고가도로, 주차타워 옥상, 다리)
    private static void Deck(Transform parent, string name, float x0, float x1, float z0, float z1, string material)
    {
        Box(name, parent, new Vector3((x0 + x1) * 0.5f, DeckY - 0.25f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.5f, z1 - z0), material, true);
        // 판 아래 테두리 보 (아래에서 올려다볼 때 두께감)
        Box(name + "Beam", parent, new Vector3((x0 + x1) * 0.5f, DeckY - 0.9f, z0 + 0.4f), new Vector3(x1 - x0, 0.8f, 0.8f), "Concrete", false);
        Box(name + "Beam", parent, new Vector3((x0 + x1) * 0.5f, DeckY - 0.9f, z1 - 0.4f), new Vector3(x1 - x0, 0.8f, 0.8f), "Concrete", false);
    }

    // zStart(높이 yStart)에서 zEnd(높이 yEnd)까지 비스듬한 판. rails면 양옆에 낮은 난간
    private static void Ramp(Transform parent, float x0, float x1, float zStart, float zEnd, float yStart, float yEnd, bool rails)
    {
        Vector3 bottom = new Vector3((x0 + x1) * 0.5f, yStart, zStart);
        Vector3 top = new Vector3((x0 + x1) * 0.5f, yEnd, zEnd);
        Vector3 along = top - bottom;
        float length = along.magnitude + 0.4f;  // 위아래 이음새가 벌어지지 않게 조금 길게
        Quaternion rotation = Quaternion.LookRotation(along.normalized, Vector3.up);
        Vector3 normal = rotation * Vector3.up;
        Vector3 center = (bottom + top) * 0.5f - normal * 0.25f;

        GameObject slab = Box("Ramp", parent, center, new Vector3(x1 - x0, 0.5f, length), "Asphalt", true);
        slab.transform.localRotation = rotation;
        if (!rails)
        {
            return;
        }

        Vector3 right = rotation * Vector3.right;
        foreach (float side in new[] { -1f, 1f })
        {
            Vector3 railCenter = center + normal * 0.75f + right * side * ((x1 - x0) * 0.5f - 0.15f);
            GameObject rail = Box("RampRail", parent, railCenter, new Vector3(0.3f, 1f, length), "Concrete", true);
            rail.transform.localRotation = rotation;
        }
    }

    // ===================== 출발 안전가옥 =====================

    private static BoxCollider BuildStartSafeRoom(Transform root, out Transform startPoint)
    {
        Transform room = Group("StartSafeRoom", root);

        // 벽 (북쪽 가운데에 문 자리 2.4m)
        Box("WallSouth", room, new Vector3(0f, 1.75f, -4.15f), new Vector3(8.6f, 3.5f, 0.3f), "SafeWall", true);
        Box("WallWest", room, new Vector3(-4.15f, 1.75f, 1f), new Vector3(0.3f, 3.5f, 10.6f), "SafeWall", true);
        Box("WallEast", room, new Vector3(4.15f, 1.75f, 1f), new Vector3(0.3f, 3.5f, 10.6f), "SafeWall", true);
        Box("WallNorthL", room, new Vector3(-2.65f, 1.75f, 6.15f), new Vector3(2.9f, 3.5f, 0.3f), "SafeWall", true);
        Box("WallNorthR", room, new Vector3(2.65f, 1.75f, 6.15f), new Vector3(2.9f, 3.5f, 0.3f), "SafeWall", true);
        Box("DoorTop", room, new Vector3(0f, 3.05f, 6.15f), new Vector3(2.4f, 0.9f, 0.3f), "SafeWall", true);
        Box("Roof", room, new Vector3(0f, 3.6f, 1f), new Vector3(8.6f, 0.2f, 10.6f), "RoofDark", true);

        // 활짝 열린 빨간 철문 (L4D 안전가옥)
        Box("SafeDoor", room, new Vector3(1.3f, 1.25f, 7.35f), new Vector3(0.12f, 2.5f, 2.3f), "SafeDoor", true);
        Box("DoorSign", room, new Vector3(0f, 3.2f, 6.32f), new Vector3(1.6f, 0.35f, 0.05f), "LampGreen", false);

        // 안쪽: 간이 침대, 탁자, 탄약 상자 자리
        Box("Cot", room, new Vector3(-3f, 0.3f, -2f), new Vector3(1f, 0.6f, 2.2f), "Cot", true);
        Box("Cot", room, new Vector3(3f, 0.3f, -2f), new Vector3(1f, 0.6f, 2.2f), "Cot", true);
        Box("Table", room, new Vector3(0f, 0.45f, -3f), new Vector3(2.4f, 0.9f, 1f), "Wood", true);
        Box("AmmoCrate", room, new Vector3(-0.6f, 1.05f, -3f), new Vector3(0.8f, 0.3f, 0.5f), "Military", false);
        Box("MedKitBox", room, new Vector3(0.6f, 1.0f, -3f), new Vector3(0.5f, 0.2f, 0.4f), "MedRed", false);

        AddLight(room, "SafeLight", new Vector3(0f, 3.2f, 1f), LightType.Point, new Color(0.55f, 1f, 0.6f), 2.2f, 10f, 0f, Vector3.zero);

        // 둘레의 건물은 BuildGeneratedBuildings가 자동으로 세운다.

        startPoint = new GameObject("StartPoint").transform;
        startPoint.SetParent(room, false);
        startPoint.localPosition = new Vector3(0f, 0f, 0f);

        GameObject area = new GameObject("StartSafeRoomArea");
        area.transform.SetParent(room, false);
        area.transform.localPosition = new Vector3(0f, 1.5f, 1f);
        BoxCollider trigger = area.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(8f, 3f, 10f);
        return trigger;
    }

    // ===================== 중간 안전가옥 (L4D 체크포인트) =====================
    // 공원 북쪽 끝. 남쪽 문으로 들어가서 북쪽 문으로 나가면 주차타워. 안에서는 좀비가 새로 나오지 않는다. (AIDirector)
    private static BoxCollider BuildMidSafeRoom(Transform root)
    {
        Transform room = Group("MidSafeRoom", root);
        const float x0 = 44f, x1 = 54f, z0 = 188f, z1 = 198f;
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        const float doorX0 = 48f, doorX1 = 50.4f;  // 문 자리 (남북 같은 위치)

        Box("WallWest", room, new Vector3(x0 + 0.15f, 1.75f, cz), new Vector3(0.3f, 3.5f, z1 - z0), "SafeWall", true);
        Box("WallEast", room, new Vector3(x1 - 0.15f, 1.75f, cz), new Vector3(0.3f, 3.5f, z1 - z0), "SafeWall", true);
        foreach (float z in new[] { z0 + 0.15f, z1 - 0.15f })
        {
            Box("WallL", room, new Vector3((x0 + doorX0) * 0.5f, 1.75f, z), new Vector3(doorX0 - x0, 3.5f, 0.3f), "SafeWall", true);
            Box("WallR", room, new Vector3((doorX1 + x1) * 0.5f, 1.75f, z), new Vector3(x1 - doorX1, 3.5f, 0.3f), "SafeWall", true);
            Box("DoorTop", room, new Vector3((doorX0 + doorX1) * 0.5f, 3.05f, z), new Vector3(doorX1 - doorX0, 0.9f, 0.3f), "SafeWall", true);
            Box("DoorSign", room, new Vector3((doorX0 + doorX1) * 0.5f, 3.2f, z + (z < cz ? -0.18f : 0.18f)), new Vector3(1.6f, 0.35f, 0.05f), "LampGreen", false);
        }

        Box("Roof", room, new Vector3(cx, 3.6f, cz), new Vector3(x1 - x0, 0.2f, z1 - z0), "RoofDark", true);
        // 활짝 열린 빨간 철문 두 개 (안쪽으로 열려 벽에 붙어 있다)
        Box("SafeDoor", room, new Vector3(doorX0 - 0.1f, 1.25f, z0 + 1.35f), new Vector3(0.12f, 2.5f, 2.3f), "SafeDoor", true);
        Box("SafeDoor", room, new Vector3(doorX1 + 0.1f, 1.25f, z1 - 1.35f), new Vector3(0.12f, 2.5f, 2.3f), "SafeDoor", true);

        // 안쪽: 간이 침대, 탁자 위 보급품 자리 (SupplySpawner가 진짜 보급품으로 바꾼다)
        Box("Cot", room, new Vector3(x0 + 1.2f, 0.3f, cz + 2f), new Vector3(1f, 0.6f, 2.2f), "Cot", true);
        Box("Cot", room, new Vector3(x0 + 1.2f, 0.3f, cz - 2f), new Vector3(1f, 0.6f, 2.2f), "Cot", true);
        Box("Table", room, new Vector3(x1 - 1f, 0.45f, cz), new Vector3(1f, 0.9f, 2.6f), "Wood", true);
        Box("MidAmmoCrate", room, new Vector3(x1 - 1f, 1.05f, cz - 0.7f), new Vector3(0.5f, 0.3f, 0.8f), "Military", false);
        Box("MidMedKitBox", room, new Vector3(x1 - 1f, 1.0f, cz + 0.6f), new Vector3(0.4f, 0.2f, 0.5f), "MedRed", false);
        AddLight(room, "SafeLight", new Vector3(cx, 3.2f, cz), LightType.Point, new Color(0.55f, 1f, 0.6f), 2.2f, 10f, 0f, Vector3.zero);

        GameObject area = new GameObject("MidSafeRoomArea");
        area.transform.SetParent(room, false);
        area.transform.localPosition = new Vector3(cx, 1.5f, cz);
        BoxCollider trigger = area.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(x1 - x0 - 0.6f, 3f, z1 - z0 - 0.6f);
        return trigger;
    }

    // ===================== 피날레 광장 표시 / 스폰 포인트 / 경로 =====================

    private static void BuildFinaleMarkers(Transform root, out BoxCollider area, out Transform extraction)
    {
        Transform plaza = Group("FinalePlaza", root);
        extraction = new GameObject("ExtractionPoint").transform;
        extraction.SetParent(plaza, false);
        extraction.localPosition = new Vector3(0f, 0f, 418f);

        GameObject areaObject = new GameObject("FinaleArea");
        areaObject.transform.SetParent(plaza, false);
        areaObject.transform.localPosition = new Vector3(0f, 2f, 402f);
        area = areaObject.AddComponent<BoxCollider>();
        area.isTrigger = true;
        area.size = new Vector3(80f, 4f, 60f);
    }

    private static Transform[] Points(Transform root, string groupName, string prefix, Vector3[] positions)
    {
        Transform group = Group(groupName, root);
        Transform[] result = new Transform[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            Transform point = new GameObject(prefix + i).transform;
            point.SetParent(group, false);
            point.localPosition = positions[i];
            result[i] = point;
        }

        return result;
    }

    // ===================== 공원 / 고가 / 광장 소품 =====================

    private static void Tree(Transform parent, Vector3 position, float scale)
    {
        Transform tree = Group("Tree", parent);
        tree.localPosition = position;
        tree.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
        // 잎은 2.4m보다 위에만 (아래로 지나다닐 수 있게, 콜라이더도 붙지 않게)
        Cylinder("Trunk", tree, new Vector3(0f, 1.8f * scale, 0f), 0.35f * scale, 3.6f * scale, "Bark", true);
        Primitive(PrimitiveType.Sphere, "Leaves", tree, new Vector3(0f, 4.3f * scale, 0f), Vector3.one * 3.2f * scale, "Leaves", false);
        Primitive(PrimitiveType.Sphere, "Leaves", tree, new Vector3(0.7f * scale, 3.8f * scale, 0.4f * scale), Vector3.one * 2.2f * scale, "Leaves", false);
        Primitive(PrimitiveType.Sphere, "Leaves", tree, new Vector3(-0.6f * scale, 4f * scale, -0.5f * scale), Vector3.one * 2.4f * scale, "LeavesDark", false);
    }

    private static void Bench(Transform parent, Vector3 position, float yaw)
    {
        Transform bench = Group("Bench", parent);
        bench.localPosition = position;
        bench.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Box("Seat", bench, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.08f, 0.5f), "Wood", true);
        Box("Back", bench, new Vector3(0f, 0.8f, -0.22f), new Vector3(1.8f, 0.5f, 0.06f), "Wood", false);
        Box("LegL", bench, new Vector3(-0.8f, 0.22f, 0f), new Vector3(0.08f, 0.45f, 0.45f), "MetalDark", false);
        Box("LegR", bench, new Vector3(0.8f, 0.22f, 0f), new Vector3(0.08f, 0.45f, 0.45f), "MetalDark", false);
    }

    private static void Fountain(Transform parent, Vector3 position)
    {
        Transform fountain = Group("Fountain", parent);
        fountain.localPosition = position;
        Cylinder("Basin", fountain, new Vector3(0f, 0.3f, 0f), 8f, 0.6f, "Concrete", true);
        Cylinder("Water", fountain, new Vector3(0f, 0.58f, 0f), 7.4f, 0.04f, "Water", false);
        Cylinder("Pillar", fountain, new Vector3(0f, 1.2f, 0f), 0.9f, 2.4f, "Concrete", true);
        Cylinder("Bowl", fountain, new Vector3(0f, 2.4f, 0f), 2.6f, 0.3f, "Concrete", false);
        AddLight(fountain, "FountainLight", new Vector3(0f, 1f, 0f), LightType.Point, new Color(0.5f, 0.75f, 1f), 1.2f, 9f, 0f, Vector3.zero);
    }

    // 고가도로 위 표지판 문 (기둥 둘 + 가로 보 + 초록 표지판)
    private static void Gantry(Transform parent, Vector3 position)
    {
        Transform gantry = Group("Gantry", parent);
        gantry.localPosition = position;
        Box("PostL", gantry, new Vector3(-7.4f, 3f, 0f), new Vector3(0.4f, 6f, 0.4f), "MetalDark", true);
        Box("PostR", gantry, new Vector3(7.4f, 3f, 0f), new Vector3(0.4f, 6f, 0.4f), "MetalDark", true);
        Box("Beam", gantry, new Vector3(0f, 5.8f, 0f), new Vector3(15.2f, 0.4f, 0.4f), "MetalDark", false);
        Box("SignA", gantry, new Vector3(-3.5f, 5f, -0.25f), new Vector3(5f, 1.6f, 0.1f), "SignGreen", false);
        Box("SignB", gantry, new Vector3(3.5f, 5f, -0.25f), new Vector3(5f, 1.6f, 0.1f), "SignGreen", false);
    }

    private static void Helipad(Transform parent, Vector3 center)
    {
        Transform pad = Group("Helipad", parent);
        pad.localPosition = center;
        Cylinder("Pad", pad, new Vector3(0f, 0.04f, 0f), 12f, 0.08f, "Concrete", false);
        Box("H_Left", pad, new Vector3(-1.6f, 0.09f, 0f), new Vector3(0.7f, 0.02f, 4.6f), "LineYellow", false);
        Box("H_Right", pad, new Vector3(1.6f, 0.09f, 0f), new Vector3(0.7f, 0.02f, 4.6f), "LineYellow", false);
        Box("H_Middle", pad, new Vector3(0f, 0.09f, 0f), new Vector3(2.5f, 0.02f, 0.7f), "LineYellow", false);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Box("PadLight", pad, new Vector3(Mathf.Sin(angle) * 6.4f, 0.15f, Mathf.Cos(angle) * 6.4f), new Vector3(0.25f, 0.25f, 0.25f), "LampRed", false);
        }

        Light flare = AddLight(pad, "Flare", new Vector3(0f, 1f, 0f), LightType.Point, new Color(1f, 0.2f, 0.15f), 3f, 16f, 0f, Vector3.zero);
        AddFlicker(flare, MapFlickerLight.FlickerMode.Fire);
    }

    // 무전기 (AIDirector가 "Radio"라는 이름으로 찾아 [E] 구조 요청 자리로 쓴다)
    private static void Radio(Transform parent, Vector3 position)
    {
        Box("RadioTable", parent, position + new Vector3(0f, 0.45f, 0f), new Vector3(1.4f, 0.9f, 0.8f), "Wood", true);
        Box("Radio", parent, position + new Vector3(0f, 1.05f, 0f), new Vector3(0.6f, 0.3f, 0.35f), "Military", false);
        Light radioLight = AddLight(parent, "RadioLight", position + new Vector3(0f, 1.4f, 0f), LightType.Point, new Color(0.3f, 1f, 0.4f), 1.2f, 4f, 0f, Vector3.zero);
        AddFlicker(radioLight, MapFlickerLight.FlickerMode.Siren);
    }

    // 내 Drum / Box 프리팹 (쏘면 터지고 부서진다)
    private static GameObject _drumPrefab;
    private static GameObject _cratePrefab;
    private static bool _breakablesSearched;

    private static void Breakable(Transform parent, bool drum, Vector3 position)
    {
        if (!_breakablesSearched)
        {
            _breakablesSearched = true;
            _drumPrefab = FindPrefabWith<Drum>();
            _cratePrefab = FindPrefabWith<Box>();
        }

        GameObject prefab = drum ? _drumPrefab : _cratePrefab;
        if (prefab == null)
        {
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.localPosition = position;
        instance.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
    }

    // ===================== 건물 =====================


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
            for (int attempt = 0; attempt < 10; attempt++)
            {
                string candidate = Pick(pool);
                if (!MeasureModel(Kit.Commercial, candidate, out Bounds bounds))
                {
                    continue;
                }

                if (bounds.size.x * unit <= remaining + 0.05f && bounds.size.z * unit <= maxDepth)
                {
                    chosen = candidate;
                    chosenBounds = bounds;
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
                if (!MeasureModel(Kit.Commercial, filler, out Bounds f))
                {
                    break;
                }

                used = remaining;
                float depth = Mathf.Min(maxDepth, 8f);
                Vector3 stretch = new Vector3(used / (f.size.x * unit), 1f, depth / (f.size.z * unit));
                Vector3 front = start + direction * (cursor + used * 0.5f);
                Place(Kit.Commercial, filler, parent, front, Yaw(facing), CityScale, stretch, colliders, true, material);
            }

            cursor += used + 0.2f;
        }
    }

    // ===================== 소품 =====================



    private static void Barricade(Transform parent, Vector3 center, float yaw, float width)
    {
        Transform group = Group("Barricade", parent);
        group.localPosition = center;
        group.localRotation = Quaternion.Euler(0f, yaw, 0f);
        for (float x = -width * 0.5f + 1.2f; x < width * 0.5f; x += 2.4f)
        {
            Place(Kit.Roads, _rng.NextDouble() < 0.5 ? "construction-barrier" : "construction-fence", group, new Vector3(x, 0f, 0f),
                Range(-8f, 8f), CityScale, Vector3.one, false, false, "K_Roads");
        }

        // 넘어가지 못하게 막는 콘크리트 블록
        Box("Blocks", group, new Vector3(0f, 0.5f, -0.9f), new Vector3(width, 1f, 0.6f), "Concrete", true);
        Place(Kit.Roads, "construction-light", group, new Vector3(width * 0.5f - 0.6f, 0f, -0.9f), 0f, CityScale, Vector3.one, false, false, "K_Roads");
    }

    private static void Car(Transform parent, string model, Vector3 position, float yaw, bool burning)
    {
        Transform car = Place(Kit.Cars, model, parent, position, yaw, CarScale, Vector3.one, true, false, burning ? "Charred" : "K_Cars");
        if (car != null && burning)
        {
            Light fire = AddLight(parent, "FireLight", position + new Vector3(0f, 1.8f, 0f), LightType.Point, new Color(1f, 0.5f, 0.15f), 2.6f, 9f, 0f, Vector3.zero);
            AddFlicker(fire, MapFlickerLight.FlickerMode.Fire);
        }
    }

    private static void Siren(Transform parent, Vector3 position, Color color)
    {
        Light light = AddLight(parent, "Siren", position, LightType.Point, color, 2f, 10f, 0f, Vector3.zero);
        AddFlicker(light, MapFlickerLight.FlickerMode.Siren);
    }

    private static void StreetLight(Transform parent, Vector3 position, Vector3 armDirection, bool broken)
    {
        if (!MeasureModel(Kit.Roads, "light-curved", out Bounds b))
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

        // 기둥에만 콜라이더 (팔 아래로는 지나간다)
        float pole = Mathf.Max(0.0001f, b.size.x);
        BoxCollider post = lamp.gameObject.AddComponent<BoxCollider>();
        post.center = new Vector3(b.center.x, b.center.y, armSign < 0f ? b.max.z - pole * 0.5f : b.min.z + pole * 0.5f);
        post.size = new Vector3(pole, b.size.y, pole);

        Vector3 headLocal = new Vector3(b.center.x, b.max.y - b.size.y * 0.04f, armSign < 0f ? b.min.z + b.size.z * 0.12f : b.max.z - b.size.z * 0.12f);
        Vector3 head = parent.InverseTransformPoint(lamp.TransformPoint(headLocal));
        Light light = AddLight(parent, "StreetLight", head - Vector3.up * 0.15f, LightType.Spot, new Color(1f, 0.8f, 0.55f), 3f, 16f, 105f,
            new Vector3(90f, 0f, 0f));
        if (broken)
        {
            AddFlicker(light, MapFlickerLight.FlickerMode.Broken);
        }
    }

    private static void WallLamp(Transform parent, Vector3 position, Vector3 facing, MapFlickerLight.FlickerMode mode)
    {
        Transform group = Group("WallLamp", parent);
        group.localPosition = position;
        Box("LampHead", group, facing * 0.3f, new Vector3(0.35f, 0.15f, 0.35f), "LampWarm", false);
        Vector3 direction = new Vector3(facing.x * 0.6f, -1f, facing.z * 0.6f);
        Light light = AddLight(group, "Light", facing * 0.35f - Vector3.up * 0.15f, LightType.Spot, new Color(1f, 0.78f, 0.5f), 2.4f, 11f, 110f,
            Quaternion.LookRotation(direction).eulerAngles);
        AddFlicker(light, mode);
    }

    // ===================== 피날레 광장 =====================


    private static void Sandbags(Transform parent, Vector3 center, float yaw, float width)
    {
        Transform group = Group("Sandbags", parent);
        group.localPosition = center;
        group.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Box("Base", group, new Vector3(0f, 0.3f, 0f), new Vector3(width, 0.6f, 0.9f), "Sandbag", true);
        Box("Top", group, new Vector3(0f, 0.75f, 0f), new Vector3(width - 0.4f, 0.3f, 0.7f), "Sandbag", true);
    }

    // ===================== 스폰 포인트 / 진행 경로 =====================



    // ===================== Player / 스포너 / 분위기 / NavMesh =====================

    private static void MovePlayer(Transform startPoint)
    {
        PlayerMove player = Object.FindAnyObjectByType<PlayerMove>();
        if (player == null)
        {
            Log.Add("⚠ Player를 찾지 못했습니다.");
            return;
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        float lift = controller != null ? controller.height * 0.5f - controller.center.y + controller.skinWidth : 1f;
        player.transform.position = startPoint.position + Vector3.up * lift;
        player.transform.rotation = Quaternion.identity;

        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.transform.rotation = Quaternion.identity;
        }

        Log.Add("✔ Player를 출발 안전가옥으로 옮겼습니다. (앞쪽 문으로 나가면 골목)");
    }

    private static void AssignSpawner(Transform[] spawnPoints)
    {
        ZombieSpawner spawner = Object.FindAnyObjectByType<ZombieSpawner>();
        if (spawner == null)
        {
            Log.Add("⚠ ZombieSpawner가 없어서 스폰 포인트를 연결하지 못했습니다.");
            return;
        }

        SerializedObject serialized = new SerializedObject(spawner);
        SerializedProperty property = serialized.FindProperty("_spawnPoints");
        if (property == null)
        {
            Log.Add("⚠ ZombieSpawner에 _spawnPoints 칸이 없습니다.");
            return;
        }

        property.arraySize = spawnPoints.Length;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
        }

        serialized.ApplyModifiedProperties();
        Log.Add("✔ ZombieSpawner의 Spawn Points를 새 맵의 " + spawnPoints.Length + "곳으로 바꿨습니다. (옛 스폰 포인트 오브젝트는 그대로 둠)");
    }

    // 해 질 녘 + 옅은 안개 (멀리 있는 좀비가 안개 속에서 나타난다)
    private static void ApplyAtmosphere()
    {
        Light sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(22f, -35f, 0f);
            sun.color = new Color(1f, 0.72f, 0.5f);
            sun.intensity = 0.9f;
            sun.shadows = LightShadows.Soft;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.42f, 0.42f, 0.48f);
        RenderSettings.ambientEquatorColor = new Color(0.36f, 0.33f, 0.32f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.15f, 0.14f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.5f, 0.45f, 0.42f);
        RenderSettings.fogStartDistance = 25f;
        RenderSettings.fogEndDistance = 170f;
        Log.Add("✔ 해 질 녘 조명과 옅은 안개");
    }


    private static void SetInt(SerializedObject serialized, string name, int value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
        {
            property.intValue = value;
        }
    }

    private static void SetRef(Object target, string field, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property != null)
        {
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetArray(Object target, string field, Object[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
        {
            return;
        }

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // ===================== 씬 / 프리팹 찾기 =====================

    private static bool OpenSceneWithPlayer()
    {
        if (Object.FindAnyObjectByType<PlayerMove>() != null)
        {
            return true;
        }

        foreach (string path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Where(p => !p.StartsWith(BackupRoot) && !p.StartsWith("Assets/Backup_"))
                     .OrderBy(p => Path.GetFileNameWithoutExtension(p) == "GameScene" ? 0 : 1))
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (Object.FindAnyObjectByType<PlayerMove>() != null)
            {
                return true;
            }
        }

        EditorUtility.DisplayDialog("L4D 맵", "Player(PlayerMove)가 있는 씬을 찾지 못했습니다. GameScene을 연 뒤 다시 실행해 주세요.", "확인");
        return false;
    }

    private static GameObject FindPrefabWith<T>() where T : Component
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/Backup_"))
            {
                continue;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponent<T>() != null)
            {
                return prefab;
            }
        }

        return null;
    }

    private static void Backup(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return;
        }

        string folder = BackupRoot + "/" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        EnsureFolder(folder);
        if (AssetDatabase.CopyAsset(assetPath, folder + "/" + Path.GetFileName(assetPath)))
        {
            Log.Add("✔ 바꾸기 전 씬 백업: " + folder);
        }
    }

    // ===================== Kenney 모델 =====================

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
            Debug.LogWarning("[L4D 맵] 모델을 찾지 못해 건너뜁니다: " + path);
        }

        ModelCache[path] = model;
        return model;
    }

    // 모델을 배율 1로 놓았을 때의 범위
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
            if (!LocalMeshBounds(wrapper.transform, out bounds))
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

    private static bool LocalMeshBounds(Transform root, out Bounds result)
    {
        result = new Bounds();
        bool any = false;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
            {
                continue;
            }

            Bounds local = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 p = root.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!any)
                {
                    result = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                {
                    result.Encapsulate(p);
                }
            }
        }

        return any;
    }

    // Kenney 1단위가 Unity에 몇 단위로 들어왔는지 (FBX 단위와 상관없이 맞추려고 직접 잰다)
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

    // 모델을 감싼 빈 오브젝트를 놓는다. anchorFront = true면 position이 모델 정면(+Z) 바닥 가운데, false면 바닥 가운데.
    private static Transform Place(Kit kit, string name, Transform parent, Vector3 position, float yaw,
        float metersPerUnit, Vector3 stretch, bool collider, bool anchorFront, string material)
    {
        GameObject model = LoadModel(kit, name);
        if (model == null || !MeasureModel(kit, name, out Bounds b))
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
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = mat;
            }

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

    // 색 팔레트 텍스처: 일반 2D 텍스처, 밉맵 없음, 가장자리 반복 없음 (멀리서 색이 섞이지 않게)
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

            if (importer.textureType == TextureImporterType.Default && importer.textureShape == TextureImporterShape.Texture2D
                && !importer.mipmapEnabled && importer.wrapMode == TextureWrapMode.Clamp)
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

    // Kenney 모델은 재질을 따로 입히므로 모델 안의 재질은 가져오지 않는다.
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

            if (importer.materialImportMode == ModelImporterMaterialImportMode.None && Mathf.Approximately(importer.globalScale, 1f)
                && importer.useFileScale && !importer.importCameras && !importer.importLights
                && importer.animationType == ModelImporterAnimationType.None && !importer.importAnimation)
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

    // ===================== 기본 도형 =====================

    private static Transform Group(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, string material, bool collider)
    {
        return Primitive(PrimitiveType.Cube, name, parent, center, size, material, collider);
    }

    private static GameObject Cylinder(string name, Transform parent, Vector3 center, float diameter, float height, string material, bool collider)
    {
        return Primitive(PrimitiveType.Cylinder, name, parent, center, new Vector3(diameter, height * 0.5f, diameter), material, collider);
    }

    private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 center, Vector3 scale, string material, bool collider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = Mat(material);
        if (!collider)
        {
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        return go;
    }

    private static Light AddLight(Transform parent, string name, Vector3 position, LightType type, Color color, float intensity,
        float range, float spotAngle, Vector3 euler)
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
        }

        light.shadows = LightShadows.None;
        return light;
    }

    private static void AddFlicker(Light light, MapFlickerLight.FlickerMode mode)
    {
        MapFlickerLight flicker = light.gameObject.AddComponent<MapFlickerLight>();
        flicker.Setup(mode, 1f);
        EditorUtility.SetDirty(flicker);
        SerializedObject serialized = new SerializedObject(flicker);
        SerializedProperty modeProperty = serialized.FindProperty("_mode");
        if (modeProperty != null)
        {
            modeProperty.enumValueIndex = (int)mode;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // 움직이지 않는 맵이라고 표시한다. (빛 계산, NavMesh)
    private static void MarkStatic(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponent<Light>() != null || t.GetComponentInParent<Drum>(true) != null || t.GetComponentInParent<Box>(true) != null)
            {
                continue;
            }

            t.gameObject.isStatic = true;
        }
    }

    // ===================== 재질 =====================

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
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

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
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (emission.maxColorComponent > 0f)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
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

    private static void MaterialSpec(string name, out Color color, out float smoothness, out float metallic, out Color emission, out string texture)
    {
        smoothness = 0.15f;
        metallic = 0f;
        emission = Color.black;
        texture = null;
        switch (name)
        {
            case "K_Commercial": color = new Color(0.85f, 0.85f, 0.87f); texture = KitFolder(Kit.Commercial) + "Textures/colormap.png"; break;
            case "K_CommercialA": color = new Color(0.85f, 0.85f, 0.87f); texture = KitFolder(Kit.Commercial) + "Textures/variation-a.png"; break;
            case "K_CommercialB": color = new Color(0.85f, 0.85f, 0.87f); texture = KitFolder(Kit.Commercial) + "Textures/variation-b.png"; break;
            case "K_Roads": color = new Color(0.78f, 0.78f, 0.8f); smoothness = 0.25f; texture = KitFolder(Kit.Roads) + "Textures/colormap.png"; break;
            case "K_Cars": color = new Color(0.82f, 0.82f, 0.82f); smoothness = 0.45f; texture = KitFolder(Kit.Cars) + "Textures/colormap.png"; break;
            case "Asphalt": color = new Color(0.14f, 0.14f, 0.15f); smoothness = 0.25f; break;
            case "Concrete": color = new Color(0.45f, 0.44f, 0.42f); break;
            case "Tile": color = new Color(0.38f, 0.37f, 0.35f); smoothness = 0.4f; break;
            case "RoofDark": color = new Color(0.12f, 0.12f, 0.13f); break;
            case "SafeWall": color = new Color(0.36f, 0.4f, 0.36f); break;
            case "SafeDoor": color = new Color(0.55f, 0.06f, 0.04f); metallic = 0.4f; smoothness = 0.35f; break;
            case "Cot": color = new Color(0.25f, 0.3f, 0.2f); break;
            case "Wood": color = new Color(0.33f, 0.22f, 0.12f); break;
            case "Military": color = new Color(0.2f, 0.26f, 0.15f); break;
            case "MedRed": color = new Color(0.8f, 0.1f, 0.1f); break;
            case "Sandbag": color = new Color(0.45f, 0.4f, 0.3f); break;
            case "Charred": color = new Color(0.07f, 0.065f, 0.06f); smoothness = 0.05f; break;
            case "LineWhite": color = new Color(0.7f, 0.7f, 0.68f); break;
            case "LineYellow": color = new Color(0.85f, 0.7f, 0.1f); break;
            case "LampWarm": color = new Color(0.9f, 0.8f, 0.6f); emission = new Color(2.5f, 2f, 1.3f); break;
            case "LampRed": color = new Color(0.5f, 0.05f, 0.05f); emission = new Color(2.5f, 0.15f, 0.1f); break;
            case "LampGreen": color = new Color(0.1f, 0.5f, 0.15f); emission = new Color(0.2f, 2.2f, 0.5f); break;
            case "Grass": color = new Color(0.2f, 0.32f, 0.15f); smoothness = 0.05f; break;
            case "Leaves": color = new Color(0.16f, 0.3f, 0.12f); smoothness = 0.1f; break;
            case "LeavesDark": color = new Color(0.11f, 0.22f, 0.09f); smoothness = 0.1f; break;
            case "Bark": color = new Color(0.22f, 0.15f, 0.09f); break;
            case "Brick": color = new Color(0.38f, 0.2f, 0.15f); smoothness = 0.08f; break;
            case "Water": color = new Color(0.12f, 0.22f, 0.28f); smoothness = 0.95f; break;
            case "MetalDark": color = new Color(0.2f, 0.21f, 0.22f); metallic = 0.6f; smoothness = 0.4f; break;
            case "SignGreen": color = new Color(0.05f, 0.35f, 0.15f); smoothness = 0.3f; break;
            default: color = new Color(0.5f, 0.5f, 0.5f); break;
        }
    }

    // ===================== 공통 =====================

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static float Yaw(Vector3 facing)
    {
        return Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
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
