using UnityEngine;
using UnityEngine.UI;

// 움직이는 조준점 (L4D / Back 4 Blood 방식)
//  - 네 개의 짧은 선 + 가운데 점. 총알이 퍼지는 만큼(GunFeedback.CurrentSpread) 선이 벌어진다.
//    쏠수록, 움직일수록, 점프 중일수록 벌어지고, 멈추면 다시 모인다.
//  - 명중하면 큰 X 표시와 조준점 팝 애니메이션이 재생된다.
//    흰색: 맞힘, 노랑: 헤드샷, 빨강: 처치. 탄 퍼짐 계산은 그대로 사용한다.
// GunFeedback이 시작할 때 코드로 만든다. 씬에 있던 고정 조준점 이미지는 숨긴다.
public class UI_Crosshair : MonoBehaviour
{
    public static UI_Crosshair Instance { get; private set; }

    [SerializeField] private float _lineLength = 14f;
    [SerializeField] private float _lineThickness = 3f;
    [SerializeField] private float _minGap = 6f;
    [SerializeField] private Color _color = new Color(1f, 1f, 1f, 0.9f);

    [Header("명중 표시")]
    [SerializeField, Min(1f)] private float _hitMarkLength = 24f;
    [SerializeField, Min(1f)] private float _hitMarkThickness = 5f;
    [SerializeField, Min(1f)] private float _hitMarkDistance = 18f;

    [Header("명중 시 조준점 확대/복귀")]
    [SerializeField, Min(0.04f)] private float _hitPulseDuration = 0.24f;
    [SerializeField, Range(0f, 1f)] private float _hitPulseScale = 0.5f;
    [SerializeField, Min(0f)] private float _hitPulseGap = 10f;

    private GunFeedback _gun;
    private Camera _camera;
    private RectTransform[] _lines;
    private Image[] _lineImages;
    private Image _dot;
    private RectTransform[] _marks;
    private Image[] _markImages;
    private float _gap;
    private float _markTimer;
    private float _markDuration;
    private float _markSize;
    private Color _markColor;
    private float _pulseTimer;
    private float _pulseStrength;
    private int _lastHitFrame = -1;
    private int _markPriority = -1;

    public static UI_Crosshair Create(GunFeedback gun)
    {
        GameObject canvasObject = new GameObject("UI_Crosshair", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 18;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;  // 높이 기준 → 각도를 픽셀로 바꾸기 쉽다.

        UI_Crosshair crosshair = canvasObject.AddComponent<UI_Crosshair>();
        crosshair._gun = gun;
        crosshair.Build();
        HideSceneCrosshair(canvasObject);
        Instance = crosshair;
        return crosshair;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // 4장에서 만든 가운데 고정 조준점(Crosshair 이미지)을 끈다.
    private static void HideSceneCrosshair(GameObject mine)
    {
        foreach (Image image in FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (image.transform.IsChildOf(mine.transform))
            {
                continue;
            }

            string objectName = image.gameObject.name.ToLowerInvariant();
            string spriteName = image.sprite != null ? image.sprite.name.ToLowerInvariant() : string.Empty;
            if (objectName.Contains("crosshair") || spriteName.Contains("crosshair"))
            {
                image.enabled = false;
            }
        }
    }

    private void Build()
    {
        _camera = Camera.main;
        _gap = _minGap;
        RectTransform root = (RectTransform)transform;

        // 위, 아래, 왼쪽, 오른쪽 선
        _lines = new RectTransform[4];
        _lineImages = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            bool vertical = i < 2;
            _lineImages[i] = Bar(root, "Line" + i, vertical ? new Vector2(_lineThickness, _lineLength) : new Vector2(_lineLength, _lineThickness), _color);
            _lines[i] = _lineImages[i].rectTransform;
        }

        _dot = Bar(root, "Dot", new Vector2(3f, 3f), _color);

        // 히트 마커: 대각선 네 개
        _marks = new RectTransform[4];
        _markImages = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            _markImages[i] = Bar(root, "HitMark" + i, new Vector2(_hitMarkThickness, _hitMarkLength), Color.white);
            _marks[i] = _markImages[i].rectTransform;
            _marks[i].localRotation = Quaternion.Euler(0f, 0f, 45f + 90f * i);
            _markImages[i].enabled = false;
        }
    }

    // 기존 AR/권총/SMG/산탄총/도끼의 명중 연결을 그대로 사용한다.
    public void ShowHit(bool kill, bool headshot)
    {
        int priority = (kill ? 2 : 0) + (headshot ? 1 : 0);
        bool sameFrame = _lastHitFrame == Time.frameCount;
        // 한 번의 산탄 발사로 여러 대상을 맞혀도 처치/헤드샷 표시가 약한 표시로 덮이지 않는다.
        if (!sameFrame || priority >= _markPriority)
        {
            _markColor = kill ? new Color(1f, 0.15f, 0.1f) : (headshot ? new Color(1f, 0.85f, 0.3f) : Color.white);
            _markSize = headshot ? (kill ? 1.6f : 1.45f) : (kill ? 1.3f : 1f);
            _markDuration = kill ? 0.4f : 0.28f;
            _markPriority = priority;
        }
        _markTimer = _markDuration;
        float strength = 1f + (kill ? 0.2f : 0f) + (headshot ? 0.2f : 0f);
        _pulseStrength = sameFrame ? Mathf.Max(_pulseStrength, strength) : strength;
        _pulseTimer = _hitPulseDuration;
        _lastHitFrame = Time.frameCount;
    }

    private static float PulseEnvelope(float remaining, float duration)
    {
        float t = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
        // Instant expansion, followed by a smooth, bounded return to the normal spread display.
        return t * t * (3f - 2f * t);
    }

    private void LateUpdate()
    {
        if (_lines == null || _marks == null) return;
        if (_camera == null) _camera = Camera.main;
        float dt = Time.deltaTime;
        // 1. 탄 퍼짐 각도를 화면 픽셀로: 화면 높이 1080 기준, 세로 시야각의 절반이 540픽셀
        float spread = _gun != null ? _gun.CurrentSpread : 0f;
        float fov = _camera != null ? _camera.fieldOfView : 60f;
        float pixels = Mathf.Tan(spread * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 540f;
        float target = _minGap + pixels;
        _gap = Mathf.Lerp(_gap, target, 1f - Mathf.Exp(-25f * dt));

        float pulse = PulseEnvelope(_pulseTimer, _hitPulseDuration) * _pulseStrength;
        _pulseTimer = Mathf.Max(0f, _pulseTimer - dt);
        float length = _lineLength * (1f + _hitPulseScale * pulse);
        float thickness = _lineThickness * (1f + 0.16f * pulse);
        float visibleGap = _gap + _hitPulseGap * pulse;
        float half = length * 0.5f;
        _lines[0].anchoredPosition = new Vector2(0f, visibleGap + half);
        _lines[1].anchoredPosition = new Vector2(0f, -visibleGap - half);
        _lines[2].anchoredPosition = new Vector2(-visibleGap - half, 0f);
        _lines[3].anchoredPosition = new Vector2(visibleGap + half, 0f);
        for (int i = 0; i < _lines.Length; i++)
            _lines[i].sizeDelta = i < 2 ? new Vector2(thickness, length) : new Vector2(length, thickness);
        _dot.rectTransform.sizeDelta = Vector2.one * (3f + 1.2f * pulse);

        // 많이 벌어지면 살짝 흐려진다.
        float alpha = Mathf.Lerp(0.95f, 0.55f, Mathf.Clamp01(pixels / 90f));
        alpha = Mathf.Lerp(alpha, 1f, Mathf.Clamp01(pulse * 0.6f));
        foreach (Image line in _lineImages)
        {
            Color c = _color;
            c.a = alpha;
            line.color = c;
        }

        // 2. 히트 마커
        bool showMark = _markTimer > 0f;
        if (showMark)
        {
            _markTimer = Mathf.Max(0f, _markTimer - dt);
        }

        float t = _markDuration > 0f ? Mathf.Clamp01(_markTimer / _markDuration) : 0f;
        float pop = 1f + 0.32f * t * t;
        float markScale = _markSize * pop;
        float distance = (_hitMarkDistance + (1f - t) * 3f) * markScale;
        for (int i = 0; i < 4; i++)
        {
            _markImages[i].enabled = showMark;
            if (!showMark)
            {
                continue;
            }

            float angle = (45f + 90f * i) * Mathf.Deg2Rad;
            _marks[i].anchoredPosition = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * distance;
            _marks[i].sizeDelta = new Vector2(_hitMarkThickness * (1f + 0.12f * t), (_hitMarkLength * markScale));
            Color c = _markColor;
            c.a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.6f));
            _markImages[i].color = c;
        }
    }

    private static Image Bar(RectTransform parent, string name, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        Outline outline = go.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(1f, -1f);
        return image;
    }
}
