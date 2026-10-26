using System.Collections;
using UnityEngine;
using FreeModelsImportFixed20261026;

public class PlayerGunFire : MonoBehaviour
{
    [SerializeField] private ParticleSystem _hitEffectPrefab;
    [SerializeField] private FreeModelsMuzzleFlash _muzzleFlash;
    [SerializeField] private FreeModelsReloadMotion _reloadMotion;

    [Header("스탯")]
    [SerializeField] private int _maxBulletCount = 30;
    [SerializeField] private float _reloadingTime = 3f;
    [SerializeField] private float _range = 100f;
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _fireInterval = 0.1f;

    private float _fireTimer = 0f;
    private int _bulletCount = 0;
    private Coroutine _reloadCoroutine = null;

    private void Start()
    {
        _bulletCount = _maxBulletCount;
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
            // Restore the aim pose before the shot so its flash is at the barrel.
            CancelReload(true);
            _bulletCount--;
            _fireTimer = _fireInterval;

            Shoot();

        }

        // 중복 재장전을 방지한다.
        if (Input.GetKeyDown(KeyCode.R)
            && _reloadCoroutine == null)
        {
            _reloadCoroutine = StartCoroutine(Reloading_Coroutine());
        }
    }

    private IEnumerator Reloading_Coroutine()
    {
        Debug.Log("재장전 시작!");
        if (_reloadMotion == null && Camera.main != null)
            _reloadMotion = Camera.main.GetComponentInChildren<FreeModelsReloadMotion>(true);
        if (_reloadMotion != null) _reloadMotion.BeginReload();
        float duration = Mathf.Max(0.05f, _reloadingTime);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (_reloadMotion != null) _reloadMotion.SetProgress(elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }

        _bulletCount = _maxBulletCount;
        if (_reloadMotion != null) _reloadMotion.CompleteReload();

        Debug.Log("재장전 완료!");

        _reloadCoroutine = null;
    }

    private void CancelReload(bool notify)
    {
        bool wasReloading = _reloadCoroutine != null;
        if (wasReloading) StopCoroutine(_reloadCoroutine);
        _reloadCoroutine = null;
        if (_reloadMotion != null && (wasReloading || _reloadMotion.IsReloading)) _reloadMotion.CancelReload();
        if (wasReloading && notify) Debug.Log("재장전 취소!");
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
