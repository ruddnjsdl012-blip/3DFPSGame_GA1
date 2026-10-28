using UnityEngine;

// 손전등. Main Camera에 붙이고 F 키로 켜고 끈다.
//  - 빛이 시선을 살짝 늦게 따라와서 손에 든 것처럼 보인다.
//  - 켜고 끌 때 '딸깍' 소리가 난다. (소리 파일 없이 코드로 만든다)
[RequireComponent(typeof(Camera))]
public class PlayerFlashlight : MonoBehaviour
{
    [SerializeField] private KeyCode _toggleKey = KeyCode.F;
    [SerializeField] private bool _startOn = false;

    [Header("빛")]
    [SerializeField] private Color _color = new Color(1f, 0.95f, 0.85f);
    [SerializeField] private float _intensity = 9f;
    [SerializeField] private float _range = 40f;
    [SerializeField] private float _spotAngle = 60f;
    [SerializeField] private float _innerSpotAngle = 30f;
    [SerializeField] private Vector3 _offset = new Vector3(0.18f, -0.15f, 0.25f);  // 카메라 기준 오른쪽 아래 앞

    [Header("흔들림")]
    [SerializeField] private float _followSpeed = 14f;  // 작을수록 시선을 더 늦게 따라온다.

    private Light _light;
    private Transform _beam;
    private AudioSource _audio;
    private AudioClip _click;
    private Quaternion _rotation;

    public bool IsOn => _light != null && _light.enabled;

    private void Awake()
    {
        _beam = new GameObject("Flashlight").transform;
        _beam.SetParent(transform, false);
        _beam.localPosition = _offset;

        _light = _beam.gameObject.AddComponent<Light>();
        _light.type = LightType.Spot;
        _light.color = _color;
        _light.intensity = _intensity;
        _light.range = _range;
        _light.spotAngle = _spotAngle;
        _light.innerSpotAngle = _innerSpotAngle;
        _light.shadows = LightShadows.Soft;
        _light.enabled = _startOn;

        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
        _click = CreateClick();

        _rotation = transform.rotation;
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing && Input.GetKeyDown(_toggleKey))
        {
            _light.enabled = !_light.enabled;
            _audio.PlayOneShot(_click, 0.6f);
        }
    }

    private void LateUpdate()
    {
        // 카메라가 돌아간 뒤에, 빛은 조금 늦게 따라간다.
        _rotation = Quaternion.Slerp(_rotation, transform.rotation, 1f - Mathf.Exp(-_followSpeed * Time.deltaTime));
        _beam.rotation = _rotation;
    }

    private static AudioClip CreateClick()
    {
        const int rate = 22050;
        int samples = rate / 25;
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            data[i] = (Random.value * 2f - 1f) * Mathf.Exp(-t * 18f) * 0.6f;
        }

        AudioClip clip = AudioClip.Create("FlashlightClick", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
