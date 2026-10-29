using System.Collections;
using UnityEngine;

// 2번 무기: 리볼버 (Reichsrevolver M1879)
//  - 왼쪽 클릭 한 번에 한 발 (누르고 있어도 연사되지 않는다). 한 발이 강하다. (머리는 3배)
//  - 6발 장전, 예비탄은 무한 (L4D 권총처럼 탄약이 떨어져도 쓸 수 있는 보조 무기)
//  - R 또는 다 쏘고 왼쪽 클릭: 재장전 (탄창 열고 → 닫기, 리볼버 애니메이션)
//  - 반동은 AR보다 한 발이 크고, 총구 화염은 리볼버 총구에서 나온다.
// 모델은 Resources/Weapons/Revolver (Tools > L4D 맵 > 도끼·리볼버 모델 설정이 만든다). 없으면 간단한 모양으로 대신한다.
public class PlayerRevolver : MonoBehaviour
{
    [Header("스탯")]
    [SerializeField] private int _clipSize = 6;
    [SerializeField] private float _damage = 35f;
    [SerializeField] private float _headshotMultiplier = 3f;
    [SerializeField] private float _fireInterval = 0.22f;
    [SerializeField] private float _reloadTime = 1.6f;
    [SerializeField] private float _range = 100f;
    [SerializeField] private float _kickScale = 1.8f;  // AR 한 발 반동의 몇 배

    [Header("화면 속 위치 (카메라 기준)")]
    [SerializeField] private Vector3 _handPosition = new Vector3(0.15f, -0.16f, 0.42f);
    [SerializeField] private Vector3 _itemTilt = new Vector3(0f, -2f, 0f);

    private PlayerWeapons _weapons;
    private PlayerInventory _inventory;
    private GunFeedback _feedback;
    private PlayerGunFire _rifle;
    private WeaponArmView _view;
    private Transform _muzzle;
    private Animator _gunAnimator;
    private int _rounds;
    private float _fireTimer;
    private Coroutine _reload;
    private float _kick;
    private float _reloadBlend;
    private bool _shotToggle;

    public int Rounds => _rounds;
    public int ClipSize => _clipSize;
    public bool IsReloading => _reload != null;

    private void Awake()
    {
        _rounds = _clipSize;
    }

    private void Start()
    {
        _weapons = GetComponent<PlayerWeapons>();
        _inventory = GetComponent<PlayerInventory>();
        _feedback = GetComponent<GunFeedback>();
        _rifle = GetComponent<PlayerGunFire>();

        Transform item = WeaponModels.Create("Revolver");
        _muzzle = WeaponModels.Find(item, "Muzzle");
        _gunAnimator = item.GetComponentInChildren<Animator>();
        if (_gunAnimator != null)
        {
            _gunAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _gunAnimator.applyRootMotion = false;
            // 쏘는 애니메이션의 'RotateCyl' 이벤트를 받아 탄창(실린더)을 60도씩 돌린다.
            RevolverCylinder.Attach(_gunAnimator.gameObject, WeaponModels.Find(item, "Cylinder"));
        }

        _view = WeaponArmView.Create(gameObject, item);
    }

    public void CancelReload()
    {
        if (_reload != null)
        {
            StopCoroutine(_reload);
            _reload = null;
            Trigger("CloseReloader");
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        UpdateView(dt);

        if (_weapons == null || !_weapons.IsReady(WeaponSlot.Revolver) || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        if (_inventory != null && _inventory.IsHealing)
        {
            return;
        }

        _fireTimer -= dt;
        bool mouseFree = Cursor.visible;
        if (!mouseFree && Input.GetButtonDown("Fire1") && _reload == null)
        {
            if (_rounds > 0 && _fireTimer <= 0f)
            {
                Fire();
            }
            else if (_rounds <= 0)
            {
                GameSounds.Play2D("gun_empty", 0.8f);
                _reload = StartCoroutine(Reload());
            }
        }

        if (Input.GetKeyDown(KeyCode.R) && _reload == null && _rounds < _clipSize)
        {
            _reload = StartCoroutine(Reload());
        }
    }

    private void Fire()
    {
        _rounds--;
        _fireTimer = _fireInterval;
        _kick = 1f;

        Transform view = Camera.main.transform;
        Vector3 direction = _feedback != null ? _feedback.SpreadDirection(view) : view.forward;
        BulletHit.Fire(new Ray(view.position, direction), _range, _damage, _headshotMultiplier, _rifle != null ? _rifle.HitEffectPrefab : null);

        if (_feedback != null)
        {
            _feedback.OnShot(_kickScale, "revolver_shot", 0.9f, _muzzle);
        }

        // 공이치기와 탄창이 도는 리볼버 애니메이션 (두 가지를 번갈아)
        _shotToggle = !_shotToggle;
        Trigger(_shotToggle ? "Shot1" : "Shot2");
    }

    private IEnumerator Reload()
    {
        Trigger("OpenReloader");
        GameSounds.Play2D("reload_mag_out", 0.6f);
        yield return new WaitForSeconds(_reloadTime * 0.35f);
        GameSounds.Play2D("revolver_load", 0.8f);
        yield return new WaitForSeconds(_reloadTime * 0.4f);
        Trigger("CloseReloader");
        GameSounds.Play2D("reload_bolt_release", 0.7f);
        yield return new WaitForSeconds(_reloadTime * 0.25f);
        _rounds = _clipSize;  // 예비탄은 무한
        _reload = null;
    }

    private void Trigger(string name)
    {
        if (_gunAnimator == null || _gunAnimator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in _gunAnimator.parameters)
        {
            if (parameter.name == name)
            {
                _gunAnimator.SetTrigger(name);
                return;
            }
        }
    }

    // 1인칭 손과 리볼버 자세: 쏠 때 총구가 위로 튀고, 재장전할 때 옆으로 눕혀 탄창을 연다.
    private void UpdateView(float dt)
    {
        if (_view == null || _weapons == null)
        {
            return;
        }

        _view.Visible = _weapons.Current == WeaponSlot.Revolver;
        _view.Raise = _weapons.Raise(WeaponSlot.Revolver);
        _kick = Mathf.MoveTowards(_kick, 0f, dt * 6f);
        _reloadBlend = Mathf.MoveTowards(_reloadBlend, _reload != null ? 1f : 0f, dt * 5f);

        float kick = Mathf.Sin(_kick * Mathf.PI * 0.5f);
        float reload = Mathf.SmoothStep(0f, 1f, _reloadBlend);
        _view.HandTarget = _handPosition + new Vector3(0f, 0.02f, -0.06f) * kick + new Vector3(-0.05f, -0.06f, -0.04f) * reload;
        _view.ItemRotation = Quaternion.Euler(_itemTilt + new Vector3(-16f * kick, 0f, 0f) + new Vector3(25f, -20f, 65f) * reload);
    }
}
