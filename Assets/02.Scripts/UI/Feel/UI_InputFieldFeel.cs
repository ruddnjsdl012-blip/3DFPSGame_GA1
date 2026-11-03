using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 입력칸을 고르면 아래 밑줄이 붉게 차오르고 라벨이 밝아진다. 글자를 칠 때마다 밑줄이 살짝 튄다.
[RequireComponent(typeof(TMP_InputField))]
public class UI_InputFieldFeel : MonoBehaviour
{
    [SerializeField] private RectTransform _underline;
    [SerializeField] private Image _underlineImage;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Color _idleLineColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color _focusLineColor = new Color(0.9f, 0.15f, 0.12f, 1f);
    [SerializeField] private Color _idleLabelColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private Color _focusLabelColor = new Color(1f, 0.35f, 0.3f, 1f);
    [SerializeField] private float _typePulseHeight = 3f;
    [SerializeField] private float _sharpness = 12f;

    private TMP_InputField _inputField;
    private float _baseLineHeight;
    private float _focus;
    private float _pulse;

    private void Awake()
    {
        _inputField = GetComponent<TMP_InputField>();
        if (_underline != null) _baseLineHeight = _underline.sizeDelta.y;
        _inputField.onValueChanged.AddListener(OnValueChanged);
    }

    private void OnDestroy()
    {
        if (_inputField != null) _inputField.onValueChanged.RemoveListener(OnValueChanged);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float target = _inputField.isFocused ? 1f : 0f;
        _focus = Mathf.Lerp(_focus, target, 1f - Mathf.Exp(-_sharpness * dt));
        _pulse = Mathf.MoveTowards(_pulse, 0f, dt * 6f);

        if (_underline != null)
        {
            // 가운데를 기준으로 밑줄이 좌우로 차오른다.
            _underline.localScale = new Vector3(Mathf.Lerp(0.35f, 1f, _focus), 1f, 1f);
            Vector2 size = _underline.sizeDelta;
            size.y = _baseLineHeight + _focus * 1f + _pulse * _typePulseHeight;
            _underline.sizeDelta = size;
        }

        if (_underlineImage != null) _underlineImage.color = Color.Lerp(_idleLineColor, _focusLineColor, _focus);
        if (_label != null) _label.color = Color.Lerp(_idleLabelColor, _focusLabelColor, _focus);
    }

    private void OnValueChanged(string value)
    {
        _pulse = 1f;
    }
}
