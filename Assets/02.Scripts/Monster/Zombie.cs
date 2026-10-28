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
    [SerializeField] private float _moveSpeed = 5;          // 이동속도
    [SerializeField] private float _maxHealth = 30;
    [SerializeField] private float _health;

    [Tooltip("켜면 거리와 상관없이 처음부터 플레이어를 끝까지 쫓는다.")]
    [SerializeField] private bool _alwaysChase = true;

    [Header("이펙트")]
    [Tooltip("켜면 예전 Blood Gush 프리팹을 쓴다. 끄면 BloodEffects(튀는 핏방울, 피 안개, 자국, 피 웅덩이)를 쓴다.")]
    [SerializeField] private bool _useBloodPrefab = false;
    [SerializeField] private GameObject _bloodSprayPrefab;

    private float _attackTimer = 0f;
    private Vector3 _startPosition;
    private GameObject _player;
    private NavMeshAgent _agent;
    private Animator _animator;

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

        _startPosition = transform.position;
        _groanTimer = UnityEngine.Random.Range(1f, 6f);

        // 게임을 시작하면 플레이어를 찾는다.
        _player = GameObject.FindGameObjectWithTag("Player");
    }


    private void Update()
    {
        if (GameManager.Instance.State != GameState.Playing) return;

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
            Debug.Log($"{_state} -> Hit");
            _animator.SetTrigger("Hit");
            GameSounds.PlayAt("zombie_hurt", transform.position, 0.7f, 0.1f);
            _state = ZombieState.Hit;
            StartCoroutine(Hit_Coroutine());
        }
        else
        {
            Debug.Log($"{_state} -> Death");
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
        yield return new WaitForSeconds(0.5f); // 잠깐 멈칫한다!

        // 맞았으니 쫒아온다.
        Debug.Log($"{_state} -> Trace");
        _state = ZombieState.Trace;
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
        float distance = Vector3.Distance(transform.position, _player.transform.position);
        if (_isHunter || _alwaysChase || distance <= _detectDistance)
        {
            GameSounds.PlayAt("zombie_alert", transform.position, 0.8f, 0.5f);
            Debug.Log($"{_state} -> Trace");
            _animator.SetTrigger("IdleToMove");
            _state = ZombieState.Trace;
        }
    }

    // 추격: 플레이어를 쫓아간다.
    private void Trace()
    {
        // 처음 자리에서 너무 멀어지면 돌아간다.
        if (_isHunter == false && _alwaysChase == false && Vector3.Distance(transform.position, _startPosition) > _combackDistance)
        {
            Debug.Log($"{_state} -> Comeback");
            _state = ZombieState.Comeback;
            return;
        }

        // 플레이어가 공격 거리 안이면 공격한다.
        if (Vector3.Distance(transform.position, _player.transform.position) <= _attackRange)
        {
            _attackTimer = 0f;
            Debug.Log($"{_state} -> Attack");
            _animator.SetTrigger("MoveAttackDelay");
            _state = ZombieState.Attack;
            return;
        }

        // SetDestination은 목적지 설정
        // - 호출할 때마다 목적지를 향해 조금씩 이동 (X)
        // - 한 번 세팅하면 내부에서 계속해서 이동  (O)
        _agent.SetDestination(_player.transform.position);
    }

    // 복귀: 처음 자리로 돌아간다.
    private void Comeback()
    {
        // 처음 자리에 도착하면 Idle
        if (Vector3.Distance(transform.position, _startPosition) < _agent.stoppingDistance)
        {
            Debug.Log($"{_state} -> Idle");
            _animator.SetTrigger("MoveToIdle");
            _state = ZombieState.Idle;
            return;
        }

        // 처음 자리로 돌아간다
        _agent.SetDestination(_startPosition);
    }

    // 공격: 일정 시간마다 플레이어를 때린다.
    private void Attack()
    {
        StopAgent();

        // 플레이어가 공격 사정 거리 밖으로 나가면 쫓아!
        float distance = Vector3.Distance(transform.position, _player.transform.position);
        if (distance > _attackRange)
        {
            Debug.Log($"{_state} -> Trace");
            _animator.SetTrigger("AttackMove");
            _state = ZombieState.Trace;
            return;
        }

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= _attackInterval)
        {
            _animator.SetTrigger("StartAttack");
            GameSounds.PlayAt("zombie_attack", transform.position, 0.8f, 0.15f);

            _attackTimer = 0f;
        }
    }

    public void AttackAction()
    {
        DamageInfo info = new DamageInfo()
        {
            Value = _attackDamage,
            HitPoint = _player.transform.position,
            HitNormal = (transform.position - _player.transform.position).normalized,
        };

        _player.GetComponent<IDamageable>().TakeDamage(info);
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
        _agent.ResetPath();                    // 에이전트의 목적지 삭제
        _agent.velocity = Vector3.zero;        // 미끄럼 방지를 위해 가속도 제로!
    }
}
