using UnityEngine;

// 조명을 깜빡이게 한다.
//  - Broken : 고장 난 가로등/형광등. 잘 켜져 있다가 가끔 지직거리며 꺼졌다 켜진다.
//  - Fire   : 불타는 차/드럼통. 계속 일렁인다.
//  - Subtle : 아주 약하게 흔들린다.
[RequireComponent(typeof(Light))]
[DisallowMultipleComponent]
public class FlickerLight : MonoBehaviour
{
    public enum FlickerMode
    {
        Broken,
        Fire,
        Subtle,
    }

    [SerializeField] private FlickerMode _mode = FlickerMode.Broken;
    [Tooltip("같이 깜빡일 발광 오브젝트 (선택)")]
    [SerializeField] private Renderer _emissiveRenderer;

    private Light _light;
    private float _baseIntensity;
    private float _seed;
    private float _stateTimer;
    private int _blinksLeft;
    private bool _on = true;
    private MaterialPropertyBlock _block;
    private Color _baseEmission;
    private bool _hasEmission;

    public void Setup(FlickerMode mode, Renderer emissiveRenderer = null)
    {
        _mode = mode;
        _emissiveRenderer = emissiveRenderer;
    }

    private void Awake()
    {
        _light = GetComponent<Light>();
        _baseIntensity = _light.intensity;
        _seed = Random.Range(0f, 100f);
        _stateTimer = Random.Range(0.5f, 4f);

        if (_emissiveRenderer != null && _emissiveRenderer.sharedMaterial != null
            && _emissiveRenderer.sharedMaterial.HasProperty("_EmissionColor"))
        {
            _block = new MaterialPropertyBlock();
            _baseEmission = _emissiveRenderer.sharedMaterial.GetColor("_EmissionColor");
            _hasEmission = true;
        }
    }

    private void Update()
    {
        float factor;
        switch (_mode)
        {
            case FlickerMode.Fire:
                factor = 0.65f + 0.45f * Mathf.PerlinNoise(_seed, Time.time * 7f)
                               + 0.15f * Mathf.PerlinNoise(Time.time * 19f, _seed);
                break;

            case FlickerMode.Subtle:
                factor = 0.9f + 0.1f * Mathf.PerlinNoise(_seed, Time.time * 3f);
                break;

            default:
                factor = UpdateBroken();
                break;
        }

        _light.intensity = _baseIntensity * factor;

        if (_hasEmission)
        {
            _emissiveRenderer.GetPropertyBlock(_block);
            _block.SetColor("_EmissionColor", _baseEmission * factor);
            _emissiveRenderer.SetPropertyBlock(_block);
        }
    }

    private float UpdateBroken()
    {
        _stateTimer -= Time.deltaTime;
        if (_stateTimer <= 0f)
        {
            if (_blinksLeft > 0)
            {
                // 지직거리는 중: 짧게 꺼졌다 켜졌다를 반복한다.
                _on = !_on;
                _blinksLeft--;
                _stateTimer = Random.Range(0.03f, 0.12f);
            }
            else if (_on && Random.value < 0.12f)
            {
                // 가끔 한동안 꺼져 있는다.
                _on = false;
                _stateTimer = Random.Range(0.3f, 1.2f);
            }
            else
            {
                _on = true;
                _blinksLeft = Random.Range(3, 9) * 2;
                _stateTimer = Random.Range(1f, 5f);
            }
        }

        return _on ? Random.Range(0.85f, 1f) : Random.Range(0f, 0.08f);
    }
}
