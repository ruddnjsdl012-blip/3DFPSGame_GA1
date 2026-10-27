using UnityEngine;
using UnityEngine.UI;

// 총알이 퍼지는 만큼 벌어지는 동적 크로스헤어.
//  - 연사할수록 / 걷거나 뛸수록 / 공중에 있을수록 벌어지고, 멈추면 다시 모인다.
//  - 실제 탄 퍼짐 각도(PlayerGunFire.CurrentSpread)를 화면 픽셀로 바꿔서 그리므로
//    총알은 항상 크로스헤어 안쪽으로 날아간다.
//  - 조준점 아래에 살아 있는 좀비가 있으면 빨갛게 바뀐다.
//  - 재장전 중에는 흐려진다.
// PlayerGunFire가 시작할 때 자동으로 만든다.
[DisallowMultipleComponent]
public class UI_Crosshair : MonoBehaviour
{
    [Header("모양 (픽셀)")]
    [SerializeField, Min(0f)] private float _baseGap = 5f;
    [SerializeField, Min(1f)] private float _length = 9f;
    [SerializeField, Min(1f)] private float _thickness = 2f;
    [SerializeField, Min(0f)] private float _dotSize = 2f;
    [SerializeField, Min(10f)] private float _maxGap = 120f;

    [Header("색")]
    [SerializeField] private Color _color = new Color(1f, 1f, 1f, 0.92f);
    [SerializeField] private Color _enemyColor = new Color(1f, 0.25f, 0.2f, 0.95f);
    [SerializeField] private Color _outlineColor = new Color(0f, 0f, 0f, 0.6f);
    [SerializeField, Range(0f, 1f)] private float _reloadAlpha = 0.3f;

    [Header("움직임")]
    [Tooltip("벌어지는 속도")]
    [SerializeField, Min(1f)] private float _expandSpeed = 28f;
    [Tooltip("다시 모이는 속도")]
    [SerializeField, Min(1f)] private float _shrinkSpeed = 10f;
    [Tooltip("한 발 쏠 때마다 순간적으로 튀는 양 (픽셀)")]
    [SerializeField, Min(0f)] private float _shotKick = 4f;
    [SerializeField, Min(1f)] private float _enemyCheckRange = 80f;

    [Tooltip("씬에 원래 있던 고정 크로스헤어(이름에 crosshair가 들어간 UI)를 숨긴다.")]
    [SerializeField] private bool _hideSceneCrosshair = true;

    private PlayerGunFire _gun;
    private Canvas _canvas;
    private RectTransform _root;
    private RectTransform[] _bars;
    private Image[] _images;
    private float _gap;
    private float _kick;
    private float _alpha = 1f;
    private Color _currentColor;

    // 정조준(ADS) 등에서 크로스헤어를 잠시 숨길 때 쓴다. (0 = 보임, 1 = 완전히 숨김)
    public float HideAmount { get; set; }

    // 지금 크로스헤어 선 사이의 간격 (픽셀)
    public float CurrentGap => _gap;

    public static UI_Crosshair Create(PlayerGunFire gun)
    {
        UI_Crosshair crosshair = new GameObject("UI_Crosshair").AddComponent<UI_Crosshair>();
        crosshair.Bind(gun);
        return crosshair;
    }

    private void Awake()
    {
        Build();
        _currentColor = _color;
        _gap = _baseGap;
    }

    private void Start()
    {
        if (_hideSceneCrosshair)
        {
            HideSceneCrosshairs();
        }
    }

    public void Bind(PlayerGunFire gun)
    {
        if (_gun != null)
        {
            _gun.Fired -= HandleFired;
        }

        _gun = gun;
        if (_gun != null)
        {
            _gun.Fired += HandleFired;
        }
    }

    private void HandleFired()
    {
        _kick = Mathf.Min(_kick + _shotKick, _shotKick * 2.5f);
    }

    private void Update()
    {
        bool visible = GameManager.IsPlaying && Cursor.visible == false && _gun != null && _gun.isActiveAndEnabled;
        float targetAlpha = visible ? 1f : 0f;
        if (visible && _gun.IsReloading)
        {
            targetAlpha = _reloadAlpha;
        }

        targetAlpha *= 1f - Mathf.Clamp01(HideAmount);

        float dt = Time.unscaledDeltaTime;
        _alpha = Mathf.MoveTowards(_alpha, targetAlpha, dt * 8f);
        if (_alpha <= 0.001f)
        {
            if (_root.gameObject.activeSelf) _root.gameObject.SetActive(false);
            return;
        }

        if (_root.gameObject.activeSelf == false) _root.gameObject.SetActive(true);

        // 퍼짐 각도 → 화면 픽셀 (카메라 시야각 기준)
        float target = _baseGap + SpreadToPixels(_gun.CurrentSpread);
        target = Mathf.Min(target, _maxGap);
        float speed = target > _gap ? _expandSpeed : _shrinkSpeed;
        _gap = Mathf.Lerp(_gap, target, 1f - Mathf.Exp(-speed * dt));
        _kick = Mathf.MoveTowards(_kick, 0f, dt * 45f);

        float gap = _gap + _kick;
        float half = _length * 0.5f;
        _bars[0].anchoredPosition = new Vector2(0f, gap + half);   // 위
        _bars[1].anchoredPosition = new Vector2(0f, -gap - half);  // 아래
        _bars[2].anchoredPosition = new Vector2(-gap - half, 0f);  // 왼쪽
        _bars[3].anchoredPosition = new Vector2(gap + half, 0f);   // 오른쪽

        Color targetColor = IsAimingAtZombie() ? _enemyColor : _color;
        _currentColor = Color.Lerp(_currentColor, targetColor, 1f - Mathf.Exp(-20f * dt));
        Color color = _currentColor;
        color.a *= _alpha;
        for (int i = 0; i < _images.Length; i++)
        {
            _images[i].color = color;
        }
    }

    // 총알 퍼짐 각도(도)를 화면 중심에서의 거리(픽셀)로 바꾼다.
    private float SpreadToPixels(float spreadDegrees)
    {
        Camera view = Camera.main;
        float fov = view != null ? view.fieldOfView : 60f;
        float halfFov = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float spread = Mathf.Tan(Mathf.Clamp(spreadDegrees, 0f, 45f) * Mathf.Deg2Rad);
        float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
        return spread / halfFov * (Screen.height * 0.5f) / scale;
    }

    private bool IsAimingAtZombie()
    {
        Camera view = Camera.main;
        if (view == null)
        {
            return false;
        }

        Transform cam = view.transform;
        if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, _enemyCheckRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) == false)
        {
            return false;
        }

        Zombie zombie = hit.collider.GetComponentInParent<Zombie>();
        return zombie != null && zombie.Health > 0f;
    }

    private void HideSceneCrosshairs()
    {
        Graphic[] graphics = FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Graphic graphic in graphics)
        {
            if (graphic.transform.IsChildOf(transform))
            {
                continue;
            }

            if (graphic.gameObject.name.ToLowerInvariant().Contains("crosshair"))
            {
                graphic.enabled = false;
                Debug.Log($"[UI_Crosshair] 기존 크로스헤어 '{graphic.gameObject.name}'를 숨기고 동적 크로스헤어를 사용합니다.", graphic);
            }
        }
    }

    private void Build()
    {
        GameObject canvasObject = new GameObject("Crosshair_Canvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        _canvas = canvasObject.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 51;

        GameObject rootObject = new GameObject("Crosshair", typeof(RectTransform));
        _root = (RectTransform)rootObject.transform;
        _root.SetParent(canvasObject.transform, false);
        _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.sizeDelta = Vector2.zero;

        _bars = new RectTransform[4];
        _images = new Image[5];
        for (int i = 0; i < 4; i++)
        {
            bool vertical = i < 2;
            Vector2 size = vertical ? new Vector2(_thickness, _length) : new Vector2(_length, _thickness);
            _images[i] = CreatePiece("Line", size, out _bars[i]);
        }

        _images[4] = CreatePiece("Dot", new Vector2(_dotSize, _dotSize), out RectTransform dot);
        dot.anchoredPosition = Vector2.zero;
        if (_dotSize <= 0f)
        {
            dot.gameObject.SetActive(false);
        }
    }

    private Image CreatePiece(string name, Vector2 size, out RectTransform rect)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        rect = (RectTransform)go.transform;
        rect.SetParent(_root, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.color = _color;

        // 밝은 배경에서도 보이도록 검은 테두리
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = _outlineColor;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
        return image;
    }

    private void OnDestroy()
    {
        if (_gun != null)
        {
            _gun.Fired -= HandleFired;
        }
    }
}
