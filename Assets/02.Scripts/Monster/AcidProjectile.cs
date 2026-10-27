using UnityEngine;

// 스피터가 뱉은 산성액 덩어리. 포물선으로 날아가다가 어딘가에 닿으면 터져서 산성 웅덩이를 만든다.
// 플레이어에게 직접 맞으면 추가 피해를 준다. 좀비는 통과한다.
public class AcidProjectile : MonoBehaviour
{
    public const float Gravity = -15f;
    private const float MaxLifetime = 5f;

    private Vector3 _velocity;
    private GameObject _target;
    private float _damage;
    private float _age;
    private Transform _blob;

    public static void Launch(Vector3 position, Vector3 velocity, GameObject target, float directHitDamage)
    {
        GameObject go = new GameObject("AcidProjectile");
        go.transform.position = position;
        AcidProjectile projectile = go.AddComponent<AcidProjectile>();
        projectile._velocity = velocity;
        projectile._target = target;
        projectile._damage = directHitDamage;
        projectile.Build();
    }

    private void Build()
    {
        Material acid = RuntimeMaterials.Get("acid_blob", new Color(0.75f, 1f, 0.15f), new Color(0.5f, 0.9f, 0.05f), 0.9f);
        _blob = RuntimeMaterials.CreateShape(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * 0.28f, acid).transform;

        Light glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.type = LightType.Point;
        glow.color = new Color(0.6f, 1f, 0.2f);
        glow.range = 3f;
        glow.intensity = 2f;
        glow.shadows = LightShadows.None;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _age += dt;
        if (_age > MaxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 from = transform.position;
        _velocity.y += Gravity * dt;
        Vector3 to = from + _velocity * dt;

        // 날아가는 방향으로 길쭉하게, 꿀렁꿀렁
        if (_blob != null && _velocity.sqrMagnitude > 0.01f)
        {
            float wobble = 1f + Mathf.Sin(_age * 30f) * 0.12f;
            _blob.rotation = Quaternion.LookRotation(_velocity);
            _blob.localScale = new Vector3(0.26f * wobble, 0.26f / wobble, 0.36f);
        }

        Vector3 step = to - from;
        float length = step.magnitude;
        if (length > 0.0001f && CheckHit(from, step / length, length, out RaycastHit hit, out bool hitPlayer))
        {
            Burst(hit.point, hit.normal, hitPlayer);
            return;
        }

        transform.position = to;
    }

    private bool CheckHit(Vector3 from, Vector3 direction, float length, out RaycastHit result, out bool hitPlayer)
    {
        result = default;
        hitPlayer = false;
        RaycastHit[] hits = Physics.SphereCastAll(from, 0.12f, direction, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<Zombie>() != null) continue; // 좀비는 통과
            if (hit.distance <= 0f && hit.point == Vector3.zero) continue;     // 시작부터 겹친 경우
            if (hit.distance < best)
            {
                best = hit.distance;
                result = hit;
                found = true;
                hitPlayer = _target != null && hit.collider.transform.root == _target.transform.root;
            }
        }

        return found;
    }

    private void Burst(Vector3 point, Vector3 normal, bool hitPlayer)
    {
        GameAudio.PlayAt("acid_splash", point, 0.9f, 0.08f, 0.05f, 35f);

        if (hitPlayer && _target != null && _target.TryGetComponent(out IDamageable damageable))
        {
            Vector3 toSource = -_velocity;
            toSource.y = 0f;
            DamageInfo info = new DamageInfo
            {
                Value = _damage,
                HitPoint = point,
                HitNormal = toSource.sqrMagnitude > 0.001f ? toSource.normalized : Vector3.forward
            };
            damageable.TakeDamage(info);
        }

        // 웅덩이는 바닥에 생긴다. 벽이나 플레이어에 맞으면 그 아래 바닥을 찾는다.
        Vector3 ground = point;
        bool onFloor = normal.y > 0.6f && hitPlayer == false;
        if (onFloor == false)
        {
            Vector3 origin = point + normal * 0.2f + Vector3.up * 0.5f;
            RaycastHit[] downs = Physics.RaycastAll(origin, Vector3.down, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(downs, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit down in downs)
            {
                if (down.collider.GetComponentInParent<Zombie>() != null) continue;
                if (_target != null && down.collider.transform.root == _target.transform.root) continue;
                if (down.normal.y < 0.6f) continue;
                ground = down.point;
                onFloor = true;
                break;
            }
        }

        if (onFloor)
        {
            AcidPuddle.Create(ground, _target);
        }

        Destroy(gameObject);
    }
}
