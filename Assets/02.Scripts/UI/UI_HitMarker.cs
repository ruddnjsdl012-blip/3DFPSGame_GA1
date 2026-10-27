using UnityEngine;
using UnityEngine.UI;

// 총알이 좀비에 맞으면 화면 가운데에 X 모양 히트마커를 띄운다.
//  - 일반 명중: 흰색, 짧게
//  - 헤드샷: 주황색, 조금 크게 + 맑은 '딩' 소리
//  - 처치: 빨간색, 크고 길게
// PlayerGunFire가 시작할 때 자동으로 만든다.
[DisallowMultipleComponent]
public class UI_HitMarker : MonoBehaviour
{
    [SerializeField] private Color _hitColor = Color.white;
    [SerializeField] private Color _headshotColor = new Color(1f, 0.7f, 0.15f);
    [SerializeField] private Color _killColor = new Color(1f, 0.2f, 0.15f);
    [SerializeField, Min(1f)] private float _gap = 9f;
    [SerializeField, Min(1f)] private float _length = 11f;
    [SerializeField, Min(1f)] private float _thickness = 3f;
    [SerializeField, Range(0f, 1f)] private float _soundVolume = 0.6f;

    private PlayerGunFire _gun;
    private RectTransform _root;
    private Image[] _bars;
    private float _timer;
    private float _duration = 0.2f;
    private float _scale = 1f;
    private Color _color;

    public static UI_HitMarker Create(PlayerGunFire gun)
    {
        UI_HitMarker marker = new GameObject("UI_HitMarker").AddComponent<UI_HitMarker>();
        marker.Bind(gun);
        return marker;
    }

    private void Awake()
    {
        Build();
    }

    public void Bind(PlayerGunFire gun)
    {
        if (_gun != null)
        {
            _gun.ZombieHit -= HandleZombieHit;
        }

        _gun = gun;
        if (_gun != null)
        {
            _gun.ZombieHit += HandleZombieHit;
        }
    }

    private void HandleZombieHit(bool headshot, bool killed)
    {
        if (killed)
        {
            Show(_killColor, 1.45f, 0.4f);
            GameAudio.Play2D(headshot ? "ui_headshot" : "ui_hitmarker", _soundVolume, 0.02f);
        }
        else if (headshot)
        {
            Show(_headshotColor, 1.25f, 0.28f);
            GameAudio.Play2D("ui_headshot", _soundVolume * 0.8f, 0.03f);
        }
        else
        {
            Show(_hitColor, 1f, 0.18f);
            GameAudio.Play2D("ui_hitmarker", _soundVolume * 0.7f, 0.05f, 0.03f);
        }
    }

    private void Show(Color color, float scale, float duration)
    {
        // 처치 표시가 떠 있는 동안에는 약한 표시로 덮어쓰지 않는다.
        if (_timer > 0f && _color == _killColor && color != _killColor)
        {
            return;
        }

        _color = color;
        _scale = scale;
        _duration = duration;
        _timer = duration;
    }

    private void Update()
    {
        if (_timer <= 0f)
        {
            if (_root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(false);
            }

            return;
        }

        _timer -= Time.unscaledDeltaTime;
        float u = 1f - Mathf.Clamp01(_timer / _duration); // 0 → 1
        if (_root.gameObject.activeSelf == false)
        {
            _root.gameObject.SetActive(true);
        }

        // 크게 튀어나왔다가 제자리로, 끝으로 갈수록 사라짐
        float pop = Mathf.Lerp(1.5f, 1f, Mathf.Clamp01(u / 0.25f));
        _root.localScale = Vector3.one * (_scale * pop);
        Color color = _color;
        color.a = 1f - u * u;
        foreach (Image bar in _bars)
        {
            bar.color = color;
        }
    }

    private void Build()
    {
        GameObject canvasObject = new GameObject("HitMarker_Canvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 52;

        GameObject rootObject = new GameObject("Marker", typeof(RectTransform));
        _root = (RectTransform)rootObject.transform;
        _root.SetParent(canvasObject.transform, false);
        _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.sizeDelta = Vector2.zero;

        _bars = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            float angle = 45f + 90f * i;
            Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            GameObject bar = new GameObject("Bar", typeof(RectTransform));
            RectTransform rect = (RectTransform)bar.transform;
            rect.SetParent(_root, false);
            rect.sizeDelta = new Vector2(_thickness, _length);
            rect.anchoredPosition = direction * (_gap + _length * 0.5f);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            Image image = bar.AddComponent<Image>();
            image.raycastTarget = false;
            _bars[i] = image;
        }

        rootObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_gun != null)
        {
            _gun.ZombieHit -= HandleZombieHit;
        }
    }
}
