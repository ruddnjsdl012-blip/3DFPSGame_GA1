using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 화면 우측 하단의 탄약 표시(탄창 / 예비 탄약)와 화면 중앙의 재장전 UI.
// 씬에 따로 배치하지 않아도 PlayerGunFire가 플레이 시작 시 자동으로 만든다.
// (빈 GameObject에 직접 붙여 두면 그 오브젝트를 사용한다.)
[DisallowMultipleComponent]
public class UI_AmmoHud : MonoBehaviour
{
    [Header("배치")]
    [SerializeField] private Vector2 _panelMargin = new Vector2(20f, 20f);
    [SerializeField] private Vector2 _panelSize = new Vector2(272f, 104f);
    [Tooltip("화면 중앙(조준점) 기준 재장전 표시의 높이")]
    [SerializeField] private float _centerOffsetY = -80f;
    [SerializeField] private int _sortingOrder = 50;

    [Header("탄약 부족 기준")]
    [Tooltip("최대 탄약 대비 이 비율 이하이면 '탄약 부족'으로 표시한다.")]
    [SerializeField, Range(0f, 1f)] private float _lowAmmoRatio = 0.3f;

    [Header("색상")]
    [SerializeField] private Color _panelColor = new Color(0.035f, 0.05f, 0.07f, 0.85f);
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _lowColor = new Color(1f, 0.62f, 0.15f);
    [SerializeField] private Color _emptyColor = new Color(1f, 0.25f, 0.22f);
    [SerializeField] private Color _reloadColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] private Color _subTextColor = new Color(0.75f, 0.8f, 0.85f);

    private PlayerGunFire _gun;
    private int _current;
    private int _max = 1;
    private int _reserve;

    // 연출 값 (UI 연출은 일시 정지 중에도 자연스럽게 끝나도록 unscaled 시간을 쓴다.)
    private float _punch;      // 발사/재장전 완료 시 숫자가 살짝 커졌다 돌아온다.
    private float _emptyFlash; // 빈 탄창 발사 시 빨갛게 깜빡이며 흔들린다.

    // 만든 UI 참조
    private bool _built;
    private GameObject _canvasObject;
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _hintText;
    private TextMeshProUGUI _ammoText;
    private RectTransform _ammoRect;
    private Vector2 _ammoBasePosition;
    private RectTransform _barFill;
    private Image _barFillImage;
    private GameObject _reloadIndicator;
    private Image _ringFill;
    private TextMeshProUGUI _ringTimeText;
    private TextMeshProUGUI _promptText;
    private Texture2D _ringTexture;
    private Sprite _ringSprite;

    private bool _lastReloading;
    private bool _hasLastReloading;

    private bool IsEmpty => _current <= 0;
    private bool IsLow => _current > 0 && _current <= Mathf.CeilToInt(_max * _lowAmmoRatio);

    public static UI_AmmoHud Create()
    {
        GameObject root = new GameObject("UI_AmmoHud");
        return root.AddComponent<UI_AmmoHud>();
    }

    private void Awake()
    {
        BuildIfNeeded();
    }

    private void Start()
    {
        // 직접 배치했는데 아직 연결되지 않았다면 총을 찾아 연결한다.
        if (_gun == null)
        {
            PlayerGunFire gun = FindAnyObjectByType<PlayerGunFire>();
            if (gun != null)
            {
                Bind(gun);
            }
        }
    }

    public void Bind(PlayerGunFire gun)
    {
        if (_gun == gun)
        {
            return;
        }

        Unsubscribe();
        _gun = gun;

        if (_gun == null)
        {
            return;
        }

        _gun.AmmoChanged += HandleAmmoChanged;
        _gun.ReloadStarted += HandleReloadStarted;
        _gun.ReloadFinished += HandleReloadFinished;
        _gun.ReloadCanceled += HandleReloadCanceled;
        _gun.EmptyFired += HandleEmptyFired;
        _gun.ReserveChanged += HandleReserveChanged;

        BuildIfNeeded();
        _current = _gun.BulletCount;
        _max = Mathf.Max(1, _gun.MaxBulletCount);
        _reserve = _gun.ReserveAmmo;
        _hasLastReloading = false;
        RefreshAmmoText();
    }

    private void Unsubscribe()
    {
        if (_gun == null)
        {
            return;
        }

        _gun.AmmoChanged -= HandleAmmoChanged;
        _gun.ReloadStarted -= HandleReloadStarted;
        _gun.ReloadFinished -= HandleReloadFinished;
        _gun.ReloadCanceled -= HandleReloadCanceled;
        _gun.EmptyFired -= HandleEmptyFired;
        _gun.ReserveChanged -= HandleReserveChanged;
        _gun = null;
    }

    // ----- 총에서 오는 알림 (값만 기록하고, 화면 갱신은 Update에서 한다) -----

    private void HandleAmmoChanged(int current, int max)
    {
        if (current < _current)
        {
            _punch = 1f;
        }

        _current = current;
        _max = Mathf.Max(1, max);
        RefreshAmmoText();
    }

    private void HandleReserveChanged(int reserve)
    {
        if (reserve > _reserve)
        {
            _punch = 1f; // 탄약을 주웠을 때
        }

        _reserve = reserve;
        RefreshAmmoText();
    }

    private void HandleReloadStarted()
    {
        _emptyFlash = 0f;
    }

    private void HandleReloadFinished()
    {
        _punch = 1f;
    }

    private void HandleReloadCanceled()
    {
        // 원형 표시와 바는 Update에서 IsReloading을 보고 바로 원래대로 돌아간다.
        _punch = 0f;
    }

    private void HandleEmptyFired()
    {
        _emptyFlash = 1f;
    }

    // ----- 화면 갱신 -----

    private void Update()
    {
        if (_built == false)
        {
            return;
        }

        bool hasGun = _gun != null && _gun.isActiveAndEnabled;
        if (_canvasObject.activeSelf != hasGun)
        {
            _canvasObject.SetActive(hasGun);
        }

        if (hasGun == false)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        bool playing = GameManager.IsPlaying;
        bool reloading = _gun.IsReloading;

        // 숫자 펀치 / 빈 탄창 흔들림
        _punch = Mathf.MoveTowards(_punch, 0f, dt * 6f);
        _emptyFlash = Mathf.MoveTowards(_emptyFlash, 0f, dt * 2.5f);

        _ammoRect.localScale = Vector3.one * (1f + 0.12f * _punch);
        float shake = _emptyFlash > 0f ? Mathf.Sin(Time.unscaledTime * 70f) * 5f * _emptyFlash : 0f;
        _ammoRect.anchoredPosition = _ammoBasePosition + new Vector2(shake, 0f);
        _ammoText.color = Color.Lerp(GetAmmoColor(), _emptyColor, _emptyFlash);

        // 상단 제목: 평소에는 AMMO, 재장전 중에는 RELOADING
        if (_hasLastReloading == false || _lastReloading != reloading)
        {
            _hasLastReloading = true;
            _lastReloading = reloading;
            _titleText.text = reloading ? "RELOADING..." : "AMMO";
            _titleText.color = reloading ? _reloadColor : _subTextColor;
        }

        // 하단 바: 평소에는 남은 탄 비율, 재장전 중에는 진행도
        float progress = reloading ? Mathf.Clamp01(_gun.ReloadProgress) : 0f;
        float barValue = reloading ? progress : (float)_current / _max;
        _barFill.anchorMax = new Vector2(Mathf.Clamp01(barValue), 1f);
        _barFillImage.color = reloading ? _reloadColor : GetAmmoColor();

        // 화면 중앙 원형 재장전 표시
        bool showRing = reloading && playing;
        if (_reloadIndicator.activeSelf != showRing)
        {
            _reloadIndicator.SetActive(showRing);
        }

        if (showRing)
        {
            _ringFill.fillAmount = progress;
            float remaining = Mathf.Max(0f, _gun.ReloadingTime * (1f - progress));
            _ringTimeText.SetText("{0:1}", remaining);
        }

        // 탄약 부족 / 없음 안내 (깜빡임)
        bool showPrompt = playing && reloading == false && (IsEmpty || IsLow);
        if (_promptText.gameObject.activeSelf != showPrompt)
        {
            _promptText.gameObject.SetActive(showPrompt);
        }

        if (showPrompt)
        {
            if (_reserve <= 0)
            {
                _promptText.text = IsEmpty ? "OUT OF AMMO   FIND AMMO" : "LAST MAGAZINE";
            }
            else
            {
                _promptText.text = IsEmpty ? "NO AMMO   [R] RELOAD" : "LOW AMMO   [R] RELOAD";
            }

            Color color = IsEmpty ? _emptyColor : _lowColor;
            color.a = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
            _promptText.color = color;
        }
    }

    private Color GetAmmoColor()
    {
        if (IsEmpty) return _emptyColor;
        if (IsLow) return _lowColor;
        return _normalColor;
    }

    private void RefreshAmmoText()
    {
        if (_ammoText == null)
        {
            return;
        }

        // 탄창의 탄약은 크게, 예비 탄약은 작고 흐리게 표시한다. 예) 24 / 90
        // 예비 탄약이 없으면 빨간색으로 표시한다.
        string reserveColor = ColorUtility.ToHtmlStringRGB(_reserve > 0 ? _subTextColor : _emptyColor);
        _ammoText.text = _current + "<size=50%><color=#" + reserveColor + ">  / " + _reserve + "</color></size>";
    }

    // ----- UI 생성 -----

    private void BuildIfNeeded()
    {
        if (_built)
        {
            return;
        }

        _built = true;
        TMP_FontAsset font = FindFont();

        // 전용 캔버스 (기존 HUD와 같은 픽셀 단위 배치)
        _canvasObject = new GameObject("AmmoHUD_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        _canvasObject.transform.SetParent(transform, false);
        Canvas canvas = _canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = _sortingOrder;
        CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        Transform canvasTransform = _canvasObject.transform;

        // 1) 우측 하단 탄약 패널
        RectTransform panel = CreateRect("AmmoPanel", canvasTransform,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-_panelMargin.x, _panelMargin.y), _panelSize);
        CreateImage(panel, _panelColor);

        RectTransform title = CreateRect("Title", panel,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(16f, -9f), new Vector2(170f, 24f));
        _titleText = CreateText(title, font, 15f, FontStyles.Bold, TextAlignmentOptions.TopLeft, _subTextColor);
        _titleText.text = "AMMO";

        RectTransform hint = CreateRect("Hint", panel,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-16f, -12f), new Vector2(110f, 20f));
        _hintText = CreateText(hint, font, 11f, FontStyles.Normal, TextAlignmentOptions.TopRight, _subTextColor);
        _hintText.text = "R  /  RELOAD";

        _ammoRect = CreateRect("AmmoCount", panel,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-16f, 20f), new Vector2(_panelSize.x - 32f, 58f));
        _ammoBasePosition = _ammoRect.anchoredPosition;
        _ammoText = CreateText(_ammoRect, font, 46f, FontStyles.Bold, TextAlignmentOptions.BottomRight, _normalColor);
        _ammoText.richText = true;

        RectTransform barBack = CreateRect("MagazineBar", panel,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 12f), new Vector2(-32f, 5f));
        CreateImage(barBack, new Color(1f, 1f, 1f, 0.15f));

        _barFill = CreateRect("Fill", barBack,
            Vector2.zero, Vector2.one, new Vector2(0f, 0.5f),
            Vector2.zero, Vector2.zero);
        _barFillImage = CreateImage(_barFill, _normalColor);

        // 2) 화면 중앙 원형 재장전 표시
        CreateRingSprite(128, 0.22f);

        RectTransform indicator = CreateRect("ReloadIndicator", canvasTransform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f),
            new Vector2(0f, _centerOffsetY + 28f), new Vector2(200f, 90f));
        _reloadIndicator = indicator.gameObject;

        RectTransform ring = CreateRect("Ring", indicator,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(56f, 56f));
        Image ringBack = CreateImage(ring, new Color(1f, 1f, 1f, 0.18f));
        ringBack.sprite = _ringSprite;

        RectTransform ringFillRect = CreateRect("Fill", ring,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        _ringFill = CreateImage(ringFillRect, _reloadColor);
        _ringFill.sprite = _ringSprite;
        _ringFill.type = Image.Type.Filled;
        _ringFill.fillMethod = Image.FillMethod.Radial360;
        _ringFill.fillOrigin = (int)Image.Origin360.Top;
        _ringFill.fillClockwise = true;
        _ringFill.fillAmount = 0f;

        RectTransform ringTime = CreateRect("Time", ring,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        _ringTimeText = CreateText(ringTime, font, 14f, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);

        RectTransform reloadLabel = CreateRect("Label", indicator,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -62f), new Vector2(200f, 24f));
        TextMeshProUGUI reloadLabelText = CreateText(reloadLabel, font, 15f, FontStyles.Bold, TextAlignmentOptions.Top, _reloadColor);
        reloadLabelText.text = "RELOADING";
        reloadLabelText.characterSpacing = 6f;

        _reloadIndicator.SetActive(false);

        // 3) 탄약 부족 / 없음 안내 문구
        RectTransform prompt = CreateRect("AmmoPrompt", canvasTransform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, _centerOffsetY), new Vector2(360f, 30f));
        _promptText = CreateText(prompt, font, 18f, FontStyles.Bold, TextAlignmentOptions.Center, _lowColor);
        _promptText.gameObject.SetActive(false);

        RefreshAmmoText();
    }

    private static TMP_FontAsset FindFont()
    {
        // 씬에 이미 있는 TMP 글꼴(한글 포함)을 우선 사용한다.
        TextMeshProUGUI sample = FindAnyObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
        if (sample != null && sample.font != null)
        {
            return sample.font;
        }

        return TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null;
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

    // 원형 진행 표시에 쓸 고리 모양 스프라이트를 코드로 만든다. (별도 이미지 파일 불필요)
    private void CreateRingSprite(int size, float thickness)
    {
        _ringTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        _ringTexture.name = "AmmoHud_Ring";
        _ringTexture.wrapMode = TextureWrapMode.Clamp;
        _ringTexture.filterMode = FilterMode.Bilinear;

        float outer = size * 0.5f - 1f;
        float inner = outer * (1f - thickness);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(outer - distance + 0.5f) * Mathf.Clamp01(distance - inner + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        _ringTexture.SetPixels32(pixels);
        _ringTexture.Apply(false, true);
        _ringSprite = Sprite.Create(_ringTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        _ringSprite.name = "AmmoHud_Ring";
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (_ringSprite != null) Destroy(_ringSprite);
        if (_ringTexture != null) Destroy(_ringTexture);
    }
}
