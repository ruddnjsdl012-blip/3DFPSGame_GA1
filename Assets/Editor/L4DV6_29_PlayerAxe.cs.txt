using System.Collections.Generic;
using UnityEngine;

// 1번 무기: 도끼 (근접)
//  - 왼쪽 클릭(누르고 있으면 계속): 오른쪽 위로 들어 올렸다가 왼쪽 아래로 크게 내리친다.
//  - 앞쪽 부채꼴(거리 2.4m, 좌우 55도) 안의 좀비를 한 번에 여러 마리 벤다.
//    일반 좀비는 한 방, 특수 좀비도 대부분 한 방, 탱크는 조금만 (도끼로 탱크를 상대하면 안 된다)
//  - 드럼통/상자도 부술 수 있다. 벽을 치면 '깡' 소리
// 모델은 Resources/Weapons/Axe (Tools > L4D 맵 > 도끼·리볼버 모델 설정). 없으면 간단한 모양으로 대신한다.
public class PlayerAxe : MonoBehaviour
{
    [Header("스탯")]
    [SerializeField] private float _damage = 150f;
    [SerializeField] private float _tankDamage = 60f;
    [SerializeField] private float _reach = 2.4f;
    [SerializeField] private float _halfAngle = 55f;
    [SerializeField] private float _swingTime = 0.62f;   // 한 번 휘두르는 전체 시간 (다음 휘두르기까지)
    [SerializeField] private float _hitMoment = 0.17f;   // 휘두르기 시작 후 이때 맞는다.

    [Header("화면 속 자세 (카메라 기준: 손 위치 / 도끼 기울기)")]
    [SerializeField] private Vector3 _idleHand = new Vector3(0.2f, -0.24f, 0.4f);
    [SerializeField] private Vector3 _idleTilt = new Vector3(-15f, -10f, -12f);
    [SerializeField] private Vector3 _windHand = new Vector3(0.3f, -0.02f, 0.24f);
    [SerializeField] private Vector3 _windTilt = new Vector3(-65f, -25f, -35f);
    [SerializeField] private Vector3 _strikeHand = new Vector3(-0.06f, -0.32f, 0.5f);
    [SerializeField] private Vector3 _strikeTilt = new Vector3(100f, 20f, 25f);

    private PlayerWeapons _weapons;
    private PlayerInventory _inventory;
    private WeaponArmView _view;
    private float _swingTimer = -1f;   // < 0: 쉬는 중
    private bool _hitDone;
    private float _bobTime;
    private CharacterController _controller;
    private PlayerShove _shove;

    private void Start()
    {
        _weapons = GetComponent<PlayerWeapons>();
        _inventory = GetComponent<PlayerInventory>();
        _controller = GetComponent<CharacterController>();
        _shove = GetComponent<PlayerShove>();
        _view = WeaponArmView.Create(gameObject, WeaponModels.Create("Axe"));
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (_swingTimer >= 0f)
        {
            _swingTimer += dt;
            if (!_hitDone && _swingTimer >= _hitMoment)
            {
                _hitDone = true;
                Hit();
            }

            if (_swingTimer >= _swingTime)
            {
                _swingTimer = -1f;
            }
        }

        UpdateView(dt);

        if (_weapons == null || !_weapons.IsReady(WeaponSlot.Axe) || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        if (_inventory != null && _inventory.IsHealing)
        {
            return;
        }

        if (!Cursor.visible && (_shove == null || !_shove.IsActive) && Input.GetButton("Fire1") && _swingTimer < 0f)
        {
            _swingTimer = 0f;
            _hitDone = false;
            GameSounds.Play2D("axe_swing", 0.8f, 0.1f);
        }
    }

    public void InterruptForShove()
    {
        _swingTimer = -1f;
        _hitDone = true;
    }

    // ===================== 맞히기 =====================

    private void Hit()
    {
        Transform view = Camera.main.transform;
        Vector3 origin = view.position;
        Vector3 forward = view.forward;
        HashSet<Zombie> hitZombies = new HashSet<Zombie>();
        bool hitSomething = false;

        foreach (Collider collider in Physics.OverlapSphere(origin + forward * (_reach * 0.5f), _reach * 0.75f, ~0, QueryTriggerInteraction.Ignore))
        {
            Vector3 closest = Closest(collider, origin);
            Vector3 toTarget = closest - origin;
            if (toTarget.magnitude > _reach + 0.3f || (toTarget.sqrMagnitude > 0.01f && Vector3.Angle(forward, toTarget) > _halfAngle))
            {
                continue;
            }

            Zombie zombie = collider.GetComponentInParent<Zombie>();
            if (zombie != null)
            {
                if (zombie.IsDead || !hitZombies.Add(zombie))
                {
                    continue;
                }

                // 벽 너머 좀비는 못 벤다.
                Vector3 body = zombie.transform.position + Vector3.up * 1.2f * zombie.transform.lossyScale.y;
                if (Physics.Linecast(origin, body, out RaycastHit block, ~0, QueryTriggerInteraction.Ignore)
                    && block.collider.GetComponentInParent<Zombie>() == null && block.collider.GetComponentInParent<Player>() == null)
                {
                    continue;
                }

                SpecialZombie special = zombie.GetComponent<SpecialZombie>();
                float damage = special != null && special.Type == SpecialType.Tank ? _tankDamage : _damage;
                Vector3 point = Closest(collider, origin + forward * 0.8f + Vector3.up * 0.1f);
                DamageInfo info = new DamageInfo
                {
                    Value = damage,
                    HitPoint = point,
                    HitNormal = -forward,
                    Direction = (forward - view.right * 0.6f - view.up * 0.3f).normalized,  // 오른쪽 위 → 왼쪽 아래로 벤다.
                };
                zombie.TakeDamage(info);
                BulletHit.ZombieFeedback(zombie, false);
                hitSomething = true;
                continue;
            }

            // 드럼통, 상자 등 부서지는 것
            IDamageable damageable = collider.GetComponent<IDamageable>();
            if (damageable != null && collider.GetComponentInParent<Player>() == null)
            {
                damageable.TakeDamage(new DamageInfo { Value = 40f, HitPoint = closest, HitNormal = -forward, Direction = forward });
                hitSomething = true;
            }
        }

        if (hitZombies.Count > 0)
        {
            GameSounds.Play2D("axe_hit", 0.9f, 0.08f);
            CameraShake.ShakeMain(0.25f + 0.05f * hitZombies.Count);
        }
        else if (!hitSomething && Physics.Raycast(origin, forward, out RaycastHit wall, _reach, ~0, QueryTriggerInteraction.Ignore)
                 && wall.collider.GetComponentInParent<Player>() == null)
        {
            GameSounds.PlayAt("impact_concrete", wall.point, 0.9f, 0.05f);
            CameraShake.ShakeMain(0.15f);
        }
    }

    // ClosestPoint는 볼록하지 않은 Mesh Collider에서는 쓸 수 없어서 그때는 상자 범위로 잰다.
    private static Vector3 Closest(Collider collider, Vector3 point)
    {
        if (collider is MeshCollider mesh && !mesh.convex)
        {
            return collider.bounds.ClosestPoint(point);
        }

        return collider.ClosestPoint(point);
    }

    // ===================== 1인칭 자세 =====================

    private void UpdateView(float dt)
    {
        if (_view == null || _weapons == null)
        {
            return;
        }

        _view.Visible = _weapons.Current == WeaponSlot.Axe;
        _view.Raise = _weapons.Raise(WeaponSlot.Axe);

        Vector3 hand = _idleHand;
        Vector3 tilt = _idleTilt;
        if (_swingTimer >= 0f)
        {
            float windEnd = _hitMoment * 0.55f;
            float strikeEnd = _hitMoment + 0.06f;
            if (_swingTimer < windEnd)
            {
                float t = Mathf.SmoothStep(0f, 1f, _swingTimer / windEnd);
                hand = Vector3.Lerp(_idleHand, _windHand, t);
                tilt = Vector3.Lerp(_idleTilt, _windTilt, t);
            }
            else if (_swingTimer < strikeEnd)
            {
                float t = (_swingTimer - windEnd) / (strikeEnd - windEnd);
                t = t * t;  // 내리칠수록 빨라진다.
                hand = Vector3.Lerp(_windHand, _strikeHand, t);
                tilt = Vector3.Lerp(_windTilt, _strikeTilt, t);
            }
            else
            {
                float t = Mathf.SmoothStep(0f, 1f, (_swingTimer - strikeEnd) / (_swingTime - strikeEnd));
                hand = Vector3.Lerp(_strikeHand, _idleHand, t);
                tilt = Vector3.Lerp(_strikeTilt, _idleTilt, t);
            }
        }
        else if (_controller != null)
        {
            // 걸을 때 살짝 흔들린다.
            float speed = new Vector2(_controller.velocity.x, _controller.velocity.z).magnitude;
            _bobTime += dt * speed * 1.4f;
            float bob = Mathf.Clamp01(speed / 7f);
            hand += new Vector3(Mathf.Sin(_bobTime) * 0.012f, -Mathf.Abs(Mathf.Cos(_bobTime)) * 0.015f, 0f) * bob;
        }

        _view.HandTarget = hand;
        _view.ItemRotation = Quaternion.Euler(tilt);
    }
}
