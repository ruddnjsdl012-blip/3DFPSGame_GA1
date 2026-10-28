using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 보급품 화면 표시
//  - 오른쪽 아래(총알 UI 위): 구급상자 / 수류탄 개수
//  - 화면 가운데 아래: 줍기 안내 "[E] 구급상자 줍기", 주웠을 때 잠깐 뜨는 글자
//  - 치료 중: 가운데에 진행 막대
// PlayerInventory가 시작할 때 코드로 만든다.
public class UI_SupplyHud : MonoBehaviour
{
    private PlayerInventory _inventory;
    private TextMeshProUGUI _medkit;
    private TextMeshProUGUI _grenade;
    private TextMeshProUGUI _prompt;
    private TextMeshProUGUI _toast;
    private GameObject _healBar;
    private Image _healFill;
    private float _toastTimer;

    public static UI_SupplyHud Create(PlayerInventory inventory)
    {
        GameObject canvasObject = new GameObject("UI_SupplyHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 16;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        UI_SupplyHud hud = canvasObject.AddComponent<UI_SupplyHud>();
        hud.Build(inventory);
        return hud;
    }

    private void Build(PlayerInventory inventory)
    {
        _inventory = inventory;
        TMP_FontAsset font = FindKoreanFont();
        RectTransform root = (RectTransform)transform;

        // 오른쪽 아래, 총알 판(높이 120, 아래 40) 위에 보급품 칸 두 개
        RectTransform slots = Panel(root, "Slots", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 214f), new Vector2(330f, 54f), new Color(0f, 0f, 0f, 0.35f));
        _medkit = Text(slots, "Medkit", font, 28f, new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(160f, 50f), TextAlignmentOptions.Left);
        _grenade = Text(slots, "Grenade", font, 28f, new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(160f, 50f), TextAlignmentOptions.Right);

        // 화면 가운데 아래 안내
        _prompt = Text(root, "Prompt", font, 34f, new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(900f, 60f), TextAlignmentOptions.Center);
        _toast = Text(root, "Toast", font, 38f, new Vector2(0.5f, 0.5f), new Vector2(0f, -205f), new Vector2(900f, 60f), TextAlignmentOptions.Center);

        // 치료 막대
        RectTransform bar = Panel(root, "HealBar", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), new Vector2(420f, 22f), new Color(0f, 0f, 0f, 0.55f));
        bar.pivot = new Vector2(0.5f, 0.5f);
        RectTransform fill = Panel(bar, "Fill", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 0f), new Color(0.35f, 0.95f, 0.45f, 0.9f));
        fill.pivot = new Vector2(0f, 0.5f);
        fill.anchorMax = new Vector2(1f, 1f);
        fill.offsetMin = new Vector2(3f, 3f);
        fill.offsetMax = new Vector2(-3f, -3f);
        _healFill = fill.GetComponent<Image>();
        _healFill.type = Image.Type.Filled;
        _healFill.fillMethod = Image.FillMethod.Horizontal;
        _healFill.sprite = WhiteSprite();
        TextMeshProUGUI label = Text(bar, "Label", font, 26f, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(420f, 36f), TextAlignmentOptions.Center);
        label.text = "치료 중...  (Q 취소)";
        _healBar = bar.gameObject;
        _healBar.SetActive(false);

        _inventory.OnChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_inventory != null)
        {
            _inventory.OnChanged -= Refresh;
        }
    }

    public void SetPrompt(string message)
    {
        if (_prompt.text != message)
        {
            _prompt.text = message;
        }
    }

    // 잠깐 떴다가 사라지는 글자 (주웠을 때 등)
    public void ShowToast(string message, Color color)
    {
        _toast.text = message;
        _toast.color = color;
        _toastTimer = 1.6f;
    }

    private void Refresh()
    {
        int medkits = _inventory.Medkits;
        int grenades = _inventory.Grenades;
        _medkit.text = $"<color=#FF5A50>+</color> 구급 {medkits}";
        _medkit.color = medkits > 0 ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        _grenade.text = $"수류탄 {grenades}/{_inventory.MaxGrenades}";
        _grenade.color = grenades > 0 ? new Color(0.75f, 1f, 0.6f) : new Color(1f, 1f, 1f, 0.4f);
        _healBar.SetActive(_inventory.IsHealing);
    }

    private void Update()
    {
        if (_inventory.IsHealing)
        {
            if (!_healBar.activeSelf)
            {
                _healBar.SetActive(true);
            }

            _healFill.fillAmount = _inventory.HealProgress;
        }

        if (_toastTimer > 0f)
        {
            _toastTimer -= Time.deltaTime;
            Color c = _toast.color;
            c.a = Mathf.Clamp01(_toastTimer / 0.4f);
            _toast.color = c;
            if (_toastTimer <= 0f)
            {
                _toast.text = string.Empty;
            }
        }
    }

    // ===================== 만들기 도우미 =====================

    private static RectTransform Panel(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(anchorMin.x, anchorMin.y);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static TextMeshProUGUI Text(RectTransform parent, string name, TMP_FontAsset font, float size, Vector2 anchor, Vector2 position, Vector2 box, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = box;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            text.font = font;
        }

        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.richText = true;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 200);
        text.text = string.Empty;
        return text;
    }

    private static Sprite _white;

    private static Sprite WhiteSprite()
    {
        if (_white == null)
        {
            Texture2D texture = Texture2D.whiteTexture;
            _white = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }

        return _white;
    }

    // 씬에 있는 한글 폰트(11장 StateText의 폰트)를 빌려 쓴다.
    private static TMP_FontAsset FindKoreanFont()
    {
        foreach (TextMeshProUGUI text in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text.font != null && text.font.HasCharacter('탄', true, true))
            {
                return text.font;
            }
        }

        return TMP_Settings.defaultFontAsset;
    }
}
