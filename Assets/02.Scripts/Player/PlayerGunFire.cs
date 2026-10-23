using System.Collections;
using UnityEngine;

public class PlayerGunFire : MonoBehaviour
{
    [SerializeField] private ParticleSystem _hitEffectPrefab;

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
        _fireTimer -= Time.deltaTime;

        // 마우스 왼쪽 버튼을 누르고 있으면 연속 발사한다.
        if (Input.GetButton("Fire1")
            && _fireTimer <= 0f
            && _bulletCount > 0)
        {
            _bulletCount--;
            _fireTimer = _fireInterval;

            Shoot();

            // 발사하면 진행 중인 재장전을 취소한다.
            if (_reloadCoroutine != null)
            {
                StopCoroutine(_reloadCoroutine);

                Debug.Log("재장전 취소!");

                _reloadCoroutine = null;
            }
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

        yield return new WaitForSeconds(_reloadingTime);

        _bulletCount = _maxBulletCount;

        Debug.Log("재장전 완료!");

        _reloadCoroutine = null;
    }

    private void Shoot()
    {
        Transform cameraTransform = Camera.main.transform;

        Ray ray = new Ray(
            cameraTransform.position,
            cameraTransform.forward);

        RaycastHit hitInfo = new RaycastHit();

        bool isHit = Physics.Raycast(ray, out hitInfo, _range);

        if (isHit)
        {
            // 맞은 자리에 이펙트를 표시한다.
            ParticleSystem effect = Instantiate(_hitEffectPrefab);

            effect.transform.position = hitInfo.point;
            effect.transform.forward = hitInfo.normal;
            effect.Play();

            DamageInfo damageInfo = new DamageInfo();
            damageInfo.Value = _damage;
            damageInfo.HitPoint = hitInfo.point;
            damageInfo.HitNormal = hitInfo.normal;

            // IDamageable이 있는 대상에게만 대미지를 전달한다.
            IDamageable damageable =
                hitInfo.collider.GetComponent<IDamageable>();

            if (damageable != null)
            {
                damageable.TakeDamage(damageInfo);
            }
        }
    }
}