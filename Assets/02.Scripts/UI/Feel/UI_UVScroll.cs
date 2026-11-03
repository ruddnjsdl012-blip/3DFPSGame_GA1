using UnityEngine;
using UnityEngine.UI;

// RawImage의 UV를 흘려서 안개가 천천히 흐르는 것처럼 보이게 한다. 알파도 숨 쉬듯 오르내린다.
[RequireComponent(typeof(RawImage))]
public class UI_UVScroll : MonoBehaviour
{
    [SerializeField] private Vector2 _speed = new Vector2(0.01f, 0.002f);
    [SerializeField] private float _alphaMin = 0.15f;
    [SerializeField] private float _alphaMax = 0.35f;
    [SerializeField] private float _alphaPeriod = 9f;

    private RawImage _rawImage;

    private void Awake()
    {
        _rawImage = GetComponent<RawImage>();
    }

    private void Update()
    {
        Rect uv = _rawImage.uvRect;
        uv.position += _speed * Time.unscaledDeltaTime;
        uv.x = Mathf.Repeat(uv.x, 1f);
        uv.y = Mathf.Repeat(uv.y, 1f);
        _rawImage.uvRect = uv;

        Color color = _rawImage.color;
        float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / _alphaPeriod);
        color.a = Mathf.Lerp(_alphaMin, _alphaMax, wave);
        _rawImage.color = color;
    }
}
