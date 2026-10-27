using UnityEngine;

// 부머: 몸이 부풀어 있고, 죽으면 터진다.
//  - 터지면 주변의 플레이어와 좀비가 피해를 입는다.
//  - 가까이에서 터지면 화면이 초록 토사물로 가려지고, 냄새를 맡은 좀비 무리가 몰려온다.
//  - 그래서 멀리서 쏘는 게 좋다. 가끔 꾸르륵 소리를 내서 다가오는 걸 알 수 있다.
// ZombieVariants.Apply가 붙인다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Zombie))]
public class BoomerZombie : MonoBehaviour
{
    [Header("폭발")]
    [SerializeField, Min(0.5f)] private float _radius = 3.8f;
    [SerializeField, Min(0f)] private float _playerDamage = 22f;
    [SerializeField, Min(0f)] private float _zombieDamage = 45f;

    [Header("토사물 (시야 가림)")]
    [SerializeField, Min(0.5f)] private float _bileRadius = 4.5f;
    [SerializeField, Min(0.5f)] private float _bileDuration = 5f;
    [Tooltip("토사물을 뒤집어쓰면 몰려오는 좀비 수")]
    [SerializeField, Min(0)] private int _mobSize = 5;

    private Zombie _zombie;
    private float _gurgleTimer;
    private bool _exploded;

    private void Awake()
    {
        _zombie = GetComponent<Zombie>();
        _zombie.Died += HandleDied;
        _gurgleTimer = Random.Range(1f, 4f);
    }

    private void Update()
    {
        if (_zombie.IsDead || GameManager.IsPlaying == false)
        {
            return;
        }

        _gurgleTimer -= Time.deltaTime;
        if (_gurgleTimer <= 0f)
        {
            _gurgleTimer = Random.Range(3f, 6f);
            GameAudio.PlayAt("boomer_gurgle", transform.position + Vector3.up * 1.4f, 0.8f, 0.08f, 0.5f, 30f);
        }
    }

    private void HandleDied(Zombie zombie)
    {
        if (_exploded)
        {
            return;
        }

        _exploded = true;
        Vector3 center = transform.position + Vector3.up * 1.1f * transform.lossyScale.y;
        GameAudio.PlayAt("boomer_burst", center, 1f, 0.06f, 0.05f, 50f);
        CameraShake.AddAt(center, 0.6f, 14f);
        BileBurstEffect.Spawn(transform.position, _radius);

        // 주변 좀비 피해 (연쇄로 터질 수도 있다)
        Collider[] hits = Physics.OverlapSphere(center, _radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Collections.Generic.HashSet<Zombie> damaged = new System.Collections.Generic.HashSet<Zombie>();
        foreach (Collider hit in hits)
        {
            Zombie other = hit.GetComponentInParent<Zombie>();
            if (other == null || other == _zombie || other.IsDead || damaged.Add(other) == false)
            {
                continue;
            }

            Vector3 point = hit.ClosestPoint(center);
            DamageInfo info = new DamageInfo
            {
                Value = _zombieDamage,
                HitPoint = point,
                HitNormal = (center - point).sqrMagnitude > 0.0001f ? (center - point).normalized : Vector3.up
            };
            other.TakeDamage(info);
        }

        // 플레이어 피해 + 토사물
        GameObject player = _zombie.Target;
        if (player == null)
        {
            return;
        }

        Vector3 toPlayer = player.transform.position - transform.position;
        float distance = toPlayer.magnitude;
        if (distance <= _radius && player.TryGetComponent(out IDamageable damageable))
        {
            float falloff = Mathf.Lerp(1f, 0.5f, distance / _radius);
            DamageInfo info = new DamageInfo
            {
                Value = _playerDamage * falloff,
                HitPoint = player.transform.position,
                HitNormal = distance > 0.01f ? (-toPlayer / distance) : Vector3.forward
            };
            damageable.TakeDamage(info);

            Vector3 push = toPlayer;
            push.y = 0f;
            if (push.sqrMagnitude > 0.001f)
            {
                PlayerKnockback.Push(player, push.normalized * 5f * falloff + Vector3.up * 2f);
            }
        }

        if (distance <= _bileRadius)
        {
            UI_BileOverlay.Splash(_bileDuration * Mathf.Lerp(1f, 0.6f, distance / _bileRadius));

            ZombieSpawnManager manager = FindAnyObjectByType<ZombieSpawnManager>();
            if (manager != null && _mobSize > 0)
            {
                manager.CallMob(_mobSize);
            }
        }
    }

    private void OnDestroy()
    {
        if (_zombie != null)
        {
            _zombie.Died -= HandleDied;
        }
    }
}
