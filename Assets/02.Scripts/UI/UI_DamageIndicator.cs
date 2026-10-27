using UnityEngine;
using UnityEngine.UI;

// 플레이어가 맞으면 공격한 좀비가 있는 방향을 화면 가운데 둘레에 빨간 호로 보여 준다.
// (뒤에서 맞으면 아래쪽, 왼쪽에서 맞으면 왼쪽에 표시) Player가 시작할 때 자동으로 만든다.
[DisallowMultipleComponent]
public class UI_DamageIndicator : MonoBehaviour
{
    private const int Count = 4;

    [SerializeField] private Color _color = new Color(0.9f, 0.05f, 0.05f);
    [SerializeField, Min(50f)] private float _size = 420f;
    [SerializeField, Min(0.1f)] private float _duration = 1.1f;

    private Player _player;
    private Texture2D _texture;
    private Sprite _sprite;
    private RectTransform[] _arcs;
    private Image[] _images;
    private Vector3[] _directions;
    private float[] _timers;
    private int _next;

    public static UI_DamageIndicator Create(Player player)
    {
        UI_DamageIndicator indicator = new GameObject("UI_DamageIndicator").AddComponent<UI_DamageIndicator>();
        indicator.Bind(player);
        return indicator;
    }

    private void Awake()
    {
        Build();
    }

    public void Bind(Player player)
    {
        if (_player != null)
        {
            _player.DamagedFrom -= HandleDamaged;
        }

        _player = player;
        if (_player != null)
        {
            _player.DamagedFrom += HandleDamaged;
        }
    }

    // direction: 플레이어에서 공격한 쪽을 향하는 월드 방향
    private void HandleDamaged(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        int index = _next;
        _next = (_next + 1) % Count;
        _directions[index] = direction.normalized;
        _timers[index] = _duration;
    }

    private void Update()
    {
        Camera view = Camera.main;
        for (int i = 0; i < Count; i++)
        {
            if (_timers[i] <= 0f)
            {
                if (_images[i].enabled) _images[i].enabled = false;
                continue;
            }

            _timers[i] -= Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(_timers[i] / _duration);

            // 카메라가 보는 방향 기준으로 공격 방향의 각도를 계산해 호를 돌린다. (돌아보면 따라 움직임)
            float angle = 0f;
            if (view != null)
            {
                Vector3 forward = view.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                {
                    angle = Vector3.SignedAngle(forward.normalized, _directions[i], Vector3.up);
                }
            }

            _arcs[i].localRotation = Quaternion.Euler(0f, 0f, -angle);
            Color color = _color;
            color.a = 0.85f * u;
            _images[i].color = color;
            _images[i].enabled = true;
        }
    }

    private void Build()
    {
        GameObject canvasObject = new GameObject("DamageIndicator_Canvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 41;

        CreateArcSprite(256);
        _arcs = new RectTransform[Count];
        _images = new Image[Count];
        _directions = new Vector3[Count];
        _timers = new float[Count];
        for (int i = 0; i < Count; i++)
        {
            GameObject go = new GameObject("Arc", typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(_size, _size);
            Image image = go.AddComponent<Image>();
            image.sprite = _sprite;
            image.raycastTarget = false;
            image.enabled = false;
            _arcs[i] = rect;
            _images[i] = image;
        }
    }

    // 위쪽(앞 방향)에 그려진 붉은 호 모양 텍스처
    private void CreateArcSprite(int size)
    {
        _texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        _texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Abs(Mathf.Atan2(dx, dy)) * Mathf.Rad2Deg; // 위쪽이 0도
                float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.86f) / 0.1f);
                float spread = Mathf.Clamp01(1f - angle / 32f);
                float a = ring * spread * spread;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        _texture.SetPixels32(pixels);
        _texture.Apply(false, true);
        _sprite = Sprite.Create(_texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void OnDestroy()
    {
        if (_player != null)
        {
            _player.DamagedFrom -= HandleDamaged;
        }

        if (_sprite != null) Destroy(_sprite);
        if (_texture != null) Destroy(_texture);
    }
}
