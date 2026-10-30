using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class CityMapAtmosphere : MonoBehaviour
{
    private Material _sky;
    private Material _originalSky;

    private void Awake()
    {
        // 조금 더 어두운 해 질 녘. 가로등/안전가옥/손전등 밝기는 유지한다.
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.gameObject.scene == gameObject.scene && light.type == LightType.Directional)
            {
                light.intensity *= 0.7f;
            }
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.32f, 0.33f, 0.4f);
        RenderSettings.ambientEquatorColor = new Color(0.27f, 0.255f, 0.26f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.115f, 0.12f);
        RenderSettings.reflectionIntensity = 0.75f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.36f, 0.34f, 0.36f);
        RenderSettings.fogStartDistance = 25f;
        RenderSettings.fogEndDistance = 160f;

        _originalSky = RenderSettings.skybox;
        if (_originalSky != null)
        {
            _sky = new Material(_originalSky);
            _sky.name = "L4D_DarkerDusk_Runtime";
            if (_sky.HasProperty("_Exposure")) _sky.SetFloat("_Exposure", _originalSky.GetFloat("_Exposure") * 0.72f);
            RenderSettings.skybox = _sky;
        }
    }

    private void OnDestroy()
    {
        if (_sky == null) return;
        if (RenderSettings.skybox == _sky) RenderSettings.skybox = _originalSky;
        Destroy(_sky);
    }
}
