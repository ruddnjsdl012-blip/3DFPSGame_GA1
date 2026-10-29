using UnityEngine;
using UnityEngine.AI;

// NavMesh 경계와 실제 프랍 콜라이더를 함께 검사한다. 밀치기/끼임 복구가 벽을 통과하지 않게 공유한다.
public static class ZombieNavigationClearance
{
    private static readonly Collider[] Nearby = new Collider[32];
    private static readonly RaycastHit[] CastHits = new RaycastHit[32];

    public static bool IsWorld(Collider collider, Transform owner)
    {
        return collider != null && collider.enabled && !collider.isTrigger
            && !collider.transform.IsChildOf(owner)
            && collider.GetComponentInParent<Zombie>() == null
            && collider.GetComponentInParent<Player>() == null;
    }

    private static int WorldMask(Transform owner)
    {
        int mask = ~(1 << owner.gameObject.layer);
        int playerLayer = LayerMask.NameToLayer("Player");
        int zombieLayer = LayerMask.NameToLayer("Zombie");
        if (playerLayer >= 0) mask &= ~(1 << playerLayer);
        if (zombieLayer >= 0) mask &= ~(1 << zombieLayer);
        return mask;
    }

    public static void Capsule(CapsuleCollider body, Vector3 root, out Vector3 bottom, out Vector3 top, out float radius)
    {
        Vector3 scale = body.transform.lossyScale;
        radius = Mathf.Max(0.05f, body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
        float height = Mathf.Max(radius * 2f, body.height * Mathf.Abs(scale.y));
        Vector3 center = root + body.transform.TransformVector(body.center);
        // 바닥 접촉을 장애물로 세지 않게 밑면만 4cm 올린다.
        bottom = center - Vector3.up * (height * 0.5f - radius) + Vector3.up * 0.04f;
        top = center + Vector3.up * Mathf.Max(0f, height * 0.5f - radius);
    }

    public static float FootOffset(NavMeshAgent agent)
    {
        return agent.baseOffset * Mathf.Abs(agent.transform.lossyScale.y);
    }

    public static bool HasSpawnRoom(NavMeshAgent agent, Vector3 feet)
    {
        if (agent == null) return false;
        float radius = Mathf.Max(0.25f, agent.radius) + 0.04f;
        float height = Mathf.Max(radius * 2f + 0.05f, agent.height);
        Vector3 bottom = feet + Vector3.up * (radius + 0.05f);
        Vector3 top = feet + Vector3.up * (height - radius);
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, Nearby,
            WorldMask(agent.transform), QueryTriggerInteraction.Ignore);
        Collider[] hits = Nearby;
        if (count == Nearby.Length)
        {
            hits = Physics.OverlapCapsule(bottom, top, radius, WorldMask(agent.transform), QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        for (int i = 0; i < count; i++)
        {
            if (IsWorld(hits[i], agent.transform)) return false;
        }

        return true;
    }

    public static bool TryRecoverPlacement(NavMeshAgent agent, CapsuleCollider body, out Vector3 root)
    {
        root = agent.transform.position;
        if (body == null) return false;
        Vector3 feet = root - Vector3.up * FootOffset(agent);
        NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        for (int i = 0; i < 17; i++)
        {
            float radius = i == 0 ? 0f : i <= 8 ? 0.75f : 1.5f;
            Vector3 wanted = feet + Quaternion.AngleAxis((i - 1) * 45f, Vector3.up) * Vector3.forward * radius;
            if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 0.75f, filter)
                || Mathf.Abs(hit.position.y - feet.y) > 0.6f)
            {
                continue;
            }

            Vector3 candidate = hit.position + Vector3.up * FootOffset(agent);
            if ((candidate - root).sqrMagnitude > 4f || !IsClear(agent, body, candidate)
                || !RecoverySweepClear(agent, body, candidate))
            {
                continue;
            }

            root = candidate;
            return true;
        }

        return false;
    }

    private static bool RecoverySweepClear(NavMeshAgent agent, CapsuleCollider body, Vector3 destination)
    {
        Vector3 root = agent.transform.position;
        Vector3 offset = destination - root;
        float distance = offset.magnitude;
        if (distance < 0.001f) return true;
        Capsule(body, root, out Vector3 bottom, out Vector3 top, out float radius);
        int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, offset / distance, CastHits, distance,
            WorldMask(agent.transform), QueryTriggerInteraction.Ignore);
        RaycastHit[] hits = CastHits;
        if (count == CastHits.Length)
        {
            hits = Physics.CapsuleCastAll(bottom, top, radius, offset / distance, distance,
                WorldMask(agent.transform), QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsWorld(hit.collider, agent.transform) || hit.normal.y > 0.65f) continue;
            Collider other = hit.collider;
            // 현재 이미 겹친 프랍 밖으로 나오는 것은 허용하되, 사이의 다른 벽을 건너뛰지 않는다.
            if (!Physics.ComputePenetration(body, root, body.transform.rotation,
                other, other.transform.position, other.transform.rotation, out Vector3 direction, out float depth))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsClear(NavMeshAgent agent, CapsuleCollider body, Vector3 root)
    {
        if (body == null) return false;
        Capsule(body, root, out Vector3 bottom, out Vector3 top, out float radius);
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, Nearby,
            WorldMask(agent.transform), QueryTriggerInteraction.Ignore);
        Collider[] hits = Nearby;
        if (count == Nearby.Length)
        {
            hits = Physics.OverlapCapsule(bottom, top, radius, WorldMask(agent.transform), QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        for (int i = 0; i < count; i++)
        {
            Collider other = hits[i];
            if (!IsWorld(other, agent.transform)) continue;
            if (Physics.ComputePenetration(body, root, body.transform.rotation,
                other, other.transform.position, other.transform.rotation, out Vector3 direction, out float depth))
            {
                // 경사로나 계단의 작은 바닥 접촉은 허용한다. 옆면/천장 접촉은 허용하지 않는다.
                if (direction.y > 0.65f && depth < 0.3f) continue;
                return false;
            }
        }

        return true;
    }

    public static bool TryMove(NavMeshAgent agent, CapsuleCollider body, Vector3 offset)
    {
        if (agent == null || body == null || !agent.enabled || !agent.isOnNavMesh || agent.isOnOffMeshLink) return false;
        offset.y = 0f;
        float distance = offset.magnitude;
        if (distance < 0.001f) return false;

        Vector3 direction = offset / distance;
        Vector3 root = agent.transform.position;
        if (agent.Raycast(root + offset, out NavMeshHit edge))
        {
            Vector3 toEdge = edge.position - (root - Vector3.up * FootOffset(agent));
            toEdge.y = 0f;
            distance = Mathf.Min(distance, Mathf.Max(0f, Vector3.Dot(toEdge, direction) - 0.04f));
        }

        Capsule(body, root, out Vector3 bottom, out Vector3 top, out float radius);
        int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, direction, CastHits, distance + 0.025f,
            WorldMask(agent.transform), QueryTriggerInteraction.Ignore);
        RaycastHit[] hits = CastHits;
        if (count == CastHits.Length)
        {
            hits = Physics.CapsuleCastAll(bottom, top, radius, direction, distance + 0.025f,
                WorldMask(agent.transform), QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsWorld(hit.collider, agent.transform) || hit.normal.y > 0.65f) continue;
            distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.025f));
        }

        if (distance < 0.002f || !IsClear(agent, body, root + direction * distance)) return false;
        agent.Move(direction * distance);
        return true;
    }

    public static bool TrySeparate(NavMeshAgent agent, CapsuleCollider body)
    {
        if (agent == null || body == null || !agent.enabled || !agent.isOnNavMesh || agent.isOnOffMeshLink) return false;
        Vector3 root = agent.transform.position;
        Capsule(body, root, out Vector3 bottom, out Vector3 top, out float radius);
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, Nearby,
            WorldMask(agent.transform), QueryTriggerInteraction.Ignore);
        Collider[] hits = Nearby;
        if (count == Nearby.Length)
        {
            hits = Physics.OverlapCapsule(bottom, top, radius, WorldMask(agent.transform), QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        Vector3 correction = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Collider other = hits[i];
            if (!IsWorld(other, agent.transform)) continue;
            if (!Physics.ComputePenetration(body, root + correction, body.transform.rotation,
                other, other.transform.position, other.transform.rotation, out Vector3 direction, out float depth))
            {
                continue;
            }

            if (direction.y > 0.65f || direction.y < -0.65f) continue;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                correction += direction.normalized * Mathf.Min(depth + 0.035f, 1.5f);
            }
        }

        if (correction.sqrMagnitude < 0.0001f) return false;
        correction = Vector3.ClampMagnitude(correction, 1.5f);
        // 이미 겹친 콜라이더에서 나오는 보정이다. 다른 장애물 안이나 NavMesh 밖으로 보내지 않는다.
        if (agent.Raycast(root + correction, out NavMeshHit edge)
            || !IsClear(agent, body, root + correction) || !RecoverySweepClear(agent, body, root + correction))
        {
            return false;
        }

        agent.Move(correction);
        return true;
    }
}
