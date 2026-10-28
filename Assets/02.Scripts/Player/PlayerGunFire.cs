using System.Collections;
using UnityEngine;


// 마우스 왼쪽 버튼을 누르면 바라보는 방향으로 총알을 발사하고 싶다.
public class PlayerGunFire : MonoBehaviour
{
    [SerializeField] private ParticleSystem _hitEffectPrefab;

    [Header("스탯")]
    [SerializeField] private int _maxBulletCount = 30;
    [SerializeField] private float _reloadingTime = 3f;
    [SerializeField] private float _range = 100f;            // 사거리
    [SerializeField] private float _damage = 10f;            // 공격력
    [SerializeField] private float _fireInterval = 0.1f;     // 발사 속도

    private float _fireTimer = 0f;
    private int _bulletCount = 0;
    private Coroutine _reloadCoroutine = null;
    private Animator _animator;  // 군인 모델의 애니메이터
    private GunFeedback _feedback;  // 반동, 카메라 흔들림, 총구 화염, 총소리


    private void Awake()
    {
        _bulletCount = _maxBulletCount;
        _animator = GetComponentInChildren<Animator>();

        _feedback = GetComponent<GunFeedback>();
        if (_feedback == null)
        {
            _feedback = gameObject.AddComponent<GunFeedback>();
        }
    }


    private void Update()
    {
        if (GameManager.Instance.State != GameState.Playing) return;

        // 0. 발사 타이머를 지난 시간만큼 줄인다.
        _fireTimer -= Time.deltaTime;

        // 1. 마우스 왼쪽 버튼 입력 받기
        bool isMouseVisible = Cursor.visible;
        if (!isMouseVisible && Input.GetButton("Fire1") && _fireTimer <= 0f && _bulletCount > 0)
        {
            _bulletCount--;
            _fireTimer = _fireInterval;
            Shoot();
            _feedback.OnShot();

            // 서 있을 때(MoveMotion이 0)만 쏘는 동작을 한다.
            if (_animator.GetFloat("MoveMotion") == 0f)
            {
                _animator.SetTrigger("Attack");
            }

            // 이 방식은: 이 모노비헤이비어가 등록한 모든 코루틴을 실행 취소
            // StopAllCoroutines();

            if (_reloadCoroutine != null)
            {
                StopCoroutine(_reloadCoroutine);
                _animator.SetBool("IsReloading", false);  // 재장전 동작을 멈춘다.
                Debug.Log("재장전 취소!");
                _reloadCoroutine = null;
            }
        }

        // 총알이 없는데 쏘려고 하면 '딸깍'
        if (!isMouseVisible && Input.GetButtonDown("Fire1") && _bulletCount <= 0)
        {
            GameSounds.Play2D("gun_empty", 0.8f);
        }

        // 2. R키 누르면 총알 재장전 시작
        if (Input.GetKeyDown(KeyCode.R) && _reloadCoroutine == null)
        {
            _reloadCoroutine = StartCoroutine(Reloading_Coroutine());
        }
    }

    private IEnumerator Reloading_Coroutine()
    {
        Debug.Log("재장전 시작!");
        _animator.SetBool("IsReloading", true);  // 상체만 재장전 동작 (UpperBody 레이어)

        // 탄창 빼기 → 넣기 → 장전 손잡이 소리를 재장전 시간에 맞춰 나눠 낸다.
        GameSounds.Play2D("reload_mag_out", 0.8f);
        yield return new WaitForSeconds(_reloadingTime * 0.45f);
        GameSounds.Play2D("reload_mag_in", 0.85f);
        yield return new WaitForSeconds(_reloadingTime * 0.3f);
        GameSounds.Play2D("reload_bolt_pull", 0.8f);
        yield return new WaitForSeconds(_reloadingTime * 0.15f);
        GameSounds.Play2D("reload_bolt_release", 0.85f);
        yield return new WaitForSeconds(_reloadingTime * 0.1f);

        _bulletCount = _maxBulletCount;

        _animator.SetBool("IsReloading", false);
        Debug.Log("재장전 완료!");
        _reloadCoroutine = null;
    }


    private void Shoot()
    {
        // Ray: 레이저(시작위치, 날아갈 방향)
        // Physics.RayCast: 레이저를 발사
        // RayCastHit: 레이저가 물체와 부딛히면 그 정보를 저장하는 구조체

        // 2. 레이(광선)를 생성하고 발사될 위치와 진행 방향 설정하기
        Transform cameraTransform = Camera.main.transform;
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        // 3. 레이가 부딛힌 대상의 정보를 저장할 변수 생성하기
        RaycastHit hitInfo = new RaycastHit();

        // 4. 레이를 발사하고, 만일 부딛힌 물체가 있다면 이펙트 표시하기
        bool isHit = Physics.Raycast(ray, out hitInfo, _range);
        if (isHit)
        {
            // Debug.Log($"{hitInfo.collider.name}이 총알을 맞았다. 대미지: {_damage}");

            // 맞은 자리에 총알 자국 이펙트 만들기
            ParticleSystem effect = Instantiate(_hitEffectPrefab);
            effect.transform.position = hitInfo.point; // 맞은 자리
            effect.transform.forward = hitInfo.normal; // 수직 위치로 회전
            effect.Play();

            // 맞은 소리: 좀비면 살, 아니면 벽/바닥
            bool hitZombie = hitInfo.collider.GetComponentInParent<Zombie>() != null;
            GameSounds.PlayAt(hitZombie ? "hit_flesh" : "impact_concrete", hitInfo.point, hitZombie ? 0.6f : 0.8f, 0.03f);

            // 대미지 구조체 만들기
            DamageInfo damageInfo = new DamageInfo();
            damageInfo.Value = _damage;
            damageInfo.HitPoint = hitInfo.point;
            damageInfo.HitNormal = hitInfo.normal;

            hitInfo.collider.GetComponent<IDamageable>()?.TakeDamage(damageInfo);

            /*
             hitInfo.collider.GetComponent<Zombie>()?.TakeDamage(_damage);
            hitInfo.collider.GetComponent<Drum>()?.TakeDamage(_damage);
            hitInfo.collider.GetComponent<Box>()?.TakeDamage(_damage);
            */

            // 황유찬 :         Player
            // 김민쉬:     Zombie, Boss
            // 박현서:  Drum, Box, Bird
        }
    }
}
