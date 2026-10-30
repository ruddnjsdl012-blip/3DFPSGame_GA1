using UnityEngine;
using UnityEngine.AI;

// 표면/실제 프랍/완전한 경로를 검사한다. 벽 너머로 순간 이동시키지 않는다.
public static class SurvivorNavigation
{
    private static readonly Collider[] Overlaps = new Collider[32];
    private static NavMeshPath _sharedPath;

    public static int WorldMask()
    {
        int mask = ~0;
        int player = LayerMask.NameToLayer("Player");
        int zombie = LayerMask.NameToLayer("Zombie");
        if (player >= 0) mask &= ~(1 << player);
        if (zombie >= 0) mask &= ~(1 << zombie);
        return mask;
    }

    public static bool Sample(Vector3 wanted, Vector3 origin, NavMeshAgent agent, out Vector3 point, NavMeshPath reusable = null)
    {
        point = wanted;
        NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agent != null ? agent.agentTypeID : 0, areaMask = NavMesh.AllAreas };
        if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 1.3f, filter)
            || Mathf.Abs(hit.position.y - wanted.y) > 0.85f || !Free(hit.position))
        {
            return false;
        }

        if (!NavMesh.SamplePosition(origin, out NavMeshHit start, 1.5f, filter)) return false;
        if (_sharedPath == null) _sharedPath = new NavMeshPath();
        NavMeshPath path = reusable != null ? reusable : _sharedPath;
        if (!NavMesh.CalculatePath(start.position, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        point = hit.position;
        return true;
    }

    public static bool Free(Vector3 feet)
    {
        Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.down, out RaycastHit floor, 0.65f, WorldMask(), QueryTriggerInteraction.Ignore);
        int count = Physics.OverlapCapsuleNonAlloc(feet + Vector3.up * 0.4f, feet + Vector3.up * 1.45f,
            0.33f, Overlaps, WorldMask(), QueryTriggerInteraction.Ignore);
        if (count == Overlaps.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider collider = Overlaps[i];
            if (collider == null || collider.GetComponentInParent<Player>() != null || collider.GetComponentInParent<Zombie>() != null
                || collider.GetComponentInParent<SurvivorCompanion>() != null)
            {
                continue;
            }

            if (collider == floor.collider && floor.normal.y > 0.65f) continue;
            return false;
        }
        return true;
    }
}
