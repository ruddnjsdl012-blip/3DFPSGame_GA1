using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// 좀비가 맵 에셋에 끼는 문제 고치기
// 메뉴: Tools > L4D 맵 > 좀비 끼임 고치기 (콜라이더 + NavMesh 다시 굽기)
//
// 원인: NavMesh는 '콜라이더'를 보고 굽는다. 콜라이더가 없는 모델(낮은 건물 줄, 바리케이드 철망, 원뿔, 잔해,
//       표지판 등)은 NavMesh에 구멍이 안 생겨서, 좀비가 그 모델 '안'으로 길을 찾아 들어가 끼었다.
//       또 쏘면 부서지는 드럼통/상자 자리는 부서진 뒤에도 NavMesh에 구멍이 남아 좀비가 막혔다.
//
// 하는 일
//  1. CityMap 안에서 콜라이더가 없는 모델 중 사람 키 높이에 걸리는 것에 Box Collider를 붙인다.
//     (바닥 타일, 보도, 길바닥 잔해처럼 낮은 것과 머리 위 간판은 그대로 둔다)
//  2. 드럼통/상자는 NavMesh 굽기에서 빼고 NavMesh Obstacle(Carve)을 붙인다. → 부서지면 길이 다시 열린다.
//  3. 씬에 미리 놓인 좀비/플레이어는 NavMesh 굽기에서 뺀다. (그 자리에 구멍이 생기지 않게)
//  4. NavMesh를 다시 굽는다.
// Ctrl+Z로 콜라이더 추가를 되돌릴 수 있다.
[InitializeOnLoad]
public static class ZombieStuckFix
{
    private const float MinHeight = 0.4f;   // 이보다 낮은 모델은 밟고 지나가도 된다.
    private const float HeadHeight = 1.9f;  // 바닥에서 이보다 높이 떠 있는 모델은 걸리지 않는다.

    static ZombieStuckFix()
    {
        EditorApplication.delayCall += AskOnce;
    }

    private static void AskOnce()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string key = "ZombieStuckFix.Asked." + Application.dataPath;
        if (EditorPrefs.GetBool(key, false))
        {
            return;
        }

        EditorPrefs.SetBool(key, true);
        if (EditorUtility.DisplayDialog("좀비 끼임 고치기", "좀비가 맵 모델에 끼지 않도록 콜라이더를 붙이고 NavMesh를 다시 구울까요?\n나중에 Tools > L4D 맵 > 좀비 끼임 고치기로도 할 수 있습니다.", "지금 고치기", "나중에"))
        {
            Fix();
        }
    }

    [MenuItem("Tools/L4D 맵/좀비 끼임 고치기 (콜라이더 + NavMesh 다시 굽기)", priority = 3)]
    public static void Fix()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("좀비 끼임 고치기", "플레이를 멈춘 뒤에 실행해 주세요.", "확인");
            return;
        }

        CityMapInfo map = Object.FindAnyObjectByType<CityMapInfo>();
        if (map == null)
        {
            EditorUtility.DisplayDialog("좀비 끼임 고치기", "CityMap이 있는 씬(GameScene)을 연 뒤 다시 눌러 주세요.", "확인");
            return;
        }

        List<string> log = new List<string>();
        Undo.SetCurrentGroupName("좀비 끼임 고치기");
        int group = Undo.GetCurrentGroup();

        int colliders = AddMissingColliders(map.transform, out List<string> examples);
        log.Add("✔ 콜라이더가 없던 모델 " + colliders + "개에 Box Collider를 붙였습니다." + (examples.Count > 0 ? " (예: " + string.Join(", ", examples) + ")" : string.Empty));

        int obstacles = SetupBreakables(map.gameObject.scene);
        log.Add("✔ 드럼통/상자 " + obstacles + "개: NavMesh 굽기에서 빼고 NavMesh Obstacle(Carve)을 붙였습니다.");

        log.Add(Rebake(map.gameObject));
        Undo.CollapseUndoOperations(group);

        Scene scene = map.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add("✔ " + scene.path + " 저장");

        string body = "좀비 끼임을 고쳤습니다. ▶ Play로 확인해 보세요.\n\n" + string.Join("\n", log);
        Debug.Log("[좀비 끼임 고치기] " + body);
        EditorUtility.DisplayDialog("좀비 끼임 고치기", body, "확인");
    }

    // ---------- 1. 콜라이더 ----------
    private static int AddMissingColliders(Transform root, out List<string> examples)
    {
        examples = new List<string>();
        float groundY = root.position.y;
        int count = 0;
        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            // 이미 자기나 부모(모델 묶음)에 콜라이더가 있으면 그대로
            if (renderer.GetComponentInParent<Collider>(true) != null)
            {
                continue;
            }

            // 쏘면 부서지는 내 프리팹은 따로 처리
            if (renderer.GetComponentInParent<Drum>() != null || renderer.GetComponentInParent<Box>() != null)
            {
                continue;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            Bounds world = renderer.bounds;
            if (world.size.y < MinHeight || world.min.y - groundY > HeadHeight)
            {
                continue;  // 낮은 바닥 장식, 머리 위 간판
            }

            BoxCollider box = Undo.AddComponent<BoxCollider>(renderer.gameObject);
            Bounds local = filter.sharedMesh.bounds;
            box.center = local.center;
            box.size = local.size;
            count++;
            if (examples.Count < 4 && !examples.Contains(Owner(renderer.transform, root)))
            {
                examples.Add(Owner(renderer.transform, root));
            }
        }

        return count;
    }

    private static string Owner(Transform t, Transform root)
    {
        while (t.parent != null && t.parent != root && t.parent.parent != root)
        {
            t = t.parent;
        }

        return t.name;
    }

    // ---------- 2, 3. 드럼통/상자, 씬에 놓인 좀비 ----------
    private static int SetupBreakables(Scene scene)
    {
        Type modifierType = Type.GetType("Unity.AI.Navigation.NavMeshModifier, Unity.AI.Navigation");
        int count = 0;
        List<GameObject> targets = new List<GameObject>();
        foreach (Drum drum in Object.FindObjectsByType<Drum>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            targets.Add(drum.gameObject);
        }

        foreach (Box box in Object.FindObjectsByType<Box>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            targets.Add(box.gameObject);
        }

        foreach (GameObject target in targets)
        {
            if (target.scene != scene)
            {
                continue;
            }

            IgnoreFromBuild(target, modifierType);

            NavMeshObstacle obstacle = target.GetComponent<NavMeshObstacle>();
            if (obstacle == null)
            {
                obstacle = Undo.AddComponent<NavMeshObstacle>(target);
            }

            obstacle.shape = NavMeshObstacleShape.Box;
            Collider collider = target.GetComponentInChildren<Collider>();
            if (collider != null)
            {
                Bounds b = collider.bounds;
                obstacle.center = target.transform.InverseTransformPoint(b.center);
                Vector3 scale = target.transform.lossyScale;
                obstacle.size = new Vector3(b.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)), b.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)), b.size.z / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
            }

            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            count++;
        }

        // 씬에 미리 놓아 둔 좀비도 굽기에서 뺀다.
        foreach (Zombie zombie in Object.FindObjectsByType<Zombie>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (zombie.gameObject.scene == scene)
            {
                IgnoreFromBuild(zombie.gameObject, modifierType);
            }
        }

        return count;
    }

    private static void IgnoreFromBuild(GameObject target, Type modifierType)
    {
        if (modifierType == null)
        {
            return;
        }

        Component modifier = target.GetComponent(modifierType);
        if (modifier == null)
        {
            modifier = Undo.AddComponent(target, modifierType);
        }

        SerializedObject serialized = new SerializedObject(modifier);
        SerializedProperty ignore = serialized.FindProperty("m_IgnoreFromBuild");
        if (ignore != null)
        {
            ignore.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ---------- 4. NavMesh 다시 굽기 ----------
    private static string Rebake(GameObject map)
    {
        Type surfaceType = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
        if (surfaceType == null)
        {
            return "⚠ AI Navigation 패키지가 없어 NavMesh를 굽지 못했습니다.";
        }

        Component surface = map.GetComponent(surfaceType);
        if (surface == null)
        {
            surface = Undo.AddComponent(map, surfaceType);
            SerializedObject created = new SerializedObject(surface);
            SetInt(created, "m_CollectObjects", 2);  // Current Object Hierarchy
            SetInt(created, "m_UseGeometry", 1);     // Physics Colliders
            created.ApplyModifiedPropertiesWithoutUndo();
        }

        // 플레이어/좀비 레이어는 굽기에서 뺀다.
        SerializedObject serialized = new SerializedObject(surface);
        SerializedProperty layerMask = serialized.FindProperty("m_LayerMask");
        if (layerMask != null)
        {
            int exclude = 0;
            foreach (string layer in new[] { "Zombie", "Player" })
            {
                int index = LayerMask.NameToLayer(layer);
                if (index >= 0)
                {
                    exclude |= 1 << index;
                }
            }

            layerMask.intValue &= ~exclude;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        System.Reflection.PropertyInfo dataProperty = surfaceType.GetProperty("navMeshData");
        NavMeshData old = dataProperty?.GetValue(surface) as NavMeshData;
        string oldPath = old != null ? AssetDatabase.GetAssetPath(old) : null;

        surfaceType.GetMethod("BuildNavMesh")?.Invoke(surface, null);
        NavMeshData data = dataProperty?.GetValue(surface) as NavMeshData;
        if (data == null)
        {
            return "⚠ NavMesh 굽기에 실패했습니다. CityMap의 NavMesh Surface에서 Bake를 눌러 주세요.";
        }

        string path = !string.IsNullOrEmpty(oldPath) ? oldPath : Path.ChangeExtension(map.scene.path, null) + "/NavMesh-CityMap.asset";
        string folder = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
        }

        if (AssetDatabase.GetAssetPath(data) != path)
        {
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(data, path);
        }

        dataProperty.SetValue(surface, data);
        EditorUtility.SetDirty(surface);
        AssetDatabase.SaveAssets();
        return "✔ NavMesh를 다시 구웠습니다. (" + path + ")";
    }

    private static void SetInt(SerializedObject serialized, string name, int value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property != null)
        {
            property.intValue = value;
        }
    }
}
