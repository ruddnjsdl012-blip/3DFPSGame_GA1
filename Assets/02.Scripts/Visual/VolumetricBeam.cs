using UnityEngine;
using UnityEngine.Rendering;

// 스포트라이트(손전등 등)에 붙이는 가벼운 빛줄기
// 원뿔 메시를 만들어 L4D/Volumetric Beam 머티리얼(더하기 블렌딩)로 그린다. 조명이 꺼지면 같이 꺼진다.
// 머티리얼은 Resources/L4DVisual/VolumetricBeam 을 쓴다. 없으면 아무것도 하지 않는다.
[RequireComponent(typeof(Light))]
public class VolumetricBeam : MonoBehaviour
{
    private const string MaterialPath = "L4DVisual/VolumetricBeam";
    private const int Segments = 24;

    [SerializeField] private float _lengthScale = 0.45f;   // 빛 범위 중 빛줄기로 보일 길이 비율
    [SerializeField] private float _intensity = 1f;

    private Light _light;
    private MeshRenderer _renderer;
    private Transform _cone;
    private MaterialPropertyBlock _block;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static Material _material;
    private static Mesh _mesh;
    private float _builtAngle = -1f;
    private float _builtLength = -1f;

    public static VolumetricBeam Attach(Light light, float intensity)
    {
        if (light == null || light.type != LightType.Spot) return null;
        VolumetricBeam beam = light.GetComponent<VolumetricBeam>();
        if (beam == null) beam = light.gameObject.AddComponent<VolumetricBeam>();
        beam._intensity = intensity;
        return beam;
    }

    private void Awake()
    {
        _light = GetComponent<Light>();
        if (_material == null) _material = Resources.Load<Material>(MaterialPath);
        if (_material == null)
        {
            enabled = false;
            return;
        }

        if (_mesh == null) _mesh = BuildCone();
        _cone = new GameObject("BeamCone").transform;
        _cone.SetParent(transform, false);
        _cone.gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
        _renderer = _cone.gameObject.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = _material;
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.lightProbeUsage = LightProbeUsage.Off;
        _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        _block = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        if (_renderer == null) return;
        bool on = _light.enabled && _light.intensity > 0.01f;
        if (_renderer.enabled != on) _renderer.enabled = on;
        if (!on) return;

        // 원뿔 크기를 조명 각도/범위에 맞춘다. (값이 바뀔 때만)
        float length = _light.range * _lengthScale;
        if (!Mathf.Approximately(_builtAngle, _light.spotAngle) || !Mathf.Approximately(_builtLength, length))
        {
            _builtAngle = _light.spotAngle;
            _builtLength = length;
            float radius = Mathf.Tan(_light.spotAngle * 0.5f * Mathf.Deg2Rad) * length * 0.8f;
            _cone.localScale = new Vector3(radius, radius, length);
        }

        Color color = _light.color;
        color.a = 0.05f;
        _block.SetColor(ColorId, color);
        _block.SetFloat(IntensityId, _intensity * Mathf.Clamp(_light.intensity / 8f, 0.2f, 2f));
        _renderer.SetPropertyBlock(_block);
    }

    // 꼭짓점이 원점, +Z 방향으로 길이 1, 끝 반지름 1인 열린 원뿔 (uv.y = 길이 방향 0~1)
    private static Mesh BuildCone()
    {
        Vector3[] vertices = new Vector3[(Segments + 1) * 2];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[Segments * 6];
        for (int i = 0; i <= Segments; i++)
        {
            float angle = (float)i / Segments * Mathf.PI * 2f;
            Vector3 ring = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 normal = (ring - Vector3.forward).normalized;   // 원뿔 면의 실제 바깥 방향
            vertices[i * 2] = ring * 0.02f;
            vertices[i * 2 + 1] = ring + Vector3.forward;
            normals[i * 2] = normal;
            normals[i * 2 + 1] = normal;
            uv[i * 2] = new Vector2((float)i / Segments, 0f);
            uv[i * 2 + 1] = new Vector2((float)i / Segments, 1f);
        }

        for (int i = 0; i < Segments; i++)
        {
            int a = i * 2;
            triangles[i * 6] = a;
            triangles[i * 6 + 1] = a + 1;
            triangles[i * 6 + 2] = a + 3;
            triangles[i * 6 + 3] = a;
            triangles[i * 6 + 4] = a + 3;
            triangles[i * 6 + 5] = a + 2;
        }

        Mesh mesh = new Mesh { name = "BeamCone" };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.forward * 0.5f, new Vector3(2f, 2f, 1f));
        return mesh;
    }
}
