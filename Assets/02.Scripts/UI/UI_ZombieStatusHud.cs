using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 화면 상단 중앙: 처치 수 / 목표, 위험도 레벨과 경과 시간, 레벨이 오를 때 알림.
// ZombieSpawnManager가 자동으로 만든다.
[DisallowMultipleComponent]
public class UI_ZombieStatusHud : MonoBehaviour
{
    [SerializeField] private Color _panelColor = new Color(0.035f, 0.05f, 0.07f, 0.85f);
    [SerializeField] private Color _textColor = Color.white;
    [SerializeField] private Color _subTextColor = new Color(0.75f, 0.8f, 0.85f);
    [SerializeField] private Color _progressColor = new Color(0.45f, 0.9f, 0.45f);
    [SerializeField] private Color _alertColor = new Color(1f, 0.35f, 0.25f);
    [SerializeField, Min(0.5f)] private float _alertDuration = 2.5f;
    [SerializeField] private int _sortingOrder = 49;

    private ZombieSpawnManager _manager;
    private bool _built;
    private bool _korean;
    private GameObject _canvasObject;
    private TextMeshProUGUI _killText;
    private TextMeshProUGUI _levelText;
    private RectTransform _killFill;
    private TextMeshProUGUI _alertText;
    private TextMeshProUGUI _alertSubText;
    private float _alertTimer;

    private int _lastKills = -1;
    private int _lastTarget = -1;
    private int _lastLevel = -1;
    private int _lastSecond = -1;

    public static UI_ZombieStatusHud Create(ZombieSpawnManager manager)
    {
        UI_ZombieStatusHud hud = new GameObject("UI_ZombieStatusHud").AddComponent<UI_ZombieStatusHud>();
        hud.Bind(manager);
        return hud;
    }

    private void Awake()
    {
        Build();
    }

    private void Start()
    {
        if (_manager == null)
        {
            Bind(FindAnyObjectByType<ZombieSpawnManager>());
        }
    }

    public void Bind(ZombieSpawnManager manager)
    {
        if (_manager == manager)
        {
            return;
        }

        if (_manager != null)
        {
            _manager.LevelChanged -= HandleLevelChanged;
        }

        _manager = manager;

        if (_manager != null)
        {
            _manager.LevelChanged += HandleLevelChanged;
        }
    }

    private void HandleLevelChanged(int level)
    {
        if (_manager == null || _alertText == null)
        {
            return;
        }

        _alertText.text = _korean ? $"좀비가 강해졌다!  LV {level}" : $"ZOMBIES GROW STRONGER!  LV {level}";
        _alertSubText.text = _korean
            ? $"체력 x{_manager.HealthMultiplier:0.0}   공격력 x{_manager.DamageMultiplier:0.0}"
            : $"HP x{_manager.HealthMultiplier:0.0}   DMG x{_manager.DamageMultiplier:0.0}";
        _alertTimer = _alertDuration;
    }

    private void Update()
    {
        if (_built == false)
        {
            return;
        }

        GameManager game = GameManager.Instance;
        bool show = game != null && _manager != null;
        if (_canvasObject.activeSelf != show)
        {
            _canvasObject.SetActive(show);
        }

        if (show == false)
        {
            return;
        }

        // 처치 수 (바뀔 때만 갱신)
        int kills = game.KillCount;
        int target = Mathf.Max(1, game.TargetKills);
        if (kills != _lastKills || target != _lastTarget)
        {
            _lastKills = kills;
            _lastTarget = target;
            _killText.text = (_korean ? "처치  " : "KILLS  ") + kills + "<color=#" + ColorUtility.ToHtmlStringRGB(_subTextColor) + "> / " + target + "</color>";
            _killFill.anchorMax = new Vector2(Mathf.Clamp01((float)kills / target), 1f);
        }

        // 위험도 레벨과 경과 시간 (초가 바뀔 때만 갱신)
        int level = _manager.Level;
        int second = Mathf.FloorToInt(_manager.ElapsedTime);
        if (level != _lastLevel || second != _lastSecond)
        {
            _lastLevel = level;
            _lastSecond = second;
            string time = $"{second / 60:00}:{second % 60:00}";
            _levelText.text = (_korean ? "위험도 LV " : "THREAT LV ") + level + "   ·   " + time;
        }

        // 레벨업 알림 (서서히 사라짐)
        if (_alertTimer > 0f)
        {
            _alertTimer -= Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(_alertTimer / 0.6f);
            float pop = 1f + 0.15f * Mathf.Clamp01((_alertTimer - (_alertDuration - 0.2f)) / 0.2f);
            SetAlpha(_alertText, alpha);
            SetAlpha(_alertSubText, alpha);
            _alertText.rectTransform.localScale = Vector3.one * pop;
            if (_alertText.gameObject.activeSelf == false)
            {
                _alertText.gameObject.SetActive(true);
                _alertSubText.gameObject.SetActive(true);
            }
        }
        else if (_alertText.gameObject.activeSelf)
        {
            _alertText.gameObject.SetActive(false);
            _alertSubText.gameObject.SetActive(false);
        }
    }

    private static void SetAlpha(TextMeshProUGUI text, float alpha)
    {
        Color color = text.color;
        color.a = alpha;
        text.color = color;
    }

    private void Build()
    {
        if (_built)
        {
            return;
        }

        _built = true;
        TMP_FontAsset font = FindFont(out _korean);

        _canvasObject = new GameObject("ZombieStatus_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        _canvasObject.transform.SetParent(transform, false);
        Canvas canvas = _canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = _sortingOrder;
        CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        Transform canvasTransform = _canvasObject.transform;

        // 상단 중앙 패널
        RectTransform panel = CreateRect("StatusPanel", canvasTransform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -14f), new Vector2(300f, 64f));
        CreateImage(panel, _panelColor);

        RectTransform kill = CreateRect("Kills", panel,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -6f), new Vector2(0f, 28f));
        _killText = CreateText(kill, font, 21f, FontStyles.Bold, TextAlignmentOptions.Center, _textColor);
        _killText.richText = true;

        RectTransform barBack = CreateRect("KillBar", panel,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -36f), new Vector2(-40f, 4f));
        CreateImage(barBack, new Color(1f, 1f, 1f, 0.15f));
        _killFill = CreateRect("Fill", barBack, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        CreateImage(_killFill, _progressColor);

        RectTransform level = CreateRect("Level", panel,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -42f), new Vector2(0f, 20f));
        _levelText = CreateText(level, font, 13f, FontStyles.Bold, TextAlignmentOptions.Center, _subTextColor);

        // 레벨업 알림
        RectTransform alert = CreateRect("LevelUpAlert", canvasTransform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -112f), new Vector2(600f, 36f));
        _alertText = CreateText(alert, font, 26f, FontStyles.Bold, TextAlignmentOptions.Center, _alertColor);
        _alertText.gameObject.SetActive(false);

        RectTransform alertSub = CreateRect("LevelUpDetail", canvasTransform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -140f), new Vector2(600f, 24f));
        _alertSubText = CreateText(alertSub, font, 15f, FontStyles.Bold, TextAlignmentOptions.Center, _textColor);
        _alertSubText.gameObject.SetActive(false);
    }

    // 한글이 들어 있는 TMP 글꼴을 우선 찾는다. 없으면 영어로 표시한다.
    private static TMP_FontAsset FindFont(out bool korean)
    {
        korean = false;
        TMP_FontAsset fallback = null;

        foreach (TextMeshProUGUI text in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            TMP_FontAsset font = text.font;
            if (font == null)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = font;
            }

            if (font.HasCharacter('처', true, true) && font.HasCharacter('강', true, true))
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

    private static RectTransform CreateRect(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return rect;
    }

    private static Image CreateImage(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(RectTransform rect, TMP_FontAsset font, float size,
        FontStyles style, TextAlignmentOptions alignment, Color color)
    {
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            text.font = font;
        }

        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.text = string.Empty;
        return text;
    }

    private void OnDestroy()
    {
        if (_manager != null)
        {
            _manager.LevelChanged -= HandleLevelChanged;
        }
    }
}
