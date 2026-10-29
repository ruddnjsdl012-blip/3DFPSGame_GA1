using UnityEngine;
using UnityEngine.AI;

// 좀비가 어딘가에 끼어서 못 움직이면 빼 준다. (AIDirector가 만든 좀비에 붙는다)
//  - NavMesh 밖에 있으면(벽 속에 생겼거나 밀려났을 때) 가장 가까운 NavMesh 위로 옮긴다.
//  - 쫓아가는 중인데 2초 넘게 거의 못 움직이면:
//      1번째: 길을 다시 찾게 하고 살짝 옆으로 비켜 준다.
//      2번째부터: 플레이어 화면에 안 보이면 가던 길의 다음 모퉁이로 옮긴다. (보이면 조금씩만 밀어 준다)
public class ZombieUnstuck : MonoBehaviour
{
    [SerializeField] private float _checkInterval = 0.5f;
    [SerializeField] private float _stuckTime = 2f;          // 이만큼 못 움직이면 끼었다고 본다.
    [SerializeField] private float _minMove = 0.35f;         // 그동안 이보다 덜 움직였으면

    private NavMeshAgent _agent;
    private Zombie _zombie;
    private Transform _player;
    private Camera _camera;
    private Vector3 _lastPosition;
    private float _checkTimer;
    private float _stillTime;
    private int _strikes;

    private void Start()
    {
        _agent = GetComponent<NavMeshAgent>();
        _zombie = GetComponent<Zombie>();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        _player = player != null ? player.transform : null;
        _camera = Camera.main;
        _lastPosition = transform.position;
        _checkTimer = Random.Range(0f, Mathf.Max(0.1f, _checkInterval));

        // 붐비는 곳에서 서로 밀어 벽으로 끼지 않게: 좋은 회피, 그리고 서로 다른 우선순위
        if (_agent != null)
        {
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            _agent.avoidancePriority = Random.Range(30, 70);
            _agent.autoRepath = true;
        }
    }

    private void Update()
    {
        if (_agent == null || _zombie == null || _player == null || _zombie.IsDead || !_agent.enabled
            || GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        _checkTimer -= Time.deltaTime;
        if (_checkTimer > 0f)
        {
            return;
        }

        _checkTimer = _checkInterval;

        // 1. NavMesh 밖이면 가까운 NavMesh 위로
        if (!_agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
            }

            return;
        }

        // 2. 쫓아가는 중에만 끼임을 본다. (공격, 멈칫, 대기 중에는 멈춰 있는 게 정상)
        bool chasing = _zombie.State == ZombieState.Trace && !_agent.isStopped && !_agent.pathPending;
        float toPlayer = Vector3.Distance(transform.position, _player.position);
        float moved = Vector3.Distance(Flat(transform.position), Flat(_lastPosition));
        _lastPosition = transform.position;

        if (!chasing || toPlayer < 3f || moved > _minMove * (_checkInterval / _stuckTime) * 2f)
        {
            _stillTime = 0f;
            if (moved > 1f)
            {
                _strikes = 0;
            }

            return;
        }

        _stillTime += _checkInterval;
        if (_stillTime < _stuckTime)
        {
            return;
        }

        _stillTime = 0f;
        _strikes++;
        Unstick();
    }

    private void Unstick()
    {
        Vector3 next = NextCorner();
        Vector3 toward = Flat(next - transform.position);
        toward = toward.sqrMagnitude > 0.01f ? toward.normalized : transform.forward;

        if (_strikes == 1 || IsVisible())
        {
            // 살짝 옆(모퉁이 쪽)으로 비켜 주고 길을 다시 찾는다.
            Vector3 side = Vector3.Cross(Vector3.up, toward) * (Random.value < 0.5f ? -1f : 1f);
            Vector3 nudge = transform.position + toward * 0.6f + side * 0.5f;
            if (NavMesh.SamplePosition(nudge, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
            }

            _agent.ResetPath();
            _agent.SetDestination(_player.position);
            return;
        }

        // 안 보이면 가던 길의 다음 모퉁이로 옮긴다.
        if (NavMesh.SamplePosition(next, out NavMeshHit corner, 2f, NavMesh.AllAreas))
        {
            _agent.Warp(corner.position);
            _agent.SetDestination(_player.position);
        }
    }

    private Vector3 NextCorner()
    {
        if (_agent.hasPath && !_agent.pathPending)
        {
            // path/corners 복사 대신 에이전트가 이미 계산한 다음 모퉁이를 쓴다.
            return _agent.steeringTarget;
        }

        return _player.position;
    }

    private bool IsVisible()
    {
        if (_camera == null)
        {
            return false;
        }

        Vector3 target = transform.position + Vector3.up * 1.2f;
        Vector3 viewport = _camera.WorldToViewportPoint(target);
        if (viewport.z <= 0f || viewport.x < -0.05f || viewport.x > 1.05f || viewport.y < -0.05f || viewport.y > 1.05f)
        {
            return false;
        }

        if (Physics.Linecast(_camera.transform.position, target, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == transform || hit.transform.IsChildOf(transform);
        }

        return true;
    }

    private static Vector3 Flat(Vector3 value)
    {
        value.y = 0f;
        return value;
    }
}
