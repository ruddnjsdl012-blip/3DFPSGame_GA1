using UnityEngine;

// 부머가 터질 때의 효과: 초록 섬광 + 사방으로 튀는 덩어리 + 바닥에 남는 웅덩이.
// 외부 파티클 없이 코드로 만든다.
public class BileBurstEffect : MonoBehaviour
{
    private const int ChunkCount = 16;
    private const float Gravity = -14f;

    private Transform[] _chunks;
    private Vector3[] _velocities;
    private float[] _sizes;
    private bool[] _landed;
    private float _groundY;
    private Light _flash;
    private Transform _puddle;
    private float _puddleSize;
    private float _age;

    public static void Spawn(Vector3 groundPosition, float radius)
    {
        GameObject go = new GameObject("BileBurst");
        go.transform.position = groundPosition;
        go.AddComponent<BileBurstEffect>().Build(radius);
    }

    private void Build(float radius)
    {
        _groundY = transform.position.y + 0.02f;
        Material chunk = RuntimeMaterials.Get("bile_chunk", new Color(0.45f, 0.62f, 0.12f), new Color(0.08f, 0.14f, 0f), 0.85f);
        Material pool = RuntimeMaterials.Get("bile_pool", new Color(0.32f, 0.45f, 0.08f), new Color(0.05f, 0.09f, 0f), 0.95f);

        _chunks = new Transform[ChunkCount];
        _velocities = new Vector3[ChunkCount];
        _sizes = new float[ChunkCount];
        _landed = new bool[ChunkCount];
        for (int i = 0; i < ChunkCount; i++)
        {
            _sizes[i] = Random.Range(0.08f, 0.2f);
            GameObject go = RuntimeMaterials.CreateShape(PrimitiveType.Sphere, transform,
                new Vector3(0f, 1.1f, 0f) + Random.insideUnitSphere * 0.3f, Vector3.one * _sizes[i], chunk);
            _chunks[i] = go.transform;
            Vector2 flat = Random.insideUnitCircle.normalized * Random.Range(2f, radius * 1.6f);
            _velocities[i] = new Vector3(flat.x, Random.Range(2.5f, 6f), flat.y);
        }

        // 바닥 웅덩이 (납작한 원기둥)
        _puddleSize = radius * 0.9f;
        GameObject puddle = RuntimeMaterials.CreateShape(PrimitiveType.Cylinder, transform,
            new Vector3(0f, 0.015f, 0f), new Vector3(0.1f, 0.01f, 0.1f), pool);
        _puddle = puddle.transform;

        GameObject lightObject = new GameObject("Flash");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        _flash = lightObject.AddComponent<Light>();
        _flash.type = LightType.Point;
        _flash.color = new Color(0.6f, 1f, 0.3f);
        _flash.range = radius * 2.5f;
        _flash.intensity = 6f;
        _flash.shadows = LightShadows.None;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _age += dt;

        if (_flash != null)
        {
            _flash.intensity = 6f * Mathf.Exp(-_age * 9f);
            if (_age > 0.6f)
            {
                Destroy(_flash.gameObject);
                _flash = null;
            }
        }

        // 덩어리: 포물선으로 날아가 바닥에 떨어진 뒤 납작해지며 사라진다.
        for (int i = 0; i < ChunkCount; i++)
        {
            Transform chunk = _chunks[i];
            if (chunk == null) continue;

            if (_landed[i] == false)
            {
                _velocities[i].y += Gravity * dt;
                Vector3 position = chunk.position + _velocities[i] * dt;
                if (position.y <= _groundY)
                {
                    position.y = _groundY;
                    _landed[i] = true;
                }

                chunk.position = position;
            }
            else
            {
                float s = _sizes[i] * Mathf.Clamp01(1f - (_age - 2.5f) / 2f);
                chunk.localScale = new Vector3(s * 1.8f, s * 0.25f, s * 1.8f);
            }
        }

        // 웅덩이: 빠르게 퍼졌다가 천천히 마르며 줄어든다.
        float grow = Mathf.Clamp01(_age / 0.35f);
        float dry = Mathf.Clamp01((_age - 5f) / 3f);
        float size = _puddleSize * Mathf.SmoothStep(0f, 1f, grow) * (1f - dry);
        _puddle.localScale = new Vector3(size * 2f, 0.01f, size * 2f);

        if (_age > 8.2f)
        {
            Destroy(gameObject);
        }
    }
}
