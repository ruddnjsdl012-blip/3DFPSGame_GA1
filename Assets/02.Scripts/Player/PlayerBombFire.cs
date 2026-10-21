using UnityEngine;
using UnityEngine.Serialization;

// 우클릭 시작 -> 충전 -> 우클릭 해제 -> 풀에서 빌려 던지기.
[RequireComponent(typeof(BombPool))]
public class PlayerBombFire : MonoBehaviour
{
    public const int MaxBombCount = 10;

    [Header("발사 연결")]
    [SerializeField]
    private Transform _firePoint;

    [SerializeField]
    private Bomb _bombPrefab;

    [SerializeField]
    private Camera _aimCamera;

    [Header("충전 설정")]
    [SerializeField, Min(0.01f)]
    private float _maxChargeTime = 1.5f;

    [SerializeField, Min(0f)]
    private float _minThrowPower = 6f;

    [FormerlySerializedAs("_throwPower")]
    [SerializeField, Min(0f)]
    private float _maxThrowPower = 22f;

    [SerializeField, Range(0f, 0.5f)]
    private float _upwardBias = 0.12f;

    [Header("실행 중 확인")]
    [SerializeField]
    private int _remainingBombs;

    [SerializeField]
    private float _chargeTime;

    private BombPool _pool;
    private Collider[] _ownerColliders;

    private readonly RaycastHit[] _castHits = new RaycastHit[32];
    private readonly Collider[] _overlaps = new Collider[32];

    private bool _isCharging;

    public int RemainingBombs => _remainingBombs;
    public bool IsCharging => _isCharging;

    public float Charge01 => _isCharging
        ? Mathf.Clamp01(_chargeTime / Mathf.Max(0.01f, _maxChargeTime))
        : 0f;

    private void Awake()
    {
        _remainingBombs = MaxBombCount;
        _ownerColliders = GetComponentsInChildren<Collider>(true);
        _pool = GetComponent<BombPool>();

        if (_aimCamera == null)
        {
            _aimCamera = Camera.main;
        }

        if (_aimCamera == null || _firePoint == null || _bombPrefab == null)
        {
            Debug.LogError(
                "PlayerBombFire: Fire Point, Bomb Prefab, Aim Camera를 연결해 주세요.",
                this);

            enabled = false;
            return;
        }

        // 풀로 반납해도 보유량은 회복되지 않는다.
        if (_pool.Initialize(_bombPrefab, MaxBombCount) == false)
        {
            enabled = false;
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || Application.isFocused == false)
        {
            CancelCharge();
            return;
        }

        if (Input.GetMouseButtonDown(1) && _remainingBombs > 0)
        {
            _isCharging = true;
            _chargeTime = 0f;
        }

        if (_isCharging == false)
        {
            return;
        }

        if (Input.GetMouseButton(1))
        {
            _chargeTime = Mathf.Min(
                _maxChargeTime,
                _chargeTime + Time.deltaTime);
        }

        // 최대 충전이어도 버튼을 놓을 때까지 기다린다.
        if (Input.GetMouseButtonUp(1))
        {
            TryThrow(Charge01);
            CancelCharge();
        }
    }

    public bool TryThrow(float charge01)
    {
        if (enabled == false
            || _remainingBombs <= 0
            || _pool == null
            || Time.timeScale <= 0f
            || _aimCamera == null
            || _firePoint == null)
        {
            return false;
        }

        if (TryGetSpawnPosition(out Vector3 position) == false)
        {
            return false;
        }

        float power = Mathf.Lerp(
            _minThrowPower,
            _maxThrowPower,
            Mathf.Clamp01(charge01));

        Vector3 direction = (
            _aimCamera.transform.forward
            + Vector3.up * _upwardBias).normalized;

        if (_pool.TrySpawn(
                position,
                Quaternion.LookRotation(direction),
                direction * power,
                _ownerColliders,
                out Bomb bomb) == false)
        {
            return false;
        }

        // 실제 투척에 성공했을 때만 소모한다.
        _remainingBombs--;
        return true;
    }

    private bool TryGetSpawnPosition(out Vector3 position)
    {
        // 가까운 벽 너머에 폭탄이 생성되는 것을 방지한다.
        Vector3 origin = transform.position;
        origin.y = _aimCamera.transform.position.y;

        Vector3 offset = _firePoint.position - origin;
        float distance = offset.magnitude;
        float radius = _bombPrefab.CollisionRadius;

        position = _firePoint.position;

        if (distance > 0.001f)
        {
            Vector3 direction = offset / distance;

            int count = Physics.SphereCastNonAlloc(
                origin,
                radius,
                direction,
                _castHits,
                distance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            if (count == _castHits.Length)
            {
                return false;
            }

            float nearest = distance;

            for (int i = 0; i < count; i++)
            {
                if (IsOwner(_castHits[i].collider) == false)
                {
                    nearest = Mathf.Min(
                        nearest,
                        Mathf.Max(0f, _castHits[i].distance - 0.02f));
                }
            }

            position = origin + direction * nearest;
        }

        int overlaps = Physics.OverlapSphereNonAlloc(
            position,
            radius,
            _overlaps,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        if (overlaps == _overlaps.Length)
        {
            return false;
        }

        for (int i = 0; i < overlaps; i++)
        {
            if (IsOwner(_overlaps[i]) == false)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsOwner(Collider other)
    {
        for (int i = 0; i < _ownerColliders.Length; i++)
        {
            if (_ownerColliders[i] == other)
            {
                return true;
            }
        }

        return false;
    }

    private void CancelCharge()
    {
        _isCharging = false;
        _chargeTime = 0f;
    }

    private void OnDisable() => CancelCharge();

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus == false)
        {
            CancelCharge();
        }
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
        {
            CancelCharge();
        }
    }

    private void OnValidate()
    {
        _maxChargeTime = Mathf.Max(0.01f, _maxChargeTime);
        _minThrowPower = Mathf.Max(0f, _minThrowPower);
        _maxThrowPower = Mathf.Max(_minThrowPower, _maxThrowPower);
    }
}