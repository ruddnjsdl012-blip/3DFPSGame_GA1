using UnityEngine;

// 산성 웅덩이. 몇 초 동안 바닥에 남아, 안에 서 있는 플레이어에게 계속 피해를 준다.
// 지글지글 소리와 초록 빛으로 위치를 알 수 있다.
public class AcidPuddle : MonoBehaviour
{
    [SerializeField, Min(0.3f)] private float _radius = 1.9f;
    [SerializeField, Min(0.5f)] private float _lifetime = 6f;
    [Tooltip("0.5초마다 주는 피해")]
    [SerializeField, Min(0f)] private float _damagePerTick = 5f;
    [SerializeField, Min(0.1f)] private float _tickInterval = 0.5f;

    private GameObject _target;
    private float _age;
    private float _tickTimer;
    private Transform _outer;
    private Transform _inner;
    private Light _glow;
    private AudioSource _sizzle;

    public static AcidPuddle Create(Vector3 groundPosition, GameObject target)
    {
        GameObject go = new GameObject("AcidPuddle");
        go.transform.position = groundPosition;
        AcidPuddle puddle = go.AddComponent<AcidPuddle>();
        puddle._target = target;
        puddle.Build();
        return puddle;
    }

    private void Build()
    {
        Material outer = RuntimeMaterials.Get("acid_pool", new Color(0.35f, 0.55f, 0.05f), new Color(0.25f, 0.45f, 0.02f), 0.95f);
        Material inner = RuntimeMaterials.Get("acid_core", new Color(0.75f, 1f, 0.2f), new Color(0.55f, 0.9f, 0.08f), 0.95f);
        _outer = RuntimeMaterials.CreateShape(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.012f, 0f), Vector3.zero, outer).transform;
        _inner = RuntimeMaterials.CreateShape(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.018f, 0f), Vector3.zero, inner).transform;

        GameObject lightObject = new GameObject("Glow");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        _glow = lightObject.AddComponent<Light>();
        _glow.type = LightType.Point;
        _glow.color = new Color(0.55f, 1f, 0.2f);
        _glow.range = _radius * 2.2f;
        _glow.intensity = 0f;
        _glow.shadows = LightShadows.None;

        _sizzle = GameAudio.CreateLoop("acid_sizzle_loop", transform, 0.7f, true);
        if (_sizzle != null)
        {
            _sizzle.rolloffMode = AudioRolloffMode.Linear;
            _sizzle.minDistance = 1f;
            _sizzle.maxDistance = 16f;
            _sizzle.Play();
        }

        _tickTimer = 0.15f; // 웅덩이가 퍼질 시간을 조금 준다.
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _age += dt;

        // 빠르게 퍼졌다가, 마지막 1초 동안 줄어든다.
        float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_age / 0.3f));
        float shrink = 1f - Mathf.Clamp01((_age - (_lifetime - 1f)) / 1f);
        float size = _radius * grow * shrink;
        float pulse = 1f + Mathf.Sin(_age * 6f) * 0.03f;
        _outer.localScale = new Vector3(size * 2f * pulse, 0.01f, size * 2f * pulse);
        _inner.localScale = new Vector3(size * 1.2f, 0.01f, size * 1.2f / pulse);
        _glow.intensity = 1.6f * grow * shrink * (0.85f + 0.15f * Mathf.Sin(_age * 9f));
        if (_sizzle != null) _sizzle.volume = 0.7f * shrink * GameAudio.SfxScale;

        if (_age >= _lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // 안에 서 있으면 일정 간격으로 피해
        _tickTimer -= dt;
        if (_tickTimer > 0f || _target == null || GameManager.IsPlaying == false)
        {
            return;
        }

        _tickTimer = _tickInterval;
        Vector3 offset = _target.transform.position - transform.position;
        float horizontal = new Vector2(offset.x, offset.z).magnitude;
        if (horizontal <= size && offset.y > -0.6f && offset.y < 2.2f && _target.TryGetComponent(out IDamageable damageable))
        {
            DamageInfo info = new DamageInfo
            {
                Value = _damagePerTick,
                HitPoint = _target.transform.position,
                // 웅덩이 가운데 쪽을 가리킨다. (피격 방향 표시)
                HitNormal = horizontal > 0.05f ? new Vector3(-offset.x, 0f, -offset.z) / horizontal : Vector3.down
            };
            damageable.TakeDamage(info);
        }
    }
}
