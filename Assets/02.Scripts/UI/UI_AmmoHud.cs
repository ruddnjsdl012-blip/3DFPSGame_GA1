using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 오른쪽 아래 총알 UI:   30 / 120
//  - 큰 숫자: 탄창 안의 총알, 작은 숫자: 예비 총알
//  - 탄창이 1/4 이하이면 노란색, 비면 빨간색. 재장전 중에는 "재장전 중..." 표시
// PlayerGunFire가 시작할 때 코드로 만든다. (씬에 따로 놓지 않아도 된다)
// 값이 바뀔 때만 다시 그린다. (옵저버 패턴: PlayerGunFire.OnAmmoChanged 구독)
public class UI_AmmoHud : MonoBehaviour
{
    private static readonly Color NormalColor = new Color(1f, 0.97f, 0.9f);
    private static readonly Color LowColor = new Color(1f, 0.8f, 0.25f);
    private static readonly Color EmptyColor = new Color(1f, 0.25f, 0.2f);

    private PlayerGunFire _gun;
    private TextMeshProUGUI _bullet;
    private TextMeshProUGUI _reserve;
    private TextMeshProUGUI _hint;
    private float _punch;

    public static UI_AmmoHud Create(PlayerGunFire gun)
    {
        GameObject canvasObject = new GameObject("UI_AmmoHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        UI_AmmoHud hud = canvasObject.AddComponent<UI_AmmoHud>();
        hud.Build(gun);
        return hud;
    }

    private void Build(PlayerGunFire gun)
    {
        _gun = gun;
        TMP_FontAsset font = FindKoreanFont();

        // 반투명 검은 배경 판
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.SetParent(transform, false);
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-40f, 40f);
        panelRect.sizeDelta = new Vector2(330f, 120f);
        Image image = panel.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.35f);
        image.raycastTarget = false;

        // 큰 숫자 (탄창) — 판의 가운데보다 왼쪽, 오른쪽 정렬
        _bullet = CreateText(panelRect, "Bullet", font, 76f, new Vector2(-130f, 4f), new Vector2(180f, 100f), TextAlignmentOptions.Right);
        // 작은 숫자 (예비) — "/ 120"
        _reserve = CreateText(panelRect, "Reserve", font, 40f, new Vector2(-122f, -10f), new Vector2(110f, 100f), TextAlignmentOptions.Left);
        _reserve.color = new Color(0.8f, 0.8f, 0.8f);
        // 판 위의 안내 글자
        _hint = CreateText(panelRect, "Hint", font, 26f, new Vector2(-10f, 88f), new Vector2(330f, 40f), TextAlignmentOptions.Right);

        _gun.OnAmmoChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_gun != null)
        {
            _gun.OnAmmoChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        if (_gun == null)
        {
            return;
        }

        int bullet = _gun.BulletCount;
        int max = Mathf.Max(1, _gun.MaxBulletCount);
        int reserve = _gun.ReserveAmmo;

        if (_bullet.text != bullet.ToString())
        {
            _punch = 1f;
        }

        _bullet.text = bullet.ToString();
        _reserve.text = "/ " + reserve;
        _reserve.color = reserve > 0 ? new Color(0.8f, 0.8f, 0.8f) : EmptyColor;

        if (bullet <= 0)
        {
            _bullet.color = EmptyColor;
        }
        else if (bullet <= max / 4)
        {
            _bullet.color = LowColor;
        }
        else
        {
            _bullet.color = NormalColor;
        }

        if (_gun.IsReloading)
        {
            _hint.text = "재장전 중...";
            _hint.color = new Color(0.8f, 0.9f, 1f);
        }
        else if (bullet <= 0 && reserve <= 0)
        {
            _hint.text = "탄약 없음";
            _hint.color = EmptyColor;
        }
        else if (bullet <= max / 4 && reserve > 0)
        {
            _hint.text = "[R] 재장전";
            _hint.color = LowColor;
        }
        else
        {
            _hint.text = string.Empty;
        }
    }

    private void Update()
    {
        // 한 발 쏠 때마다 숫자가 살짝 커졌다 돌아온다.
        if (_punch > 0f)
        {
            _punch = Mathf.MoveTowards(_punch, 0f, Time.unscaledDeltaTime * 8f);
            _bullet.transform.localScale = Vector3.one * (1f + 0.12f * _punch);
        }

        // 재장전 중이면 안내 글자가 깜빡인다.
        if (_gun != null && _gun.IsReloading)
        {
            Color c = _hint.color;
            c.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
            _hint.color = c;
        }
    }

    // 씬에 있는 한글 폰트(11장 StateText의 폰트)를 빌려 쓴다.
    private static TMP_FontAsset FindKoreanFont()
    {
        foreach (TextMeshProUGUI text in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text.font != null && text.font.HasCharacter('재', true, true))
            {
                return text.font;
            }
        }

        return TMP_Settings.defaultFontAsset;
    }

    private static TextMeshProUGUI CreateText(RectTransform parent, string name, TMP_FontAsset font, float size, Vector2 position, Vector2 box, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = alignment == TextAlignmentOptions.Left ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = box;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
        {
            text.font = font;
        }

        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = NormalColor;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.outlineWidth = 0.2f;
        text.outlineColor = new Color32(0, 0, 0, 200);
        text.text = string.Empty;
        return text;
    }
}
