using UnityEngine;

// 맵 분위기용 빛. 고장 난 가로등(가끔 꺼짐), 불길(일렁임), 경광등(번갈아 깜빡임)
[RequireComponent(typeof(Light))]
public class MapFlickerLight : MonoBehaviour
{
    public enum FlickerMode
    {
        Broken,  // 켜져 있다가 가끔 툭툭 꺼진다.
        Fire,    // 불처럼 계속 일렁인다.
        Siren,   // 일정하게 깜빡인다.
    }

    [SerializeField] private FlickerMode _mode = FlickerMode.Broken;
    [SerializeField] private float _speed = 1f;

    private Light _light;
    private float _baseIntensity;
    private float _seed;
    private float _offTimer;
    private float _nextBreak;

    public void Setup(FlickerMode mode, float speed)
    {
        _mode = mode;
        _speed = speed;
    }

    private void Awake()
    {
        _light = GetComponent<Light>();
        _baseIntensity = _light.intensity;
        _seed = Random.Range(0f, 100f);
        _nextBreak = Random.Range(1f, 6f);
    }

    private void Update()
    {
        float time = Time.time * _speed + _seed;
        switch (_mode)
        {
            case FlickerMode.Fire:
                _light.intensity = _baseIntensity * (0.75f + 0.5f * Mathf.PerlinNoise(time * 6f, _seed));
                break;

            case FlickerMode.Siren:
                _light.intensity = Mathf.Sin(time * 8f) > 0f ? _baseIntensity : 0f;
                break;

            default:
                _nextBreak -= Time.deltaTime;
                if (_nextBreak <= 0f)
                {
                    _offTimer = Random.Range(0.05f, 0.4f);
                    _nextBreak = Random.Range(1.5f, 7f);
                }

                _offTimer -= Time.deltaTime;
                _light.intensity = _offTimer > 0f ? _baseIntensity * Random.Range(0f, 0.2f) : _baseIntensity;
                break;
        }
    }
}
