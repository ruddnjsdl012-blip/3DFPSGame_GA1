using System;
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
    [SerializeField] private float _headshotMultiplier = 2.5f;  // 머리를 맞히면 대미지 배율

    [Header("예비 총알 (탄창 밖에 가지고 다니는 총알)")]
    [SerializeField] private int _startReserveAmmo = 120;    // 처음 가진 예비 총알
    [SerializeField] private int _maxReserveAmmo = 240;      // 최대로 가질 수 있는 예비 총알

    private float _fireTimer = 0f;
    private int _bulletCount = 0;
    private Coroutine _reloadCoroutine = null;
    private Animator _animator;  // 군인 모델의 애니메이터
    private GunFeedback _feedback;  // 반동, 카메라 흔들림, 총구 화염, 총소리
    private int _reserveAmmo = 0;
    private PlayerInventory _inventory;  // 보급품 (치료 중에는 쏠 수 없다)
    private PlayerGrenade _grenade;      // 수류탄을 들고 있으면 쏠 수 없다.
    private PlayerWeapons _weapons;      // 무기 바꾸기 (1 도끼 / 2 리볼버 / 3 AR = 이 총 / 4 수류탄)
    private PlayerShove _shove;

    public ParticleSystem HitEffectPrefab => _hitEffectPrefab;  // 리볼버도 같은 총알 자국을 쓴다.

    // 총알 수가 바뀔 때 알려준다. (옵저버 패턴: UI_AmmoHud가 구독)
    public event Action OnAmmoChanged;

    public int BulletCount => _bulletCount;          // 탄창 안의 총알
    public int MaxBulletCount => _maxBulletCount;    // 탄창 크기
    public int ReserveAmmo => _reserveAmmo;          // 예비 총알
    public int MaxReserveAmmo => _maxReserveAmmo;    // 예비 총알 최대
    public bool IsReloading => _reloadCoroutine != null;


    private void Awake()
    {
        _bulletCount = _maxBulletCount;
        _reserveAmmo = Mathf.Clamp(_startReserveAmmo, 0, _maxReserveAmmo);
        _animator = GetComponentInChildren<Animator>();

        _feedback = GetComponent<GunFeedback>();
        if (_feedback == null)
        {
            _feedback = gameObject.AddComponent<GunFeedback>();
        }

        // 보급품(구급상자, 수류탄, 줍기)도 함께 붙인다.
        _inventory = GetComponent<PlayerInventory>();
        if (_inventory == null)
        {
            _inventory = gameObject.AddComponent<PlayerInventory>();
        }

        // 수류탄 들기/던지기도 함께 붙인다.
        _grenade = GetComponent<PlayerGrenade>();
        if (_grenade == null)
        {
            _grenade = gameObject.AddComponent<PlayerGrenade>();
        }

        // 무기 바꾸기 (도끼, 리볼버도 함께 붙는다)
        _weapons = GetComponent<PlayerWeapons>();
        if (_weapons == null)
        {
            _weapons = gameObject.AddComponent<PlayerWeapons>();
        }

        _shove = GetComponent<PlayerShove>();
        if (_shove == null) _shove = gameObject.AddComponent<PlayerShove>();
    }


    private void Start()
    {
        // 오른쪽 아래 총알 UI (현재 총알 / 예비 총알)
        UI_AmmoHud.Create(this);
    }

    // 보급품(탄약 상자)에서 부른다. 실제로 받은 수를 돌려준다.
    public int AddAmmo(int amount)
    {
        int add = Mathf.Clamp(amount, 0, _maxReserveAmmo - _reserveAmmo);
        _reserveAmmo += add;
        OnAmmoChanged?.Invoke();
        return add;
    }


    private void Update()
    {
        if (GameManager.Instance.State != GameState.Playing) return;

        // 구급상자로 치료하는 동안에는 쏘거나 재장전할 수 없다.
        if (_inventory != null && _inventory.IsHealing) return;

        // 수류탄을 들고 있으면 총은 쓰지 않는다. (왼쪽 클릭은 수류탄 던지기)
        if (_grenade != null && _grenade.IsEquipped) return;

        // 3번(AR)을 들고 있을 때만 (바꾸는 중에도 못 쏜다)
        if (_weapons != null && !_weapons.IsReady(WeaponSlot.Rifle)) return;

        // 0. 발사 타이머를 지난 시간만큼 줄인다.
        _fireTimer -= Time.deltaTime;

        // 1. 마우스 왼쪽 버튼 입력 받기
        bool isMouseVisible = Cursor.visible;
        bool shoving = _shove != null && _shove.IsActive;
        if (!isMouseVisible && !shoving && Input.GetButton("Fire1") && _fireTimer <= 0f && _bulletCount > 0)
        {
            _bulletCount--;
            OnAmmoChanged?.Invoke();
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
                OnAmmoChanged?.Invoke();
            }
        }

        // 총알이 없는데 쏘려고 하면 '딸깍'
        if (!isMouseVisible && !shoving && Input.GetButtonDown("Fire1") && _bulletCount <= 0)
        {
            GameSounds.Play2D("gun_empty", 0.8f);

            // 예비 총알이 있으면 자동으로 재장전한다.
            if (_reloadCoroutine == null && _reserveAmmo > 0)
            {
                _reloadCoroutine = StartCoroutine(Reloading_Coroutine());
            }
        }

        // 2. R키 누르면 총알 재장전 시작 (탄창이 덜 찼고 예비 총알이 있을 때만)
        bool canReload = _bulletCount < _maxBulletCount && _reserveAmmo > 0;
        if (Input.GetKeyDown(KeyCode.R) && _reloadCoroutine == null && canReload)
        {
            _reloadCoroutine = StartCoroutine(Reloading_Coroutine());
        }
    }

    // 다른 무기로 바꾸면 재장전을 멈춘다. (PlayerWeapons가 부른다)
    public void CancelReload()
    {
        if (_reloadCoroutine == null) return;

        StopCoroutine(_reloadCoroutine);
        _reloadCoroutine = null;
        _animator.SetBool("IsReloading", false);
        OnAmmoChanged?.Invoke();
    }

    private IEnumerator Reloading_Coroutine()
    {
        Debug.Log("재장전 시작!");
        _animator.SetBool("IsReloading", true);  // 상체만 재장전 동작 (UpperBody 레이어)
        OnAmmoChanged?.Invoke();  // UI에 '재장전 중' 표시

        // 탄창 빼기 → 넣기 → 장전 손잡이 소리를 재장전 시간에 맞춰 나눠 낸다.
        GameSounds.Play2D("reload_mag_out", 0.8f);
        yield return new WaitForSeconds(_reloadingTime * 0.45f);
        GameSounds.Play2D("reload_mag_in", 0.85f);
        yield return new WaitForSeconds(_reloadingTime * 0.3f);
        GameSounds.Play2D("reload_bolt_pull", 0.8f);
        yield return new WaitForSeconds(_reloadingTime * 0.15f);
        GameSounds.Play2D("reload_bolt_release", 0.85f);
        yield return new WaitForSeconds(_reloadingTime * 0.1f);

        // 빈 만큼만 예비 총알에서 채운다.
        int take = Mathf.Min(_maxBulletCount - _bulletCount, _reserveAmmo);
        _bulletCount += take;
        _reserveAmmo -= take;

        _animator.SetBool("IsReloading", false);
        Debug.Log("재장전 완료!");
        _reloadCoroutine = null;
        OnAmmoChanged?.Invoke();
    }


    // 맞은 자리가 좀비 머리 근처인가? (Humanoid면 머리 뼈 높이, 아니면 몸 맨 위 15%)
    private static bool IsHeadshot(Zombie zombie, RaycastHit hit)
    {
        Animator animator = zombie.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                // 콜라이더(캡슐) 겉면에 맞으므로 높이로 판단한다: 목보다 위면 헤드샷
                return hit.point.y > head.position.y - 0.12f * zombie.transform.lossyScale.y;
            }
        }

        Bounds bounds = hit.collider.bounds;
        return hit.point.y > bounds.max.y - bounds.size.y * 0.15f;
    }

    private void Shoot()
    {
        // Ray: 레이저(시작위치, 날아갈 방향)
        // Physics.RayCast: 레이저를 발사
        // RayCastHit: 레이저가 물체와 부딛히면 그 정보를 저장하는 구조체

        // 2. 레이(광선)를 생성하고 발사될 위치와 진행 방향 설정하기
        Transform cameraTransform = Camera.main.transform;
        // 탄 퍼짐: 조준점이 벌어진 만큼 총알이 조금씩 빗나간다.
        Vector3 direction = _feedback != null ? _feedback.SpreadDirection(cameraTransform) : cameraTransform.forward;
        Ray ray = new Ray(cameraTransform.position, direction);

        // 3. 레이가 부딛힌 대상의 정보를 저장할 변수 생성하기
        RaycastHit hitInfo = new RaycastHit();

        // 4. 레이를 발사하고, 만일 부딛힌 물체가 있다면 이펙트 표시하기
        bool isHit = Physics.Raycast(ray, out hitInfo, _range);
        if (isHit)
        {
            // Debug.Log($"{hitInfo.collider.name}이 총알을 맞았다. 대미지: {_damage}");

            Zombie zombie = hitInfo.collider.GetComponentInParent<Zombie>();
            bool hitZombie = zombie != null;

            // 맞은 자리에 총알 자국 이펙트 만들기 (벽/바닥만. 좀비는 BloodEffects가 피를 튀긴다)
            if (!hitZombie)
            {
                ParticleSystem effect = Instantiate(_hitEffectPrefab);
                effect.transform.position = hitInfo.point; // 맞은 자리
                effect.transform.forward = hitInfo.normal; // 수직 위치로 회전
                effect.Play();
            }

            // 맞은 소리: 좀비면 살, 아니면 벽/바닥
            GameSounds.PlayAt(hitZombie ? "hit_flesh" : "impact_concrete", hitInfo.point, hitZombie ? 0.6f : 0.8f, 0.03f);

            // 머리를 맞혔나?
            bool headshot = hitZombie && !zombie.IsDead && IsHeadshot(zombie, hitInfo);
            bool wasAlive = hitZombie && !zombie.IsDead;

            // 대미지 구조체 만들기
            DamageInfo damageInfo = new DamageInfo();
            damageInfo.Value = headshot ? _damage * _headshotMultiplier : _damage;
            damageInfo.HitPoint = hitInfo.point;
            damageInfo.HitNormal = hitInfo.normal;
            damageInfo.Direction = ray.direction;   // 피가 총알 방향으로 튀게
            damageInfo.IsHeadshot = headshot;

            hitInfo.collider.GetComponent<IDamageable>()?.TakeDamage(damageInfo);

            // 좀비 피격 효과: 번쩍임, 조준점 히트 마커, 소리 (피는 Zombie가 BloodEffects로 낸다)
            if (wasAlive)
            {
                bool killed = zombie.IsDead;
                ZombieHitFlash.Flash(zombie.gameObject, headshot);
                if (UI_Crosshair.Instance != null)
                {
                    UI_Crosshair.Instance.ShowHit(killed, headshot);
                }

                GameSounds.Play2D(headshot ? "ui_headshot" : "ui_hitmarker", headshot ? 0.7f : 0.45f, 0.05f);
            }

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
