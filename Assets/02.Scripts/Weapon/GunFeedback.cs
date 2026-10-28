using UnityEngine;

// 총을 쏠 때의 손맛: 반동, 카메라 흔들림, 총구 화염, 총소리
// PlayerGunFire가 한 발 쏠 때마다 OnShot()을 부른다. (Player에 없으면 PlayerGunFire가 자동으로 붙인다)
//  - 반동: 시점이 위로, 좌우로 조금씩 튄다. 쏘기를 멈추면 튄 양의 일부만큼 천천히 내려온다.
//  - 흔들림: 카메라가 아주 짧게 떨린다.
//  - 총구 화염: 총 끝에서 불꽃이 번쩍이고 주변이 잠깐 밝아진다.
public class GunFeedback : MonoBehaviour
{
    [Header("반동 (도)")]
    [SerializeField] private Vector2 _verticalKick = new Vector2(0.9f, 1.4f);  // 한 발마다 위로 튀는 각도 (최소, 최대)
    [SerializeField] private float _horizontalKick = 0.5f;                     // 좌우로 튀는 각도 (±)
    [SerializeField] private float _kickSpeed = 38f;                           // 튀는 빠르기 (클수록 즉각적)
    [SerializeField] private float _burstGrowth = 0.07f;                       // 계속 쏠수록 한 발마다 반동이 이만큼씩 커진다.
    [SerializeField] private float _maxBurstMultiplier = 1.7f;                 // 계속 쏠 때 반동 최대 배율
    [SerializeField, Range(0f, 1f)] private float _recoveryRatio = 0.55f;      // 튄 양 중 다시 내려오는 비율
    [SerializeField] private float _recoveryDelay = 0.12f;                     // 마지막 발사 뒤 이만큼 지나면 내려오기 시작
    [SerializeField] private float _recoverySpeed = 9f;                        // 내려오는 빠르기 (도/초)

    [Header("카메라 흔들림")]
    [SerializeField] private float _shakePerShot = 0.26f;
    [SerializeField] private float _punchPerShot = 1.8f;    // 한 발마다 화면이 위로 탁 튀는 각도 (조준은 그대로)
    [SerializeField] private float _rollPerShot = 1.4f;     // 화면이 좌우로 기울어지는 각도 (±)
    [SerializeField] private float _fovPerShot = 1.2f;      // 한 발마다 시야가 넓어지는 정도

    [Header("총구 화염")]
    [SerializeField] private Vector3 _fallbackMuzzleOffset = new Vector3(0.12f, -0.13f, 0.85f);  // 총을 못 찾았을 때 (카메라 기준)
    [SerializeField] private float _flashTime = 0.05f;
    [SerializeField] private float _flashSize = 0.26f;
    [SerializeField] private float _flashForward = 0.04f;  // 총 끝에서 이만큼 앞에 불꽃 가운데를 둔다.
    [SerializeField] private Color _flashColor = new Color(1f, 0.75f, 0.35f);

    [Header("소리")]
    [SerializeField] private float _shotVolume = 0.7f;

    private CameraRotate _cameraRotate;
    private PlayerRotate _playerRotate;
    private CameraShake _shake;
    private Camera _camera;
    private Renderer _gunRenderer;
    private FirstPersonBodyView _bodyView;
    private Transform _rightHand;

    private float _pendingPitch;
    private float _pendingYaw;
    private float _recoverable;
    private float _lastShotTime = -10f;
    private int _burstCount;      // 끊지 않고 연달아 쏜 수
    private float _drift;         // 연사할 때 한쪽으로 쏠리는 방향

    private Transform _flash;
    private SpriteRenderer[] _flashSprites;
    private Light _flashLight;
    private float _flashTimer;

    private void Start()
    {
        _camera = Camera.main;
        _cameraRotate = FindAnyObjectByType<CameraRotate>();
        _playerRotate = GetComponent<PlayerRotate>();
        if (_camera != null)
        {
            _shake = _camera.GetComponent<CameraShake>();
            if (_shake == null)
            {
                _shake = _camera.gameObject.AddComponent<CameraShake>();
            }
        }

        FindGun();
        BuildFlash();

        // 1인칭에서 군인 몸이 화면을 가리지 않게 한다.
        _bodyView = GetComponent<FirstPersonBodyView>();
        if (_bodyView == null)
        {
            _bodyView = gameObject.AddComponent<FirstPersonBodyView>();
        }

        // 발소리도 함께 붙인다.
        if (GetComponent<PlayerFootsteps>() == null)
        {
            gameObject.AddComponent<PlayerFootsteps>();
        }
    }

    // 군인 모델에서 총(이름에 gun, rifle, weapon...)을 찾고, 없으면 오른손을 기준으로 삼는다.
    private void FindGun()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            string lower = renderer.name.ToLowerInvariant();
            if (lower.Contains("gun") || lower.Contains("rifle") || lower.Contains("weapon") || lower.Contains("ak")
                || lower.Contains("m4") || lower.Contains("smg"))
            {
                _gunRenderer = renderer;
                return;
            }
        }

        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            _rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
    }

    public void OnShot()
    {
        // 연사: 쉬지 않고 쏠수록 반동이 커지고, 한쪽으로 조금씩 쏠린다.
        if (Time.time - _lastShotTime > 0.3f)
        {
            _burstCount = 0;
            _drift = Random.Range(-1f, 1f);
        }

        _burstCount++;
        _lastShotTime = Time.time;
        float burst = Mathf.Min(1f + _burstGrowth * (_burstCount - 1), _maxBurstMultiplier);
        _drift = Mathf.Clamp(_drift + Random.Range(-0.35f, 0.35f), -1f, 1f);

        // 1. 반동: 위로 튀고 좌우로 (조준점이 실제로 움직인다)
        float up = Random.Range(_verticalKick.x, _verticalKick.y) * burst;
        float side = (Random.Range(-_horizontalKick, _horizontalKick) + _drift * _horizontalKick * 0.6f * (burst - 1f) * 2f);
        _pendingPitch -= up;  // 음수 = 위로
        _pendingYaw += side;
        _recoverable += up * _recoveryRatio;

        // 2. 카메라 흔들림 + 화면 튐 + 시야 넓어짐 (보이는 화면만)
        if (_shake != null)
        {
            _shake.Shake(_shakePerShot);
            _shake.Punch(new Vector3(-_punchPerShot * Random.Range(0.8f, 1.2f), side * 1.5f, Random.Range(-_rollPerShot, _rollPerShot)), _fovPerShot);
        }

        // 3. 총구 화염
        ShowFlash();

        // 1인칭 모델의 쏘는 동작
        if (_bodyView != null)
        {
            _bodyView.OnShot();
        }

        // 4. 총소리
        GameSounds.Play2D("gun_shot", _shotVolume, 0.06f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // 튈 양을 아주 짧은 시간에 나눠서 적용한다. (뚝뚝 끊기지 않게)
        float t = 1f - Mathf.Exp(-_kickSpeed * dt);
        float pitch = _pendingPitch * t;
        float yaw = _pendingYaw * t;
        _pendingPitch -= pitch;
        _pendingYaw -= yaw;

        // 쏘기를 멈추면 천천히 내려온다.
        if (_recoverable > 0f && Time.time - _lastShotTime > _recoveryDelay)
        {
            float step = Mathf.Min(_recoverable, _recoverySpeed * dt);
            _recoverable -= step;
            pitch += step;
        }

        if (Mathf.Abs(pitch) > 0f || Mathf.Abs(yaw) > 0f)
        {
            // 카메라와 몸은 따로 돌아가므로 좌우 반동은 둘 다에 넣는다.
            if (_cameraRotate != null)
            {
                _cameraRotate.AddRecoil(pitch, yaw);
            }

            if (_playerRotate != null)
            {
                _playerRotate.AddRecoil(yaw);
            }
        }

        UpdateFlash(dt);
    }

    // ===================== 총구 화염 =====================

    private void BuildFlash()
    {
        _flash = new GameObject("MuzzleFlash").transform;
        Sprite star = CreateFlashSprite();

        // 크기와 각도가 다른 두 장을 겹쳐서 불꽃 모양을 만든다.
        _flashSprites = new SpriteRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            GameObject go = new GameObject("Flash" + i);
            go.transform.SetParent(_flash, false);
            SpriteRenderer sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = star;
            sprite.color = i == 0 ? _flashColor : new Color(1f, 0.95f, 0.8f);
            go.transform.localScale = Vector3.one * (i == 0 ? 1f : 0.55f);
            _flashSprites[i] = sprite;
        }

        _flashLight = _flash.gameObject.AddComponent<Light>();
        _flashLight.type = LightType.Point;
        _flashLight.color = _flashColor;
        _flashLight.range = 9f;
        _flashLight.intensity = 4f;
        _flashLight.shadows = LightShadows.None;
        _flash.gameObject.SetActive(false);
    }

    private void ShowFlash()
    {
        if (_flash == null || _camera == null)
        {
            return;
        }

        _flash.position = MuzzlePosition();
        _flash.rotation = _camera.transform.rotation * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        _flash.localScale = Vector3.one * (_flashSize * Random.Range(0.8f, 1.25f));
        _flashLight.intensity = Random.Range(3f, 5f);
        _flash.gameObject.SetActive(true);
        _flashTimer = _flashTime;
    }

    private void UpdateFlash(float dt)
    {
        if (_flash == null || !_flash.gameObject.activeSelf)
        {
            return;
        }

        _flashTimer -= dt;
        if (_flashTimer <= 0f)
        {
            _flash.gameObject.SetActive(false);
            return;
        }

        // 불꽃이 켜져 있는 동안 총이 흔들려도 총 끝에 붙어 있게
        _flash.position = MuzzlePosition();
    }

    // 총 끝 위치: 총 메시에서 시선 방향으로 가장 앞쪽 → 없으면 오른손 앞 → 없으면 카메라 기준 고정 위치
    private Vector3 MuzzlePosition()
    {
        Transform cam = _camera.transform;

        // 1인칭 모델(카메라에 붙은 군인) 총의 진짜 총구 (총 메시에서 자동으로 찾은 점)
        if (_bodyView != null && _bodyView.TryGetMuzzle(out Vector3 muzzle))
        {
            return muzzle + cam.forward * _flashForward;
        }

        // 아직 못 찾았으면 1인칭 모델의 오른손 앞
        if (_bodyView != null && _bodyView.ViewHand != null)
        {
            return _bodyView.ViewHand.position + cam.forward * 0.55f;
        }
        if (_gunRenderer != null)
        {
            Bounds b = _gunRenderer.bounds;
            Vector3 f = cam.forward;
            float reach = Mathf.Abs(f.x) * b.extents.x + Mathf.Abs(f.y) * b.extents.y + Mathf.Abs(f.z) * b.extents.z;
            return b.center + f * reach;
        }

        if (_rightHand != null)
        {
            return _rightHand.position + cam.forward * 0.55f;
        }

        return cam.TransformPoint(_fallbackMuzzleOffset);
    }

    // 가운데가 밝고 가시가 뻗은 별 모양 그림을 코드로 만든다.
    private static Sprite CreateFlashSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c;
                float dy = (y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float spikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 3f)), 8f);
                float alpha = Mathf.Clamp01(1f - r / (0.35f + 0.65f * spikes));
                alpha = Mathf.Max(alpha * alpha, Mathf.Clamp01(1f - r * 3f));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
