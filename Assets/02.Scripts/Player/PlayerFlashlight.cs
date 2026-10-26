using UnityEngine;

// 카메라에 붙이는 손전등. F키로 켜고 끈다.
// 진짜 손전등처럼 가운데 밝은 빛(메인)과 주변으로 넓게 퍼지는 약한 빛(스필) 두 개로 비춘다.
// 빛이 시선을 살짝 늦게 따라와서 손에 든 것처럼 보인다.
// Play 중에 Inspector에서 값을 바꾸면 바로 반영된다.
[DisallowMultipleComponent]
public class PlayerFlashlight : MonoBehaviour
{
    [SerializeField] private KeyCode _toggleKey = KeyCode.F;
    [SerializeField] private bool _startOn = true;
    [SerializeField] private Color _color = new Color(1f, 0.95f, 0.85f);

    [Header("가운데 밝은 빛")]
    [SerializeField, Min(0f)] private float _brightness = 9f;
    [Tooltip("빛이 닿는 거리(m)")]
    [SerializeField, Min(1f)] private float _reach = 45f;
    [Tooltip("빛이 퍼지는 각도. 클수록 넓게 비춘다.")]
    [SerializeField, Range(10f, 150f)] private float _beamAngle = 75f;
    [Tooltip("가장자리가 부드럽게 어두워지는 정도 (0 = 또렷한 원, 1 = 아주 부드럽게)")]
    [SerializeField, Range(0f, 1f)] private float _beamSoftness = 0.4f;
    [SerializeField] private bool _castShadows = true;

    [Header("주변으로 퍼지는 빛")]
    [SerializeField] private bool _useSpill = true;
    [SerializeField, Min(0f)] private float _spillBrightness = 2.5f;
    [SerializeField, Min(1f)] private float _spillReach = 22f;
    [SerializeField, Range(30f, 170f)] private float _spillAngle = 125f;

    [Header("움직임")]
    [Tooltip("시선을 따라오는 속도. 작을수록 더 늦게 따라온다.")]
    [SerializeField, Min(1f)] private float _followSpeed = 14f;

    private Light _main;
    private Light _spill;
    private Transform _pivot;
    private bool _on;

    public bool IsOn => _on;

    private void Awake()
    {
        // 두 빛을 한 기준점 아래에 두고, 그 기준점이 시선을 늦게 따라온다.
        Transform old = transform.Find("Flashlight");
        if (old != null)
        {
            Destroy(old.gameObject);
        }

        GameObject pivot = new GameObject("Flashlight");
        pivot.transform.SetParent(transform, false);
        pivot.transform.localPosition = new Vector3(0.15f, -0.12f, 0.1f);
        _pivot = pivot.transform;
        _pivot.rotation = transform.rotation;

        _main = CreateSpot("Main");
        _spill = CreateSpot("Spill");
        _on = _startOn;
        ApplySettings();
    }

    private Light CreateSpot(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(_pivot, false);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Spot;
        return light;
    }

    // Inspector 값을 빛에 반영한다.
    private void ApplySettings()
    {
        if (_main == null || _spill == null)
        {
            return;
        }

        _main.color = _color;
        _main.intensity = _brightness;
        _main.range = _reach;
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
    }
}
