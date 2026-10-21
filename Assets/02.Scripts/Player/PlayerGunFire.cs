using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerGunFire : MonoBehaviour
{
    [SerializeField] private ParticleSystem _hitEffectPrefab;

    [Header("스탯")]
    [SerializeField, Min(1)] private int _maxBulletCount = 30;
    [SerializeField, Min(0.01f)] private float _reloadingTime = 3f;
    [SerializeField, Min(0f)] private float _range = 100f;
    [SerializeField, Min(0f)] private float _damage = 10f;

    [FormerlySerializedAs("_fireinterval")]
    [SerializeField, Min(0.01f)] private float _fireInterval = 0.1f;

    private float _fireTimer = 0f;
    private int _bulletCount = 0;
    private bool _isReloading;
    private Coroutine _reloadCoroutine;

    private void Start()
    {
        _bulletCount = _maxBulletCount;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            return;
        }

        _fireTimer = Mathf.Max(0f, _fireTimer - Time.deltaTime);

        // 재장전 중이 아니고 탄창이 가득 차 있지 않을 때 시작한다.
        if (Input.GetKeyDown(KeyCode.R)
            && _isReloading == false
            && _bulletCount < _maxBulletCount)
        {
            _reloadCoroutine = StartCoroutine(ReloadingCoroutine());
        }

        // 재장전 중에는 발사하지 않는다.
        if (_isReloading)
        {
            return;
        }

        if (Input.GetButton("Fire1")
            && _fireTimer <= 0f
            && _bulletCount > 0)
        {
            _bulletCount--;
            _fireTimer = _fireInterval;
            Shoot();
        }
    }

    private IEnumerator ReloadingCoroutine()
    {
        _isReloading = true;
        Debug.Log("재장전 시작!", this);

        yield return new WaitForSeconds(_reloadingTime);

        _bulletCount = _maxBulletCount;
        _isReloading = false;
        _reloadCoroutine = null;

        Debug.Log("재장전 완료!", this);
    }

    private void Shoot()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError(
                "Main Camera의 Tag를 MainCamera로 설정해 주세요.",
                this);

            return;
        }

        Transform cameraTransform = mainCamera.transform;

        Ray ray = new Ray(
            cameraTransform.position,
            cameraTransform.forward);

        bool isHit = Physics.Raycast(
            ray,
            out RaycastHit hitInfo,
            _range);

        if (isHit == false)
        {
            return;
        }

        if (hitInfo.collider.gameObject == gameObject)
        {
            return;
        }

        // 실제 체력 감소는 대상의 체력 스크립트와 연결할 때 구현한다.
        // Debug.Log($"명중: {hitInfo.collider.name}, 공격력: {_damage}", this);

        if (_hitEffectPrefab == null)
        {
            return;
        }

        ParticleSystem effect = Instantiate(_hitEffectPrefab);
        effect.transform.position = hitInfo.point;
        effect.transform.forward = hitInfo.normal;
        effect.Play();
    }

    private void OnDisable()
    {
        // 비활성화될 때 진행 중인 재장전을 취소한다.
        if (_reloadCoroutine != null)
        {
            StopCoroutine(_reloadCoroutine);
            _reloadCoroutine = null;
        }

        _isReloading = false;
    }
}