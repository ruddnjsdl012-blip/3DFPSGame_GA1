using UnityEngine;
using UnityEngine.AI;

// 플레이어의 공중 좌표 대신 발밑의 걸을 수 있는 지점을 공유한다.
// 같은 프레임의 같은 목표/에이전트 설정은 물리 검사와 NavMesh 검사를 다시 하지 않는다.
public static class ZombieNavigationTarget
{
    private static Transform _target;
    private static int _frame = -1;
    private static int _agentType;
    private static int _areaMask;
    private static int _ignoredLayers;
    private static bool _valid;
    private static Vector3 _position;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _target = null;
        _frame = -1;
        _valid = false;
    }

    public static bool TryGet(Transform target, int agentType, int areaMask, int zombieLayer, out Vector3 position)
    {
        position = Vector3.zero;
        if (target == null)
        {
            return false;
        }

        int ignoredLayers = (1 << target.gameObject.layer) | (1 << zombieLayer);
        if (_frame == Time.frameCount && _target == target && _agentType == agentType
            && _areaMask == areaMask && _ignoredLayers == ignoredLayers)
        {
            position = _position;
            return _valid;
        }

        _frame = Time.frameCount;
        _target = target;
        _agentType = agentType;
        _areaMask = areaMask;
        _ignoredLayers = ignoredLayers;

        Vector3 wanted = target.position;
        // 점프 중에도 플레이어 바로 아래의 바닥을 향한다. 다른 층의 가까운 면으로 잘못 붙지 않게 한다.
        if (Physics.Raycast(wanted + Vector3.up * 0.2f, Vector3.down, out RaycastHit floor,
            20f, ~ignoredLayers, QueryTriggerInteraction.Ignore))
        {
            wanted = floor.point;
        }

        NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = areaMask };
        _valid = NavMesh.SamplePosition(wanted, out NavMeshHit hit, 2f, filter);
        if (!_valid)
        {
            _valid = NavMesh.SamplePosition(wanted, out hit, 4f, filter);
        }

        _position = _valid ? hit.position : wanted;
        position = _position;
        return _valid;
    }
}
