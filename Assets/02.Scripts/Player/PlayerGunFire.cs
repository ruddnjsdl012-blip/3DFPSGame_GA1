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

    [Header("재장전")]
    [Tooltip("탄창이 빈 상태에서 발사 버튼을 누르면 자동으로 재장전한다.")]
    [SerializeField] private bool _autoReloadWhenEmpty = true;

    private float _fireTimer = 0f;
    private int _bulletCount = 0;
    private bool _isReloading = false;
    private float _reloadProgress = 0f;
    private float _currentReloadDuration = 0f;
    private Coroutine _reloadCoroutine = null;

    // 옵저버 패턴: UI는 매 프레임 값을 확인하지 않고, 값이 바뀔 때만 알림을 받는다.
    public event Action<int, int> AmmoChanged; // (현재 탄약, 최대 탄약)
    public event Action ReloadStarted;
    public event Action ReloadFinished;
    public event Action ReloadCanceled;
    public event Action EmptyFired;            // 빈 탄창으로 방아쇠를 당겼을 때

    public int BulletCount => _bulletCount;
    public int MaxBulletCount => _maxBulletCount;
    public bool IsReloading => _isReloading;
    public float ReloadProgress => _reloadProgress; // 0 ~ 1
    public float ReloadingTime => _isReloading ? _currentReloadDuration : Mathf.Max(0.05f, _reloadingTime);

    // 탄창을 이미 뺀 뒤에는 발사로 재장전을 취소할 수 없다.
    private bool CanCancelReload => _reloadMotion != null ? _reloadMotion.IsMagazineOut == false : _reloadProgress < 0.2f;

    private void Awake()
    {
        _maxBulletCount = Mathf.Max(1, _maxBulletCount);
        _bulletCount = _maxBulletCount;
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
    }

    private void Update()
    {
        if (GameManager.IsPlaying == false)
        {
            return;
        }

        _fireTimer -= Time.deltaTime;
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
            }
            else
            {
                // 탄이 없으면 발사하지 않는다. 누르는 순간에만 '빈 탄창' 알림을 보낸다.
                if (Input.GetButtonDown("Fire1"))
                {
                    EmptyFired?.Invoke();
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

    // 중복 재장전과 탄창이 가득 찬 상태의 재장전을 막는다.
    public bool TryStartReload()
    {
        if (_isReloading || _bulletCount >= _maxBulletCount)
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
        _bulletCount = _maxBulletCount;
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
        Transform cameraTransform = viewCamera.transform;

        Ray ray = new Ray(
            cameraTransform.position,
            cameraTransform.forward);

        RaycastHit hitInfo = new RaycastHit();

        bool isHit = Physics.Raycast(ray, out hitInfo, _range);

        if (isHit)
        {
            // Zombie.TakeDamage owns blood, including the killing hit.
            Zombie hitZombie = hitInfo.collider.GetComponentInParent<Zombie>();
            if (hitZombie == null && _hitEffectPrefab != null)
            {
                ParticleSystem effect = Instantiate(_hitEffectPrefab, hitInfo.point + hitInfo.normal * 0.015f,
                    Quaternion.LookRotation(hitInfo.normal));
                effect.Play();
            }

            DamageInfo damageInfo = new DamageInfo();
            damageInfo.Value = _damage;
            damageInfo.HitPoint = hitInfo.point;
            damageInfo.HitNormal = hitInfo.normal;

            // IDamageable이 있는 대상에게만 대미지를 전달한다.
            IDamageable damageable =
                hitInfo.collider.GetComponent<IDamageable>();
            if (damageable == null) damageable = hitInfo.collider.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                damageable.TakeDamage(damageInfo);
            }
        }
    }
}
