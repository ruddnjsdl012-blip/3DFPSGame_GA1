using UnityEngine;
using UnityEngine.UI;

// 부머 담즙을 뒤집어쓰면 화면이 초록색으로 얼룩진다. (시간이 지나면 서서히 걷힌다)
public class BileOverlay : MonoBehaviour
{
    private static BileOverlay _instance;

    private Image[] _blobs;
    private Image _tint;
    private float _time;
    private float _duration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    public static void Show(float duration)
    {
        if (_instance == null)
        {
            GameObject canvasObject = new GameObject("BileOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4;  // 글자(HUD)보다 아래
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _instance = canvasObject.AddComponent<BileOverlay>();
            _instance.Build();
        }

        _instance.Splash(duration);
    }

    private void Build()
    {
        _tint = CreateImage("Tint", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, null);
        Sprite blob = CreateBlobSprite();
        _blobs = new Image[9];
        for (int i = 0; i < _blobs.Length; i++)
        {
            _blobs[i] = CreateImage("Blob" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 500f), blob);
        }

        gameObject.SetActive(false);
    }

    private void Splash(float duration)
    {
        gameObject.SetActive(true);
        _time = 0f;
        _duration = duration;

        // 얼룩을 화면 곳곳에 랜덤으로 뿌린다.
        foreach (Image blob in _blobs)
        {
            RectTransform rect = blob.rectTransform;
            rect.anchoredPosition = new Vector2(Random.Range(-850f, 850f), Random.Range(-450f, 450f));
            float size = Random.Range(350f, 750f);
            rect.sizeDelta = new Vector2(size, size * Random.Range(0.7f, 1.2f));
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        }
    }

    private void Update()
    {
        _time += Time.deltaTime;
        float left = 1f - Mathf.Clamp01(_time / _duration);
        float alpha = left < 0.4f ? left / 0.4f : 1f;
        float wobble = 0.85f + 0.15f * Mathf.Sin(_time * 3f);

        _tint.color = new Color(0.3f, 0.45f, 0.05f, 0.35f * alpha);
        for (int i = 0; i < _blobs.Length; i++)
        {
            _blobs[i].color = new Color(0.45f, 0.65f, 0.1f, 0.85f * alpha * wobble);
            // 얼룩이 천천히 흘러내린다.
            _blobs[i].rectTransform.anchoredPosition += Vector2.down * (12f + i * 2f) * Time.deltaTime;
        }

        if (_time >= _duration)
        {
            gameObject.SetActive(false);
        }
    }

    private Image CreateImage(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    // 가장자리가 울퉁불퉁하고 흐린 얼룩 그림을 코드로 만든다.
    private static Sprite CreateBlobSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float c = (size - 1) * 0.5f;
        float seed = Random.Range(0f, 100f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c;
                float dy = (y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float edge = 0.6f + 0.3f * Mathf.PerlinNoise(seed + Mathf.Cos(angle) * 2f, seed + Mathf.Sin(angle) * 2f);
                float alpha = Mathf.Clamp01((edge - r) * 4f);
                alpha *= 0.75f + 0.25f * Mathf.PerlinNoise(x * 0.08f + seed, y * 0.08f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }
}
