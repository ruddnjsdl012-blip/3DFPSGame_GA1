using System.Collections.Generic;
using UnityEngine;

public enum PickupType
{
    Ammo,
    Medkit,
    Bombs
}

// 땅에 놓인 보급품 (탄약 상자, 구급상자, 수류탄 상자).
//  - 플레이어가 가까이 가면 자동으로 줍는다. 단, 필요할 때만 줍는다. (탄약이 가득하면 탄약 상자는 그대로 남음)
//  - 모양은 코드로 만든다. (외부 모델 없이 동작) 어두운 맵에서도 보이도록 은은한 빛을 낸다.
//  - 맵에 놓인 것은 주우면 일정 시간 뒤 같은 자리에 다시 생기고,
//    좀비가 떨어뜨린 것은 일정 시간이 지나면 깜빡이다가 사라진다.
// PickupManager가 만든다. Create()로 직접 만들 수도 있다.
[DisallowMultipleComponent]
public class Pickup : MonoBehaviour
{
    [SerializeField] private PickupType _type = PickupType.Ammo;
    [SerializeField, Min(1)] private int _amount = 60;
    [Tooltip("주운 뒤 다시 생기는 시간 (0이면 다시 생기지 않음)")]
    [SerializeField, Min(0f)] private float _respawnTime;
    [Tooltip("이 시간이 지나면 사라진다 (0이면 계속 있음)")]
    [SerializeField, Min(0f)] private float _lifetime;
    [SerializeField, Min(0.3f)] private float _pickupRadius = 1.4f;
    [SerializeField] private bool _small;

    private static Player _player;
    private static PlayerGunFire _gun;
    private static PlayerBombFire _bombs;
    private static float _nextSearchTime;
    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

    private Transform _visual;
    private Light _light;
    private bool _available = true;
    private float _respawnTimer;
    private float _age;
    private float _appear;
    private float _seed;
    private bool _warnedFull;

    public PickupType Type => _type;
    public bool IsAvailable => _available;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _player = null;
        _gun = null;
        _bombs = null;
        _nextSearchTime = 0f;
        Materials.Clear();
    }

    public static Pickup Create(PickupType type, int amount, Vector3 groundPosition,
        float respawnTime = 0f, float lifetime = 0f, bool small = false, Transform parent = null)
    {
        GameObject go = new GameObject($"Pickup_{type}");
        if (parent != null) go.transform.SetParent(parent, true);
        go.transform.position = groundPosition;
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        Pickup pickup = go.AddComponent<Pickup>();
        pickup._type = type;
        pickup._amount = Mathf.Max(1, amount);
        pickup._respawnTime = respawnTime;
        pickup._lifetime = lifetime;
        pickup._small = small;
        pickup.Build();
        return pickup;
    }

    private void Start()
    {
        if (_visual == null)
        {
            Build(); // 씬에 직접 붙였을 때
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (_available == false)
        {
            _respawnTimer -= dt;
            if (_respawnTimer <= 0f)
            {
                SetAvailable(true);
            }

            return;
        }

        if (_lifetime > 0f)
        {
            _age += dt;
            if (_age >= _lifetime)
            {
                Destroy(gameObject);
                return;
            }
        }

        Animate(dt);
        FindPlayer();
        if (_player == null)
        {
            return;
        }

        Vector3 offset = _player.transform.position - transform.position;
        float distance = new Vector2(offset.x, offset.z).magnitude;
        if (_light != null)
        {
            bool near = distance < 35f;
            if (_light.enabled != near) _light.enabled = near;
        }

        bool inRange = distance <= _pickupRadius && offset.y > -0.8f && offset.y < 2.6f;
        if (inRange == false)
        {
            _warnedFull = false;
            return;
        }

        if (GameManager.IsPlaying && TryCollect())
        {
            if (_respawnTime > 0f)
            {
                SetAvailable(false);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    private bool TryCollect()
    {
        switch (_type)
        {
            case PickupType.Ammo:
                {
                    if (_gun == null) return false;
                    int added = _gun.AddReserveAmmo(_amount);
                    if (added > 0)
                    {
                        GameAudio.Play2D("pickup_ammo", 0.8f, 0.04f);
                        UI_PickupFeed.Show($"+{added} AMMO", UI_PickupFeed.AmmoColor);
                        return true;
                    }

                    WarnFull("AMMO FULL");
                    return false;
                }

            case PickupType.Medkit:
                {
                    float healed = _player.Heal(_amount);
                    if (healed > 0.5f)
                    {
                        GameAudio.Play2D("pickup_health", 0.8f, 0.03f);
                        UI_PickupFeed.Show($"+{Mathf.RoundToInt(healed)} HP", UI_PickupFeed.HealthColor, true);
                        return true;
                    }

                    WarnFull("HEALTH FULL");
                    return false;
                }

            case PickupType.Bombs:
                {
                    if (_bombs == null) return false;
                    int added = _bombs.AddBombs(_amount);
                    if (added > 0)
                    {
                        GameAudio.Play2D("pickup_bomb", 0.8f, 0.04f);
                        UI_PickupFeed.Show(added == 1 ? "+1 BOMB" : $"+{added} BOMBS", UI_PickupFeed.BombColor);
                        return true;
                    }

                    WarnFull("BOMBS FULL");
                    return false;
                }
        }

        return false;
    }

    // 가득 차서 못 주울 때 한 번만 알려 준다. (멀어졌다가 다시 오면 또 알려 줌)
    private void WarnFull(string text)
    {
        if (_warnedFull)
        {
            return;
        }

        _warnedFull = true;
        UI_PickupFeed.Show(text, UI_PickupFeed.MutedColor);
    }

    private static void FindPlayer()
    {
        if (_player != null && _gun != null && _bombs != null)
        {
            return;
        }

        // 못 찾았을 때 매 프레임 검색하지 않도록 간격을 둔다.
        if (Time.unscaledTime < _nextSearchTime)
        {
            return;
        }

        _nextSearchTime = Time.unscaledTime + 1f;
        if (_player == null) _player = FindAnyObjectByType<Player>();
        if (_gun == null) _gun = FindAnyObjectByType<PlayerGunFire>();
        if (_bombs == null) _bombs = FindAnyObjectByType<PlayerBombFire>();
    }

    private void SetAvailable(bool available)
    {
        _available = available;
        _warnedFull = false;
        if (_visual != null) _visual.gameObject.SetActive(available);
        if (_light != null) _light.enabled = available;
        if (available)
        {
            _appear = 0f;
        }
        else
        {
            _respawnTimer = _respawnTime;
        }
    }

    private void Animate(float dt)
    {
        if (_visual == null)
        {
            return;
        }

        _appear = Mathf.Min(1f, _appear + dt * 4f);
        float pop = 1f + Mathf.Sin(_appear * Mathf.PI) * 0.25f;
        float scale = (_small ? 0.7f : 1f) * Mathf.SmoothStep(0f, 1f, _appear) * pop;

        float t = Time.time + _seed;
        _visual.localPosition = new Vector3(0f, 0.12f + Mathf.Sin(t * 2.2f) * 0.04f, 0f);
        _visual.localRotation = Quaternion.Euler(0f, t * 45f, 0f);
        _visual.localScale = Vector3.one * scale;

        // 사라지기 5초 전부터 깜빡인다.
        if (_lifetime > 0f && _lifetime - _age < 5f)
        {
            bool on = Mathf.Repeat(_age * (_lifetime - _age < 2f ? 8f : 4f), 1f) < 0.6f;
            if (_visual.gameObject.activeSelf != on) _visual.gameObject.SetActive(on);
        }
    }

    // ----- 모양 만들기 -----

    private void Build()
    {
        _seed = Random.Range(0f, 10f);
        _visual = new GameObject("Visual").transform;
        _visual.SetParent(transform, false);

        Color glow;
        switch (_type)
        {
            case PickupType.Medkit:
                BuildMedkit(_visual);
                glow = new Color(0.45f, 1f, 0.5f);
                break;
            case PickupType.Bombs:
                BuildBombCrate(_visual);
                glow = new Color(1f, 0.55f, 0.2f);
                break;
            default:
                BuildAmmoBox(_visual);
                glow = new Color(1f, 0.82f, 0.35f);
                break;
        }

        GameObject lightObject = new GameObject("Glow");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        _light = lightObject.AddComponent<Light>();
        _light.type = LightType.Point;
        _light.color = glow;
        _light.range = _small ? 1.8f : 2.6f;
        _light.intensity = _small ? 1f : 1.5f;
        _light.shadows = LightShadows.None;
        _light.enabled = false;
    }

    private static void BuildAmmoBox(Transform root)
    {
        Material body = GetMaterial("ammo_body", new Color(0.27f, 0.32f, 0.17f), Color.black, 0.25f);
        Material dark = GetMaterial("ammo_dark", new Color(0.17f, 0.2f, 0.11f), Color.black, 0.2f);
        Material stripe = GetMaterial("ammo_stripe", new Color(1f, 0.78f, 0.15f), new Color(0.9f, 0.6f, 0.08f), 0.3f);
        Material brass = GetMaterial("brass", new Color(0.86f, 0.66f, 0.28f), new Color(0.12f, 0.08f, 0.02f), 0.75f);
        Material copper = GetMaterial("copper", new Color(0.72f, 0.38f, 0.2f), Color.black, 0.7f);

        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.14f, 0f), new Vector3(0.6f, 0.28f, 0.34f), body);
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), new Vector3(0.62f, 0.05f, 0.36f), dark);
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.17f, 0f), new Vector3(0.605f, 0.06f, 0.345f), stripe);
        Part(root, PrimitiveType.Cube, new Vector3(0.32f, 0.2f, 0f), new Vector3(0.04f, 0.04f, 0.14f), dark);
        Part(root, PrimitiveType.Cube, new Vector3(-0.32f, 0.2f, 0f), new Vector3(0.04f, 0.04f, 0.14f), dark);

        // 상자 위에 세워 둔 탄
        for (int i = 0; i < 5; i++)
        {
            float x = -0.16f + i * 0.08f;
            Part(root, PrimitiveType.Cylinder, new Vector3(x, 0.375f, 0.04f), new Vector3(0.035f, 0.05f, 0.035f), brass);
            Part(root, PrimitiveType.Sphere, new Vector3(x, 0.43f, 0.04f), new Vector3(0.034f, 0.045f, 0.034f), copper);
        }
    }

    private static void BuildMedkit(Transform root)
    {
        Material white = GetMaterial("med_body", new Color(0.92f, 0.92f, 0.9f), new Color(0.05f, 0.05f, 0.05f), 0.4f);
        Material trim = GetMaterial("med_trim", new Color(0.62f, 0.64f, 0.66f), Color.black, 0.35f);
        Material red = GetMaterial("med_cross", new Color(0.95f, 0.08f, 0.08f), new Color(0.9f, 0.05f, 0.05f), 0.4f);
        Material dark = GetMaterial("med_handle", new Color(0.12f, 0.12f, 0.13f), Color.black, 0.3f);

        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), new Vector3(0.46f, 0.2f, 0.32f), white);
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(0.47f, 0.035f, 0.33f), trim);
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.235f, -0.12f), new Vector3(0.16f, 0.03f, 0.03f), dark);

        // 윗면과 앞뒤의 빨간 십자
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.219f, 0.02f), new Vector3(0.2f, 0.006f, 0.06f), red);
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.219f, 0.02f), new Vector3(0.06f, 0.006f, 0.2f), red);
        for (int side = -1; side <= 1; side += 2)
        {
            float z = 0.161f * side;
            Part(root, PrimitiveType.Cube, new Vector3(0f, 0.095f, z), new Vector3(0.14f, 0.045f, 0.006f), red);
            Part(root, PrimitiveType.Cube, new Vector3(0f, 0.095f, z), new Vector3(0.045f, 0.14f, 0.006f), red);
        }
    }

    private static void BuildBombCrate(Transform root)
    {
        Material body = GetMaterial("bomb_body", new Color(0.2f, 0.24f, 0.2f), Color.black, 0.25f);
        Material stripe = GetMaterial("bomb_stripe", new Color(1f, 0.45f, 0.1f), new Color(0.9f, 0.35f, 0.05f), 0.3f);
        Material grenade = GetMaterial("grenade", new Color(0.24f, 0.31f, 0.19f), Color.black, 0.45f);
        Material metal = GetMaterial("grenade_top", new Color(0.55f, 0.56f, 0.58f), Color.black, 0.7f);

        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), new Vector3(0.44f, 0.16f, 0.3f), body);
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), new Vector3(0.445f, 0.03f, 0.305f), stripe);
        for (int side = -1; side <= 1; side += 2)
        {
            float x = 0.09f * side;
            Part(root, PrimitiveType.Sphere, new Vector3(x, 0.23f, 0f), new Vector3(0.12f, 0.14f, 0.12f), grenade);
            Part(root, PrimitiveType.Cylinder, new Vector3(x, 0.31f, 0f), new Vector3(0.04f, 0.02f, 0.04f), metal);
            Part(root, PrimitiveType.Cube, new Vector3(x + 0.03f, 0.27f, 0f), new Vector3(0.015f, 0.08f, 0.025f), metal);
        }
    }

    private static void Part(Transform parent, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false; // 총알과 좀비가 막히지 않도록
            Destroy(collider);
        }

        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        Renderer renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
    }

    private static Material GetMaterial(string key, Color color, Color emission, float smoothness)
    {
        if (Materials.TryGetValue(key, out Material cached) && cached != null)
        {
            return cached;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader);
        material.name = "Pickup_" + key;
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

        Materials[key] = material;
        return material;
    }
}
