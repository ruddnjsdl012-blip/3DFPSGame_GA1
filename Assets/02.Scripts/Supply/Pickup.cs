using System.Collections.Generic;
using UnityEngine;

public enum PickupType
{
    Ammo = 0,
    Medkit = 1,
    Grenade = 2,
    Weapon = 3,
}

public class Pickup : MonoBehaviour
{
    public static readonly List<Pickup> All = new List<Pickup>();
    private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();
    private PickupType _type;
    private WeaponSlot _weapon;
    private Transform _visual;
    private Transform _camera;
    private Renderer _weaponMarker;
    private Light _glow;
    private float _glowBase;
    private bool _highlight;
    private float _time;
    private float _nextCameraSearch;

    public PickupType Type => _type;
    public WeaponSlot Weapon => _weapon;
    public bool Reusable => _type == PickupType.Ammo;
    public Vector3 FocusPoint => transform.position + Vector3.up * (_type == PickupType.Ammo ? 0.4f : _type == PickupType.Weapon ? 0.09f : 0.2f);
    public string DisplayName
    {
        get
        {
            switch (_type)
            {
                case PickupType.Ammo: return "탄약";
                case PickupType.Medkit: return "구급상자";
                case PickupType.Weapon: return PlayerWeapons.DisplayName(_weapon);
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
        return CreateInternal(type, WeaponSlot.None, position, yaw, parent);
    }

    public static Pickup CreateWeapon(WeaponSlot weapon, Vector3 position, float yaw, Transform parent)
    {
        return CreateInternal(PickupType.Weapon, weapon, position, yaw, parent);
    }

    private static Pickup CreateInternal(PickupType type, WeaponSlot weapon, Vector3 position, float yaw, Transform parent)
    {
        GameObject go = new GameObject("Pickup_" + (type == PickupType.Weapon ? weapon.ToString() : type.ToString()));
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        Pickup pickup = go.AddComponent<Pickup>();
        pickup._type = type;
        pickup._weapon = weapon;
        pickup.Build();
        return pickup;
    }

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    public void SetHighlight(bool on)
    {
        if (_highlight == on) return;
        _highlight = on;
        if (_weaponMarker != null)
        {
            _weaponMarker.sharedMaterial = GetMaterial(on ? new Color(0.25f, 0.8f, 1f) : new Color(0.08f, 0.18f, 0.24f), on ? 0.9f : 0.25f);
        }
    }

    public void Consume()
    {
        if (Reusable) return;
        SetHighlight(false);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void Update()
    {
        _time += Time.deltaTime;
        if (_camera == null && Time.time >= _nextCameraSearch)
        {
            _nextCameraSearch = Time.time + 1f;
            _camera = Camera.main != null ? Camera.main.transform : null;
        }
        bool nearby = _highlight || (_camera != null && (_camera.position - transform.position).sqrMagnitude < 324f);
        if (_glow != null) _glow.enabled = nearby;
        if (!nearby) return;
        if ((_type == PickupType.Medkit || _type == PickupType.Grenade) && _visual != null)
        {
            _visual.localRotation = Quaternion.Euler(0f, _time * 60f, 0f);
            _visual.localPosition = new Vector3(0f, 0.12f + Mathf.Sin(_time * 2.5f) * 0.04f, 0f);
        }
        if (_glow != null)
        {
            _glow.intensity = _glowBase * (0.75f + 0.25f * Mathf.Sin(_time * 3f)) * (_highlight ? 2.2f : 1f);
        }
    }

    private void Build()
    {
        _visual = new GameObject("Visual").transform;
        _visual.SetParent(transform, false);
        if (_type == PickupType.Weapon)
        {
            WeaponModels.CreateDisplay(_weapon, _visual);
            _weaponMarker = Part(PrimitiveType.Cube, Vector3.zero, new Vector3(0.96f, 0.012f, 1.02f),
                new Color(0.08f, 0.18f, 0.24f), Vector3.zero, 0.25f);
            return;
        }
        Color glowColor;
        if (_type == PickupType.Ammo)
        {
            Color green = new Color(0.25f, 0.33f, 0.18f);
            Part(PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 0.4f, 0.5f), green, Vector3.zero);
            Part(PrimitiveType.Cube, new Vector3(0.05f, 0.5f, 0.02f), new Vector3(0.7f, 0.2f, 0.42f), green * 1.15f, new Vector3(0f, 8f, 0f));
            Part(PrimitiveType.Cube, new Vector3(0f, 0.2f, -0.255f), new Vector3(0.92f, 0.08f, 0.02f), new Color(0.95f, 0.75f, 0.15f), Vector3.zero, 0.6f);
            for (int i = 0; i < 4; i++)
            {
                Part(PrimitiveType.Cube, new Vector3(-0.22f + i * 0.15f, 0.66f, 0f), new Vector3(0.06f, 0.14f, 0.12f), new Color(0.15f, 0.15f, 0.15f), new Vector3(0f, 0f, -12f + i * 6f));
                Part(PrimitiveType.Cylinder, new Vector3(-0.22f + i * 0.15f, 0.75f, 0f), new Vector3(0.025f, 0.03f, 0.025f), new Color(0.85f, 0.65f, 0.2f), Vector3.zero, 0.3f);
            }
            glowColor = new Color(1f, 0.85f, 0.4f);
        }
        else if (_type == PickupType.Medkit)
        {
            Color red = new Color(0.85f, 0.08f, 0.08f);
            Part(PrimitiveType.Cube, new Vector3(0f, 0.11f, 0f), new Vector3(0.46f, 0.22f, 0.32f), new Color(0.92f, 0.92f, 0.9f), Vector3.zero);
            Part(PrimitiveType.Cube, new Vector3(0f, 0.225f, 0f), new Vector3(0.22f, 0.012f, 0.07f), red, Vector3.zero, 0.5f);
            Part(PrimitiveType.Cube, new Vector3(0f, 0.225f, 0f), new Vector3(0.07f, 0.012f, 0.22f), red, Vector3.zero, 0.5f);
            Part(PrimitiveType.Cube, new Vector3(0f, 0.11f, -0.165f), new Vector3(0.16f, 0.05f, 0.012f), red, Vector3.zero, 0.5f);
            Part(PrimitiveType.Cube, new Vector3(0f, 0.11f, -0.165f), new Vector3(0.05f, 0.16f, 0.012f), red, Vector3.zero, 0.5f);
            Part(PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(0.18f, 0.03f, 0.03f), Color.gray, Vector3.zero);
            glowColor = new Color(1f, 0.35f, 0.3f);
        }
        else
        {
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.09f : 0.09f;
                Part(PrimitiveType.Sphere, new Vector3(x, 0.1f, 0f), new Vector3(0.15f, 0.19f, 0.15f), new Color(0.22f, 0.3f, 0.16f), Vector3.zero);
                Part(PrimitiveType.Cylinder, new Vector3(x, 0.21f, 0f), new Vector3(0.06f, 0.025f, 0.06f), Color.gray, Vector3.zero);
                Part(PrimitiveType.Cube, new Vector3(x + 0.035f, 0.18f, 0f), new Vector3(0.02f, 0.1f, 0.03f), Color.gray, new Vector3(0f, 0f, -15f));
            }
            glowColor = new Color(0.55f, 1f, 0.45f);
        }
        GameObject light = new GameObject("Glow");
        light.transform.SetParent(transform, false);
        light.transform.localPosition = Vector3.up * 0.6f;
        _glow = light.AddComponent<Light>();
        _glow.type = LightType.Point;
        _glow.color = glowColor;
        _glow.range = _type == PickupType.Ammo ? 3.5f : 2.5f;
        _glowBase = _type == PickupType.Ammo ? 2.5f : 1.8f;
        _glow.intensity = _glowBase;
        _glow.shadows = LightShadows.None;
    }

    private Renderer Part(PrimitiveType shape, Vector3 position, Vector3 scale, Color color, Vector3 euler, float emission = 0f)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.GetComponent<Collider>().enabled = false;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(_visual, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(euler);
        part.transform.localScale = scale;
        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = GetMaterial(color, emission);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return renderer;
    }

    private static Material GetMaterial(Color color, float emission)
    {
        Color key = new Color(color.r, color.g, color.b, emission);
        if (Materials.TryGetValue(key, out Material cached) && cached != null) return cached;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
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
