using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 전투에 반응하는 후처리 (L4D식)
//  - 체력이 낮으면 색이 빠지고 붉은 비네트가 심장박동처럼 뛴다. (거의 죽으면 흑백)
//  - 맞으면 화면 가장자리가 붉게 번쩍이고 색수차·렌즈 왜곡이 잠깐 생긴다.
//  - 호드/피날레로 긴장도가 오르면 대비가 세지고 채도가 낮아진다.
//  - 폭발이 가까우면 화면이 잠깐 하얗게 번쩍인다.
// 효과마다 전역 Volume을 하나씩 만들고, 그 Volume의 가중치(weight)만 바꿔서 기존 색보정 위에 부드럽게 섞는다.
public class CombatPostFX : MonoBehaviour
{
    private const float LowHealthStart = 0.45f;   // 체력 비율이 이보다 낮으면 효과 시작
    private const float LowHealthFull = 0.12f;    // 이보다 낮으면 최대 (흑백)

    private static CombatPostFX _instance;

    [SerializeField] private float _hitDecay = 3.2f;
    [SerializeField] private float _blastDecay = 2.2f;
    [SerializeField] private float _tensionSmooth = 1.5f;

    private Volume _lowHealth;
    private Volume _hit;
    private Volume _tension;
    private Volume _blast;
    private Player _player;
    private float _hitWeight;
    private float _blastWeight;
    private float _tensionWeight;
    private float _lowWeight;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    public static void Explosion(Vector3 position, float strength)
    {
        if (_instance == null) return;
        Camera camera = Camera.main;
        float distance = camera != null ? Vector3.Distance(camera.transform.position, position) : 0f;
        float weight = strength * Mathf.Clamp01(1f - distance / 35f);
        _instance._blastWeight = Mathf.Max(_instance._blastWeight, weight);
    }

    private void Awake()
    {
        _instance = this;
        _lowHealth = CreateVolume("LowHealth", 100, profile =>
        {
            ColorAdjustments color = profile.Add<ColorAdjustments>(false);
            color.saturation.Override(-100f);
            color.contrast.Override(25f);
            color.postExposure.Override(-0.25f);
            Vignette vignette = profile.Add<Vignette>(false);
            vignette.color.Override(new Color(0.35f, 0f, 0f));
            vignette.intensity.Override(0.55f);
            vignette.smoothness.Override(0.6f);
        });
        _tension = CreateVolume("Tension", 101, profile =>
        {
            ColorAdjustments color = profile.Add<ColorAdjustments>(false);
            color.contrast.Override(30f);
            color.saturation.Override(-30f);
            Vignette vignette = profile.Add<Vignette>(false);
            vignette.intensity.Override(0.42f);
            vignette.color.Override(new Color(0.05f, 0f, 0f));
        });
        _hit = CreateVolume("Hit", 102, profile =>
        {
            Vignette vignette = profile.Add<Vignette>(false);
            vignette.color.Override(new Color(0.55f, 0f, 0f));
            vignette.intensity.Override(0.6f);
            vignette.smoothness.Override(0.5f);
            ChromaticAberration aberration = profile.Add<ChromaticAberration>(false);
            aberration.intensity.Override(0.8f);
            LensDistortion lens = profile.Add<LensDistortion>(false);
            lens.intensity.Override(-0.18f);
        });
        _blast = CreateVolume("Blast", 103, profile =>
        {
            ColorAdjustments color = profile.Add<ColorAdjustments>(false);
            color.postExposure.Override(1.6f);
            color.colorFilter.Override(new Color(1f, 0.85f, 0.7f));
            ChromaticAberration aberration = profile.Add<ChromaticAberration>(false);
            aberration.intensity.Override(0.6f);
            LensDistortion lens = profile.Add<LensDistortion>(false);
            lens.intensity.Override(0.15f);
        });
    }

    private void Start()
    {
        _player = FindAnyObjectByType<Player>();
        if (_player != null) _player.OnDamaged += OnDamaged;
    }

    private void OnDestroy()
    {
        if (_player != null) _player.OnDamaged -= OnDamaged;
        if (_instance == this) _instance = null;
        foreach (Volume volume in new[] { _lowHealth, _tension, _hit, _blast })
        {
            if (volume == null || volume.sharedProfile == null) continue;
            foreach (VolumeComponent component in volume.sharedProfile.components) Destroy(component);
            Destroy(volume.sharedProfile);
        }
    }

    private void OnDamaged()
    {
        _hitWeight = 1f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // 체력 (심장박동처럼 비네트가 뛴다)
        float low = 0f;
        if (_player != null && _player.MaxHealth > 0f)
        {
            float ratio = _player.Health / _player.MaxHealth;
            low = Mathf.Clamp01((LowHealthStart - ratio) / (LowHealthStart - LowHealthFull));
            if (_player.Health <= 0f) low = 1f;
        }

        _lowWeight = Mathf.MoveTowards(_lowWeight, low, dt * 0.8f);
        float beat = _lowWeight > 0f ? 0.88f + 0.12f * Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * 4.2f)), 6f) : 1f;
        _lowHealth.weight = _lowWeight * beat;

        // 긴장도 (AI 디렉터: 호드, 피날레)
        float tension = 0f;
        if (AIDirector.Instance != null)
        {
            tension = Mathf.Max(AIDirector.Instance.Tension * 0.6f, AIDirector.Instance.HordeActive ? 0.85f : 0f);
        }

        _tensionWeight = Mathf.MoveTowards(_tensionWeight, tension, dt / _tensionSmooth);
        _tension.weight = _tensionWeight;

        // 피격 / 폭발: 번쩍였다가 빠르게 사라진다.
        _hitWeight = Mathf.MoveTowards(_hitWeight, 0f, dt * _hitDecay);
        _hit.weight = _hitWeight * _hitWeight;
        _blastWeight = Mathf.MoveTowards(_blastWeight, 0f, dt * _blastDecay);
        _blast.weight = _blastWeight * _blastWeight;
    }

    private Volume CreateVolume(string name, int priority, System.Action<VolumeProfile> build)
    {
        GameObject go = new GameObject("PostFX_" + name);
        go.transform.SetParent(transform, false);
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = priority;
        volume.weight = 0f;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = name;
        build(profile);
        volume.sharedProfile = profile;
        return volume;
    }
}
