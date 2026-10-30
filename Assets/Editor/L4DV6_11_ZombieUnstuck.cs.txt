using UnityEngine;
using UnityEngine.AI;

// 경로 대기 중/잘못 정지된 경우도 감시한다. 플레이어 쪽으로 순간이동시키지 않는다.
public class ZombieUnstuck : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float _checkInterval = 0.25f;
    [SerializeField, Min(0.5f)] private float _stuckTime = 1f;
    [SerializeField, Min(0.05f)] private float _minMove = 0.15f;

    private NavMeshAgent _agent;
    private Zombie _zombie;
    private CapsuleCollider _body;
    private Vector3 _lastPosition;
    private float _checkTimer;
    private float _stillTime;
    private float _side;
    private int _normalPriority;
    private float _priorityUntil;

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _zombie = GetComponent<Zombie>();
        _body = GetComponent<CapsuleCollider>();
        _lastPosition = transform.position;
        _checkTimer = Random.Range(0f, Mathf.Max(0.1f, _checkInterval));
        _side = Random.value < 0.5f ? -1f : 1f;

        if (_agent != null)
        {
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            _normalPriority = Random.Range(30, 70);
            _agent.avoidancePriority = _normalPriority;
            _agent.autoRepath = true;
        }
    }

    private void Update()
    {
        if (_agent == null || _zombie == null)
        {
            return;
        }

        _checkTimer -= Time.deltaTime;
        if (_checkTimer > 0f)
        {
            return;
        }

        float interval = Mathf.Max(0.1f, _checkInterval);
        _checkTimer = interval;
        Vector3 moved = transform.position - _lastPosition;
        moved.y = 0f;
        _lastPosition = transform.position;

        // 피격/사망/특수 능력 사용처럼 의도적으로 멈춘 동안은 건드리지 않는다.
        if (!_zombie.CanPursue)
        {
            _stillTime = 0f;
            return;
        }

        if (!_agent.enabled || !_agent.isOnNavMesh)
        {
            _zombie.RefreshPursuit();
            return;
        }

        if (_agent.isOnOffMeshLink)
        {
            _stillTime = 0f;
            return;
        }

        if (Time.time >= _priorityUntil && _agent.avoidancePriority != _normalPriority)
        {
            _agent.avoidancePriority = _normalPriority;
        }

        // NavMesh와 실제 프랍 사이의 겹침도 검사한다.
        if (ZombieNavigationClearance.TrySeparate(_agent, _body))
        {
            _stillTime = 0f;
            _zombie.RefreshPursuit();
            return;
        }

        float minimumStep = _minMove * interval / 0.5f;
        if (moved.sqrMagnitude > minimumStep * minimumStep)
        {
            _stillTime = 0f;
            return;
        }

        // isStopped/pathPending 자체가 문제일 수도 있으므로 이 조건들로 감시를 생략하지 않는다.
        _stillTime += interval;
        if (_stillTime < Mathf.Max(0.5f, _stuckTime))
        {
            return;
        }

        _stillTime = 0f;
        // 서로 양보만 하며 통로에 머무는 군중에는 잠깐 다른 회피 우선순위를 준다.
        _agent.avoidancePriority = Random.Range(10, 25);
        _priorityUntil = Time.time + 0.8f;
        if (!_agent.pathPending && !_agent.isStopped)
        {
            TrySmallStep();
        }

        _zombie.RefreshPursuit();
    }

    private void TrySmallStep()
    {
        Vector3 toward = _agent.hasPath ? _agent.steeringTarget - transform.position
            : _zombie.Target.position - transform.position;
        toward.y = 0f;
        toward = toward.sqrMagnitude > 0.01f ? toward.normalized : transform.forward;
        // 양옆과 뒤쪽을 순서대로 검사한다. NavMesh와 실제 충돌이 모두 허용한 방향만 쓴다.
        for (int i = 0; i < 4; i++)
        {
            float angle = (i < 2 ? 65f : 115f) * (i % 2 == 0 ? _side : -_side);
            Vector3 step = Quaternion.AngleAxis(angle, Vector3.up) * toward * 0.55f;
            if (ZombieNavigationClearance.TryMove(_agent, _body, step)) break;
        }

        _side = -_side;
    }
}
