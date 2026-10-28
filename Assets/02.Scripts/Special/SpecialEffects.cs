using System.Collections.Generic;
using UnityEngine;

// 특수 좀비 효과를 코드로 만드는 도우미 (모델/이펙트 파일 없이 기본 도형으로)
public static class SpecialEffects
{
    private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();
    private static float _lastBileTime = -100f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Materials.Clear();
        _lastBileTime = -100f;
    }

    // 부머 담즙: 화면이 초록색으로 가려지고 좀비 무리가 몰려온다.
    public static void Bile(Vector3 from)
    {
        BileOverlay.Show(8f);
        GameSounds.Play2D("acid_splash", 0.8f);
        CameraShake.ShakeMain(0.35f);

        // 무리는 너무 자주 부르지 않는다.
        if (Time.time - _lastBileTime > 8f && AIDirector.Instance != null)
        {
            _lastBileTime = Time.time;
            AIDirector.Instance.OnPlayerBiled();
        }
    }

    // 부머가 토하는 줄기
    public static void VomitStream(Vector3 from, Vector3 to)
    {
        GameObject stream = Shape(PrimitiveType.Cylinder, new Color(0.5f, 0.8f, 0.15f), 0.6f);
        Vector3 middle = (from + to) * 0.5f;
        float length = Vector3.Distance(from, to);
        stream.transform.position = middle;
        stream.transform.up = (to - from).normalized;
        EffectAnimator.Add(stream, new Vector3(0.05f, length * 0.5f, 0.05f), new Vector3(0.35f, length * 0.5f, 0.35f), 0.08f, 0.5f);
    }

    // 펑 터지는 공 (부머 폭발, 산성 튐)
    public static void Burst(Vector3 position, Color color, float radius)
    {
        GameObject ball = Shape(PrimitiveType.Sphere, color, 0.8f);
        ball.transform.position = position;
        EffectAnimator.Add(ball, Vector3.one * 0.2f, Vector3.one * radius * 2f, 0.12f, 0.35f);

        GameObject light = new GameObject("BurstLight");
        light.transform.position = position;
        Light l = light.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = radius * 3f;
        l.intensity = 4f;
        Object.Destroy(light, 0.4f);
    }

    // 콜라이더 없는 기본 도형 (총알이나 플레이어를 막지 않는다)
    public static GameObject Shape(PrimitiveType type, Color color, float emission)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        Renderer renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = GetMaterial(color, emission);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    public static Material GetMaterial(Color color, float emission)
    {
        Color key = new Color(color.r, color.g, color.b, emission);
        if (Materials.TryGetValue(key, out Material cached) && cached != null)
        {
            return cached;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetFloat("_Smoothness", 0.8f);
        if (emission > 0f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * emission);
        }

        Materials[key] = material;
        return material;
    }
}
