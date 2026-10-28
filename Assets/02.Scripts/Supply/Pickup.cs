using System.Collections.Generic;
using UnityEngine;

public enum PickupType
{
    Ammo,     // 탄약 더미: 예비 총알을 가득 채운다. 없어지지 않고 계속 쓸 수 있다. (L4D)
    Medkit,   // 구급상자: 하나만 가지고 다닐 수 있다. Q로 사용
    Grenade,  // 수류탄: 최대 3개. 우클릭으로 던진다.
}

// 바닥에 놓인 보급품 하나.
// 모양은 기본 도형(상자, 공, 원기둥)으로 코드에서 만든다. (따로 모델이 필요 없다)
// 줍기는 PlayerInventory가 한다. (가까이 가서 E)
public class Pickup : MonoBehaviour
{
    // 맵에 있는 모든 보급품 (PlayerInventory가 가장 가까운 것을 찾을 때 쓴다)
    public static readonly List<Pickup> All = new List<Pickup>();

    private static readonly Color AmmoColor = new Color(0.25f, 0.33f, 0.18f);
    private static readonly Color MedkitColor = new Color(0.92f, 0.92f, 0.9f);
    private static readonly Color GrenadeColor = new Color(0.22f, 0.3f, 0.16f);

    private PickupType _type;
    private Transform _visual;
    private Light _glow;
    private float _glowBase;
    private bool _highlight;
    private float _time;

    public PickupType Type => _type;
    public bool Reusable => _type == PickupType.Ammo;

    public string DisplayName
    {
        get
        {
            switch (_type)
            {
                case PickupType.Ammo: return "탄약";
                case PickupType.Medkit: return "구급상자";
                default: return "수류탄";
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        All.Clear();
        Materials.Clear();
    }

    public static Pickup Create(PickupType type, Vector3 position, float yaw, Transform parent)
    {
        GameObject go = new GameObject("Pickup_" + type);
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        Pickup pickup = go.AddComponent<Pickup>();
        pickup._type = type;
        pickup.Build();
        return pickup;
    }

    private void OnEnable()
    {
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    // 플레이어가 가까이 오면 더 밝게 빛난다.
    public void SetHighlight(bool on)
    {
        _highlight = on;
    }

    private void Update()
    {
        _time += Time.deltaTime;

        // 작은 보급품은 천천히 돌면서 살짝 떠 있다. (멀리서도 눈에 띄게)
        if (_type != PickupType.Ammo && _visual != null)
        {
            _visual.localRotation = Quaternion.Euler(0f, _time * 60f, 0f);
            _visual.localPosition = new Vector3(0f, 0.12f + Mathf.Sin(_time * 2.5f) * 0.04f, 0f);
        }

        if (_glow != null)
        {
            float pulse = 0.75f + 0.25f * Mathf.Sin(_time * 3f);
            _glow.intensity = _glowBase * pulse * (_highlight ? 2.2f : 1f);
        }
    }

    // ===================== 모양 만들기 =====================

    private void Build()
    {
        _visual = new GameObject("Visual").transform;
        _visual.SetParent(transform, false);

        Color glowColor;
        switch (_type)
        {
            case PickupType.Ammo:
                BuildAmmo();
                glowColor = new Color(1f, 0.85f, 0.4f);
                break;
            case PickupType.Medkit:
                BuildMedkit();
                glowColor = new Color(1f, 0.35f, 0.3f);
                break;
            default:
                BuildGrenade();
                glowColor = new Color(0.55f, 1f, 0.45f);
                break;
        }

        GameObject light = new GameObject("Glow");
        light.transform.SetParent(transform, false);
        light.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        _glow = light.AddComponent<Light>();
        _glow.type = LightType.Point;
        _glow.color = glowColor;
        _glow.range = _type == PickupType.Ammo ? 3.5f : 2.5f;
        _glowBase = _type == PickupType.Ammo ? 2.5f : 1.8f;
        _glow.intensity = _glowBase;
        _glow.shadows = LightShadows.None;
    }

    // 탄약 더미: 국방색 상자 두 개 + 위에 놓인 탄창들 + 노란 띠
    private void BuildAmmo()
    {
        Part(PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 0.4f, 0.5f), AmmoColor, Vector3.zero);
        Part(PrimitiveType.Cube, new Vector3(0.05f, 0.5f, 0.02f), new Vector3(0.7f, 0.2f, 0.42f), AmmoColor * 1.15f, new Vector3(0f, 8f, 0f));
        Part(PrimitiveType.Cube, new Vector3(0f, 0.2f, -0.255f), new Vector3(0.92f, 0.08f, 0.02f), new Color(0.95f, 0.75f, 0.15f), Vector3.zero, 0.6f);
        for (int i = 0; i < 4; i++)
        {
            Part(PrimitiveType.Cube, new Vector3(-0.22f + i * 0.15f, 0.66f, 0f), new Vector3(0.06f, 0.14f, 0.12f), new Color(0.15f, 0.15f, 0.15f), new Vector3(0f, 0f, -12f + i * 6f));
            Part(PrimitiveType.Cylinder, new Vector3(-0.22f + i * 0.15f, 0.75f, 0f), new Vector3(0.025f, 0.03f, 0.025f), new Color(0.85f, 0.65f, 0.2f), Vector3.zero, 0.3f);
        }
    }

    // 구급상자: 흰 상자 + 위와 옆에 빨간 십자
    private void BuildMedkit()
    {
        Part(PrimitiveType.Cube, new Vector3(0f, 0.11f, 0f), new Vector3(0.46f, 0.22f, 0.32f), MedkitColor, Vector3.zero);
        Color red = new Color(0.85f, 0.08f, 0.08f);
        Part(PrimitiveType.Cube, new Vector3(0f, 0.225f, 0f), new Vector3(0.22f, 0.012f, 0.07f), red, Vector3.zero, 0.5f);
        Part(PrimitiveType.Cube, new Vector3(0f, 0.225f, 0f), new Vector3(0.07f, 0.012f, 0.22f), red, Vector3.zero, 0.5f);
        Part(PrimitiveType.Cube, new Vector3(0f, 0.11f, -0.165f), new Vector3(0.16f, 0.05f, 0.012f), red, Vector3.zero, 0.5f);
        Part(PrimitiveType.Cube, new Vector3(0f, 0.11f, -0.165f), new Vector3(0.05f, 0.16f, 0.012f), red, Vector3.zero, 0.5f);
        Part(PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(0.18f, 0.03f, 0.03f), new Color(0.2f, 0.2f, 0.2f), Vector3.zero);
    }

    // 수류탄 두 개
    private void BuildGrenade()
    {
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? -0.09f : 0.09f;
            Part(PrimitiveType.Sphere, new Vector3(x, 0.1f, 0f), new Vector3(0.15f, 0.19f, 0.15f), GrenadeColor, Vector3.zero);
            Part(PrimitiveType.Cylinder, new Vector3(x, 0.21f, 0f), new Vector3(0.06f, 0.025f, 0.06f), new Color(0.35f, 0.35f, 0.35f), Vector3.zero);
            Part(PrimitiveType.Cube, new Vector3(x + 0.035f, 0.18f, 0f), new Vector3(0.02f, 0.1f, 0.03f), new Color(0.4f, 0.4f, 0.4f), new Vector3(0f, 0f, -15f));
            Part(PrimitiveType.Cylinder, new Vector3(x - 0.04f, 0.24f, 0f), new Vector3(0.04f, 0.004f, 0.04f), new Color(0.9f, 0.8f, 0.3f), new Vector3(90f, 0f, 0f), 0.4f);
        }
    }

    private void Part(PrimitiveType shape, Vector3 position, Vector3 scale, Color color, Vector3 euler, float emission = 0f)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        Destroy(part.GetComponent<Collider>());  // 총알이 막히지 않게
        part.transform.SetParent(_visual, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(euler);
        part.transform.localScale = scale;

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = GetMaterial(color, emission);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // 같은 색은 재질 하나를 같이 쓴다.
    private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

    private static Material GetMaterial(Color color, float emission)
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
        material.SetFloat("_Smoothness", 0.35f);
        if (emission > 0f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * emission);
        }

        Materials[key] = material;
        return material;
    }
}
