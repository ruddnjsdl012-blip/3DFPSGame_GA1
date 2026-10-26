using UnityEngine;
using UnityEngine.AI;

// 좀비 한 마리가 목표까지 벽을 피해 가는 방향을 알려 준다.
//  1) NavMesh로 길(꺾이는 지점 목록)을 계산하고, 다음 꺾이는 지점을 향해 간다.
//  2) 다른 좀비나 물체에 막혀 제자리걸음이면 잠깐 옆으로 비켜 간다.
// NavMesh가 없으면 예전처럼 목표를 향해 직선으로 간다.
public class ZombiePathFollower
{
    private const float RepathInterval = 0.3f;    // 길을 다시 계산하는 간격
    private const float CornerReachDistance = 0.4f;
    private const float StuckCheckInterval = 0.5f;
    private const float StuckMoveDistance = 0.15f; // 0.5초 동안 이만큼도 못 움직이면 막힌 것
    private const float SidestepTime = 0.45f;

    // NavMeshPath는 생성자/필드 초기화에서 만들 수 없어서, 처음 길을 계산할 때 만든다.
    private NavMeshPath _path;
    private readonly Vector3[] _corners = new Vector3[32];
    private int _cornerCount;
    private int _cornerIndex;
    private float _repathTimer;
    private Vector3 _lastTarget;

    private float _lastCallTime = -10f;
    private float _stuckTimer;
    private Vector3 _stuckCheckPosition;
    private float _sidestepTimer;
    private Vector3 _sidestepDirection;

    // position에서 target으로 가려면 지금 어느 방향(수평, 길이 1)으로 가야 하는지
    public Vector3 GetDirection(Vector3 position, Vector3 target)
    {
        float dt = Time.deltaTime;

        // 공격/피격 등으로 한동안 안 움직였다면 막힘 검사를 새로 시작한다.
        if (Time.time - _lastCallTime > 0.25f)
        {
            ResetStuck(position);
            _repathTimer = 0f;
        }
        _lastCallTime = Time.time;

        // 일정 간격으로, 또는 목표가 많이 움직이면 길을 다시 계산한다.
        _repathTimer -= dt;
        if (_repathTimer <= 0f || (target - _lastTarget).sqrMagnitude > 4f)
        {
            _repathTimer = RepathInterval + Random.Range(0f, 0.1f); // 좀비마다 계산 시점을 흩뜨린다.
            Recalculate(position, target);
        }

        // 도착한 꺾이는 지점은 건너뛴다.
        while (_cornerIndex < _cornerCount)
        {
            Vector3 toCorner = _corners[_cornerIndex] - position;
            toCorner.y = 0f;
            if (toCorner.sqrMagnitude > CornerReachDistance * CornerReachDistance)
            {
                break;
            }

            _cornerIndex++;
        }

        Vector3 goal = _cornerIndex < _cornerCount ? _corners[_cornerIndex] : target;
        Vector3 direction = goal - position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        direction.Normalize();
        return AvoidStuck(position, direction, dt);
    }

    private void Recalculate(Vector3 position, Vector3 target)
    {
        _lastTarget = target;
        _cornerCount = 0;
        _cornerIndex = 0;

        if (ZombieNavMesh.IsAvailable == false)
        {
            return;
        }

        if (NavMesh.SamplePosition(position, out NavMeshHit from, 2f, NavMesh.AllAreas) == false
            || NavMesh.SamplePosition(target, out NavMeshHit to, 3f, NavMesh.AllAreas) == false)
        {
            return;
        }

        if (_path == null)
        {
            _path = new NavMeshPath();
        }

        if (NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, _path) == false
            || _path.status == NavMeshPathStatus.PathInvalid)
        {
            return;
        }

        _cornerCount = _path.GetCornersNonAlloc(_corners);
        _cornerIndex = 1; // 0번은 출발점
    }

    private Vector3 AvoidStuck(Vector3 position, Vector3 direction, float dt)
    {
        if (_sidestepTimer > 0f)
        {
            _sidestepTimer -= dt;
            if (_sidestepTimer <= 0f)
            {
                ResetStuck(position);
            }

            return (_sidestepDirection + direction * 0.35f).normalized;
        }

        _stuckTimer += dt;
        if (_stuckTimer >= StuckCheckInterval)
        {
            Vector3 moved = position - _stuckCheckPosition;
            moved.y = 0f;

            if (moved.sqrMagnitude < StuckMoveDistance * StuckMoveDistance)
            {
                // 막혔다: 왼쪽 또는 오른쪽으로 잠깐 비켜 가고, 길도 다시 계산한다.
                float side = Random.value < 0.5f ? -1f : 1f;
                _sidestepDirection = Vector3.Cross(Vector3.up, direction) * side;
                _sidestepTimer = SidestepTime;
                _repathTimer = 0f;
            }

            _stuckTimer = 0f;
            _stuckCheckPosition = position;
        }

        return direction;
    }

    private void ResetStuck(Vector3 position)
    {
        _stuckTimer = 0f;
        _sidestepTimer = 0f;
        _stuckCheckPosition = position;
    }
}
