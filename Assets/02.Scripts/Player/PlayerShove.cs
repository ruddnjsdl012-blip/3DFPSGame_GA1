using System.Collections.Generic;
using UnityEngine;

// 우클릭은 언제나 밀치기. 총의 재장전 코루틴/탄약은 건드리지 않는다.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class PlayerShove : MonoBehaviour
{
    [SerializeField, Min(0.2f)] private float _cooldown = 0.65f;
    [SerializeField, Min(0.5f)] private float _reach = 2.2f;
    [SerializeField, Range(10f, 90f)] private float _halfAngle = 65f;
    [SerializeField, Min(0.1f)] private float _staggerTime = 0.7f;
    [SerializeField, Min(0f)] private float _pushDistance = 1.35f;
    [SerializeField, Min(0.1f)] private float _motionTime = 0.34f;
    [SerializeField, Min(0f)] private float _impactDelay = 0.055f;

    private readonly Collider[] _nearby = new Collider[96];
    private readonly HashSet<Zombie> _visited = new HashSet<Zombie>();
    private CharacterController _controller;
    private PlayerInventory _inventory;
    private PlayerGrenade _grenade;
    private PlayerAxe _axe;
    private PlayerWeapons _weapons;
    private WeaponArmView _emptyHand;
    private Transform _camera;
    private float _started = -10f;
    private float _nextReady;
    private bool _impactPending;

    public bool IsActive => Time.time < _started + _motionTime;
    public float ViewAmount
    {
        get
        {
            float t = Mathf.Clamp01((Time.time - _started) / _motionTime);
            return t < 0.28f ? Mathf.SmoothStep(0f, 1f, t / 0.28f)
                : Mathf.SmoothStep(1f, 0f, (t - 0.28f) / 0.72f);
        }
    }

    private void Start()
    {
        _controller = GetComponent<CharacterController>();
        _inventory = GetComponent<PlayerInventory>();
        _grenade = GetComponent<PlayerGrenade>();
        _axe = GetComponent<PlayerAxe>();
        _weapons = GetComponent<PlayerWeapons>();
        _emptyHand = WeaponArmView.Create(gameObject, null);
        _camera = Camera.main != null ? Camera.main.transform : null;
    }

    private void Update()
    {
        if (_emptyHand != null)
        {
            _emptyHand.Visible = _weapons != null && _weapons.Current == WeaponSlot.None && IsActive;
            _emptyHand.Raise = 1f;
        }
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing || Cursor.visible)
        {
            _impactPending = false;
            return;
        }

        if (_impactPending && Time.time >= _started + Mathf.Min(_impactDelay, _motionTime))
        {
            _impactPending = false;
            Hit();
        }

        // 누르고 있으면 쿨다운마다 반복한다. 장전 여부/무기 종류는 제한 조건이 아니다.
        if (_camera == null || !Input.GetMouseButton(1) || Time.time < _nextReady) return;
        _started = Time.time;
        _nextReady = Time.time + Mathf.Max(_cooldown, _motionTime);
        _impactPending = true;
        if (_emptyHand != null && _weapons != null) _emptyHand.Visible = _weapons.Current == WeaponSlot.None;
        if (_inventory != null) _inventory.InterruptHealing();
        if (_grenade != null) _grenade.CancelChargeForShove();
        if (_axe != null) _axe.InterruptForShove();
        GameSounds.Play2D("axe_swing", 0.55f, 0.12f);
        CameraShake.ShakeMain(0.1f);
    }

    private void Hit()
    {
        if (_camera == null) return;
        Vector3 origin = _controller != null ? _controller.bounds.center + Vector3.up * 0.15f : transform.position;
        Vector3 forward = Vector3.ProjectOnPlane(_camera.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = transform.forward;

        int count = Physics.OverlapSphereNonAlloc(origin, _reach + 0.6f, _nearby, ~0, QueryTriggerInteraction.Ignore);
        Collider[] candidates = _nearby;
        if (count == _nearby.Length)
        {
            candidates = Physics.OverlapSphere(origin, _reach + 0.6f, ~0, QueryTriggerInteraction.Ignore);
            count = candidates.Length;
        }

        _visited.Clear();
        int pushed = 0;
        for (int i = 0; i < count; i++)
        {
            Collider collider = candidates[i];
            Zombie zombie = collider.GetComponentInParent<Zombie>();
            if (zombie == null || zombie.IsDead || !_visited.Add(zombie)) continue;
            CapsuleCollider body = zombie.GetComponent<CapsuleCollider>();
            Vector3 center = body != null ? body.bounds.center : zombie.transform.position;
            Vector3 toward = center - origin;
            if (Mathf.Abs(toward.y) > 1.5f) continue;
            toward.y = 0f;
            float radius = body != null ? body.radius * Mathf.Max(Mathf.Abs(body.transform.lossyScale.x), Mathf.Abs(body.transform.lossyScale.z)) : 0f;
            if (toward.magnitude > _reach + radius || (toward.sqrMagnitude > 0.01f && Vector3.Angle(forward, toward) > _halfAngle)) continue;

            int worldMask = ~((1 << gameObject.layer) | (1 << zombie.gameObject.layer));
            if (Physics.Linecast(origin, center, worldMask, QueryTriggerInteraction.Ignore)) continue;
            Vector3 direction = toward.sqrMagnitude > 0.01f ? toward.normalized : forward;
            if (zombie.TryShove(direction, _pushDistance, _staggerTime)) pushed++;
        }

        if (pushed > 0)
        {
            GameSounds.Play2D("hit_flesh", 0.8f, 0.1f);
            CameraShake.ShakeMain(0.18f);
        }
    }
}
