using UnityEngine;

// 벽이나 바닥에 남는 피 자국 하나. 시간이 지나면 옅어지다 사라진다. (BloodEffects가 돌려 쓴다)
public class BloodDecal : MonoBehaviour
{
    private MeshRenderer _renderer;
    private MaterialPropertyBlock _block;
    private Color _color;
    private float _life;
    private float _age;
    private float _size;
    private float _growTo;
    private float _growTime;

    public static BloodDecal Create(Material material)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "BloodDecal";
        Destroy(quad.GetComponent<Collider>());
        BloodDecal decal = quad.AddComponent<BloodDecal>();
        decal._renderer = quad.GetComponent<MeshRenderer>();
        decal._renderer.sharedMaterial = material;
        decal._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        decal._block = new MaterialPropertyBlock();
        return decal;
    }

    public void Place(Vector3 point, Vector3 normal, float size, Color color, Texture2D texture, float life)
    {
        gameObject.SetActive(true);
        // 면에서 아주 살짝 띄워서 깜빡이지 않게. 쿼드의 앞면이 면 바깥을 보도록 돌리고, 아무 각도로 회전
        transform.position = point + normal * 0.012f;
        transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        _size = size;
        _growTo = size;
        _growTime = 0f;
        transform.localScale = Vector3.one * size;
        _color = color;
        _life = life;
        _age = 0f;

        _block.Clear();
        _block.SetTexture("_BaseMap", texture);
        _block.SetTexture("_MainTex", texture);
        Apply(1f);
    }

    // 피 웅덩이처럼 천천히 커진다.
    public void Grow(float to, float seconds)
    {
        _growTo = to;
        _growTime = Mathf.Max(0.1f, seconds);
    }

    private void Update()
    {
        _age += Time.deltaTime;

        if (_growTime > 0f && _size < _growTo)
        {
            // 처음엔 빠르게, 나중엔 천천히 퍼진다.
            float t = Mathf.Clamp01(_age / _growTime);
            float eased = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * Mathf.Lerp(_size, _growTo, eased);
        }

        // 마지막 3초 동안 옅어진다.
        float fade = Mathf.Clamp01((_life - _age) / 3f);
        Apply(fade);
        if (_age >= _life)
        {
            gameObject.SetActive(false);
        }
    }

    private void Apply(float alpha)
    {
        Color c = _color;
        c.a = alpha;
        _block.SetColor("_BaseColor", c);
        _block.SetColor("_Color", c);
        _renderer.SetPropertyBlock(_block);
    }
}
