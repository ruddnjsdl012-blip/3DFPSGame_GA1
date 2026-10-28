using UnityEngine;

// 산성 웅덩이. 위에 서 있으면 점점 더 아프다. (L4D 스피터: 오래 있을수록 대미지가 커진다)
public class AcidPuddle : MonoBehaviour
{
    private float _radius;
    private float _life;
    private float _age;
    private float _tickTimer;
    private float _timeInside;
    private Transform _visual;
    private Light _light;
    private static Player _player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _player = null;
    }

    public static AcidPuddle Create(Vector3 position, float radius, float life)
    {
        GameObject go = new GameObject("AcidPuddle");
        go.transform.position = position;
        AcidPuddle puddle = go.AddComponent<AcidPuddle>();
        puddle._radius = radius;
        puddle._life = life;

        GameObject disc = SpecialEffects.Shape(PrimitiveType.Cylinder, new Color(0.45f, 0.95f, 0.1f), 1.2f);
        disc.transform.SetParent(go.transform, false);
        disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        disc.transform.localScale = new Vector3(0.1f, 0.01f, 0.1f);
        puddle._visual = disc.transform;

        GameObject lightObject = new GameObject("Glow");
        lightObject.transform.SetParent(go.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        puddle._light = lightObject.AddComponent<Light>();
        puddle._light.type = LightType.Point;
        puddle._light.color = new Color(0.5f, 1f, 0.2f);
        puddle._light.range = radius * 2.5f;
        puddle._light.intensity = 2f;
        puddle._light.shadows = LightShadows.None;

        // 지글지글 소리 (그 자리에서 반복)
        AudioClip sizzle = GameSounds.Get("acid_sizzle_loop");
        if (sizzle != null)
        {
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = sizzle;
            source.loop = true;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = 25f;
            source.volume = 0.7f;
            source.Play();
        }

        GameSounds.PlayAt("acid_splash", position, 1f, 0.1f);
        return puddle;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _age += dt;

        // 퍼졌다가 끝날 때 줄어든다.
        float grow = Mathf.Clamp01(_age / 0.35f);
        float shrink = Mathf.Clamp01((_life - _age) / 0.8f);
        float size = _radius * 2f * grow * shrink;
        float wobble = 1f + 0.04f * Mathf.Sin(_age * 6f);
        _visual.localScale = new Vector3(size * wobble, 0.01f, size / wobble);
        _light.intensity = 2f * shrink * (0.8f + 0.2f * Mathf.Sin(_age * 9f));

        DamagePlayer(dt, size * 0.5f);

        if (_age >= _life)
        {
            Destroy(gameObject);
        }
    }

    private void DamagePlayer(float dt, float radius)
    {
        if (_player == null)
        {
            _player = FindAnyObjectByType<Player>();
            if (_player == null)
            {
                return;
            }
        }

        if (GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        Vector3 offset = _player.transform.position - transform.position;
        bool inside = Mathf.Abs(offset.y) < 2f && new Vector2(offset.x, offset.z).magnitude < radius;
        if (!inside)
        {
            _timeInside = Mathf.Max(0f, _timeInside - dt * 2f);
            return;
        }

        _timeInside += dt;
        _tickTimer -= dt;
        if (_tickTimer > 0f)
        {
            return;
        }

        // 0.5초마다. 처음엔 약하다가 오래 있을수록 세진다.
        _tickTimer = 0.5f;
        DamageInfo info = new DamageInfo();
        info.Value = Mathf.Lerp(1.5f, 7f, Mathf.Clamp01(_timeInside / 3f));
        info.HitPoint = _player.transform.position;
        info.HitNormal = Vector3.up;
        _player.TakeDamage(info);
    }
}
