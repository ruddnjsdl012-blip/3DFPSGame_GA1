using UnityEngine;

// 배경 이미지를 천천히 확대/이동(켄 번스 효과)하고, 마우스 위치에 따라 살짝 따라 움직이게(시차) 한다.
// 이미지는 화면보다 조금 크게(_baseScale) 잡아 가장자리가 드러나지 않게 한다.
public class UI_BackgroundDrift : MonoBehaviour
{
    [SerializeField] private float _baseScale = 1.08f;
    [SerializeField] private float _zoomAmount = 0.03f;
    [SerializeField] private float _zoomPeriod = 24f;
    [SerializeField] private Vector2 _driftAmount = new Vector2(18f, 8f);
    [SerializeField] private float _driftPeriod = 31f;
    [SerializeField] private float _parallax = 22f;
    [SerializeField] private float _parallaxSharpness = 3f;

    private RectTransform _rectTransform;
    private Vector2 _basePosition;
    private Vector2 _parallaxOffset;

    private void Awake()
    {
        _rectTransform = (RectTransform)transform;
        _basePosition = _rectTransform.anchoredPosition;
    }

    private void Update()
    {
        float time = Time.unscaledTime;
        float zoom = _baseScale + _zoomAmount * (0.5f + 0.5f * Mathf.Sin(time * Mathf.PI * 2f / _zoomPeriod));
        _rectTransform.localScale = new Vector3(zoom, zoom, 1f);

        float phase = time * Mathf.PI * 2f / _driftPeriod;
        Vector2 drift = new Vector2(Mathf.Sin(phase) * _driftAmount.x, Mathf.Cos(phase * 0.7f) * _driftAmount.y);

        // 마우스가 화면 가운데에서 벗어난 만큼 반대 방향으로 조금 밀어 깊이감을 준다.
        Vector2 mouse = new Vector2(Input.mousePosition.x / Mathf.Max(1, Screen.width), Input.mousePosition.y / Mathf.Max(1, Screen.height));
        Vector2 targetOffset = (mouse - new Vector2(0.5f, 0.5f)) * -_parallax;
        _parallaxOffset = Vector2.Lerp(_parallaxOffset, targetOffset, 1f - Mathf.Exp(-_parallaxSharpness * Time.unscaledDeltaTime));

        _rectTransform.anchoredPosition = _basePosition + drift + _parallaxOffset;
    }
}
