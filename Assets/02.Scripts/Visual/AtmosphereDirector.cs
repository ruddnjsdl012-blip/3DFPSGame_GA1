using System;
using UnityEngine;
using UnityEngine.Rendering;

// 진행할수록 시간이 흐르는 분위기 (늦은 오후 → 해질녘 → 노을 끝 → 블루 아워 → 밤)
// 플레이어가 경로를 얼마나 진행했는지(CityMapInfo의 경로 지점)로 시간대를 정하고,
// 해/달, 안개, 하늘, 환경광, 시간대별 Volume(색보정)을 부드럽게 바꾼다.
// 뒤로 돌아가도 시간은 되돌아가지 않는다. (가장 멀리 간 지점 기준)
//
// 시간대 값은 Inspector의 _keys 에서 바로 고칠 수 있다. (Tools > L4D 비주얼 > 시간대 미리보기로 확인)
public class AtmosphereDirector : MonoBehaviour
{
    [Serializable]
    public class Key
    {
        public string name = "Key";
        [Range(0f, 1f)] public float progress;
        [Header("해 / 달")]
        public Vector2 sunAngles = new Vector2(20f, 235f);   // x: 높이(도), y: 방위(도)
        public Color sunColor = Color.white;
        public float sunIntensity = 1f;
        [Range(0f, 1f)] public float shadowStrength = 0.85f;
        [Header("안개")]
        public Color fogColor = Color.gray;
        public float fogStart = 15f;
        public float fogEnd = 250f;
        [Header("하늘 / 환경광 (라이트맵이 없는 물체용)")]
        public float skyExposure = 1f;
        public Color skyTint = new Color(0.5f, 0.5f, 0.5f);
        public Color ambientSky = new Color(0.45f, 0.47f, 0.5f);
        public Color ambientEquator = new Color(0.3f, 0.32f, 0.33f);
        public Color ambientGround = new Color(0.12f, 0.11f, 0.1f);
        [Header("색보정 (이 시간대의 Volume)")]
        public Volume volume;
    }

    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    private static readonly int TintId = Shader.PropertyToID("_Tint");

    [SerializeField] private Light _sun;
    [SerializeField] private Key[] _keys = new Key[0];
    [SerializeField] private float _smoothTime = 6f;            // 시간대가 바뀌는 데 걸리는 시간 (초)
    [SerializeField] private float _updateInterval = 0.25f;     // 진행도 계산 간격 (초)

    private CityMapInfo _map;
    private Transform _player;
    private Material _sky;
    private float _target;
    private float _progress;
    private float _velocity;
    private float _timer;

    public float Progress => _progress;

    // 에디터 도구(L4D 비주얼)가 해와 시간대 값을 넣을 때 쓴다.
    public void Configure(Light sun, Key[] keys)
    {
        _sun = sun;
        _keys = keys;
    }

    private void Start()
    {
        _map = GetComponentInParent<CityMapInfo>();
        if (_map == null) _map = FindAnyObjectByType<CityMapInfo>();
        PlayerMove player = FindAnyObjectByType<PlayerMove>();
        _player = player != null ? player.transform : null;

        // 하늘 머티리얼은 복사본을 바꾼다. (원본 에셋을 건드리지 않게)
        if (RenderSettings.skybox != null)
        {
            _sky = new Material(RenderSettings.skybox);
            RenderSettings.skybox = _sky;
        }

        _target = CurrentRouteProgress();
        _progress = _target;
        Apply(_progress);
    }

    private void OnDestroy()
    {
        if (_sky != null) Destroy(_sky);
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _timer = _updateInterval;
            _target = Mathf.Max(_target, CurrentRouteProgress());
        }

        if (Mathf.Abs(_progress - _target) < 0.0001f) return;
        _progress = Mathf.SmoothDamp(_progress, _target, ref _velocity, _smoothTime);
        Apply(_progress);
    }

    private float CurrentRouteProgress()
    {
        if (_map == null || _player == null || _map.RoutePoints == null || _map.RoutePoints.Length < 2) return 0f;
        return (float)_map.GetRouteIndex(_player.position) / (_map.RoutePoints.Length - 1);
    }

    // 0 = 출발, 1 = 피날레. 에디터 미리보기에서도 부른다.
    public void Apply(float progress)
    {
        if (_keys == null || _keys.Length == 0) return;
        int b = 0;
        while (b < _keys.Length - 1 && _keys[b].progress < progress) b++;
        int a = Mathf.Max(0, b - 1);
        Key from = _keys[a];
        Key to = _keys[b];
        float u = a == b || to.progress <= from.progress ? 1f : Mathf.Clamp01((progress - from.progress) / (to.progress - from.progress));
        u = u * u * (3f - 2f * u);

        // 해/달: 방향이 크게 바뀌면 (해가 지고 달이 뜨면) 중간에 빛을 0으로 줄였다가 바꾼다.
        if (_sun != null)
        {
            Quaternion qa = Quaternion.Euler(from.sunAngles.x, from.sunAngles.y, 0f);
            Quaternion qb = Quaternion.Euler(to.sunAngles.x, to.sunAngles.y, 0f);
            float intensity;
            if (Quaternion.Angle(qa, qb) > 60f)
            {
                _sun.transform.rotation = u < 0.5f ? qa : qb;
                intensity = u < 0.5f ? Mathf.Lerp(from.sunIntensity, 0f, u * 2f) : Mathf.Lerp(0f, to.sunIntensity, (u - 0.5f) * 2f);
            }
            else
            {
                _sun.transform.rotation = Quaternion.Slerp(qa, qb, u);
                intensity = Mathf.Lerp(from.sunIntensity, to.sunIntensity, u);
            }

            _sun.intensity = intensity;
            _sun.color = Color.Lerp(from.sunColor, to.sunColor, u);
            _sun.shadowStrength = Mathf.Lerp(from.shadowStrength, to.shadowStrength, u);
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = Color.Lerp(from.fogColor, to.fogColor, u);
        RenderSettings.fogStartDistance = Mathf.Lerp(from.fogStart, to.fogStart, u);
        RenderSettings.fogEndDistance = Mathf.Lerp(from.fogEnd, to.fogEnd, u);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Color.Lerp(from.ambientSky, to.ambientSky, u);
        RenderSettings.ambientEquatorColor = Color.Lerp(from.ambientEquator, to.ambientEquator, u);
        RenderSettings.ambientGroundColor = Color.Lerp(from.ambientGround, to.ambientGround, u);

        Material sky = _sky != null ? _sky : null;
        if (sky != null)
        {
            if (sky.HasProperty(ExposureId)) sky.SetFloat(ExposureId, Mathf.Lerp(from.skyExposure, to.skyExposure, u));
            if (sky.HasProperty(TintId)) sky.SetColor(TintId, Color.Lerp(from.skyTint, to.skyTint, u));
        }

        // 시간대 Volume: 앞 시간대는 가중치 1, 다음 시간대는 u (우선순위가 더 높아서 그 위에 섞인다)
        for (int i = 0; i < _keys.Length; i++)
        {
            Volume volume = _keys[i].volume;
            if (volume == null) continue;
            volume.priority = 10 + i;
            volume.weight = i == a ? 1f : i == b ? u : 0f;
        }
    }
}
