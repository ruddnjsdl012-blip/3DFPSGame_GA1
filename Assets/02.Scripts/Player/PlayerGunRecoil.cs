using UnityEngine;
using UnityEngine.UI;

// 총을 쏠 때의 반동과 화면 번쩍임.
//  - 조준 반동: 쏠 때마다 시점이 위로, 좌우로 랜덤하게 튄다. 총알도 그쪽으로 날아간다.
//    연사 중에는 좌우가 한쪽으로 조금씩 쏠리고, 첫 발은 조금 더 크게 튄다.
//    쏘기를 멈추면 튄 양의 일부만큼 천천히 돌아온다. (마우스로 직접 내린 만큼은 빼고 돌아온다)
//  - 총 모델 반동: 1인칭 총과 팔이 뒤로 밀렸다가 스프링처럼 돌아온다.
//  - 화면 번쩍임: 쏠 때 화면 전체가 아주 짧게 주황빛으로 밝아진다.
// PlayerGunFire가 시작할 때 자동으로 붙인다. 값은 Play 중 Inspector에서 바로 조절할 수 있다.
[DisallowMultipleComponent]
public class PlayerGunRecoil : MonoBehaviour
{
    private const string FirstPersonName = "__FreeModels_FirstPerson";

    [Header("조준 반동 (도)")]
    [Tooltip("한 발마다 위로 튀는 각도 (최소, 최대) 사이에서 랜덤")]
    [SerializeField] private Vector2 _verticalKick = new Vector2(0.35f, 0.7f);
    [Tooltip("한 발마다 좌우로 튀는 각도 (± 이 값 사이에서 랜덤)")]
    [SerializeField, Min(0f)] private float _horizontalKick = 0.35f;
    [Tooltip("연사 중 좌우가 한쪽으로 쏠리는 최대 정도")]
    [SerializeField, Min(0f)] private float _horizontalDrift = 0.2f;
    [Tooltip("첫 발 반동 배율")]
    [SerializeField, Min(0f)] private float _firstShotMultiplier = 1.4f;
    [Tooltip("반동이 시점에 적용되는 빠르기 (클수록 즉각적으로 튐)")]
    [SerializeField, Min(1f)] private float _kickSnappiness = 28f;

    [Header("반동 회복")]
    [Tooltip("튄 양 중 자동으로 돌아오는 비율")]
    [SerializeField, Range(0f, 1f)] private float _recoveryRatio = 0.75f;
    [Tooltip("마지막 발사 후 이 시간이 지나면 돌아오기 시작")]
    [SerializeField, Min(0f)] private float _recoveryDelay = 0.12f;
    [Tooltip("돌아오는 속도 (도/초)")]
    [SerializeField, Min(0f)] private float _recoverySpeed = 10f;

    [Header("총 모델 반동")]
    [SerializeField] private bool _viewmodelKick = true;
    [Tooltip("뒤로 밀리는 거리 (m)")]
    [SerializeField, Min(0f)] private float _kickBack = 0.035f;
    [Tooltip("총구가 들리는 각도")]
    [SerializeField, Min(0f)] private float _kickPitch = 3f;
    [Tooltip("좌우로 비틀리는 각도")]
    [SerializeField, Min(0f)] private float _kickRoll = 1.5f;
    [SerializeField, Min(1f)] private float _springStiffness = 320f;
    [SerializeField, Range(0.05f, 1f)] private float _springDamping = 0.55f;

    [Header("정조준 중")]
    [Tooltip("정조준 중 조준 반동 배율")]
    [SerializeField, Range(0f, 1f)] private float _aimKickMultiplier = 0.7f;
    [Tooltip("정조준 중 총 모델이 흔들리는 배율 (작을수록 조준선이 덜 흔들림)")]
    [SerializeField, Range(0f, 1f)] private float _aimViewmodelMultiplier = 0.4f;

    [Header("화면 번쩍임")]
    [SerializeField] private bool _screenFlash = true;
    [SerializeField] private Color _flashColor = new Color(1f, 0.78f, 0.45f);
    [Tooltip("번쩍일 때의 불투명도 (최소, 최대) 사이에서 랜덤")]
    [SerializeField] private Vector2 _flashAlpha = new Vector2(0.06f, 0.12f);
    [Tooltip("번쩍임이 사라지는 시간 (초)")]
    [SerializeField, Min(0.01f)] private float _flashTime = 0.07f;

    private PlayerGunFire _gun;
    private CameraRotate _cameraRotate;
    private PlayerRotate _playerRotate;

    private float _pendingPitch;
    private float _pendingYaw;
    private float _recoverable;
    private float _lastShotTime = -10f;
    private float _drift;
    private int _shotsInBurst;

    private Transform _viewmodel;
    private Vector3 _viewBasePosition;
    private Quaternion _viewBaseRotation;
    private Vector3 _springPos, _springVel, _springRot, _springRotVel;

    // 정조준(PlayerAim)이 설정한다. 총 모델을 화면 가운데로 옮기는 위치(카메라 기준)와 정조준 정도(0~1)
    public Vector3 AimOffset { get; set; }
    public float AimBlend { get; set; }

    private GameObject _flashCanvas;
    private Image _flashImage;
    private float _flashTimer;
    private float _flashPeak;

    private void Awake()
    {
        _gun = GetComponent<PlayerGunFire>();
        _cameraRotate = FindAnyObjectByType<CameraRotate>();
        _playerRotate = GetComponentInParent<PlayerRotate>();
        if (_playerRotate == null)
        {
            _playerRotate = FindAnyObjectByType<PlayerRotate>();
        }
    }

    private void OnEnable()
    {
        if (_gun != null)
        {
            _gun.Fired += HandleFired;
        }
    }

    private void OnDisable()
    {
        if (_gun != null)
        {
            _gun.Fired -= HandleFired;
        }

        ResetViewmodel();
        if (_flashImage != null)
        {
            _flashImage.enabled = false;
        }
    }

    private void Start()
    {
        Camera view = Camera.main;
        if (view != null)
        {
            _viewmodel = view.transform.Find(FirstPersonName);
            if (_viewmodel != null)
            {
                _viewBasePosition = _viewmodel.localPosition;
                _viewBaseRotation = _viewmodel.localRotation;
            }
        }

        BuildFlashOverlay();
    }

    // 한 발 쏠 때마다 PlayerGunFire가 알려 준다.
    private void HandleFired()
    {
        bool newBurst = Time.time - _lastShotTime > 0.35f;
        _shotsInBurst = newBurst ? 0 : _shotsInBurst + 1;
        _lastShotTime = Time.time;

        // 1) 조준 반동 (랜덤)
        float multiplier = _shotsInBurst == 0 ? _firstShotMultiplier : 1f;
        multiplier *= Mathf.Lerp(1f, _aimKickMultiplier, AimBlend);
        float up = Random.Range(_verticalKick.x, Mathf.Max(_verticalKick.x, _verticalKick.y)) * multiplier;

        if (newBurst)
        {
            _drift = Random.Range(-_horizontalDrift, _horizontalDrift) * 0.5f;
        }
        else
        {
            _drift = Mathf.Clamp(_drift + Random.Range(-_horizontalDrift, _horizontalDrift) * 0.5f, -_horizontalDrift, _horizontalDrift);
        }

        float side = (Random.Range(-_horizontalKick, _horizontalKick) + _drift) * Mathf.Lerp(1f, _aimKickMultiplier, AimBlend);
        _pendingPitch -= up; // 음수 = 위로
        _pendingYaw += side;
        _recoverable += up * _recoveryRatio;

        // 2) 총 모델 반동 (스프링에 순간 속도를 준다)
        if (_viewmodelKick)
        {
            float omega = Mathf.Sqrt(_springStiffness);
            _springVel += new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0f, 0.2f), -1f) * (_kickBack * omega);
            _springRotVel += new Vector3(-_kickPitch * Random.Range(0.8f, 1.2f),
                Random.Range(-0.5f, 0.5f) * _kickRoll,
                Random.Range(-_kickRoll, _kickRoll)) * omega;
        }

        // 3) 화면 번쩍임
        if (_screenFlash)
        {
            _flashPeak = Random.Range(_flashAlpha.x, Mathf.Max(_flashAlpha.x, _flashAlpha.y));
            _flashTimer = _flashTime;
        }
    }

    private void Update()
    {
        UpdateFlash();

        if (GameManager.IsPlaying == false)
        {
            return;
        }

        float dt = Time.deltaTime;

        // 튈 양을 아주 짧은 시간에 나눠 적용한다. (뚝뚝 끊기지 않게)
        float t = 1f - Mathf.Exp(-_kickSnappiness * dt);
        float pitch = _pendingPitch * t;
        float yaw = _pendingYaw * t;
        _pendingPitch -= pitch;
        _pendingYaw -= yaw;

        if (_cameraRotate != null && Mathf.Abs(pitch) > 0f)
        {
            _cameraRotate.AddRecoil(pitch);
        }

        if (_playerRotate != null && Mathf.Abs(yaw) > 0f)
        {
            _playerRotate.AddRecoil(yaw);
        }

        if (_cameraRotate == null)
        {
            return;
        }

        // 플레이어가 마우스로 직접 내린 만큼은 자동 회복에서 뺀다. (너무 많이 내려가지 않게)
        if (_cameraRotate.LastMouseDelta > 0f)
        {
            _recoverable = Mathf.Max(0f, _recoverable - _cameraRotate.LastMouseDelta);
        }

        // 쏘기를 멈추면 천천히 돌아온다.
        if (_recoverable > 0f && Time.time - _lastShotTime > _recoveryDelay)
        {
            float step = Mathf.Min(_recoverable, _recoverySpeed * dt);
            _recoverable -= step;
            _cameraRotate.AddRecoil(step);
        }
    }

    private void LateUpdate()
    {
        if (_viewmodel == null)
        {
            return;
        }

        StepSpring(Time.deltaTime);
        float kick = Mathf.Lerp(1f, _aimViewmodelMultiplier, AimBlend);
        _viewmodel.localPosition = _viewBasePosition + AimOffset + _springPos * kick;
        _viewmodel.localRotation = _viewBaseRotation * Quaternion.Euler(_springRot * kick);
    }

    private void StepSpring(float dt)
    {
        if (dt <= 0f)
        {
            return;
        }

        float k = _springStiffness;
        float c = 2f * Mathf.Sqrt(k) * _springDamping;
        int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.008f), 1, 8);
        float h = Mathf.Min(dt, 0.1f) / steps;
        for (int i = 0; i < steps; i++)
        {
            _springVel += (-k * _springPos - c * _springVel) * h;
            _springPos += _springVel * h;
            _springRotVel += (-k * _springRot - c * _springRotVel) * h;
            _springRot += _springRotVel * h;
        }
    }

    private void ResetViewmodel()
    {
        _springPos = _springVel = _springRot = _springRotVel = Vector3.zero;
        if (_viewmodel != null)
        {
            _viewmodel.localPosition = _viewBasePosition;
            _viewmodel.localRotation = _viewBaseRotation;
        }
    }

    // ----- 화면 번쩍임 -----

    private void BuildFlashOverlay()
    {
        _flashCanvas = new GameObject("UI_MuzzleFlashOverlay", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = _flashCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40; // HUD(49~50) 아래, 게임 화면 위

        GameObject imageObject = new GameObject("Flash", typeof(RectTransform));
        RectTransform rect = (RectTransform)imageObject.transform;
        rect.SetParent(_flashCanvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        _flashImage = imageObject.AddComponent<Image>();
        _flashImage.raycastTarget = false;
        _flashImage.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, 0f);
        _flashImage.enabled = false;
    }

    private void UpdateFlash()
    {
        if (_flashImage == null)
        {
            return;
        }

        if (_flashTimer <= 0f)
        {
            if (_flashImage.enabled)
            {
                _flashImage.enabled = false;
            }

            return;
        }

        _flashTimer -= Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(_flashTimer / _flashTime);
        Color color = _flashColor;
        color.a = _flashPeak * k * k;
        _flashImage.color = color;
        _flashImage.enabled = color.a > 0.001f;
    }

    private void OnDestroy()
    {
        if (_flashCanvas != null)
        {
            Destroy(_flashCanvas);
        }
    }
}
