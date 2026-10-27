using System.Collections.Generic;
using UnityEngine;

// 코드로 만드는 효과(부머 토사물, 산성액 등)에 쓰는 머티리얼과 도형 도우미.
// URP Lit 셰이더를 쓰고, 없으면 Standard 셰이더를 쓴다.
public static class RuntimeMaterials
{
    private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Cache.Clear();
    }

    public static Material Get(string key, Color color, Color emission, float smoothness = 0.5f)
    {
        if (Cache.TryGetValue(key, out Material cached) && cached != null)
        {
            return cached;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader);
        material.name = "Runtime_" + key;
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Glossiness", smoothness);
        if (emission.maxColorComponent > 0.001f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        Cache[key] = material;
        return material;
    }

    // 콜라이더 없는 기본 도형 (총알과 이동을 막지 않음)
    public static GameObject CreateShape(PrimitiveType shape, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Object.Destroy(collider);
        }

        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        Renderer renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }
}
