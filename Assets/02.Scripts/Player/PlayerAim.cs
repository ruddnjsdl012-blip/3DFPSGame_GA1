using Input = GameInput;
using UnityEngine;
using UnityEngine.UI;

// 정조준 (ADS). 마우스 오른쪽 버튼을 누르고 있는 동안 조준한다.
//  - 1인칭 총과 팔이 화면 가운데로 올라오고, 시야가 살짝 확대된다.
//  - 총알 퍼짐과 반동이 줄고, 크로스헤어 대신 가운데에 빨간 점(도트 조준점)이 보인다.
//  - 조준 중에는 천천히 걷고 달릴 수 없으며, 마우스 감도도 확대한 만큼 낮아진다.
//  - 재장전 중에는 조준이 풀렸다가, 버튼을 계속 누르고 있으면 재장전이 끝난 뒤 다시 조준한다.
// PlayerGunFire가 시작할 때 자동으로 붙인다. 값은 Play 중 Inspector에서 바로 조절할 수 있다.
[DisallowMultipleComponent]
public class PlayerAim : MonoBehaviour
{
    private const string FirstPersonName = "__FreeModels_FirstPerson";
    private const int AimMouseButton = 1; // 마우스 오른쪽 버튼 (Fire2는 Alt 키도 포함돼서 쓰지 않는다)

    [Header("조준 동작")]
    [Tooltip("조준 자세로 바뀌는 시간 (초)")]
    [SerializeField, Min(0.01f)] private float _aimTime = 0.16f;
    [Tooltip("시야 확대 배율 (1.35 = 시야각을 1.35분의 1로)")]
    [SerializeField, Range(1f, 3f)] private float _zoom = 1.35f;

    [Header("조준 중 성능")]
    [Tooltip("총알 퍼짐 배율")]
    [SerializeField, Range(0f, 1f)] private float _spreadMultiplier = 0.3f;
    [Tooltip("이동 속도 배율")]
    [SerializeField, Range(0.1f, 1f)] private float _moveSpeedMultiplier = 0.6f;
    [Tooltip("마우스 감도 배율 (확대한 만큼 자동으로 낮춘 값에 한 번 더 곱한다)")]
    [SerializeField, Range(0.1f, 2f)] private float _aimSensitivity = 1f;

    [Header("총 모델 위치")]
    [Tooltip("조준선(총 윗면)이 화면 가운데보다 얼마나 아래에 올지 (도)")]
    [SerializeField, Range(0f, 6f)] private float _sightDropAngle = 1.6f;
    [Tooltip("조준할 때 총을 눈 쪽으로 당기는 거리")]
    [SerializeField, Min(0f)] private float _pullCloser = 0.04f;
    [Tooltip("자동 계산된 조준 위치에 더하는 미세 조정 (카메라 기준 x 오른쪽, y 위, z 앞)")]
    [SerializeField] private Vector3 _offsetAdjust = Vector3.zero;
    [Tooltip("총 모델을 찾지 못했을 때 쓰는 조준 위치")]
    [SerializeField] private Vector3 _fallbackOffset = new Vector3(-0.225f, 0.13f, -0.04f);

    [Header("도트 조준점")]
    [SerializeField] private bool _showRedDot = true;
    [SerializeField] private Color _dotColor = new Color(1f, 0.15f, 0.1f, 1f);
    [SerializeField, Min(1f)] private float _dotSize = 5f;

    private PlayerGunFire _gun;
    private PlayerGunRecoil _recoil;
    private CameraRotate _cameraRotate;
    private PlayerRotate _playerRotate;
    private PlayerMove _move;
    private UI_Crosshair _crosshair;
    private Camera _camera;

    private Vector3 _aimOffset;
    private float _baseFov = 60f;
    private float _blend;
    private bool _wantAim;
    private bool _fovApplied;

    private GameObject _dotCanvas;
    private Image _dot;
    private Image _dotGlow;
    private Texture2D _dotTexture;
    private Sprite _dotSprite;

    // 조준 버튼을 누르고 있고 조준할 수 있는 상태인지
    public bool IsAiming => _wantAim;

    // 조준 정도 (0 = 허리 사격, 1 = 완전히 조준)
    public float AimBlend { get; private set; }

    private void Awake()
    {
        _gun = GetComponent<PlayerGunFire>();
        _move = GetComponentInParent<PlayerMove>();
        if (_move == null) _move = FindAnyObjectByType<PlayerMove>();
        _playerRotate = GetComponentInParent<PlayerRotate>();
        if (_playerRotate == null) _playerRotate = FindAnyObjectByType<PlayerRotate>();
        _cameraRotate = FindAnyObjectByType<CameraRotate>();
    }

    private void Start()
    {
        _recoil = GetComponent<PlayerGunRecoil>();
        _camera = Camera.main;
        if (_camera != null)
        {
            _baseFov = _camera.fieldOfView;
            _aimOffset = ComputeAimOffset(_camera);
        }

        if (_showRedDot)
        {
            BuildRedDot();
        }
    }

    private void Update()
    {
        if (_camera == null)
        {
            _camera = Camera.main;
            if (_camera == null) return;
            _baseFov = _camera.fieldOfView;
            _aimOffset = ComputeAimOffset(_camera);
        }

        bool want = CanAim() && Input.GetMouseButton(AimMouseButton);
        if (want != _wantAim)
        {
            if (want) GameAudio.Play2D("aim_in", 0.45f, 0.05f, 0.08f);
            else if (_blend > 0.3f) GameAudio.Play2D("aim_out", 0.35f, 0.05f, 0.08f);
            _wantAim = want;
        }

        // 일시정지(timeScale 0) 중에도 자세가 풀리도록 unscaled 시간을 쓴다.
        _blend = Mathf.MoveTowards(_blend, want ? 1f : 0f, Time.unscaledDeltaTime / _aimTime);
        float e = _blend * _blend * (3f - 2f * _blend);
        AimBlend = e;

        Apply(e);
    }

    private bool CanAim()
    {
        return GameManager.IsPlaying
               && Cursor.visible == false
               && _gun != null && _gun.isActiveAndEnabled
               && _gun.IsReloading == false;
    }

    private void Apply(float e)
    {
        if (_recoil == null) _recoil = GetComponent<PlayerGunRecoil>();
        // 시야 확대. 조준하지 않을 때는 다른 코드가 바꾼 시야각을 기본값으로 따른다.
        if (e > 0f)
        {
            _camera.fieldOfView = Mathf.Lerp(_baseFov, _baseFov / _zoom, e);
            _fovApplied = true;
        }
        else
        {
            if (_fovApplied)
            {
                _camera.fieldOfView = _baseFov;
                _fovApplied = false;
            }

            _baseFov = _camera.fieldOfView;
        }

        if (_recoil != null)
        {
            _recoil.AimOffset = (_aimOffset + _offsetAdjust) * e;
            _recoil.AimBlend = e;
        }

        if (_gun != null)
        {
            _gun.SpreadMultiplier = Mathf.Lerp(1f, _spreadMultiplier, e);
        }

        if (_move != null)
        {
            _move.SpeedMultiplier = Mathf.Lerp(1f, _moveSpeedMultiplier, e);
            _move.SprintBlocked = _wantAim;
        }

        // 확대한 만큼 감도를 낮춰서 화면이 움직이는 느낌을 같게 유지한다.
        float zoomRatio = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(_baseFov * 0.5f * Mathf.Deg2Rad);
        float sensitivity = zoomRatio * Mathf.Lerp(1f, _aimSensitivity * GameSettings.AimSensitivity, e);
        if (_cameraRotate != null) _cameraRotate.SensitivityMultiplier = sensitivity;
        if (_playerRotate != null) _playerRotate.SensitivityMultiplier = sensitivity;

        if (_crosshair == null) _crosshair = FindAnyObjectByType<UI_Crosshair>();
        if (_crosshair != null) _crosshair.HideAmount = e;

        UpdateRedDot(e);
    }

    // 총 모델의 크기를 재서, 총 윗면이 화면 가운데 바로 아래에 오도록 옮길 위치를 계산한다.
    private Vector3 ComputeAimOffset(Camera view)
    {
        Transform viewmodel = view.transform.Find(FirstPersonName);
        Transform gunModel = viewmodel != null ? FindGunModel(viewmodel) : null;
        if (gunModel == null)
        {
            Debug.LogWarning("[PlayerAim] 1인칭 총 모델을 찾지 못해 기본 조준 위치를 씁니다.", this);
            return _fallbackOffset;
        }

        Transform cam = view.transform;
        bool any = false;
        Bounds bounds = new Bounds();
        foreach (MeshFilter filter in gunModel.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh != null)
            {
                Encapsulate(ref bounds, ref any, cam, filter.transform, filter.sharedMesh.bounds);
            }
        }

        foreach (SkinnedMeshRenderer skinned in gunModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skinned.sharedMesh != null)
            {
                Encapsulate(ref bounds, ref any, cam, skinned.transform, skinned.sharedMesh.bounds);
            }
        }

        if (any == false)
        {
            return _fallbackOffset;
        }

        // 카메라 기준 좌표. 근접 클리핑 면에 총이 잘리지 않을 만큼만 당긴다.
        float scaleZ = Mathf.Max(Mathf.Abs(cam.lossyScale.z), 0.0001f);
        float nearLocal = (view.nearClipPlane + 0.03f) / scaleZ;
        float dz = -_pullCloser;
        if (bounds.min.z + dz < nearLocal)
        {
            dz = Mathf.Min(0f, nearLocal - bounds.min.z);
        }

        float depth = Mathf.Max(0.05f, bounds.center.z + dz);
        float sightY = -Mathf.Tan(_sightDropAngle * Mathf.Deg2Rad) * depth;
        return new Vector3(-bounds.center.x, sightY - bounds.max.y, dz);
    }

    private static Transform FindGunModel(Transform viewmodel)
    {
        // 총구 화염은 총 모델 아래에 붙어 있다.
        FreeModelsImportFixed20261026.FreeModelsMuzzleFlash muzzle =
            viewmodel.GetComponentInChildren<FreeModelsImportFixed20261026.FreeModelsMuzzleFlash>(true);
        if (muzzle != null && muzzle.transform.parent != null && muzzle.transform.parent != viewmodel)
        {
            return muzzle.transform.parent;
        }

        foreach (Transform child in viewmodel)
        {
            if (child.name.Contains("Arms") == false && child.GetComponentInChildren<Renderer>(true) != null)
            {
                return child;
            }
        }

        return null;
    }

    private static void Encapsulate(ref Bounds bounds, ref bool any, Transform cam, Transform owner, Bounds local)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = local.center + Vector3.Scale(local.extents,
                new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            Vector3 p = cam.InverseTransformPoint(owner.TransformPoint(corner));
            if (any == false)
            {
                bounds = new Bounds(p, Vector3.zero);
                any = true;
            }
            else
            {
                bounds.Encapsulate(p);
            }
        }
    }

    // ----- 도트 조준점 -----

    private void BuildRedDot()
    {
        _dotCanvas = new GameObject("UI_AimDot", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = _dotCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 51;

        const int size = 32;
        _dotTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        _dotTexture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01((1f - r) * 3f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        _dotTexture.SetPixels32(pixels);
        _dotTexture.Apply(false, true);
        _dotSprite = Sprite.Create(_dotTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);

        _dotGlow = CreateDotImage("Glow", _dotSize * 3.2f);
        _dot = CreateDotImage("Dot", _dotSize);
        _dotCanvas.SetActive(false);
    }

    private Image CreateDotImage(string name, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(_dotCanvas.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        Image image = go.AddComponent<Image>();
        image.sprite = _dotSprite;
        image.raycastTarget = false;
        return image;
    }

    private void UpdateRedDot(float e)
    {
        if (_dotCanvas == null)
        {
            return;
        }

        // 총이 거의 다 올라왔을 때 나타난다.
        float a = Mathf.Clamp01((e - 0.55f) / 0.45f);
        bool show = a > 0.001f;
        if (_dotCanvas.activeSelf != show)
        {
            _dotCanvas.SetActive(show);
        }

        if (show == false)
        {
            return;
        }

        Color color = _dotColor;
        color.a = _dotColor.a * a;
        _dot.color = color;
        color.a = _dotColor.a * a * 0.22f;
        _dotGlow.color = color;
    }

    private void OnDisable()
    {
        _wantAim = false;
        _blend = 0f;
        AimBlend = 0f;
        if (_camera != null && _fovApplied)
        {
            _camera.fieldOfView = _baseFov;
            _fovApplied = false;
        }

        if (_recoil != null) { _recoil.AimOffset = Vector3.zero; _recoil.AimBlend = 0f; }
        if (_gun != null) _gun.SpreadMultiplier = 1f;
        if (_move != null) { _move.SpeedMultiplier = 1f; _move.SprintBlocked = false; }
        if (_cameraRotate != null) _cameraRotate.SensitivityMultiplier = 1f;
        if (_playerRotate != null) _playerRotate.SensitivityMultiplier = 1f;
        if (_crosshair != null) _crosshair.HideAmount = 0f;
        if (_dotCanvas != null) _dotCanvas.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_dotCanvas != null) Destroy(_dotCanvas);
        if (_dotSprite != null) Destroy(_dotSprite);
        if (_dotTexture != null) Destroy(_dotTexture);
    }
}
