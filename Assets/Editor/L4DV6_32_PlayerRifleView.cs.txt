using UnityEngine;

// 군인 모델을 읽거나 보정할 수 없는 경우에도 습득한 AR은 별도 모델로 보인다.
public sealed class PlayerRifleView : MonoBehaviour
{
    private PlayerWeapons _weapons;
    private PlayerGunFire _rifle;
    private FirstPersonBodyView _body;
    private WeaponArmView _view;
    private Transform _muzzle;
    private int _lastRounds;
    private float _kick;
    private float _reloadBlend;

    private void Start()
    {
        _weapons = GetComponent<PlayerWeapons>();
        _rifle = GetComponent<PlayerGunFire>();
        _body = GetComponent<FirstPersonBodyView>();
        Transform item = WeaponModels.Create("Rifle");
        _muzzle = WeaponModels.Find(item, "Muzzle");
        _view = WeaponArmView.Create(gameObject, item);
        _lastRounds = _rifle != null ? _rifle.BulletCount : 0;
    }

    private void Update()
    {
        if (_view == null || _weapons == null) return;
        if (_body == null) _body = GetComponent<FirstPersonBodyView>();
        bool originalReady = _body != null && _body.isActiveAndEnabled && _body.HasRifleVisual;
        _view.Visible = _weapons.Current == WeaponSlot.Rifle && _weapons.HasWeapon(WeaponSlot.Rifle) && !originalReady;
        _view.Raise = _weapons.Raise(WeaponSlot.Rifle);
        if (_rifle != null)
        {
            if (_rifle.BulletCount < _lastRounds) _kick = 1f;
            _lastRounds = _rifle.BulletCount;
        }
        _kick = Mathf.MoveTowards(_kick, 0f, Time.deltaTime * 8f);
        _reloadBlend = Mathf.MoveTowards(_reloadBlend, _rifle != null && _rifle.IsReloading ? 1f : 0f, Time.deltaTime * 5f);
        float reload = Mathf.SmoothStep(0f, 1f, _reloadBlend);
        _view.HandTarget = new Vector3(0.17f, -0.2f, 0.38f) + new Vector3(0f, 0.02f, -0.05f) * _kick
            + new Vector3(-0.03f, -0.06f, -0.04f) * reload;
        _view.ItemRotation = Quaternion.Euler(-4f * _kick + 15f * reload, -2f, 22f * reload);
    }

    public bool TryGetMuzzle(out Vector3 position)
    {
        if (_view != null && _view.Visible && _muzzle != null)
        {
            position = _muzzle.position;
            return true;
        }
        position = Vector3.zero;
        return false;
    }
}
