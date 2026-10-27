using Input = GameInput;
using System;
using System.Collections;
using UnityEngine;
using FreeModelsImportFixed20261026;

public class PlayerGunFire : MonoBehaviour
{
    [SerializeField] private ParticleSystem _hitEffectPrefab;
    [SerializeField] private FreeModelsMuzzleFlash _muzzleFlash;
    [SerializeField] private FreeModelsReloadMotion _reloadMotion;

    [Header("UI")]
    [Tooltip("비워 두면 플레이 시작 시 탄약 HUD를 자동으로 만든다.")]
    [SerializeField] private UI_AmmoHud _ammoHud;

    [Header("스탯")]
    [SerializeField] private int _maxBulletCount = 30;
    [Tooltip("탄이 남아 있을 때의 재장전 시간")]
    [SerializeField] private float _reloadingTime = 3f;
    [Tooltip("탄창이 비었을 때의 재장전 시간 (장전 손잡이를 당기는 동작이 추가된다)")]
    [SerializeField] private float _emptyReloadingTime = 3.6f;
    [SerializeField] private float _range = 100f;
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _fireInterval = 0.1f;

    [Header("예비 탄약 (탄창 밖에 가지고 있는 총알)")]
    [SerializeField, Min(0)] private int _startReserveAmmo = 90;
    [SerializeField, Min(0)] private int _maxReserveAmmo = 210;

    [Header("탄 퍼짐 (크로스헤어가 벌어지는 만큼 총알이 흩어진다, 단위: 도)")]
    [SerializeField, Min(0f)] private float _baseSpread = 0.35f;
    [SerializeField, Min(0f)] private float _spreadPerShot = 0.35f;
    [SerializeField, Min(0f)] private float _maxShotSpread = 3.5f;
    [Tooltip("쏘지 않을 때 퍼짐이 줄어드는 속도 (도/초)")]
    [SerializeField, Min(0f)] private float _spreadRecovery = 5f;
    [Tooltip("최고 속도로 움직일 때 더해지는 퍼짐")]
    [SerializeField, Min(0f)] private float _moveSpread = 1.4f;
    [Tooltip("공중에 떠 있을 때 더해지는 퍼짐")]
    [SerializeField, Min(0f)] private float _airSpread = 3f;

    [Header("헤드샷")]
    [Tooltip("좀비 몸 위쪽 이 비율 안을 맞히면 헤드샷")]
    [SerializeField, Range(0.05f, 0.4f)] private float _headZone = 0.2f;
    [SerializeField, Min(1f)] private float _headshotMultiplier = 2.5f;

    [Header("재장전")]
    [Tooltip("탄창이 빈 상태에서 발사 버튼을 누르면 자동으로 재장전한다.")]
    [SerializeField] private bool _autoReloadWhenEmpty = true;

    private float _fireTimer = 0f;
    private int _bulletCount = 0;
    private int _reserveAmmo = 0;
    private bool _isReloading = false;
    private float _reloadProgress = 0f;
    private float _currentReloadDuration = 0f;
    private Coroutine _reloadCoroutine = null;
    private float _shotSpread;
    private CharacterController _body;

    // 옵저버 패턴: UI는 매 프레임 값을 확인하지 않고, 값이 바뀔 때만 알림을 받는다.
    public event Action<int, int> AmmoChanged; // (현재 탄약, 최대 탄약)
    public event Action ReloadStarted;
    public event Action ReloadFinished;
    public event Action ReloadCanceled;
    public event Action EmptyFired;            // 빈 탄창으로 방아쇠를 당겼을 때
    public event Action Fired;                 // 총알을 한 발 쐈을 때 (반동, 화면 번쩍임)
    public event Action<bool, bool> ZombieHit; // 좀비를 맞혔을 때 (헤드샷인지, 처치했는지)
    public event Action<int> ReserveChanged;   // 예비 탄약이 바뀌었을 때

    public int BulletCount => _bulletCount;
    public int MaxBulletCount => _maxBulletCount;
    public int ReserveAmmo => _reserveAmmo;
    public int MaxReserveAmmo => _maxReserveAmmo;
    public bool NeedsAmmo => _reserveAmmo < _maxReserveAmmo;
    // 탄창과 예비 탄약을 합친 남은 총알
    public int TotalAmmo => _bulletCount + _reserveAmmo;
    public bool IsReloading => _isReloading;
    public float ReloadProgress => _reloadProgress; // 0 ~ 1
    // 지금 총알이 흩어지는 각도 (도). 크로스헤어가 이 값만큼 벌어진다.
    public float CurrentSpread { get; private set; }

    // 정조준 등에서 퍼짐을 줄이는 배율 (1 = 그대로)
    public float SpreadMultiplier { get; set; } = 1f;

    public float ReloadingTime => _isReloading ? _currentReloadDuration : Mathf.Max(0.05f, _reloadingTime);

    // 탄창을 이미 뺀 뒤에는 발사로 재장전을 취소할 수 없다.
    private bool CanCancelReload => _reloadMotion != null ? _reloadMotion.IsMagazineOut == false : _reloadProgress < 0.2f;

    private void Awake()
    {
        _body = GetComponentInParent<CharacterController>();
        _maxBulletCount = Mathf.Max(1, _maxBulletCount);
        _bulletCount = _maxBulletCount;
        _maxReserveAmmo = Mathf.Max(0, _maxReserveAmmo);
        _reserveAmmo = Mathf.Clamp(_startReserveAmmo, 0, _maxReserveAmmo);
    }

    private void Start()
    {
        // 씬에 탄약 HUD가 없으면 코드로 만든다. (씬/프리팹 수정 없이 동작)
        if (_ammoHud == null)
        {
            _ammoHud = FindAnyObjectByType<UI_AmmoHud>();
        }

        if (_ammoHud == null)
        {
            _ammoHud = UI_AmmoHud.Create();
        }

        _ammoHud.Bind(this);
        AmmoChanged?.Invoke(_bulletCount, _maxBulletCount);
        ReserveChanged?.Invoke(_reserveAmmo);

        // 반동/화면 번쩍임 컴포넌트가 없으면 붙인다. (씬 수정 없이 동작)
        if (GetComponent<PlayerGunRecoil>() == null)
        {
            gameObject.AddComponent<PlayerGunRecoil>();
        }

        // 정조준 (마우스 오른쪽 버튼)
        if (GetComponent<PlayerAim>() == null)
        {
            gameObject.AddComponent<PlayerAim>();
        }

        // 히트마커
        if (FindAnyObjectByType<UI_HitMarker>() == null)
        {
            UI_HitMarker.Create(this);
        }

        // 움직이는 크로스헤어
        if (FindAnyObjectByType<UI_Crosshair>() == null)
        {
            UI_Crosshair.Create(this);
        }
    }

    private void Update()
    {
        if (GameManager.IsPlaying == false)
        {
            return;
        }

        _fireTimer -= Time.deltaTime;
        UpdateSpread();

        // 마우스 왼쪽 버튼을 누르고 있으면 연속 발사한다.
        bool isMouseVisible = Cursor.visible;
        if (!isMouseVisible && Input.GetButton("Fire1") && _fireTimer <= 0f)
        {
            if (_isReloading && CanCancelReload == false)
            {
                // 탄창이 빠진 상태: 재장전이 끝날 때까지 쏠 수 없다.
            }
            else if (_bulletCount > 0)
            {
                // 탄창을 빼기 전이라면 발사가 재장전을 취소하고, 총은 조준 자세로 빠르게 돌아온다.
                CancelReload(true);
                _bulletCount--;
                _fireTimer = _fireInterval;

                AmmoChanged?.Invoke(_bulletCount, _maxBulletCount);
                Shoot();
                _shotSpread = Mathf.Min(_maxShotSpread, _shotSpread + _spreadPerShot);
                Fired?.Invoke();
            }
            else if (_isReloading == false)
            {
                // 탄이 없으면 발사하지 않는다. 누르는 순간에만 '빈 탄창' 알림을 보낸다. (재장전 중에는 조용히 기다림)
                if (Input.GetButtonDown("Fire1"))
                {
                    EmptyFired?.Invoke();
                    GameAudio.Play2D("gun_empty", 0.9f, 0.03f);
                }

                if (_autoReloadWhenEmpty)
                {
                    TryStartReload();
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            TryStartReload();
        }
    }

    // 연사할수록, 움직이거나 공중에 있을수록 총알이 흩어진다.
    private void UpdateSpread()
    {
        if (_fireTimer < -0.05f)
        {
            _shotSpread = Mathf.MoveTowards(_shotSpread, 0f, _spreadRecovery * Time.deltaTime);
        }

        float move = 0f;
        float air = 0f;
        if (_body != null)
        {
            Vector3 v = _body.velocity;
            move = Mathf.Clamp01(new Vector2(v.x, v.z).magnitude / 7f) * _moveSpread;
            air = _body.isGrounded ? 0f : _airSpread;
        }

        CurrentSpread = (_baseSpread + _shotSpread + move + air) * Mathf.Max(0f, SpreadMultiplier);
    }

    // 탄약 상자를 주웠을 때. 실제로 늘어난 양을 돌려준다.
    public int AddReserveAmmo(int amount)
    {
        int added = Mathf.Clamp(amount, 0, _maxReserveAmmo - _reserveAmmo);
        if (added > 0)
        {
            _reserveAmmo += added;
            ReserveChanged?.Invoke(_reserveAmmo);

            // 탄창이 빈 채로 탄약을 주우면 바로 재장전한다.
            if (_bulletCount <= 0 && _autoReloadWhenEmpty && isActiveAndEnabled && GameManager.IsPlaying)
            {
                TryStartReload();
            }
        }

        return added;
    }

    // 중복 재장전, 탄창이 가득 찬 상태, 예비 탄약이 없는 상태의 재장전을 막는다.
    public bool TryStartReload()
    {
        if (_isReloading || _bulletCount >= _maxBulletCount || _reserveAmmo <= 0)
        {
            return false;
        }

        _isReloading = true;
        _reloadProgress = 0f;
        _currentReloadDuration = Mathf.Max(0.05f, _bulletCount <= 0 ? _emptyReloadingTime : _reloadingTime);
        _reloadCoroutine = StartCoroutine(Reloading_Coroutine());
        return true;
    }

    private IEnumerator Reloading_Coroutine()
    {
        Debug.Log("재장전 시작!");
        if (_reloadMotion == null && Camera.main != null)
            _reloadMotion = Camera.main.GetComponentInChildren<FreeModelsReloadMotion>(true);
        // 탄창이 비었으면 빈 탄창 재장전(탄창 버리기 + 장전 손잡이), 아니면 일반 재장전 모션.
        bool emptyReload = _bulletCount <= 0;
        if (_reloadMotion != null) _reloadMotion.BeginReload(emptyReload, _currentReloadDuration);
        ReloadStarted?.Invoke();

        float duration = _currentReloadDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            _reloadProgress = elapsed / duration;
            if (_reloadMotion != null) _reloadMotion.SetProgress(_reloadProgress);
            yield return null;
            elapsed += Time.deltaTime;
        }

        _reloadProgress = 1f;
        // 예비 탄약에서 모자란 만큼만 채운다.
        int loaded = Mathf.Min(_maxBulletCount - _bulletCount, _reserveAmmo);
        _bulletCount += loaded;
        _reserveAmmo -= loaded;
        ReserveChanged?.Invoke(_reserveAmmo);
        if (_reloadMotion != null) _reloadMotion.CompleteReload();

        _isReloading = false;
        _reloadCoroutine = null;

        Debug.Log("재장전 완료!");
        AmmoChanged?.Invoke(_bulletCount, _maxBulletCount);
        ReloadFinished?.Invoke();
    }

    private void CancelReload(bool notify)
    {
        bool wasReloading = _isReloading;
        if (_reloadCoroutine != null) StopCoroutine(_reloadCoroutine);
        _reloadCoroutine = null;
        _isReloading = false;
        _reloadProgress = 0f;
        if (_reloadMotion != null && (wasReloading || _reloadMotion.IsReloading)) _reloadMotion.CancelReload();

        if (wasReloading)
        {
            if (notify) Debug.Log("재장전 취소!");
            ReloadCanceled?.Invoke();
        }
    }

    private void OnDisable() { CancelReload(false); }

    private void Shoot()
    {
        Camera viewCamera = Camera.main;
        if (viewCamera == null) return;
        if (_muzzleFlash == null)
            _muzzleFlash = viewCamera.GetComponentInChildren<FreeModelsMuzzleFlash>(true);
        if (_muzzleFlash != null) _muzzleFlash.PlayShot();
        GameAudio.Play2D("gun_shot", 0.75f, 0.05f);
        Transform cameraTransform = viewCamera.transform;

        // 퍼짐 각도 안에서 랜덤한 방향으로 쏜다.
        Vector2 offset = UnityEngine.Random.insideUnitCircle * Mathf.Tan(CurrentSpread * Mathf.Deg2Rad);
        Vector3 direction = (cameraTransform.forward + cameraTransform.right * offset.x + cameraTransform.up * offset.y).normalized;
        Ray ray = new Ray(cameraTransform.position, direction);

        RaycastHit hitInfo = new RaycastHit();

        // 트리거(안전가옥 판정 상자 등)는 총알이 통과한다.
        bool isHit = Physics.Raycast(ray, out hitInfo, _range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        if (isHit)
        {
            // Zombie.TakeDamage owns blood, including the killing hit.
            Zombie hitZombie = hitInfo.collider.GetComponentInParent<Zombie>();
            if (hitZombie != null)
            {
                GameAudio.PlayAt("hit_flesh", hitInfo.point, 0.55f, 0.1f, 0.03f, 30f);
            }
            else
            {
                GameAudio.PlayAt("impact_concrete", hitInfo.point, 0.8f, 0.12f, 0.03f, 35f);
            }

            if (hitZombie == null && _hitEffectPrefab != null)
            {
                ParticleSystem effect = Instantiate(_hitEffectPrefab, hitInfo.point + hitInfo.normal * 0.015f,
                    Quaternion.LookRotation(hitInfo.normal));
                effect.Play();
            }

            // 머리 쪽을 맞히면 헤드샷 (좀비 Collider의 위쪽 일부)
            bool headshot = false;
            if (hitZombie != null)
            {
                Bounds body = hitInfo.collider.bounds;
                headshot = hitInfo.point.y >= body.max.y - body.size.y * _headZone;
            }

            DamageInfo damageInfo = new DamageInfo();
            damageInfo.Value = headshot ? _damage * _headshotMultiplier : _damage;
            damageInfo.HitPoint = hitInfo.point;
            damageInfo.HitNormal = hitInfo.normal;

            // IDamageable이 있는 대상에게만 대미지를 전달한다.
            IDamageable damageable =
                hitInfo.collider.GetComponent<IDamageable>();
            if (damageable == null) damageable = hitInfo.collider.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                bool wasAlive = hitZombie != null && hitZombie.Health > 0f;
                damageable.TakeDamage(damageInfo);

                if (wasAlive)
                {
                    ZombieHit?.Invoke(headshot, hitZombie.Health <= 0f);
                }
            }
        }
    }
}
