using UnityEngine;

// 뼈대 없는 좀비 모델을 코드로 움직이는 애니메이션.
// 모델(자식 "__FreeModels_Visual") 전체를 발 위치를 중심으로 기울이고 흔든다.
//  - 대기: 숨 쉬듯 부풀었다 줄었다, 천천히 흔들거림
//  - 걷기/달리기: 앞으로 숙이고 좌우로 비틀거리며 절뚝임 (좀비마다 걸음걸이가 조금씩 다름)
//  - 공격: 앞으로 확 덮치듯 숙였다 돌아옴
//  - 피격: 맞은 방향으로 휘청
//  - 죽음: 총알이 날아온 반대쪽으로 쓰러진 뒤 땅속으로 가라앉음
// Zombie가 시작할 때 자동으로 붙인다.
[DisallowMultipleComponent]
public class ZombieAnimator : MonoBehaviour
{
    private const string VisualName = "__FreeModels_Visual";

    [Header("걷기")]
    [SerializeField] private float _walkLean = 10f;
    [SerializeField] private float _runLean = 17f;
    [SerializeField] private float _swayRoll = 6f;
    [SerializeField] private float _bob = 0.06f;
    [Tooltip("한 걸음 거리 (m). 짧을수록 종종걸음")]
    [SerializeField, Min(0.2f)] private float _strideLength = 1.3f;

    [Header("공격 / 피격")]
    [SerializeField] private float _attackLean = 28f;
    [SerializeField] private float _attackReach = 0.35f;
    [SerializeField] private float _flinchAngle = 14f;
    [SerializeField, Min(1f)] private float _springStiffness = 140f;
    [SerializeField, Range(0.05f, 1f)] private float _springDamping = 0.45f;

    [Header("죽음")]
    [SerializeField, Min(0.1f)] private float _fallTime = 0.55f;
    [SerializeField, Min(0f)] private float _sinkDelay = 1.4f;
    [SerializeField, Min(0.1f)] private float _sinkTime = 1.4f;
    [SerializeField, Min(0f)] private float _sinkDepth = 1.3f;

    private Transform _visual;
    private Vector3 _restPosition;
    private Quaternion _restRotation;
    private Vector3 _restScale;
    private CharacterController _controller;

    // 좀비마다 다른 걸음걸이
    private float _seed;
    private float _tempo;
    private float _limp;
    private float _phase;
    private float _speed;

    private float _attackTimer = 10f;
    private Vector3 _springRot, _springRotVel, _springPos, _springPosVel;

    private bool _dead;
    private float _deathTimer;
    private Vector3 _fallAxis = Vector3.right;
    private float _fallTwist;

    // 죽고 나서 사라질 때까지 걸리는 시간
    public float DeathDuration => _sinkDelay + _sinkTime + 0.1f;

    // 모델 크기 배율 (부머처럼 몸이 부푼 좀비). 좀비 전체 크기는 Transform으로 바꾼다.
    public Vector3 BodyScale { get; set; } = Vector3.one;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _visual = transform.Find(VisualName);
        if (_visual == null)
        {
            // 모델 이름이 다르면 렌더러가 있는 첫 자식을 쓴다. (체력바 캔버스 제외)
            foreach (Transform child in transform)
            {
                if (child.GetComponentInChildren<Canvas>(true) == null && child.GetComponentInChildren<Renderer>(true) != null)
                {
                    _visual = child;
                    break;
                }
            }
        }

        if (_visual != null)
        {
            _restPosition = _visual.localPosition;
            _restRotation = _visual.localRotation;
            _restScale = _visual.localScale;
        }

        _seed = Random.Range(0f, 100f);
        _tempo = Random.Range(0.85f, 1.15f);
        _limp = Random.Range(-1f, 1f);
        _phase = Random.Range(0f, 6.28f);
    }

    // 공격 동작 (데미지를 주기 조금 전에 부른다)
    public void PlayAttack()
    {
        if (_dead == false)
        {
            _attackTimer = 0f;
        }
    }

    // 맞은 방향(총알이 날아간 방향)으로 휘청인다. strength: 휘청이는 세기 (탱크는 작게)
    public void PlayHit(Vector3 worldDirection, float strength = 1f)
    {
        if (_dead)
        {
            return;
        }

        Vector3 local = Flat(transform.InverseTransformDirection(worldDirection));
        float omega = Mathf.Sqrt(_springStiffness);
        _springRotVel += new Vector3(local.z, Random.Range(-0.3f, 0.3f), -local.x) * (_flinchAngle * omega * strength);
        _springPosVel += local * (0.08f * omega * strength);
    }

    // 쓰러진다. worldDirection: 쓰러질 방향 (총알이 날아간 방향)
    public void PlayDeath(Vector3 worldDirection)
    {
        if (_dead)
        {
            return;
        }

        _dead = true;
        _deathTimer = 0f;
        Vector3 local = Flat(transform.InverseTransformDirection(worldDirection));
        if (local.sqrMagnitude < 0.01f)
        {
            local = Vector3.back;
        }

        // 위쪽을 쓰러질 방향으로 돌리는 회전축
        _fallAxis = Vector3.Cross(Vector3.up, local).normalized;
        _fallTwist = Random.Range(-25f, 25f);
    }

    private void LateUpdate()
    {
        if (_visual == null)
        {
            return;
        }

        float dt = Time.deltaTime;
        if (_dead)
        {
            AnimateDeath(dt);
            return;
        }

        // 실제 이동 속도 (부드럽게)
        float speed = 0f;
        if (_controller != null && _controller.enabled)
        {
            Vector3 v = _controller.velocity;
            speed = new Vector2(v.x, v.z).magnitude;
        }

        _speed = Mathf.Lerp(_speed, speed, 1f - Mathf.Exp(-8f * dt));
        float move = Mathf.Clamp01(_speed / 1.5f);
        float run = Mathf.Clamp01((_speed - 3f) / 3f);
        float time = Time.time + _seed;

        // 걸음 주기
        _phase += _speed / _strideLength * Mathf.PI * _tempo * dt;
        float step = Mathf.Sin(_phase);

        float lean = Mathf.Lerp(3f, Mathf.Lerp(_walkLean, _runLean, run), move)
                     + Mathf.Sin(time * 0.9f) * 1.5f * (1f - move);
        float roll = step * _swayRoll * move + _limp * 3f * move
                     + Mathf.Sin(time * 0.7f) * 2f * (1f - move);
        float yaw = Mathf.Sin(_phase * 0.5f + _seed) * 5f * move;
        float bob = Mathf.Abs(step) * _bob * move * (step * _limp > 0f ? 1.6f : 1f);
        float breathe = 1f + Mathf.Sin(time * 2.4f) * 0.012f * (1f - move);

        // 공격: 0.15초 동안 덮치고, 잠깐 멈췄다가 돌아온다.
        _attackTimer += dt;
        float attack = 0f;
        if (_attackTimer < 0.6f)
        {
            if (_attackTimer < 0.15f) attack = EaseOut(_attackTimer / 0.15f);
            else if (_attackTimer < 0.25f) attack = 1f;
            else attack = 1f - Smooth((_attackTimer - 0.25f) / 0.35f);
        }

        StepSpring(dt);

        Quaternion pose = Quaternion.Euler(lean + _attackLean * attack + _springRot.x, yaw + _springRot.y, roll + _springRot.z);
        Vector3 offset = new Vector3(0f, -bob, _attackReach * attack) + _springPos;

        _visual.localRotation = pose * _restRotation;
        _visual.localPosition = _restPosition + offset;
        Vector3 rest = Vector3.Scale(_restScale, BodyScale);
        _visual.localScale = new Vector3(rest.x, rest.y * breathe, rest.z);
    }

    private void AnimateDeath(float dt)
    {
        _deathTimer += dt;

        // 쓰러지기 (점점 빨라지다가 바닥에서 살짝 튕김)
        float u = Mathf.Clamp01(_deathTimer / _fallTime);
        float angle = 90f * u * u;
        if (_deathTimer > _fallTime)
        {
            float b = _deathTimer - _fallTime;
            angle = 90f - 8f * Mathf.Exp(-b * 9f) * Mathf.Abs(Mathf.Sin(b * 18f));
        }

        // 쓰러진 뒤 땅속으로 가라앉기
        float sink = Smooth((_deathTimer - _sinkDelay) / _sinkTime) * _sinkDepth;
        Quaternion fall = Quaternion.AngleAxis(angle, _fallAxis) * Quaternion.Euler(0f, _fallTwist * u, 0f);

        _visual.localRotation = fall * _restRotation;
        _visual.localPosition = _restPosition + new Vector3(0f, 0.12f * u - sink, 0f);
        _visual.localScale = Vector3.Scale(_restScale, BodyScale);
    }

    private void StepSpring(float dt)
    {
        if (dt <= 0f)
        {
            return;
        }

        float k = _springStiffness;
        float c = 2f * Mathf.Sqrt(k) * _springDamping;
        int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.01f), 1, 6);
        float h = Mathf.Min(dt, 0.1f) / steps;
        for (int i = 0; i < steps; i++)
        {
            _springRotVel += (-k * _springRot - c * _springRotVel) * h;
            _springRot += _springRotVel * h;
            _springPosVel += (-k * _springPos - c * _springPosVel) * h;
            _springPos += _springPosVel * h;
        }
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero;
    }

    private static float EaseOut(float x)
    {
        x = 1f - Mathf.Clamp01(x);
        return 1f - x * x;
    }

    private static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}
