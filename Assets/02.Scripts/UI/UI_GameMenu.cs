using Input = GameInput;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 시작 화면, 일시 정지 메뉴, 설정, 조작법, 결과 화면.
//  - 시작 화면: 게임 시작 / 설정 / 조작법 / 종료 (다시 시작할 때는 건너뜀)
//  - 일시 정지: Esc 또는 P. 계속하기 / 설정 / 조작법 / 다시 시작 / 시작 화면으로 / 종료
//  - 설정: 마우스 감도, 정조준 감도, 위아래 반전, 시야각, 밝기, 전체/효과음/음악 볼륨 (바로 적용, 자동 저장)
//  - 결과: 처치 수, 생존 시간, 레벨 + 다시 시작 / 시작 화면으로
// 씬에 배치하지 않아도 GameManager가 시작할 때 자동으로 만든다. 한글 글꼴이 없으면 영어로 표시한다.
[DisallowMultipleComponent]
public class UI_GameMenu : MonoBehaviour
{
    [SerializeField] private bool _showTitleOnStart = true;
    [SerializeField] private string _gameTitle = "ZOMBIE CITY";

    [Header("색")]
    [SerializeField] private Color _accent = new Color(0.85f, 0.14f, 0.1f);
    [SerializeField] private Color _panelColor = new Color(0.05f, 0.06f, 0.08f, 0.92f);
    [SerializeField] private Color _buttonColor = new Color(0.13f, 0.14f, 0.17f, 0.95f);
    [SerializeField] private Color _textColor = new Color(0.93f, 0.93f, 0.95f);
    [SerializeField] private Color _subTextColor = new Color(0.65f, 0.68f, 0.72f);

    private enum Page { None, Title, Pause, Settings, Controls, Result }

    public static bool IsOpen { get; private set; }

    public bool ShowTitleOnStart => _showTitleOnStart;

    private GameManager _game;
    private Page _page = Page.None;
    private Page _backPage = Page.None;
    private bool _korean;
    private TMP_FontAsset _font;
    private Sprite _roundSprite;
    private Sprite _circleSprite;
    private Sprite _vignetteSprite;
    private readonly List<Object> _generated = new List<Object>();

    private Image _dim;
    private Image _vignette;
    private readonly Dictionary<Page, CanvasGroup> _pages = new Dictionary<Page, CanvasGroup>();
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _resultTitle;
    private TextMeshProUGUI _resultStats;
    private readonly List<System.Action> _settingsRefreshers = new List<System.Action>();
    private float _fade;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsOpen = false;
    }

    public static UI_GameMenu Create()
    {
        return new GameObject("UI_GameMenu").AddComponent<UI_GameMenu>();
    }

    private void Awake()
    {
        Build();
        ShowPage(Page.None);
    }

    private void Start()
    {
        GameSettings.ApplyAll();
        _game = GameManager.Instance;
        if (_game != null)
        {
            _game.MenuHandlesPause = true;
            _game.StateChanged += HandleStateChanged;
        }
    }

    // GameManager가 부른다. 시작 화면을 띄우고 '게임 시작'을 기다린다.
    public void OpenTitle()
    {
        ShowPage(Page.Title);
    }

    private void HandleStateChanged(GameState state)
    {
        switch (state)
        {
            case GameState.Pause:
                ShowPage(Page.Pause);
                break;
            case GameState.Playing:
                ShowPage(Page.None);
                break;
            case GameState.GameOver:
            case GameState.Clear:
                RefreshResult();
                ShowPage(Page.Result);
                break;
        }
    }

    private void Update()
    {
        // 메뉴가 열려 있는 동안은 커서를 보이게 한다. (총 발사/정조준도 커서가 보이면 멈춘다)
        if (_page != Page.None)
        {
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (Cursor.visible == false) Cursor.visible = true;
        }

        _fade = Mathf.MoveTowards(_fade, 1f, Time.unscaledDeltaTime / 0.18f);
        if (_pages.TryGetValue(_page, out CanvasGroup group))
        {
            group.alpha = _fade;
        }

        // 제목이 고장 난 네온사인처럼 가끔 깜빡인다.
        if (_page == Page.Title && _titleText != null)
        {
            float t = Time.unscaledTime;
            float flicker = Mathf.PerlinNoise(t * 7f, 0.3f) > 0.82f ? 0.35f : 1f;
            Color c = _accent;
            c.a = flicker;
            _titleText.color = c;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            HandleBackKey();
        }
    }

    private void HandleBackKey()
    {
        switch (_page)
        {
            case Page.None:
                if (_game != null && _game.State == GameState.Playing) _game.TogglePause();
                break;
            case Page.Pause:
                if (_game != null) _game.TogglePause();
                break;
            case Page.Settings:
            case Page.Controls:
                CloseSubPage();
                break;
        }
    }

    // ----- 화면 전환 -----

    private void ShowPage(Page page)
    {
        _page = page;
        _fade = 0f;

        // 마지막에 누른 버튼이 선택된 채로 남아 Space/Enter로 다시 눌리지 않게 한다.
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        foreach (KeyValuePair<Page, CanvasGroup> pair in _pages)
        {
            bool active = pair.Key == page;
            pair.Value.gameObject.SetActive(active);
            pair.Value.alpha = 0f;
        }

        IsOpen = page != Page.None;
        _dim.gameObject.SetActive(IsOpen);
        _vignette.gameObject.SetActive(IsOpen);

        // 시작 화면은 진하게, 결과/일시 정지는 게임 화면이 보이도록 옅게
        Color dim = Color.black;
        dim.a = page == Page.Title ? 0.72f : 0.55f;
        _dim.color = dim;

        if (page == Page.Settings)
        {
            foreach (System.Action refresh in _settingsRefreshers) refresh();
        }
    }

    private void OpenSubPage(Page page)
    {
        _backPage = _page;
        ShowPage(page);
    }

    private void CloseSubPage()
    {
        GameSettings.Save();
        ShowPage(_backPage == Page.None ? Page.Title : _backPage);
    }

    private void RefreshResult()
    {
        if (_game == null)
        {
            return;
        }

        _resultTitle.text = string.IsNullOrEmpty(_game.ResultMessage) ? L("게임 종료", "GAME OVER") : LocalizeResult(_game.ResultMessage);
        _resultTitle.color = _game.ResultColor;

        ZombieSpawnManager spawns = FindAnyObjectByType<ZombieSpawnManager>();
        float time = spawns != null ? spawns.ElapsedTime : Time.timeSinceLevelLoad;
        int level = spawns != null ? spawns.Level : 1;
        string clock = $"{Mathf.FloorToInt(time / 60f):00}:{Mathf.FloorToInt(time % 60f):00}";
        _resultStats.text = L($"처치  {_game.KillCount} / {_game.TargetKills}      생존 시간  {clock}      레벨  {level}",
            $"KILLS  {_game.KillCount} / {_game.TargetKills}      TIME  {clock}      LEVEL  {level}");
    }

    private string LocalizeResult(string message)
    {
        if (_korean) return message;
        if (message.Contains("탈출")) return "ESCAPED!";
        if (message.Contains("승리")) return "VICTORY!";
        if (message.Contains("오버")) return "GAME OVER";
        return message;
    }

    // ----- 버튼 동작 -----

    private void OnStartGame()
    {
        ShowPage(Page.None);
        if (_game != null) _game.BeginGame();
    }

    private void OnResume()
    {
        if (_game != null && _game.State == GameState.Pause) _game.TogglePause();
    }

    private void OnRestart()
    {
        GameSettings.Save();
        if (_game != null) _game.RestartFromMenu();
    }

    private void OnTitle()
    {
        GameSettings.Save();
        if (_game != null) _game.ReturnToTitle();
    }

    private void OnQuit()
    {
        GameSettings.Save();
        if (_game != null) _game.QuitGame();
    }

    // ----- 화면 만들기 -----

    private string L(string korean, string english)
    {
        return _korean ? korean : english;
    }

    private void Build()
    {
        _font = FindFont(out _korean);
        _roundSprite = CreateRoundedSprite(32, 10);
        _circleSprite = CreateCircleSprite(32);
        _vignetteSprite = CreateVignetteSprite(64);

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        GameInput.ConfigureUI(EventSystem.current);

        GameObject canvasObject = new GameObject("GameMenu_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform root = (RectTransform)canvasObject.transform;

        _dim = CreateImage(Stretch("Dim", root), Color.black, null);
        _vignette = CreateImage(Stretch("Vignette", root), new Color(0f, 0f, 0f, 0.8f), _vignetteSprite);

        BuildTitle(root);
        BuildPause(root);
        BuildSettings(root);
        BuildControls(root);
        BuildResult(root);
    }

    private void BuildTitle(RectTransform root)
    {
        RectTransform page = CreatePage(Page.Title, root);

        _titleText = CreateText(page, "Title", _gameTitle, new Vector2(0f, 270f), new Vector2(1600f, 170f), 130f, FontStyles.Bold, TextAlignmentOptions.Center, _accent);
        _titleText.characterSpacing = 14f;
        int kills = GameManager.Instance != null ? GameManager.Instance.TargetKills : 50;
        CreateText(page, "Subtitle", L($"안전가옥까지 살아서 도착하거나, 좀비 {kills}마리를 처치하라", $"REACH THE SAFE ROOM OR KILL {kills} ZOMBIES"),
            new Vector2(0f, 170f), new Vector2(1500f, 50f), 30f, FontStyles.Normal, TextAlignmentOptions.Center, _subTextColor);

        CreateButton(page, L("게임 시작", "START GAME"), new Vector2(0f, 40f), 440f, OnStartGame, true);
        CreateButton(page, L("설정", "SETTINGS"), new Vector2(0f, -45f), 440f, () => OpenSubPage(Page.Settings));
        CreateButton(page, L("조작법", "CONTROLS"), new Vector2(0f, -130f), 440f, () => OpenSubPage(Page.Controls));
        CreateButton(page, L("게임 종료", "QUIT"), new Vector2(0f, -215f), 440f, OnQuit);

        CreateText(page, "Footer", L("WASD 이동   ·   마우스 왼쪽 사격   ·   오른쪽 정조준   ·   Esc 메뉴", "WASD MOVE   ·   LMB FIRE   ·   RMB AIM   ·   ESC MENU"),
            new Vector2(0f, -440f), new Vector2(1500f, 40f), 22f, FontStyles.Normal, TextAlignmentOptions.Center, _subTextColor);
    }

    private void BuildPause(RectTransform root)
    {
        RectTransform page = CreatePage(Page.Pause, root);
        CreatePanel(page, new Vector2(0f, 0f), new Vector2(560f, 680f));
        CreateText(page, "Header", L("일시 정지", "PAUSED"), new Vector2(0f, 262f), new Vector2(500f, 80f), 54f, FontStyles.Bold, TextAlignmentOptions.Center, _textColor);
        CreateAccentLine(page, new Vector2(0f, 215f), 120f);

        CreateButton(page, L("계속하기", "RESUME"), new Vector2(0f, 140f), 420f, OnResume, true);
        CreateButton(page, L("설정", "SETTINGS"), new Vector2(0f, 60f), 420f, () => OpenSubPage(Page.Settings));
        CreateButton(page, L("조작법", "CONTROLS"), new Vector2(0f, -20f), 420f, () => OpenSubPage(Page.Controls));
        CreateButton(page, L("다시 시작", "RESTART"), new Vector2(0f, -100f), 420f, OnRestart);
        CreateButton(page, L("시작 화면으로", "MAIN MENU"), new Vector2(0f, -180f), 420f, OnTitle);
        CreateButton(page, L("게임 종료", "QUIT"), new Vector2(0f, -260f), 420f, OnQuit);
    }

    private void BuildSettings(RectTransform root)
    {
        RectTransform page = CreatePage(Page.Settings, root);
        CreatePanel(page, Vector2.zero, new Vector2(940f, 860f));
        CreateText(page, "Header", L("설정", "SETTINGS"), new Vector2(0f, 362f), new Vector2(600f, 80f), 54f, FontStyles.Bold, TextAlignmentOptions.Center, _textColor);
        CreateAccentLine(page, new Vector2(0f, 315f), 120f);

        float y = 245f;
        const float step = 68f;
        CreateSliderRow(page, L("마우스 감도", "MOUSE SENSITIVITY"), y, 0.1f, 3f,
            () => GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v, v => v.ToString("0.00"));
        y -= step;
        CreateSliderRow(page, L("정조준 감도", "AIM SENSITIVITY"), y, 0.3f, 1.5f,
            () => GameSettings.AimSensitivity, v => GameSettings.AimSensitivity = v, v => v.ToString("0.00"));
        y -= step;
        CreateToggleRow(page, L("마우스 위아래 반전", "INVERT MOUSE Y"), y,
            () => GameSettings.InvertY, v => GameSettings.InvertY = v);
        y -= step;
        CreateSliderRow(page, L("시야각", "FIELD OF VIEW"), y, 50f, 100f,
            () => GameSettings.EffectiveFieldOfView, v => GameSettings.FieldOfView = Mathf.Round(v), v => Mathf.Round(v) + "°");
        y -= step;
        CreateSliderRow(page, L("밝기", "BRIGHTNESS"), y, 0.5f, 2.5f,
            () => GameSettings.Brightness, v => GameSettings.Brightness = v, v => Mathf.RoundToInt(v * 100f) + "%");
        y -= step;
        CreateSliderRow(page, L("전체 볼륨", "MASTER VOLUME"), y, 0f, 1f,
            () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, v => Mathf.RoundToInt(v * 100f) + "%");
        y -= step;
        CreateSliderRow(page, L("효과음", "EFFECTS"), y, 0f, 1f,
            () => GameSettings.SfxVolume, v => GameSettings.SfxVolume = v, v => Mathf.RoundToInt(v * 100f) + "%");
        y -= step;
        CreateSliderRow(page, L("음악 / 환경음", "MUSIC / AMBIENCE"), y, 0f, 1f,
            () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, v => Mathf.RoundToInt(v * 100f) + "%");

        CreateButton(page, L("기본값으로", "DEFAULTS"), new Vector2(-150f, -345f), 260f, () =>
        {
            GameSettings.ResetToDefaults();
            foreach (System.Action refresh in _settingsRefreshers) refresh();
        });
        CreateButton(page, L("돌아가기", "BACK"), new Vector2(150f, -345f), 260f, CloseSubPage, true);
    }

    private void BuildControls(RectTransform root)
    {
        RectTransform page = CreatePage(Page.Controls, root);
        CreatePanel(page, Vector2.zero, new Vector2(900f, 820f));
        CreateText(page, "Header", L("조작법", "CONTROLS"), new Vector2(0f, 342f), new Vector2(600f, 80f), 54f, FontStyles.Bold, TextAlignmentOptions.Center, _textColor);
        CreateAccentLine(page, new Vector2(0f, 295f), 120f);

        string[,] rows =
        {
            { "W A S D", "이동", "MOVE" },
            { "SHIFT", "달리기", "SPRINT" },
            { "SPACE", "점프 (공중에서 한 번 더)", "JUMP (DOUBLE JUMP)" },
            { "마우스 왼쪽", "사격", "FIRE" },
            { "마우스 오른쪽", "정조준 (누르는 동안)", "AIM DOWN SIGHTS (HOLD)" },
            { "R", "재장전", "RELOAD" },
            { "Q", "수류탄 던지기", "THROW BOMB" },
            { "F", "손전등", "FLASHLIGHT" },
            { "ESC / P", "일시 정지 메뉴", "PAUSE MENU" },
            { "ALT", "마우스 커서 풀기 / 잠그기", "UNLOCK / LOCK CURSOR" },
        };

        float y = 225f;
        for (int i = 0; i < rows.GetLength(0); i++)
        {
            string key = rows[i, 0];
            if (_korean == false)
            {
                if (key == "마우스 왼쪽") key = "LEFT MOUSE";
                else if (key == "마우스 오른쪽") key = "RIGHT MOUSE";
            }

            CreateText(page, "Key", key, new Vector2(-150f, y), new Vector2(320f, 46f), 28f, FontStyles.Bold, TextAlignmentOptions.MidlineRight, _accent);
            CreateText(page, "Action", L(rows[i, 1], rows[i, 2]), new Vector2(210f, y), new Vector2(420f, 46f), 28f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, _textColor);
            y -= 52f;
        }

        CreateButton(page, L("돌아가기", "BACK"), new Vector2(0f, -330f), 300f, CloseSubPage, true);
    }

    private void BuildResult(RectTransform root)
    {
        RectTransform page = CreatePage(Page.Result, root);
        CreatePanel(page, new Vector2(0f, -10f), new Vector2(820f, 470f));
        _resultTitle = CreateText(page, "Header", "", new Vector2(0f, 150f), new Vector2(780f, 110f), 84f, FontStyles.Bold, TextAlignmentOptions.Center, _textColor);
        _resultTitle.characterSpacing = 8f;
        _resultStats = CreateText(page, "Stats", "", new Vector2(0f, 55f), new Vector2(780f, 50f), 28f, FontStyles.Normal, TextAlignmentOptions.Center, _subTextColor);
        CreateButton(page, L("다시 시작", "RESTART"), new Vector2(0f, -45f), 400f, OnRestart, true);
        CreateButton(page, L("시작 화면으로", "MAIN MENU"), new Vector2(0f, -130f), 400f, OnTitle);
        CreateText(page, "Hint", L("R 키로도 다시 시작할 수 있습니다", "PRESS R TO RESTART"), new Vector2(0f, -200f), new Vector2(700f, 36f), 22f, FontStyles.Normal, TextAlignmentOptions.Center, _subTextColor);
    }

    // ----- UI 부품 -----

    private RectTransform CreatePage(Page page, RectTransform parent)
    {
        RectTransform rect = Stretch(page.ToString(), parent);
        CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
        _pages[page] = group;
        return rect;
    }

    private void CreatePanel(RectTransform parent, Vector2 position, Vector2 size)
    {
        RectTransform rect = CreateRect("Panel", parent, position, size);
        Image image = CreateImage(rect, _panelColor, _roundSprite);
        image.type = Image.Type.Sliced;

        // 위쪽 빨간 테두리
        RectTransform top = CreateRect("TopEdge", rect, Vector2.zero, new Vector2(0f, 4f));
        top.anchorMin = new Vector2(0f, 1f);
        top.anchorMax = new Vector2(1f, 1f);
        top.pivot = new Vector2(0.5f, 1f);
        top.anchoredPosition = Vector2.zero;
        top.sizeDelta = new Vector2(-20f, 4f);
        CreateImage(top, _accent, null);
    }

    private void CreateAccentLine(RectTransform parent, Vector2 position, float width)
    {
        CreateImage(CreateRect("Line", parent, position, new Vector2(width, 3f)), _accent, null);
    }

    private Button CreateButton(RectTransform parent, string label, Vector2 position, float width, UnityAction onClick, bool primary = false)
    {
        RectTransform rect = CreateRect("Button_" + label, parent, position, new Vector2(width, 64f));
        Image image = CreateImage(rect, Color.white, _roundSprite);
        image.type = Image.Type.Sliced;
        image.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        Color normal = primary ? Color.Lerp(_buttonColor, _accent, 0.55f) : _buttonColor;
        colors.normalColor = normal;
        colors.highlightedColor = Color.Lerp(normal, _accent, 0.75f) + new Color(0.08f, 0.08f, 0.08f, 0f);
        colors.pressedColor = _accent * 0.7f;
        colors.selectedColor = normal;
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(() => GameAudio.Play2D("ui_hitmarker", 0.35f, 0.02f));
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = CreateText(rect, "Label", label, Vector2.zero, new Vector2(width - 20f, 60f), 30f, FontStyles.Bold, TextAlignmentOptions.Center, _textColor);
        text.characterSpacing = 3f;
        return button;
    }

    private void CreateSliderRow(RectTransform parent, string label, float y, float min, float max,
        System.Func<float> getter, System.Action<float> setter, System.Func<float, string> format)
    {
        CreateText(parent, "Label", label, new Vector2(-235f, y), new Vector2(330f, 50f), 28f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, _textColor);

        RectTransform rect = CreateRect("Slider", parent, new Vector2(115f, y), new Vector2(360f, 30f));
        // 막대 어디를 눌러도 반응하도록 투명한 판을 깐다.
        CreateImage(rect, Color.clear, null).raycastTarget = true;
        Slider slider = rect.gameObject.AddComponent<Slider>();

        RectTransform background = CreateRect("Background", rect, Vector2.zero, Vector2.zero);
        background.anchorMin = new Vector2(0f, 0.5f);
        background.anchorMax = new Vector2(1f, 0.5f);
        background.sizeDelta = new Vector2(0f, 8f);
        Image backImage = CreateImage(background, new Color(1f, 1f, 1f, 0.14f), _roundSprite);
        backImage.type = Image.Type.Sliced;

        RectTransform fillArea = CreateRect("FillArea", rect, Vector2.zero, Vector2.zero);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(-24f, 8f); // 손잡이 이동 범위와 맞춘다.
        RectTransform fill = CreateRect("Fill", fillArea, Vector2.zero, Vector2.zero);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = new Vector2(-12f, 0f); // 막대 왼쪽 끝부터 채운다.
        fill.offsetMax = Vector2.zero;
        Image fillImage = CreateImage(fill, _accent, _roundSprite);
        fillImage.type = Image.Type.Sliced;

        RectTransform handleArea = CreateRect("HandleArea", rect, Vector2.zero, Vector2.zero);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.sizeDelta = new Vector2(-24f, 0f);
        RectTransform handle = CreateRect("Handle", handleArea, Vector2.zero, new Vector2(26f, 26f));
        handle.anchorMin = new Vector2(0f, 0.5f);
        handle.anchorMax = new Vector2(0f, 0.5f);
        Image handleImage = CreateImage(handle, Color.white, _circleSprite);
        handleImage.raycastTarget = true;

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.minValue = min;
        slider.maxValue = max;

        TextMeshProUGUI valueText = CreateText(parent, "Value", "", new Vector2(370f, y), new Vector2(130f, 50f), 28f, FontStyles.Bold, TextAlignmentOptions.MidlineRight, _accent);

        // 설정 화면을 열 때마다 저장된 값으로 맞춘다.
        System.Action refresh = () =>
        {
            float value = Mathf.Clamp(getter(), min, max);
            slider.SetValueWithoutNotify(value);
            valueText.text = format(value);
        };
        _settingsRefreshers.Add(refresh);
        refresh();

        slider.onValueChanged.AddListener(value =>
        {
            setter(value);
            valueText.text = format(value);
        });
    }

    private void CreateToggleRow(RectTransform parent, string label, float y, System.Func<bool> getter, System.Action<bool> setter)
    {
        CreateText(parent, "Label", label, new Vector2(-235f, y), new Vector2(330f, 50f), 28f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, _textColor);

        RectTransform rect = CreateRect("Toggle", parent, new Vector2(-48f, y), new Vector2(38f, 38f));
        Image background = CreateImage(rect, Color.white, _roundSprite);
        background.type = Image.Type.Sliced;
        background.raycastTarget = true;
        RectTransform check = CreateRect("Check", rect, Vector2.zero, new Vector2(22f, 22f));
        Image checkImage = CreateImage(check, _accent, _roundSprite);
        checkImage.type = Image.Type.Sliced;

        Toggle toggle = rect.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = checkImage;
        toggle.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = toggle.colors;
        colors.normalColor = new Color(0.25f, 0.26f, 0.3f);
        colors.highlightedColor = new Color(0.4f, 0.4f, 0.45f);
        colors.pressedColor = new Color(0.2f, 0.2f, 0.22f);
        colors.selectedColor = colors.normalColor;
        toggle.colors = colors;

        TextMeshProUGUI valueText = CreateText(parent, "Value", "", new Vector2(370f, y), new Vector2(130f, 50f), 28f, FontStyles.Bold, TextAlignmentOptions.MidlineRight, _accent);

        System.Action refresh = () =>
        {
            bool value = getter();
            toggle.SetIsOnWithoutNotify(value);
            valueText.text = value ? L("켜짐", "ON") : L("꺼짐", "OFF");
        };
        _settingsRefreshers.Add(refresh);
        refresh();

        toggle.onValueChanged.AddListener(value =>
        {
            setter(value);
            valueText.text = value ? L("켜짐", "ON") : L("꺼짐", "OFF");
            GameAudio.Play2D("ui_hitmarker", 0.3f, 0.02f);
        });
    }

    private TextMeshProUGUI CreateText(RectTransform parent, string name, string text, Vector2 position, Vector2 size,
        float fontSize, FontStyles style, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = CreateRect(name, parent, position, size);
        TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) tmp.font = _font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static RectTransform CreateRect(string name, RectTransform parent, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform Stretch(string name, RectTransform parent)
    {
        RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.zero);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Image CreateImage(RectTransform rect, Color color, Sprite sprite)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // ----- 글꼴 / 스프라이트 -----

    // 한글이 들어 있는 TMP 글꼴을 우선 찾는다. 없으면 영어로 표시한다.
    private static TMP_FontAsset FindFont(out bool korean)
    {
        korean = false;
        TMP_FontAsset fallback = null;
        foreach (TextMeshProUGUI text in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            TMP_FontAsset font = text.font;
            if (font == null) continue;
            if (fallback == null) fallback = font;
            if (font.HasCharacter('설', true, true) && font.HasCharacter('정', true, true))
            {
                korean = true;
                return font;
            }
        }

        if (fallback == null && TMP_Settings.instance != null)
        {
            fallback = TMP_Settings.defaultFontAsset;
        }

        return fallback;
    }

    private Sprite CreateRoundedSprite(int size, int radius)
    {
        Texture2D texture = NewTexture(size);
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 모서리에서 가장 가까운 둥근 중심까지의 거리로 부드러운 가장자리를 만든다.
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                float a = Mathf.Clamp01(radius - d + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
        _generated.Add(texture);
        _generated.Add(sprite);
        return sprite;
    }

    private Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = NewTexture(size);
        Color32[] pixels = new Color32[size * size];
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                float a = Mathf.Clamp01(c - d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        _generated.Add(texture);
        _generated.Add(sprite);
        return sprite;
    }

    private Sprite CreateVignetteSprite(int size)
    {
        Texture2D texture = NewTexture(size);
        Color32[] pixels = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c;
                float dy = (y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                float a = Mathf.Clamp01((r - 0.3f) / 0.7f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * a * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        _generated.Add(texture);
        _generated.Add(sprite);
        return sprite;
    }

    private static Texture2D NewTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }

    private void OnDestroy()
    {
        IsOpen = false;
        if (_game != null)
        {
            _game.StateChanged -= HandleStateChanged;
        }

        GameSettings.Save();
        foreach (Object generated in _generated)
        {
            if (generated != null) Destroy(generated);
        }
    }
}
