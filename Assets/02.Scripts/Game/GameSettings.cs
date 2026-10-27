using UnityEngine;

// 플레이어 설정 (마우스 감도, 볼륨, 시야각, 밝기). PlayerPrefs에 저장되어 다음 실행에도 유지된다.
// 값을 바꾸면 바로 게임에 적용된다. 메뉴(UI_GameMenu)의 설정 화면에서 바꾼다.
public static class GameSettings
{
    private const string Prefix = "ZombieGame.";

    public const float DefaultMouseSensitivity = 1f;
    public const float DefaultAimSensitivity = 1f;
    public const float DefaultMasterVolume = 1f;
    public const float DefaultSfxVolume = 1f;
    public const float DefaultMusicVolume = 0.8f;
    public const float DefaultBrightness = 1f;

    private static bool _loaded;
    private static float _sceneFov = -1f;
    private static float _mouseSensitivity = DefaultMouseSensitivity;
    private static float _aimSensitivity = DefaultAimSensitivity;
    private static bool _invertY;
    private static float _masterVolume = DefaultMasterVolume;
    private static float _sfxVolume = DefaultSfxVolume;
    private static float _musicVolume = DefaultMusicVolume;
    private static float _fieldOfView; // 0 = 씬에 정해진 값 그대로
    private static float _brightness = DefaultBrightness;

    // 밝기 조절 전의 원래 환경광 (씬마다 다시 기억)
    private static bool _ambientCaptured;
    private static Color _ambientLight, _ambientSky, _ambientEquator, _ambientGround;
    private static float _ambientIntensity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _loaded = false;
        _sceneFov = -1f;
        _ambientCaptured = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadOnStart()
    {
        Load();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    // 씬을 (다시) 불러오면 원래 시야각/환경광을 다시 기억하고 설정을 적용한다.
    private static void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        _sceneFov = -1f;
        _ambientCaptured = false;
        ApplyAll();
    }

    public static float MouseSensitivity
    {
        get { Load(); return _mouseSensitivity; }
        set { _mouseSensitivity = Mathf.Clamp(value, 0.05f, 5f); }
    }

    public static float AimSensitivity
    {
        get { Load(); return _aimSensitivity; }
        set { _aimSensitivity = Mathf.Clamp(value, 0.1f, 2f); }
    }

    public static bool InvertY
    {
        get { Load(); return _invertY; }
        set { _invertY = value; }
    }

    public static float MasterVolume
    {
        get { Load(); return _masterVolume; }
        set { _masterVolume = Mathf.Clamp01(value); ApplyAudio(); }
    }

    public static float SfxVolume
    {
        get { Load(); return _sfxVolume; }
        set { _sfxVolume = Mathf.Clamp01(value); ApplyAudio(); }
    }

    public static float MusicVolume
    {
        get { Load(); return _musicVolume; }
        set { _musicVolume = Mathf.Clamp01(value); ApplyAudio(); }
    }

    // 0이면 씬의 원래 시야각
    public static float FieldOfView
    {
        get { Load(); return _fieldOfView; }
        set { _fieldOfView = value <= 0f ? 0f : Mathf.Clamp(value, 50f, 100f); ApplyFieldOfView(); }
    }

    public static float Brightness
    {
        get { Load(); return _brightness; }
        set { _brightness = Mathf.Clamp(value, 0.5f, 2.5f); ApplyBrightness(); }
    }

    // 씬에 원래 정해진 시야각 (설정 화면의 기본값 표시용)
    public static float SceneFieldOfView
    {
        get
        {
            CaptureSceneFov();
            return _sceneFov > 0f ? _sceneFov : 60f;
        }
    }

    public static void Load()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        _mouseSensitivity = PlayerPrefs.GetFloat(Prefix + "MouseSensitivity", DefaultMouseSensitivity);
        _aimSensitivity = PlayerPrefs.GetFloat(Prefix + "AimSensitivity", DefaultAimSensitivity);
        _invertY = PlayerPrefs.GetInt(Prefix + "InvertY", 0) == 1;
        _masterVolume = PlayerPrefs.GetFloat(Prefix + "MasterVolume", DefaultMasterVolume);
        _sfxVolume = PlayerPrefs.GetFloat(Prefix + "SfxVolume", DefaultSfxVolume);
        _musicVolume = PlayerPrefs.GetFloat(Prefix + "MusicVolume", DefaultMusicVolume);
        _fieldOfView = PlayerPrefs.GetFloat(Prefix + "FieldOfView", 0f);
        _brightness = PlayerPrefs.GetFloat(Prefix + "Brightness", DefaultBrightness);
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(Prefix + "MouseSensitivity", _mouseSensitivity);
        PlayerPrefs.SetFloat(Prefix + "AimSensitivity", _aimSensitivity);
        PlayerPrefs.SetInt(Prefix + "InvertY", _invertY ? 1 : 0);
        PlayerPrefs.SetFloat(Prefix + "MasterVolume", _masterVolume);
        PlayerPrefs.SetFloat(Prefix + "SfxVolume", _sfxVolume);
        PlayerPrefs.SetFloat(Prefix + "MusicVolume", _musicVolume);
        PlayerPrefs.SetFloat(Prefix + "FieldOfView", _fieldOfView);
        PlayerPrefs.SetFloat(Prefix + "Brightness", _brightness);
        PlayerPrefs.Save();
    }

    public static void ResetToDefaults()
    {
        _mouseSensitivity = DefaultMouseSensitivity;
        _aimSensitivity = DefaultAimSensitivity;
        _invertY = false;
        _masterVolume = DefaultMasterVolume;
        _sfxVolume = DefaultSfxVolume;
        _musicVolume = DefaultMusicVolume;
        _fieldOfView = 0f;
        _brightness = DefaultBrightness;
        ApplyAll();
    }

    public static void ApplyAll()
    {
        Load();
        ApplyAudio();
        ApplyFieldOfView();
        ApplyBrightness();
    }

    // 전체 볼륨은 AudioListener로 (씬에 원래 있던 소리까지 모두), 효과음/음악은 GameAudio로 나눈다.
    private static void ApplyAudio()
    {
        AudioListener.volume = _masterVolume;
        GameAudio.SfxVolume = _sfxVolume;
        GameAudio.MusicVolume = _musicVolume;
    }

    private static void CaptureSceneFov()
    {
        if (_sceneFov > 0f)
        {
            return;
        }

        Camera view = Camera.main;
        if (view != null)
        {
            _sceneFov = view.fieldOfView;
        }
    }

    // 정조준 중에는 PlayerAim이 시야각을 바꾸므로, 조준이 풀릴 때 이 값을 기본으로 따른다.
    private static void ApplyFieldOfView()
    {
        Camera view = Camera.main;
        if (view == null)
        {
            return;
        }

        CaptureSceneFov();
        PlayerAim aim = Object.FindAnyObjectByType<PlayerAim>();
        if (aim != null && aim.AimBlend > 0f)
        {
            return; // 조준 중이면 조준이 풀린 뒤 PlayerAim이 이 값을 읽어 간다.
        }

        view.fieldOfView = _fieldOfView > 0f ? _fieldOfView : _sceneFov;
    }

    // 밤 맵이 너무 어두울 때 환경광을 키운다. (손전등/가로등 같은 조명은 그대로)
    private static void ApplyBrightness()
    {
        if (_ambientCaptured == false)
        {
            _ambientCaptured = true;
            _ambientLight = RenderSettings.ambientLight;
            _ambientSky = RenderSettings.ambientSkyColor;
            _ambientEquator = RenderSettings.ambientEquatorColor;
            _ambientGround = RenderSettings.ambientGroundColor;
            _ambientIntensity = RenderSettings.ambientIntensity;
        }

        float b = _brightness;
        RenderSettings.ambientLight = _ambientLight * b;
        RenderSettings.ambientSkyColor = _ambientSky * b;
        RenderSettings.ambientEquatorColor = _ambientEquator * b;
        RenderSettings.ambientGroundColor = _ambientGround * b;
        RenderSettings.ambientIntensity = _ambientIntensity * b;
        DynamicGI.UpdateEnvironment(); // 하늘(Skybox) 기반 환경광도 바로 다시 계산
    }

    // 지금 시야각 설정값 (0이면 씬의 원래 값)
    public static float EffectiveFieldOfView => _fieldOfView > 0f ? _fieldOfView : SceneFieldOfView;
}
