using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// FSM(유한 상태 머신) : 유한개의 상태가 있고, 상태마다 행동을 다르게 함으로써 AI를 구현하는 유명한 방법
// - "상태": 객체가 가질 수 있는 어떤 조건이나 상황
// - "전이": 조건에 따라 현재 상태에서 다른 상태로 넘어가는 것
// - 그리고 위 방식에 따라 규칙을 세워둔 것을 "머신"


// 좀비가 가질 수 있는 상태
public enum ZombieState
{
    Idle,       // 대기: 제자리에서 플레이어를 기다린다.
    Trace,      // 추격: 플레이어를 쫓아간다.
    Comeback,    // 복귀: 처음 자리로 돌아간다.
    Attack,     // 공격: 일정 시간마다 플레이어를 때린다.
    Hit,        // 피격: 공격(총알, 폭탄)을 받아 잠깐 멈칫한다.
    Death,      // 죽음: 쓰러지고 잠시 뒤 사라진다.
}

[RequireComponent(typeof(NavMeshAgent))]
public class Zombie : MonoBehaviour, IDamageable
{
    [Header("스탯")]
    [SerializeField] private float _detectDistance = 8;    // 플레이어를 발견하는 거리
    [SerializeField] private float _combackDistance = 15;   // 처음 자리에서 이만큼 멀어지면 돌아가는 거리
    [SerializeField] private float _attackDamage = 10f;     // 공격 대미지
    [SerializeField] private float _attackRange = 1.5f;     // 공격 범위
    [SerializeField] private float _attackInterval = 1.2f;  // 공격 간격
    [SerializeField] private float _moveSpeed = 6f;         // 일반 좀비 이동속도
    [SerializeField] private float _maxHealth = 30;
    [SerializeField] private float _health;

    [Tooltip("켜면 거리와 상관없이 처음부터 플레이어를 끝까지 쫓는다.")]
    [SerializeField] private bool _alwaysChase = true;

    [Header("추격 반응과 경로 계산")]
    [SerializeField, Min(0.03f)] private float _closeRepathInterval = 0.07f;
    [SerializeField, Min(2f)] private float _closeRepathDistance = 12f;
    [SerializeField, Min(0.05f)] private float _repathInterval = 0.15f;
    [SerializeField, Min(0.1f)] private float _farRepathInterval = 0.3f;
    [SerializeField, Min(5f)] private float _farRepathDistance = 25f;
    [SerializeField, Min(0f)] private float _commonHitStun = 0.18f;

    private static readonly int MoveStateHash = Animator.StringToHash("Base Layer.Move");

    [Header("이펙트")]
    [Tooltip("켜면 예전 Blood Gush 프리팹을 쓴다. 끄면 BloodEffects(튀는 핏방울, 피 안개, 자국, 피 웅덩이)를 쓴다.")]
    [SerializeField] private bool _useBloodPrefab = false;
    [SerializeField] private GameObject _bloodSprayPrefab;

    private float _nextAttackTime;
    private Vector3 _startPosition;
    private GameObject _player;
    private NavMeshAgent _agent;
    private Animator _animator;
    private IDamageable _playerDamageable;
    private SpecialZombie _special;
    private float _nextPathTime;
    private Vector3 _lastDestination;
    private bool _hasDestination;

    // AI 디렉터가 만든 좀비는 거리와 상관없이 플레이어를 끝까지 쫓는다. (L4D 호드)
    private bool _isHunter = false;

    // 맞아도 멈칫하지 않는다. (특수 좀비 탱크)
    private bool _canStagger = true;


    public float MaxHealth => _maxHealth;
    public float Health => _health;
    public bool IsDead => _state == ZombieState.Death;
    public ZombieState State => _state;

    // 좀비가 죽는 순간 발행하는 이벤트 (스포너, UI 등이 구독한다)
    public event Action<Zombie> Died;

    // 좀비가 플레이어를 때리는 순간 발행하는 이벤트 (특수 좀비 탱크가 플레이어를 날려 보낼 때 구독)
    public event Action<Zombie> Attacked;


    private ZombieState _state = ZombieState.Idle;

    private void Start()
    {
        _health = _maxHealth;

        // 길을 찾아 걷게 해주는 컴포넌트 NavMeshAgent를 찾아서 참조
        _agent = GetComponent<NavMeshAgent>();
        _agent.speed = _moveSpeed;

        _animator = GetComponentInChildren<Animator>();
        if (_animator != null)
        {
            // 상태와 공격 이벤트는 계속 계산하고, 화면 밖의 뼈 변환 작업만 줄인다.
            _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }

        _special = GetComponent<SpecialZombie>();
        if (_special == null)
        {
            _agent.acceleration = Mathf.Max(_agent.acceleration, 28f);
            _agent.angularSpeed = Mathf.Max(_agent.angularSpeed, 900f);
        }

        _agent.autoBraking = false;
        _nextPathTime = 0f;
        ZombieHealthBar healthBar = GetComponent<ZombieHealthBar>();
        if (healthBar != null)
        {
            healthBar.SetVisible(_special != null && _special.Type == SpecialType.Tank);
        }

        // 맞거나 죽는 동작 중에 모델이 땅에 파묻히거나 미끄러지지 않게 모델 위치를 고정한다.
        if (GetComponent<ZombieModelLock>() == null)
        {
            gameObject.AddComponent<ZombieModelLock>();
        }

        _startPosition = transform.position;
        _groanTimer = UnityEngine.Random.Range(1f, 6f);

        // 게임을 시작하면 플레이어를 찾는다.
        _player = GameObject.FindGameObjectWithTag("Player");
        _playerDamageable = _player != null ? _player.GetComponent<IDamageable>() : null;
    }


    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing || _player == null)
        {
            StopAgent();
            return;
        }

        UpdateGroan();

        switch (_state)
        {
            case ZombieState.Idle:
                Idle();
                break;

            case ZombieState.Trace:
                Trace();
                break;

            case ZombieState.Comeback:
                Comeback();
                break;

            case ZombieState.Attack:
                Attack();
                break;
        }
    }

    // 대미지를 받는다.
    public void TakeDamage(DamageInfo damageInfo)
    {
        if (_state == ZombieState.Death)
        {
            return;
        }

        _health -= damageInfo.Value;

        StopAllCoroutines();

        if (_health > 0f && _canStagger == false)
        {
            // 멈칫하지 않는 좀비(탱크): 피만 튀고 계속 다가온다.
            SpawnBlood(damageInfo);
            GameSounds.PlayAt("zombie_hurt", transform.position, 0.5f, 0.3f);
        }
        else if (_health > 0f)
        {
            // 맞은 자리에서 피가 튄다.
            SpawnBlood(damageInfo);

            // 잠깐 멈칫 하고 싶다!
            _animator.SetTrigger("Hit");
            GameSounds.PlayAt("zombie_hurt", transform.position, 0.7f, 0.1f);
            _state = ZombieState.Hit;
            StartCoroutine(Hit_Coroutine());
        }
        else
        {
            _animator.SetTrigger("Death");
            SpawnBlood(damageInfo);
            BloodEffects.Death(this);  // 크게 튀고, 쓰러진 자리에 피 웅덩이
            GameSounds.PlayAt("zombie_death", transform.position, 0.9f, 0.1f);
            _state = ZombieState.Death;
            Died?.Invoke(this);
            StartCoroutine(Death_Coroutine());
        }

    }

    private void SpawnBlood(DamageInfo damageInfo)
    {
        if (_useBloodPrefab && _bloodSprayPrefab != null)
        {
            // 예전 방식: 좀비의 자식으로 블러드 스프레이를 생성
            GameObject bloodSpray = Instantiate(_bloodSprayPrefab, transform);
            bloodSpray.transform.position = damageInfo.HitPoint;
            bloodSpray.transform.forward = damageInfo.HitNormal;
            return;
        }

        BloodEffects.Hit(this, damageInfo);
    }

    private IEnumerator Hit_Coroutine()
    {
        StopAgent();
        yield return new WaitForSeconds(_special == null ? _commonHitStun : 0.5f);

        // 일반 좀비는 짧게 움찔한 뒤 현재 플레이어 위치를 곧바로 다시 노린다.
        ResumeChase();
    }

    private IEnumerator Death_Coroutine()
    {
        StopAgent();
        yield return new WaitForSeconds(2f);
        Destroy(gameObject);
    }

    // 대기: 제자리에서 플레이어를 기다린다.
    private void Idle()
    {
        StopAgent();

        // 플레이어 가까워지면 Trace 상태로 전이한다.
        float distanceSquared = (transform.position - _player.transform.position).sqrMagnitude;
        if (_isHunter || _alwaysChase || distanceSquared <= _detectDistance * _detectDistance)
        {
            GameSounds.PlayAt("zombie_alert", transform.position, 0.8f, 0.5f);

            ResumeChase();
        }
    }

    // 추격: 플레이어를 쫓아간다.
    private void Trace()
    {
        // 처음 자리에서 너무 멀어지면 돌아간다.
        if (_isHunter == false && _alwaysChase == false && (transform.position - _startPosition).sqrMagnitude > _combackDistance * _combackDistance)
        {
            _state = ZombieState.Comeback;
            return;
        }

        // 플레이어가 공격 거리 안이면 공격한다.
        if ((transform.position - _player.transform.position).sqrMagnitude <= _attackRange * _attackRange)
        {
            _animator.ResetTrigger("AttackMove");
            _animator.SetTrigger("MoveAttackDelay");
            _state = ZombieState.Attack;
            Attack();
            return;
        }

        // SetDestination은 목적지 설정
        // - 호출할 때마다 목적지를 향해 조금씩 이동 (X)
        // - 한 번 세팅하면 내부에서 계속해서 이동  (O)
        RequestPath(_player.transform.position);
    }

    // 복귀: 처음 자리로 돌아간다.
    private void Comeback()
    {
        // 처음 자리에 도착하면 Idle
        if ((transform.position - _startPosition).sqrMagnitude < _agent.stoppingDistance * _agent.stoppingDistance)
        {
            _animator.SetTrigger("MoveToIdle");
            _state = ZombieState.Idle;
            return;
        }

        // 처음 자리로 돌아간다
        RequestPath(_startPosition);
    }

    // 공격: 일정 시간마다 플레이어를 때린다.
    private void Attack()
    {
        StopAgent();

        // 플레이어가 공격 사정 거리 밖으로 나가면 쫓아!
        float distanceSquared = (transform.position - _player.transform.position).sqrMagnitude;
        if (distanceSquared > _attackRange * _attackRange)
        {
            ResumeChase();
            return;
        }

        if (_special != null && _special.IsUsingAbility)
        {
            return;
        }

        Vector3 look = _player.transform.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), _agent.angularSpeed * Time.deltaTime);
        }

        // 첫 공격은 바로 시작한다. 공격을 취소하고 다시 접근해도 기존 재공격 간격은 지킨다.
        if (Time.time >= _nextAttackTime)
        {
            _animator.SetTrigger("StartAttack");
            GameSounds.PlayAt("zombie_attack", transform.position, 0.8f, 0.15f);

            _nextAttackTime = Time.time + _attackInterval;
        }
    }

    public void AttackAction()
    {
        if (_state != ZombieState.Attack || _player == null || _playerDamageable == null || GameManager.Instance == null
            || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        float reach = _attackRange + 0.15f;
        if ((transform.position - _player.transform.position).sqrMagnitude > reach * reach
            || (_special != null && _special.IsUsingAbility))
        {
            return;
        }

        DamageInfo info = new DamageInfo()
        {
            Value = _attackDamage,
            HitPoint = _player.transform.position,
            HitNormal = (transform.position - _player.transform.position).normalized,
        };

        _playerDamageable.TakeDamage(info);
        Attacked?.Invoke(this);
    }

    // 가끔 신음 소리를 낸다. (쫓아올 때 더 자주, 같은 소리가 겹치지 않게 전체에서 0.4초 간격)
    private float _groanTimer;

    private void UpdateGroan()
    {
        if (_state == ZombieState.Death)
        {
            return;
        }

        _groanTimer -= Time.deltaTime;
        if (_groanTimer > 0f)
        {
            return;
        }

        _groanTimer = _state == ZombieState.Idle ? UnityEngine.Random.Range(5f, 11f) : UnityEngine.Random.Range(2.5f, 6f);
        GameSounds.PlayAt("zombie_groan", transform.position, 0.7f, 0.4f);
    }

    // AI 디렉터가 부른다. 감지 거리와 복귀 거리를 무시하고 플레이어를 쫓는다.
    public void BeginHunt()
    {
        _isHunter = true;
        _nextPathTime = 0f;
    }

    // 특수 좀비 능력치. Instantiate 바로 뒤(Start 전에) 부른다. (SpecialZombie가 부른다)
    public void SetupSpecial(float maxHealth, float moveSpeed, float attackDamage, float attackRange, float attackInterval, bool canStagger)
    {
        _maxHealth = maxHealth;
        _health = maxHealth;
        _moveSpeed = moveSpeed;
        _attackDamage = attackDamage;
        _attackRange = attackRange;
        _attackInterval = attackInterval;
        _canStagger = canStagger;
        if (_agent != null)
        {
            _agent.speed = moveSpeed;
        }
    }

    private void StopAgent()
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh || _agent.isStopped)
        {
            return;
        }

        _agent.isStopped = true;
        if (_agent.hasPath || _agent.pathPending)
        {
            _agent.ResetPath();
        }

        _agent.velocity = Vector3.zero;
        _hasDestination = false;
    }

    private void ResumeChase()
    {
        ZombieState previousState = _state;
        _state = ZombieState.Trace;
        _hasDestination = false;
        _nextPathTime = 0f;

        if (_animator != null)
        {
            // 달리기 복귀 때 남아 있던 공격 트리거와 긴 전환 때문에 서 있는 모습을 방지한다.
            _animator.ResetTrigger("StartAttack");
            _animator.ResetTrigger("MoveAttackDelay");
            _animator.ResetTrigger("MoveToIdle");
            _animator.ResetTrigger("Hit");
            _animator.ResetTrigger("IdleToMove");
            _animator.ResetTrigger("AttackMove");
            if (_animator.HasState(0, MoveStateHash))
            {
                _animator.CrossFadeInFixedTime(MoveStateHash, 0.06f, 0);
            }
            else
            {
                _animator.SetTrigger(previousState == ZombieState.Idle ? "IdleToMove" : "AttackMove");
            }
        }

        if (_player != null && GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
        {
            RequestPath(_player.transform.position, true);
        }
    }

    private void RequestPath(Vector3 destination, bool force = false)
    {
        if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh || _agent.pathPending
            || (_special != null && _special.IsUsingAbility))
        {
            return;
        }

        float distanceSquared = (transform.position - destination).sqrMagnitude;
        bool targetMoved = !_hasDestination || (destination - _lastDestination).sqrMagnitude >= 0.01f;
        bool reachedOldTarget = _hasDestination && targetMoved && (!_agent.hasPath
            || _agent.remainingDistance <= _agent.stoppingDistance + 0.35f);
        if (!force && !reachedOldTarget && Time.time < _nextPathTime)
        {
            return;
        }

        float interval = distanceSquared <= _closeRepathDistance * _closeRepathDistance ? _closeRepathInterval
            : distanceSquared > _farRepathDistance * _farRepathDistance ? _farRepathInterval : _repathInterval;
        _nextPathTime = Time.time + Mathf.Max(0.03f, interval) + UnityEngine.Random.Range(0f, 0.01f);
        _agent.isStopped = false;

        // 이전 목적지에 도착하면 갱신 타이머를 기다리지 않는다. 정지한 목표의 경로만 재사용한다.
        if (_hasDestination && _agent.hasPath && !_agent.isPathStale && !targetMoved)
        {
            return;
        }

        if (_agent.SetDestination(destination))
        {
            _lastDestination = destination;
            _hasDestination = true;
        }
    }
}
