using System.Collections;
using UnityEngine;

// FSM(유한 상태 머신) : 유한개의 상태가 있고, 상태마다 행동을 다르게 함으로써 AI를 구현하는 유명한 방법
// - "상태": 객체가 가질 수 있는 어떤 조건이나 상황
// - "전이": 조건에 따라 현재 상태에서 다른 상태로 넘어가는 것
// - 그리고 위 방식에 따라 규칙을 세워둔 것을 "머신"


// 좀비가 가질 수 있는 상태
public enum ZombieState
{
    Idle, // 대기: 제자리에서 플레이어를 기다린다.
    Trace, // 추격: 플레이어를 쫓아간다.
    Comeback, // 복귀: 처음 자리로 돌아간다.
    Attack, // 공격: 일정 시간마다 플레이어를 때린다.
    Hit, // 피격: 공격(총알, 폭탄)을 받아 잠깐 멈칫한다.
    Death, // 죽음: 쓰러지고 잠시 뒤 사라진다.
}

public class Zombie : MonoBehaviour, IDamageable
{
    [Header("스탯")]
    [SerializeField] private float _detectDistance = 8; // 플레이어를 발견하는 거리
    [SerializeField] private float _combackDistance = 15; // 처음 자리에서 이만큼 멀어지면 돌아가는 거리
    [SerializeField] private float _attackDamage = 10f; // 공격 대미지
    [SerializeField] private float _attackRange = 1.5f; // 공격 범위
    [SerializeField] private float _attackInterval = 1.2f; // 공격 간격
    [SerializeField] private float _moveSpeed = 5; // 이동속도
    [SerializeField] private float _turnSpeed = 540f; // 회전 속도 (도/초)
    [SerializeField] private float _maxHealth = 30;
    private float _health;

    [Header("이펙트")]
    [SerializeField] private GameObject _bloodSprayPrefab;

    private float _attackTimer = 0f;
    private CharacterController _controller;
    private Vector3 _startPosition;
    private GameObject _player;

    // 스포너가 만든 좀비는 거리와 상관없이 플레이어를 끝까지 쫓는다.
    private bool _isHunter = false;

    // 난이도 배율을 적용하기 전의 원래 스탯
    private bool _baseCaptured = false;
    private float _baseMaxHealth;
    private float _baseAttackDamage;
    private bool _started = false;

    public float MaxHealth => _maxHealth;
    public float Health => _health;
    public float AttackDamage => _attackDamage;
    public float AttackRange => _attackRange;
    public float MoveSpeed => _moveSpeed;
    public ZombieState State => _state;
    public bool IsDead => _state == ZombieState.Death;
    public GameObject Target => _player;

    // ----- 특수 좀비용 (ZombieVariants가 설정) -----
    public ZombieKind Kind { get; private set; } = ZombieKind.Normal;
    // 맞아도 멈칫하지 않는다. (탱크)
    public bool IgnoreStagger { get; set; }
    // 켜면 추격 중에도 제자리에 서서 플레이어를 바라본다. (스피터가 침을 뱉을 때)
    public bool HoldPosition { get; set; }
    // 죽었을 때 (부머 폭발, 탱크 보상 등)
    public event System.Action<Zombie> Died;
    // 근접 공격이 플레이어에게 맞았을 때 (탱크 넉백 등)
    public event System.Action<Zombie, GameObject> AttackLanded;
    private Vector3 _bodyScale = Vector3.one;


    private ZombieState _state = ZombieState.Idle;

    // 벽을 돌아서 가는 길을 찾아 준다. (NavMesh가 없으면 직선 이동)
    // Unity 객체를 쓰므로 필드 초기화가 아니라 Start에서 만든다.
    private ZombiePathFollower _pathFollower;

    // 가끔 신음 소리를 낸다.
    private float _groanTimer;

    // 코드로 움직이는 애니메이션 (걷기, 공격, 피격, 쓰러짐)
    private ZombieAnimator _animator;
    private bool _attackWindup;

    private void Start()
    {
        CaptureBaseStats();
        _started = true;
        _health = _maxHealth;
        _pathFollower = new ZombiePathFollower();
        _groanTimer = Random.Range(1.5f, 8f);

        _animator = GetComponent<ZombieAnimator>();
        if (_animator == null)
        {
            _animator = gameObject.AddComponent<ZombieAnimator>();
        }

        _animator.BodyScale = _bodyScale;

        _controller = GetComponent<CharacterController>();
        _startPosition = transform.position;

        // 게임을 시작하면 플레이어를 찾는다.
        _player = GameObject.FindGameObjectWithTag("Player");
    }


    private void Update()
    {
        if (GameManager.IsPlaying == false)
        {
            return;
        }

        if (_player == null)
        {
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

    // 스포너에서 부른다. 감지 거리와 복귀 거리를 무시하고 플레이어를 추격한다.
    public void BeginHunt()
    {
        _isHunter = true;
    }

    // 시간이 지날수록 강해지는 난이도 배율을 적용한다. (원래 스탯 기준)
    public void ApplyStatMultiplier(float healthMultiplier, float damageMultiplier)
    {
        CaptureBaseStats();

        // 이미 싸우고 있는 좀비는 남은 체력 비율을 유지한다.
        float ratio = _started && _maxHealth > 0f ? _health / _maxHealth : 1f;

        _maxHealth = _baseMaxHealth * Mathf.Max(0.01f, healthMultiplier);
        _attackDamage = _baseAttackDamage * Mathf.Max(0f, damageMultiplier);

        if (_started && _state != ZombieState.Death)
        {
            _health = _maxHealth * ratio;
        }
    }

    private void CaptureBaseStats()
    {
        if (_baseCaptured)
        {
            return;
        }

        _baseCaptured = true;
        _baseMaxHealth = _maxHealth;
        _baseAttackDamage = _attackDamage;
    }

    // 특수 좀비 스탯. 스폰 직후(Start 전, 난이도 배율 적용 전)에 부른다.
    public void SetupVariant(ZombieKind kind, float healthMultiplier, float damageMultiplier, float speedMultiplier,
        float rangeMultiplier, float intervalMultiplier, Vector3 bodyScale)
    {
        Kind = kind;
        _maxHealth *= Mathf.Max(0.01f, healthMultiplier);
        _attackDamage *= Mathf.Max(0f, damageMultiplier);
        _moveSpeed *= Mathf.Max(0.05f, speedMultiplier);
        _attackRange *= Mathf.Max(0.1f, rangeMultiplier);
        _attackInterval *= Mathf.Max(0.1f, intervalMultiplier);
        _bodyScale = bodyScale;
        _baseCaptured = false; // 바뀐 값을 난이도 배율의 기준으로 삼는다.
        if (_started)
        {
            _health = _maxHealth;
            if (_animator != null) _animator.BodyScale = _bodyScale;
        }
    }

    // 대미지를 받는다.
    public void TakeDamage(DamageInfo damageInfo)
    {
        if (GameManager.IsPlaying == false)
        {
            return;
        }

        if (_state == ZombieState.Death)
        {
            return;
        }

        _health = Mathf.Max(0f, _health - damageInfo.Value);

        // World-space blood is also emitted on a killing hit and outlives the zombie.
        if (_bloodSprayPrefab != null)
        {
            Vector3 normal = damageInfo.HitNormal.sqrMagnitude > 0.0001f
                ? damageInfo.HitNormal.normalized : -transform.forward;
            Instantiate(_bloodSprayPrefab, damageInfo.HitPoint + normal * 0.025f, Quaternion.LookRotation(normal));
        }

        StopAllCoroutines();

        // 총알(폭발)이 날아간 방향: 맞은 면의 반대쪽
        Vector3 shotDirection = damageInfo.HitNormal.sqrMagnitude > 0.0001f
            ? -damageInfo.HitNormal.normalized : -transform.forward;

        if (_health > 0f && IgnoreStagger)
        {
            // 멈칫하지 않고 계속 다가온다. (탱크)
            if (_animator != null) _animator.PlayHit(shotDirection, 0.3f);
            if (_state == ZombieState.Idle || _state == ZombieState.Comeback || _state == ZombieState.Hit)
            {
                _state = ZombieState.Trace;
            }
        }
        else if (_health > 0f)
        {
            if (_animator != null) _animator.PlayHit(shotDirection);

            GameAudio.PlayAt("zombie_hurt", transform.position + Vector3.up * 1.6f, 0.75f, 0.1f, 0.12f, 30f);

            // 잠깐 멈칫 하고 싶다!
            Debug.Log($"{_state} -> Hit");
            _state = ZombieState.Hit;
            StartCoroutine(Hit_Coroutine());
        }
        else
        {
            Debug.Log($"{_state} -> Death");
            _state = ZombieState.Death;
            GameAudio.PlayAt("zombie_death", transform.position + Vector3.up * 1.4f, 0.9f, 0.08f, 0.1f, 35f);
            if (_animator != null) _animator.PlayDeath(shotDirection);

            // 쓰러진 좀비 위의 체력바는 숨긴다.
            foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true))
            {
                canvas.gameObject.SetActive(false);
            }

            StartCoroutine(Death_Coroutine());

            // 가끔 탄약/구급상자/수류탄을 떨어뜨린다.
            PickupManager.OnZombieKilled(transform.position);
            Died?.Invoke(this);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.NotifyZombieDefeated(this);
            }
        }
    }

    private IEnumerator Hit_Coroutine()
    {
        yield return new WaitForSeconds(0.3f); // 잠깐 멈칫한다!

        // 맞았으니 쫒아온다.
        Debug.Log($"{_state} -> Trace");
        _state = ZombieState.Trace;
    }

    private IEnumerator Death_Coroutine()
    {
        // 시체는 길을 막지 않고 총알도 통과한다.
        _controller.enabled = false;

        // 쓰러지고 가라앉는 애니메이션이 끝난 뒤 지운다.
        float wait = _animator != null ? _animator.DeathDuration : 0.5f;
        yield return new WaitForSeconds(wait);
        Destroy(gameObject);
    }

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

        // 쫓아올 때는 더 자주 신음한다. 같은 소리가 겹치지 않게 전체에서 0.5초 간격을 둔다.
        bool chasing = _state == ZombieState.Trace || _state == ZombieState.Attack;
        _groanTimer = chasing ? Random.Range(2.5f, 6f) : Random.Range(5f, 11f);
        GameAudio.PlayAt("zombie_groan", transform.position + Vector3.up * 1.6f, 0.7f, 0.1f, 0.5f, 30f);
    }

    // 대기: 제자리에서 플레이어를 기다린다.
    private void Idle()
    {
        // Todo. Idle 애니메이션 실행

        // 플레이어 가까워지면 Trace 상태로 전이한다.
        float distance = Vector3.Distance(transform.position, _player.transform.position);
        if (_isHunter || distance <= _detectDistance)
        {
            // 플레이어를 발견하면 소리를 지른다. (가까이에서 발견한 좀비만)
            if (distance < 25f)
            {
                GameAudio.PlayAt("zombie_alert", transform.position + Vector3.up * 1.6f, 0.85f, 0.1f, 0.6f, 40f);
            }

            Debug.Log($"{_state} -> Trace");
            _state = ZombieState.Trace;
        }
    }

    // 추격: 플레이어를 쫓아간다.
    private void Trace()
    {
        // Todo. Run 애니메이션 실행


        // 처음 자리에서 너무 멀어지면 돌아간다. (스포너 좀비는 돌아가지 않는다)
        if (_isHunter == false
            && Vector3.Distance(transform.position, _startPosition) > _combackDistance)
        {
            Debug.Log($"{_state} -> Comeback");
            _state = ZombieState.Comeback;
            return;
        }

        // 플레이어가 공격 거리 안이면 공격한다.
        if (Vector3.Distance(transform.position, _player.transform.position) <= _attackRange)
        {
            _attackTimer = 0f;
            _attackWindup = false;
            Debug.Log($"{_state} -> Attack");
            _state = ZombieState.Attack;
            return;
        }

        // 제자리에서 플레이어를 바라본다. (스피터가 침을 뱉는 중)
        if (HoldPosition)
        {
            Vector3 face = _player.transform.position - transform.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), _turnSpeed * Time.deltaTime);
            }

            _controller.SimpleMove(Vector3.zero);
            return;
        }


        // 벽을 피해 플레이어에게 가는 방향
        Vector3 direction = _pathFollower.GetDirection(transform.position, _player.transform.position);
        Move(direction);
    }

    // 복귀: 처음 자리로 돌아간다.
    private void Comeback()
    {
        // 처음 자리에 도착하면 Idle
        if (Vector3.Distance(transform.position, _startPosition) < 1f)
        {
            Debug.Log($"{_state} -> Idle");
            _state = ZombieState.Idle;
            return;
        }

        // 벽을 피해 처음 자리로 돌아간다
        Vector3 direction = _pathFollower.GetDirection(transform.position, _startPosition);
        Move(direction);
    }

    // 정한 방향으로 걷는다. 몸은 그 방향으로 부드럽게 돌린다.
    private void Move(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            _controller.SimpleMove(Vector3.zero);
            return;
        }

        Quaternion look = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, _turnSpeed * Time.deltaTime);
        _controller.SimpleMove(direction * _moveSpeed);
    }

    // 공격: 일정 시간마다 플레이어를 때린다.
    private void Attack()
    {
        // 플레이어가 공격 사정 거리 밖으로 나가면 쫓아!
        float distance = Vector3.Distance(transform.position, _player.transform.position);
        if (distance > _attackRange)
        {
            Debug.Log($"{_state} -> Trace");
            _state = ZombieState.Trace;
            return;
        }

        // 공격하는 동안에도 플레이어 쪽을 바라본다.
        Vector3 toPlayer = _player.transform.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), _turnSpeed * Time.deltaTime);
        }

        _attackTimer += Time.deltaTime;

        // 때리기 직전에 덮치는 동작을 시작한다.
        if (_attackWindup == false && _attackTimer >= _attackInterval - 0.15f)
        {
            _attackWindup = true;
            if (_animator != null) _animator.PlayAttack();
        }

        if (_attackTimer >= _attackInterval)
        {
            _attackWindup = false;

            GameAudio.PlayAt("zombie_attack", transform.position + Vector3.up * 1.5f, 0.85f, 0.1f, 0.15f, 25f);

            if (_player.TryGetComponent(out IDamageable damageable))
            {
                DamageInfo info = new DamageInfo
                {
                    Value = _attackDamage,
                    HitPoint = _player.transform.position,
                    HitNormal = (transform.position - _player.transform.position).normalized
                };

                damageable.TakeDamage(info);
                AttackLanded?.Invoke(this, _player);
            }

            _attackTimer = 0f;
        }
    }
}
