using TMPro;
using UnityEngine;
using UnityEngine.UI;

// AI 디렉터의 화면 글자: 목표, 경고(호드!), 버튼 안내(E), 좀비 수, 피날레 남은 시간
// AIDirector가 시작할 때 코드로 만든다. (씬에 따로 놓지 않아도 된다)
public class DirectorHUD : MonoBehaviour
{
    private TextMeshProUGUI _objective;
    private TextMeshProUGUI _alert;
    private TextMeshProUGUI _prompt;
    private TextMeshProUGUI _info;
    private TextMeshProUGUI _timer;
    private float _alertTime;
    private float _alertDuration;

    // 보스(탱크) 체력 막대
    private GameObject _bossBar;
    private RectTransform _bossFill;
    private TextMeshProUGUI _bossName;

    // 특수 좀비 등 다른 스크립트가 화면 글자를 쓸 때
    public static DirectorHUD Instance { get; private set; }

    public static DirectorHUD Create()
    {
        GameObject canvasObject = new GameObject("DirectorHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        DirectorHUD hud = canvasObject.AddComponent<DirectorHUD>();
        hud.Build();
        Instance = hud;
        return hud;
    }

    private void Build()
    {
        TMP_FontAsset font = FindKoreanFont();
        _objective = CreateText("Objective", font, 34f, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Color(1f, 0.95f, 0.8f));
        _timer = CreateText("Timer", font, 44f, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Color(1f, 0.85f, 0.3f));
        _alert = CreateText("Alert", font, 72f, new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), Color.red);
        _prompt = CreateText("Prompt", font, 38f, new Vector2(0.5f, 0f), new Vector2(0f, 230f), Color.white);
        _info = CreateText("Info", font, 30f, new Vector2(0f, 1f), new Vector2(40f, -36f), new Color(0.85f, 0.85f, 0.85f));
        _info.alignment = TextAlignmentOptions.Left;
        BuildBossBar(font);
    }

    private void BuildBossBar(TMP_FontAsset font)
    {
        _bossBar = new GameObject("BossBar", typeof(RectTransform), typeof(Image));
        RectTransform bar = (RectTransform)_bossBar.transform;
        bar.SetParent(transform, false);
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f);
        bar.pivot = new Vector2(0.5f, 0.5f);
        bar.anchoredPosition = new Vector2(0f, -172f);
        bar.sizeDelta = new Vector2(640f, 22f);
        Image back = _bossBar.GetComponent<Image>();
        back.color = new Color(0f, 0f, 0f, 0.6f);
        back.raycastTarget = false;

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        _bossFill = (RectTransform)fill.transform;
        _bossFill.SetParent(bar, false);
        _bossFill.anchorMin = Vector2.zero;
        _bossFill.anchorMax = Vector2.one;
        _bossFill.offsetMin = new Vector2(3f, 3f);
        _bossFill.offsetMax = new Vector2(-3f, -3f);
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = new Color(0.9f, 0.15f, 0.1f);
        fillImage.raycastTarget = false;

        _bossName = CreateText("BossName", font, 30f, new Vector2(0.5f, 1f), new Vector2(0f, -138f), new Color(1f, 0.4f, 0.3f));
        _bossBar.SetActive(false);
    }

    // 보스 체력 막대. name이 비어 있으면 숨긴다.
    public void SetBoss(string name, float ratio)
    {
        bool show = !string.IsNullOrEmpty(name);
        if (_bossBar.activeSelf != show)
        {
            _bossBar.SetActive(show);
        }

        _bossName.text = show ? name : string.Empty;
        if (show)
        {
            _bossFill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // 씬에 있는 한글 폰트(11장 StateText의 폰트)를 빌려 쓴다.
    private static TMP_FontAsset FindKoreanFont()
    {
        foreach (TextMeshProUGUI text in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text.font != null && text.font.HasCharacter('좀', true, true))
            {
                return text.font;
            }
        }

        return TMP_Settings.defaultFontAsset;
    }

    private TextMeshProUGUI CreateText(string name, TMP_FontAsset font, float size, Vector2 anchor, Vector2 position, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x == 0f ? 0f : 0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(1400f, 90f);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            text.font = font;
        }

        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 200);
        text.text = string.Empty;
        return text;
    }

    public void SetObjective(string message)
    {
        _objective.text = message;
    }

    public void SetPrompt(string message)
    {
        _prompt.text = message;
    }

    public void SetInfo(string message)
    {
        _info.text = message;
    }

    public void SetTimer(string message)
    {
        _timer.text = message;
    }

    public void ShowAlert(string message, Color color, float duration)
    {
        _alert.text = message;
        _alert.color = color;
        _alertTime = 0f;
        _alertDuration = duration;
    }

    private void Update()
    {
        if (_alertDuration <= 0f)
        {
            return;
        }

        // 크게 나타났다가 서서히 사라진다.
        _alertTime += Time.deltaTime;
        float t = _alertTime / _alertDuration;
        Color color = _alert.color;
        color.a = t < 0.7f ? 1f : Mathf.Clamp01((1f - t) / 0.3f);
        _alert.color = color;
        _alert.transform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Mathf.Clamp01(_alertTime * 4f));
        if (t >= 1f)
        {
            _alert.text = string.Empty;
            _alertDuration = 0f;
        }
    }
}
