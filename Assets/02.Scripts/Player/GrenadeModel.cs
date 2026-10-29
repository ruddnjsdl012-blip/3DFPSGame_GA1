using System.Collections.Generic;
using UnityEngine;

// 수류탄 모양을 코드로 만든다. (모델 파일 없이 기본 도형으로)
//  - 국방색 달걀 모양 몸통 + 가로 홈 + 위쪽 금속 신관 + 옆으로 내려오는 안전 손잡이 + 안전핀 고리
// 손에 든 수류탄(GrenadeArmView)과 던진 수류탄(PlayerBombFire)이 같은 모양을 쓴다.
public static class GrenadeModel
{
    private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Materials.Clear();
    }

    // height: 수류탄 전체 높이 (m). 진짜 수류탄은 약 0.11m
    public static GameObject Create(float height = 0.11f)
    {
        GameObject root = new GameObject("GrenadeModel");
        float s = height / 0.11f;

        Color body = new Color(0.27f, 0.32f, 0.17f);
        Color groove = new Color(0.16f, 0.19f, 0.1f);
        Color metal = new Color(0.55f, 0.55f, 0.52f);
        Color ring = new Color(0.78f, 0.7f, 0.35f);

        // 몸통 (살짝 길쭉한 공)
        Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 0.04f, 0f), new Vector3(0.06f, 0.072f, 0.06f), Vector3.zero, body, 0.35f, s);
        // 가로 홈 두 줄 + 세로 홈 두 줄 (파인애플 무늬 느낌)
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.052f, 0f), new Vector3(0.062f, 0.0018f, 0.062f), Vector3.zero, groove, 0.2f, s);
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(0.062f, 0.0018f, 0.062f), Vector3.zero, groove, 0.2f, s);
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.062f, 0.0012f, 0.075f), new Vector3(90f, 0f, 0f), groove, 0.2f, s);
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.062f, 0.0012f, 0.075f), new Vector3(90f, 90f, 0f), groove, 0.2f, s);
        // 위쪽 신관 (금속 원기둥)
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(0.024f, 0.011f, 0.024f), Vector3.zero, metal, 0.7f, s);
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.093f, 0f), new Vector3(0.016f, 0.004f, 0.016f), Vector3.zero, metal, 0.7f, s);
        // 안전 손잡이 (옆으로 길게 내려오는 판)
        Part(root.transform, PrimitiveType.Cube, new Vector3(0.022f, 0.088f, 0f), new Vector3(0.026f, 0.005f, 0.014f), new Vector3(0f, 0f, -12f), metal, 0.6f, s);
        Part(root.transform, PrimitiveType.Cube, new Vector3(0.034f, 0.058f, 0f), new Vector3(0.005f, 0.055f, 0.014f), new Vector3(0f, 0f, 8f), metal, 0.6f, s);
        // 안전핀 고리
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(-0.018f, 0.086f, 0f), new Vector3(0.022f, 0.0012f, 0.022f), new Vector3(0f, 0f, 90f), ring, 0.8f, s);
        return root;
    }

    // 던진 수류탄(Bomb 프리팹)의 겉모습을 수류탄 모양으로 바꾼다. 콜라이더, 물리, 폭발 스크립트는 그대로 둔다.
    public static void ReplaceVisual(GameObject bomb, float height = 0.11f)
    {
        Bounds bounds = new Bounds(bomb.transform.position, Vector3.zero);
        bool found = false;
        foreach (Renderer renderer in bomb.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
            {
                continue;  // 연기 꼬리(Trail), 파티클은 그대로
            }

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }

            renderer.enabled = false;
        }

        GameObject model = Create(height);
        model.transform.SetParent(bomb.transform, false);
        // 원래 모양의 가운데에 놓는다. (수류탄 몸통 가운데는 높이의 약 40%)
        Vector3 center = found ? bomb.transform.InverseTransformPoint(bounds.center) : Vector3.zero;
        Vector3 lossy = bomb.transform.lossyScale;
        model.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, lossy.x), 1f / Mathf.Max(0.0001f, lossy.y), 1f / Mathf.Max(0.0001f, lossy.z));
        model.transform.localPosition = center - Vector3.Scale(model.transform.localScale, new Vector3(0f, height * 0.4f, 0f));
    }

    private static void Part(Transform parent, PrimitiveType shape, Vector3 position, Vector3 scale, Vector3 euler, Color color, float smoothness, float s)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        Object.Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position * s;
        part.transform.localRotation = Quaternion.Euler(euler);
        // 유니티 원기둥은 높이가 2라서 y를 반으로
        part.transform.localScale = shape == PrimitiveType.Cylinder ? new Vector3(scale.x, scale.y * 0.5f, scale.z) * s : scale * s;
        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = GetMaterial(color, smoothness);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    private static Material GetMaterial(Color color, float smoothness)
    {
        Color key = new Color(color.r, color.g, color.b, smoothness);
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
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", smoothness > 0.5f ? 0.8f : 0f);
        Materials[key] = material;
        return material;
    }
}
