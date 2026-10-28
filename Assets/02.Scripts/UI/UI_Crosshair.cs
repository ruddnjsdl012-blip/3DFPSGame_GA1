using UnityEngine;
using UnityEngine.UI;

// 움직이는 조준점 (L4D / Back 4 Blood 방식)
//  - 네 개의 짧은 선 + 가운데 점. 총알이 퍼지는 만큼(GunFeedback.CurrentSpread) 선이 벌어진다.
//    쏠수록, 움직일수록, 점프 중일수록 벌어지고, 멈추면 다시 모인다.
//  - 좀비를 맞히면 X 모양 히트 마커가 번쩍인다. (흰색: 맞힘, 빨강: 처치, 크게: 헤드샷)
// GunFeedback이 시작할 때 코드로 만든다. 씬에 있던 고정 조준점 이미지는 숨긴다.
public class UI_Crosshair : MonoBehaviour
{
    public static UI_Crosshair Instance { get; private set; }

    [SerializeField] private float _lineLength = 14f;
    [SerializeField] private float _lineThickness = 3f;
    [SerializeField] private float _minGap = 6f;
    [SerializeField] private Color _color = new Color(1f, 1f, 1f, 0.9f);

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
            _markImages[i] = Bar(root, "HitMark" + i, new Vector2(3f, 12f), Color.white);
            _marks[i] = _markImages[i].rectTransform;
            _marks[i].localRotation = Quaternion.Euler(0f, 0f, 45f + 90f * i);
            _markImages[i].enabled = false;
        }
    }

    // 좀비를 맞혔을 때 (PlayerGunFire가 부른다)
    public void ShowHit(bool kill, bool headshot)
    {
        _markColor = kill ? new Color(1f, 0.15f, 0.1f) : (headshot ? new Color(1f, 0.85f, 0.3f) : Color.white);
        _markSize = headshot ? 1.6f : (kill ? 1.35f : 1f);
        _markDuration = kill ? 0.35f : 0.2f;
        _markTimer = _markDuration;
    }

    private void LateUpdate()
    {
        // 1. 탄 퍼짐 각도를 화면 픽셀로: 화면 높이 1080 기준, 세로 시야각의 절반이 540픽셀
        float spread = _gun != null ? _gun.CurrentSpread : 0f;
        float fov = _camera != null ? _camera.fieldOfView : 60f;
        float pixels = Mathf.Tan(spread * Mathf.Deg2Rad) / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 540f;
        float target = _minGap + pixels;
        _gap = Mathf.Lerp(_gap, target, 1f - Mathf.Exp(-25f * Time.deltaTime));

        float half = _lineLength * 0.5f;
        _lines[0].anchoredPosition = new Vector2(0f, _gap + half);
        _lines[1].anchoredPosition = new Vector2(0f, -_gap - half);
        _lines[2].anchoredPosition = new Vector2(-_gap - half, 0f);
        _lines[3].anchoredPosition = new Vector2(_gap + half, 0f);

        // 많이 벌어지면 살짝 흐려진다.
        float alpha = Mathf.Lerp(0.95f, 0.55f, Mathf.Clamp01(pixels / 90f));
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
            _markTimer -= Time.deltaTime;
        }

        float t = _markDuration > 0f ? Mathf.Clamp01(_markTimer / _markDuration) : 0f;
        float distance = (8f + (1f - t) * 6f) * _markSize;
        for (int i = 0; i < 4; i++)
        {
            _markImages[i].enabled = showMark;
            if (!showMark)
            {
                continue;
            }

            float angle = (45f + 90f * i) * Mathf.Deg2Rad;
            _marks[i].anchoredPosition = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * distance;
            _marks[i].sizeDelta = new Vector2(3f, 12f * _markSize);
            Color c = _markColor;
            c.a = t;
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
