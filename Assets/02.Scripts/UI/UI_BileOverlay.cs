using UnityEngine;
using UnityEngine.UI;

// 부머 토사물을 뒤집어썼을 때 화면을 초록 얼룩으로 가린다. 얼룩은 천천히 흘러내리며 사라진다.
// UI_BileOverlay.Splash(시간)으로 부르면 처음 한 번 자동으로 만들어진다.
[DisallowMultipleComponent]
public class UI_BileOverlay : MonoBehaviour
{
    [SerializeField] private Color _color = new Color(0.42f, 0.58f, 0.1f, 0.85f);
    [SerializeField] private Color _edgeColor = new Color(0.12f, 0.2f, 0.02f, 0.75f);

    private static UI_BileOverlay _instance;

    private RawImage _blobs;
    private RawImage _blobs2;
    private Image _edge;
    private Texture2D _blobTexture;
    private Texture2D _edgeTexture;
    private Sprite _edgeSprite;
    private float _duration;
    private float _timer;
    private float _appear;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    public static void Splash(float duration)
    {
        if (_instance == null)
        {
            _instance = FindAnyObjectByType<UI_BileOverlay>();
            if (_instance == null)
            {
                _instance = new GameObject("UI_BileOverlay").AddComponent<UI_BileOverlay>();
            }
        }

        _instance.Begin(duration);
    }

    private void Awake()
    {
        Build();
    }

    private void Begin(float duration)
    {
        // 이미 뒤집어쓴 상태면 더 긴 쪽으로 늘린다.
        if (_timer <= 0f)
        {
            _appear = 0f; // 깨끗한 상태에서 새로 덮일 때만 처음부터
        }

        _duration = Mathf.Max(duration, _timer);
        _timer = _duration;
        _blobs.uvRect = new Rect(Random.value, Random.value, 1f, 1f);
        _blobs2.uvRect = new Rect(Random.value, Random.value, 1.3f, 1.3f);
        GameAudio.Play2D("hit_flesh", 0.7f, 0.1f);
    }

    private void Update()
    {
        if (_timer <= 0f)
        {
            SetVisible(false);
            return;
        }

        float dt = Time.unscaledDeltaTime;
        _timer -= dt;
        SetVisible(true);

        // 처음 0.15초에 확 덮이고, 마지막 2초 동안 옅어진다.
        _appear = Mathf.MoveTowards(_appear, 1f, dt / 0.15f);
        float fade = Mathf.Clamp01(_timer / 2f);
        float alpha = _appear * fade;

        // 얼룩이 천천히 아래로 흘러내린다.
        Rect a = _blobs.uvRect;
        _blobs.uvRect = new Rect(a.x, a.y + dt * 0.035f, a.width, a.height);
        Rect b = _blobs2.uvRect;
        _blobs2.uvRect = new Rect(b.x + Mathf.Sin(Time.unscaledTime * 0.7f) * dt * 0.01f, b.y + dt * 0.02f, b.width, b.height);

        Color color = _color;
        color.a *= alpha;
        _blobs.color = color;
        color.a *= 0.7f;
        _blobs2.color = color;
        Color edge = _edgeColor;
        edge.a *= alpha;
        _edge.color = edge;
    }

    private void SetVisible(bool visible)
    {
        if (_blobs.enabled == visible) return;
        _blobs.enabled = visible;
        _blobs2.enabled = visible;
        _edge.enabled = visible;
    }

    private void Build()
    {
        GameObject canvasObject = new GameObject("BileOverlay_Canvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 42; // 피격 방향 표시 위, HUD 아래

        _blobTexture = CreateBlobTexture(256, 22);
        _edgeTexture = CreateEdgeTexture(64);
        _edgeSprite = Sprite.Create(_edgeTexture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f), 100f);

        _edge = CreateFullScreen<Image>("Edge", canvasObject.transform);
        _edge.sprite = _edgeSprite;
        _blobs2 = CreateFullScreen<RawImage>("Blobs2", canvasObject.transform);
        _blobs2.texture = _blobTexture;
        _blobs = CreateFullScreen<RawImage>("Blobs", canvasObject.transform);
        _blobs.texture = _blobTexture;
        SetVisible(true);
        SetVisible(false);
    }

    private static T CreateFullScreen<T>(string name, Transform parent) where T : Graphic
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        T graphic = go.AddComponent<T>();
        graphic.raycastTarget = false;
        return graphic;
    }

    // 이어 붙여도 이음새가 없는 얼룩 텍스처 (반복 타일)
    private static Texture2D CreateBlobTexture(int size, int count)
    {
        float[] alpha = new float[size * size];
        for (int k = 0; k < count; k++)
        {
            float cx = Random.value * size;
            float cy = Random.value * size;
            float r = Random.Range(0.06f, 0.2f) * size;
            float strength = Random.Range(0.5f, 1f);
            int ir = Mathf.CeilToInt(r * 1.6f);
            for (int dy = -ir; dy <= ir; dy++)
            {
                for (int dx = -ir; dx <= ir; dx++)
                {
                    // 얼룩 가장자리를 울퉁불퉁하게
                    float angle = Mathf.Atan2(dy, dx);
                    float wobble = 1f + 0.25f * Mathf.Sin(angle * 5f + k) + 0.12f * Mathf.Sin(angle * 11f + k * 2f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / (r * wobble);
                    float a = Mathf.Clamp01((1.15f - d) * 3f) * strength;
                    if (a <= 0f) continue;
                    int x = ((int)cx + dx) % size; if (x < 0) x += size;
                    int y = ((int)cy + dy) % size; if (y < 0) y += size;
                    int index = y * size + x;
                    alpha[index] = Mathf.Max(alpha[index], a);
                }
            }
        }

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Repeat;
        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha[i] * 255f));
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateEdgeTexture(int size)
    {
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
                float a = Mathf.Clamp01((r - 0.2f) / 0.8f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (_blobTexture != null) Destroy(_blobTexture);
        if (_edgeSprite != null) Destroy(_edgeSprite);
        if (_edgeTexture != null) Destroy(_edgeTexture);
    }
}
