using UnityEngine;

// 카메라에 붙이는 손전등. F키로 켜고 끈다.
//  - 빛이 나오는 곳을 총 앞쪽에 두어서, 손과 총은 거의 밝아지지 않고 정면만 밝게 비춘다.
//    (벽에 바짝 붙으면 벽을 뚫지 않도록 자동으로 뒤로 당긴다)
//  - 가운데 밝은 빛(메인) + 주변으로 넓게 퍼지는 약한 빛(스필) 두 겹으로 비춘다.
//  - 손과 총은 아주 약한 보조광으로만 살짝 보이게 한다.
//  - 빛이 시선을 살짝 늦게 따라와서 손에 든 것처럼 보인다.
// Play 중에 Inspector에서 값을 바꾸면 바로 반영된다.
[DisallowMultipleComponent]
public class PlayerFlashlight : MonoBehaviour
{
    private const string FirstPersonName = "__FreeModels_FirstPerson";

    [SerializeField] private KeyCode _toggleKey = KeyCode.F;
    [SerializeField] private bool _startOn = true;
    [SerializeField] private Color _color = new Color(1f, 0.95f, 0.85f);

    [Header("가운데 밝은 빛")]
    [SerializeField, Min(0f)] private float _beamBrightness = 10f;
    [Tooltip("빛이 닿는 거리(m)")]
    [SerializeField, Min(1f)] private float _beamReach = 50f;
    [Tooltip("빛이 퍼지는 각도. 클수록 넓게 비춘다.")]
    [SerializeField, Range(10f, 150f)] private float _beamAngle = 72f;
    [Tooltip("가장자리가 부드럽게 어두워지는 정도 (0 = 또렷한 원, 1 = 아주 부드럽게)")]
    [SerializeField, Range(0f, 1f)] private float _beamSoftness = 0.4f;
    [SerializeField] private bool _castShadows = true;

    [Header("주변으로 퍼지는 빛")]
    [SerializeField] private bool _useSpill = true;
    [SerializeField, Min(0f)] private float _spillBrightness = 2.5f;
    [SerializeField, Min(1f)] private float _spillReach = 24f;
    [SerializeField, Range(30f, 170f)] private float _spillAngle = 125f;

    [Header("손과 총")]
    [Tooltip("손전등을 켰을 때 손과 총이 밝아지는 정도 (0이면 전혀 안 밝아짐)")]
    [SerializeField, Min(0f)] private float _handBrightness = 0.5f;
    [Tooltip("빛이 나오는 곳. 0이면 총 끝 바로 앞을 자동으로 찾는다.")]
    [SerializeField, Min(0f)] private float _beamStartDistance = 0f;

    [Header("움직임")]
    [Tooltip("시선을 따라오는 속도. 작을수록 더 늦게 따라온다.")]
    [SerializeField, Min(1f)] private float _followSpeed = 14f;

    private Light _main;
    private Light _spill;
    private Light _handFill;
    private Transform _pivot;
    private Transform _beamOrigin;
    private float _autoStartDistance = 0.9f;
    private bool _on;

    public bool IsOn => _on;

    private void Awake()
    {
        Transform old = transform.Find("Flashlight");
        if (old != null)
        {
            Destroy(old.gameObject);
        }

        // 기준점(카메라 위치)이 시선을 늦게 따라오고, 빛은 그 앞쪽에서 나온다.
        GameObject pivot = new GameObject("Flashlight");
        pivot.transform.SetParent(transform, false);
        _pivot = pivot.transform;
        _pivot.rotation = transform.rotation;

        GameObject origin = new GameObject("BeamOrigin");
        origin.transform.SetParent(_pivot, false);
        _beamOrigin = origin.transform;

        _main = CreateLight("Main", _beamOrigin, LightType.Spot);
        _spill = CreateLight("Spill", _beamOrigin, LightType.Spot);
        _handFill = CreateLight("HandFill", transform, LightType.Point);
        _handFill.transform.localPosition = new Vector3(0.05f, 0.18f, 0.2f);

        _autoStartDistance = FindGunFront() + 0.08f;
        SetupLayers();

        _on = _startOn;
        ApplySettings();
    }

    private static Light CreateLight(string name, Transform parent, LightType type)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Light light = go.AddComponent<Light>();
        light.type = type;
        return light;
    }

    // 1인칭 총/팔 모델의 가장 앞쪽 끝이 카메라에서 얼마나 떨어져 있는지
    private float FindGunFront()
    {
        Transform view = transform.Find(FirstPersonName);
        if (view == null)
        {
            return 0.9f;
        }

        float front = 0f;
        foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>(true))
        {
            Bounds b = renderer.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = b.center + Vector3.Scale(b.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                front = Mathf.Max(front, transform.InverseTransformPoint(point).z);
            }
        }

        return Mathf.Clamp(front, 0.3f, 1.6f);
    }

    // 1인칭 모델이 따로 레이어에 있으면, 메인/스필 빛은 그 레이어를 비추지 않고 보조광만 비추게 한다.
    // (렌더 파이프라인에 따라 레이어 구분이 무시될 수 있지만, 빛이 총 앞에서 나오므로 어느 쪽이든 손은 거의 안 밝아진다)
    private void SetupLayers()
    {
        Transform view = transform.Find(FirstPersonName);
        int layer = view != null ? view.gameObject.layer : 0;
        if (layer == 0)
        {
            return; // 세상과 같은 Default 레이어면 나누지 않는다.
        }

        int viewMask = 1 << layer;
        _main.cullingMask = ~viewMask;
        _spill.cullingMask = ~viewMask;
        _handFill.cullingMask = viewMask;
    }

    // Inspector 값을 빛에 반영한다.
    private void ApplySettings()
    {
        if (_main == null || _spill == null || _handFill == null)
        {
            return;
        }

        _main.color = _color;
        _main.intensity = _beamBrightness;
        _main.range = _beamReach;
        _main.spotAngle = _beamAngle;
        _main.innerSpotAngle = _beamAngle * (1f - _beamSoftness);
        _main.shadows = _castShadows ? LightShadows.Soft : LightShadows.None;
        _main.enabled = _on;

        _spill.color = _color;
        _spill.intensity = _spillBrightness;
        _spill.range = _spillReach;
        _spill.spotAngle = _spillAngle;
        _spill.innerSpotAngle = _spillAngle * 0.2f;
        _spill.shadows = LightShadows.None;
        _spill.enabled = _on && _useSpill;

        _handFill.color = _color;
        _handFill.intensity = _handBrightness;
        _handFill.range = 1.4f;
        _handFill.shadows = LightShadows.None;
        _handFill.enabled = _on && _handBrightness > 0f;
    }

    private void OnValidate()
    {
        ApplySettings();
    }

    private void Update()
    {
        if (Input.GetKeyDown(_toggleKey) && GameManager.IsPlaying)
        {
            _on = !_on;
            ApplySettings();
        }
    }

    private void LateUpdate()
    {
        // 카메라 회전을 부드럽게 따라간다.
        float t = 1f - Mathf.Exp(-_followSpeed * Time.deltaTime);
        _pivot.rotation = Quaternion.Slerp(_pivot.rotation, transform.rotation, t);

        // 빛은 총 앞에서 나오되, 앞에 벽이 가까우면 벽 안으로 들어가지 않게 당긴다.
        float start = _beamStartDistance > 0f ? _beamStartDistance : _autoStartDistance;
        if (Physics.Raycast(_pivot.position, _pivot.forward, out RaycastHit hit, start + 0.3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            start = Mathf.Max(0.05f, hit.distance - 0.3f);
        }

        _beamOrigin.localPosition = new Vector3(0f, 0f, start);
    }
}
