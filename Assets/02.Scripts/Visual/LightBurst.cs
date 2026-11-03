using System.Collections.Generic;
using UnityEngine;

// 짧게 번쩍이는 실시간 조명 (총구 화염, 폭발)
// 조명 16개를 미리 만들어 돌려 쓴다. (매번 만들고 지우지 않는다)
public static class LightBurst
{
    private const int PoolSize = 16;

    private sealed class Burst
    {
        public Light Light;
        public float Start;
        public float Duration;
        public float Intensity;
        public bool Flicker;
        public float Seed;
    }

    private sealed class Runner : MonoBehaviour
    {
        private void LateUpdate()
        {
            Tick();
        }
    }

    private static readonly List<Burst> Pool = new List<Burst>(PoolSize);
    private static Transform _root;
    private static int _next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Pool.Clear();
        _root = null;
        _next = 0;
    }

    // 총구 화염 (동료/적 총)
    public static void Muzzle(Vector3 position)
    {
        Spawn(position, new Color(1f, 0.78f, 0.45f), 3.2f, 7f, 0.07f, false);
    }

    // 드럼통/수류탄 폭발: 주위를 주황색으로 크게 밝히고, 화면 후처리도 번쩍이게 한다.
    public static void Explosion(Vector3 position)
    {
        Spawn(position + Vector3.up * 1.2f, new Color(1f, 0.55f, 0.24f), 18f, 24f, 0.9f, true);
        CombatPostFX.Explosion(position, 1f);
    }

    public static void Spawn(Vector3 position, Color color, float intensity, float range, float duration, bool flicker)
    {
        if (!EnsurePool()) return;

        // 비어 있는 것을 먼저 쓰고, 다 쓰는 중이면 가장 오래된 것을 다시 쓴다.
        Burst burst = null;
        for (int i = 0; i < Pool.Count && burst == null; i++)
        {
            if (!Pool[i].Light.enabled) burst = Pool[i];
        }

        if (burst == null)
        {
            burst = Pool[_next];
            _next = (_next + 1) % Pool.Count;
        }

        burst.Light.transform.position = position;
        burst.Light.color = color;
        burst.Light.range = range;
        burst.Light.intensity = intensity;
        burst.Light.enabled = true;
        burst.Start = Time.time;
        burst.Duration = Mathf.Max(0.01f, duration);
        burst.Intensity = intensity;
        burst.Flicker = flicker;
        burst.Seed = Random.value * 100f;
    }

    private static bool EnsurePool()
    {
        if (_root != null) return true;
        GameObject root = new GameObject("@LightBurst");
        root.AddComponent<Runner>();
        _root = root.transform;
        Pool.Clear();
        for (int i = 0; i < PoolSize; i++)
        {
            GameObject go = new GameObject("Burst" + i);
            go.transform.SetParent(_root, false);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.enabled = false;
            Pool.Add(new Burst { Light = light });
        }

        return true;
    }

    private static void Tick()
    {
        float now = Time.time;
        for (int i = 0; i < Pool.Count; i++)
        {
            Burst burst = Pool[i];
            if (burst.Light == null || !burst.Light.enabled) continue;
            float t = (now - burst.Start) / burst.Duration;
            if (t >= 1f)
            {
                burst.Light.enabled = false;
                continue;
            }

            // 확 밝아졌다가 빠르게 줄어든다. 폭발은 불처럼 일렁인다.
            float fade = (1f - t) * (1f - t);
            float flicker = burst.Flicker ? 0.7f + 0.6f * Mathf.PerlinNoise(now * 22f, burst.Seed) : 1f;
            burst.Light.intensity = burst.Intensity * fade * flicker;
        }
    }
}
