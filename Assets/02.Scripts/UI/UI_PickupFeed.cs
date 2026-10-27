using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 보급품을 주웠을 때 화면 아래쪽 가운데에 "+60 AMMO", "+50 HP" 같은 알림을 띄운다.
// 체력을 회복하면 화면 가장자리가 잠깐 초록빛으로 물든다.
// 처음 Show()를 부를 때 자동으로 만들어진다.
[DisallowMultipleComponent]
public class UI_PickupFeed : MonoBehaviour
{
    public static readonly Color AmmoColor = new Color(1f, 0.85f, 0.35f);
    public static readonly Color HealthColor = new Color(0.45f, 1f, 0.55f);
    public static readonly Color BombColor = new Color(1f, 0.62f, 0.3f);
    public static readonly Color MutedColor = new Color(0.75f, 0.8f, 0.85f);

    private const int MaxLines = 4;
    private const float LineHeight = 30f;
    private const float HoldTime = 1.6f;
    private const float FadeTime = 0.45f;

    private static UI_PickupFeed _instance;

    private class Line
    {
        public TextMeshProUGUI Text;
        public float Age;
        public Color Color;
        public float Y;
    }

    private readonly List<Line> _active = new List<Line>();
    private readonly Stack<Line> _pool = new Stack<Line>();
    private RectTransform _root;
    private TMP_FontAsset _font;
    private Image _tint;
    private float _tintTimer;
    private Color _tintColor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    public static void Show(string message, Color color, bool healTint = false)
    {
        if (_instance == null)
        {
            _instance = FindAnyObjectByType<UI_PickupFeed>();
            if (_instance == null)
            {
                _instance = new GameObject("UI_PickupFeed").AddComponent<UI_PickupFeed>();
            }
        }

        _instance.Add(message, color);
        if (healTint)
        {
            _instance.Tint(color);
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        Build();
    }

    private void Add(string message, Color color)
    {
        Line line = _pool.Count > 0 ? _pool.Pop() : CreateLine();
        line.Text.text = message;
        line.Color = color;
        line.Age = 0f;
        line.Y = -LineHeight; // 아래에서 올라오며 나타난다.
        line.Text.gameObject.SetActive(true);
        _active.Insert(0, line);

        // 너무 많으면 가장 오래된 것부터 지운다.
        while (_active.Count > MaxLines)
        {
            Recycle(_active[_active.Count - 1]);
        }
    }

    private void Tint(Color color)
    {
        _tintColor = color;
        _tintTimer = 0.5f;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Line line = _active[i];
            line.Age += dt;
            if (line.Age >= HoldTime + FadeTime)
            {
                Recycle(line);
                continue;
            }

            float targetY = i * LineHeight;
            line.Y = Mathf.Lerp(line.Y, targetY, 1f - Mathf.Exp(-14f * dt));
            float alpha = Mathf.Clamp01(line.Age / 0.12f) * (1f - Mathf.Clamp01((line.Age - HoldTime) / FadeTime));
            float pop = 1f + 0.25f * Mathf.Clamp01(1f - line.Age / 0.15f);

            RectTransform rect = line.Text.rectTransform;
            rect.anchoredPosition = new Vector2(0f, line.Y);
            rect.localScale = Vector3.one * pop;
            Color color = line.Color;
            color.a = alpha;
            line.Text.color = color;
        }

        if (_tint != null)
        {
            if (_tintTimer > 0f)
            {
                _tintTimer -= dt;
                Color color = _tintColor;
                color.a = 0.16f * Mathf.Clamp01(_tintTimer / 0.5f);
                _tint.color = color;
                if (_tint.enabled == false) _tint.enabled = true;
            }
            else if (_tint.enabled)
            {
                _tint.enabled = false;
            }
        }
    }

    private void Recycle(Line line)
    {
        _active.Remove(line);
        line.Text.gameObject.SetActive(false);
        _pool.Push(line);
    }

    private void Build()
    {
        GameObject canvasObject = new GameObject("PickupFeed_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 48;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;

        // 회복할 때 화면 가장자리를 물들이는 비네트
        GameObject tintObject = new GameObject("HealTint", typeof(RectTransform));
        RectTransform tintRect = (RectTransform)tintObject.transform;
        tintRect.SetParent(canvasObject.transform, false);
        tintRect.anchorMin = Vector2.zero;
        tintRect.anchorMax = Vector2.one;
        tintRect.offsetMin = Vector2.zero;
        tintRect.offsetMax = Vector2.zero;
        _tint = tintObject.AddComponent<Image>();
        _tint.sprite = CreateVignetteSprite();
        _tint.raycastTarget = false;
        _tint.enabled = false;

        GameObject rootObject = new GameObject("Lines", typeof(RectTransform));
        _root = (RectTransform)rootObject.transform;
        _root.SetParent(canvasObject.transform, false);
        _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0f);
        _root.pivot = new Vector2(0.5f, 0f);
        _root.anchoredPosition = new Vector2(0f, 150f);
        _root.sizeDelta = new Vector2(400f, LineHeight * MaxLines);

        _font = FindFont();
    }

    private Line CreateLine()
    {
        GameObject go = new GameObject("Line", typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(_root, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(400f, LineHeight);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) text.font = _font;
        text.fontSize = 22f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.characterSpacing = 4f;
        // 밝은 곳에서도 읽히도록 검은 외곽선
        text.outlineWidth = 0.18f;
        text.outlineColor = new Color32(0, 0, 0, 190);
        return new Line { Text = text };
    }

    private static TMP_FontAsset FindFont()
    {
        TextMeshProUGUI sample = FindAnyObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
        if (sample != null && sample.font != null)
        {
            return sample.font;
        }

        return TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null;
    }

    // 가운데는 투명하고 가장자리로 갈수록 진해지는 텍스처
    private static Sprite CreateVignetteSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c;
                float dy = (y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                float a = Mathf.Clamp01((r - 0.35f) / 0.65f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * a * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
