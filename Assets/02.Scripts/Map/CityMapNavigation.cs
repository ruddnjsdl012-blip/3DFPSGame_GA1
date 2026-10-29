using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

// 씬 파일을 덮어쓰지 않고, 로딩 시 한 번 현재 맵의 콜라이더로 길을 만든다.
public static class CityMapNavigation
{
    public static void Prepare(CityMapInfo map)
    {
        if (map == null) return;
        int added = 0;
        foreach (MeshFilter filter in map.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null || filter.GetComponent<Renderer>() == null
                || filter.GetComponentInParent<Zombie>() != null || filter.GetComponentInParent<Player>() != null
                || HasSolidParent(filter.transform, map.transform))
            {
                continue;
            }

            Renderer renderer = filter.GetComponent<Renderer>();
            if (renderer.bounds.size.y < 0.35f) continue;
            BoxCollider collider = filter.gameObject.AddComponent<BoxCollider>();
            collider.center = filter.sharedMesh.bounds.center;
            collider.size = filter.sharedMesh.bounds.size;
            added++;
        }

        // 자동차/나무/바리케이드/건물의 윗면을 목적지로 사용하지 않는다.
        // Ground와 Elevated는 보존하므로 주차타워·고가도로·경사로는 계속 걸을 수 있다.
        MarkBlocked(map.transform.Find("Props"));
        MarkBlocked(map.transform.Find("Buildings"));
        PrepareBreakables(map);

        NavMeshSurface surface = map.GetComponent<NavMeshSurface>();
        if (surface == null) surface = map.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        int excluded = 0;
        foreach (string layer in new[] { "Player", "Zombie" })
        {
            int index = LayerMask.NameToLayer(layer);
            if (index >= 0) excluded |= 1 << index;
        }

        surface.layerMask = surface.layerMask.value & ~excluded;
        NavMeshBuildSettings settings = surface.GetBuildSettings();
        surface.overrideVoxelSize = true;
        surface.voxelSize = Mathf.Clamp(settings.agentRadius / 4f, 0.06f, 0.12f);
        surface.overrideTileSize = true;
        surface.tileSize = 128;
        Physics.SyncTransforms();
        NavMeshData previousData = surface.navMeshData;
        surface.BuildNavMesh();
        if (surface.navMeshData == null || surface.navMeshData == previousData)
        {
            Debug.LogError("[CityMapNavigation] 맵 경로 생성에 실패했습니다. CityMap의 NavMeshSurface 설정을 확인하세요.", map);
        }
        else
        {
            Debug.Log("[CityMapNavigation] 현재 맵 콜라이더로 경로 생성 완료 (보강 콜라이더 " + added + "개).", map);
        }
    }

    private static bool HasSolidParent(Transform target, Transform root)
    {
        while (target != null)
        {
            foreach (Collider collider in target.GetComponents<Collider>())
            {
                if (collider.enabled && !collider.isTrigger) return true;
            }

            if (target == root) break;
            target = target.parent;
        }

        return false;
    }

    private static void MarkBlocked(Transform group)
    {
        if (group == null) return;
        foreach (Collider collider in group.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger || collider.bounds.size.y < 0.35f
                || collider.GetComponentInParent<Drum>() != null || collider.GetComponentInParent<Box>() != null)
            {
                continue;
            }

            NavMeshModifier modifier = collider.GetComponent<NavMeshModifier>();
            if (modifier == null) modifier = collider.gameObject.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = 1; // Unity 기본 Not Walkable
        }
    }

    private static void PrepareBreakables(CityMapInfo map)
    {
        foreach (Drum drum in Object.FindObjectsByType<Drum>(FindObjectsSortMode.None))
        {
            if (drum.gameObject.scene == map.gameObject.scene) Breakable(drum.gameObject);
        }

        foreach (Box box in Object.FindObjectsByType<Box>(FindObjectsSortMode.None))
        {
            if (box.gameObject.scene == map.gameObject.scene) Breakable(box.gameObject);
        }
    }

    private static void Breakable(GameObject target)
    {
        Collider collider = target.GetComponentInChildren<Collider>();
        if (collider == null || collider.isTrigger) return;
        NavMeshModifier modifier = target.GetComponent<NavMeshModifier>();
        if (modifier == null) modifier = target.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;
        NavMeshObstacle obstacle = target.GetComponent<NavMeshObstacle>();
        if (obstacle == null) obstacle = target.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        Bounds bounds = collider.bounds;
        obstacle.center = target.transform.InverseTransformPoint(bounds.center);
        Vector3 scale = target.transform.lossyScale;
        obstacle.size = new Vector3(bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)), bounds.size.z / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;
        obstacle.carvingTimeToStationary = 0.1f;
    }
}
