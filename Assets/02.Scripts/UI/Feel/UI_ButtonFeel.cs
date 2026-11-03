using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 버튼에 손맛을 더한다.
// 마우스를 올리면 살짝 커지고 오른쪽으로 밀리며 강조 바가 늘어나고, 누르면 눌렸다가 튕겨 나온다.
// 일시정지 중(Time.timeScale = 0)에도 움직이도록 unscaledDeltaTime을 쓴다.
[RequireComponent(typeof(RectTransform))]
public class UI_ButtonFeel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    [Header("크기")]
    [SerializeField] private RectTransform _scaleTarget;
    [SerializeField] private float _hoverScale = 1.04f;
    [SerializeField] private float _pressScale = 0.94f;
    [SerializeField] private float _punchScale = 1.12f;

    [Header("밀림")]
    [SerializeField] private float _hoverShiftX = 10f;

    [Header("강조 바 (선택)")]
    [SerializeField] private RectTransform _accentBar;
    [SerializeField] private float _accentIdleWidth = 6f;
    [SerializeField] private float _accentHoverWidth = 18f;

    [Header("색 (선택)")]
    [SerializeField] private Graphic _tintTarget;
    [SerializeField] private Color _idleColor = Color.white;
    [SerializeField] private Color _hoverColor = new Color(1f, 0.3f, 0.25f);

    [Header("소리 (선택)")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _hoverClip;
    [SerializeField] private AudioClip _clickClip;
    [SerializeField] private float _volume = 0.6f;

    [Header("반응 속도")]
    [SerializeField] private float _sharpness = 14f;

    private Vector2 _basePosition;
    private Vector3 _currentScale = Vector3.one;
    private float _punch;
    private bool _isHovered;
    private bool _isPressed;
    private Selectable _selectable;

    public bool IsHovered => _isHovered;

    private void Awake()
    {
        if (_scaleTarget == null) _scaleTarget = (RectTransform)transform;
        _selectable = GetComponent<Selectable>();
        _basePosition = _scaleTarget.anchoredPosition;
    }

    private void OnDisable()
    {
        // 패널이 꺼졌다 켜질 때 커진 상태로 남지 않게 되돌린다.
        _isHovered = false;
        _isPressed = false;
        _punch = 0f;
        _currentScale = Vector3.one;
        if (_scaleTarget == null) return;

        _scaleTarget.localScale = Vector3.one;
        _scaleTarget.anchoredPosition = _basePosition;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float t = 1f - Mathf.Exp(-_sharpness * dt);

        float targetScale = _isPressed ? _pressScale : (_isHovered ? _hoverScale : 1f);
        _punch = Mathf.MoveTowards(_punch, 0f, dt * 4f);
        float punchOffset = Mathf.Sin(_punch * Mathf.PI) * (_punchScale - 1f);

        _currentScale = Vector3.Lerp(_currentScale, Vector3.one * targetScale, t);
        _scaleTarget.localScale = _currentScale + Vector3.one * punchOffset;

        Vector2 targetPosition = _basePosition + new Vector2(_isHovered ? _hoverShiftX : 0f, 0f);
        _scaleTarget.anchoredPosition = Vector2.Lerp(_scaleTarget.anchoredPosition, targetPosition, t);

        if (_accentBar != null)
        {
            Vector2 size = _accentBar.sizeDelta;
            size.x = Mathf.Lerp(size.x, _isHovered ? _accentHoverWidth : _accentIdleWidth, t);
            _accentBar.sizeDelta = size;
        }

        if (_tintTarget != null)
        {
            _tintTarget.color = Color.Lerp(_tintTarget.color, _isHovered ? _hoverColor : _idleColor, t);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable()) return;

        _isHovered = true;
        PlayClip(_hoverClip, 0.5f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        _isPressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsInteractable()) return;

        _isPressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_isPressed) return;

        _isPressed = false;
        Punch();
    }

    public void OnSelect(BaseEventData eventData)
    {
        // 키보드/패드로 고른 경우에도 마우스를 올린 것처럼 보이게 한다.
        if (eventData is PointerEventData) return;

        _isHovered = true;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isHovered = false;
    }

    public void OnSubmit(BaseEventData eventData)
    {
        Punch();
    }

    // 눌린 뒤 튕겨 나오는 연출과 클릭 소리를 낸다.
    public void Punch()
    {
        _punch = 1f;
        PlayClip(_clickClip, 1f);
    }

    private bool IsInteractable()
    {
        return _selectable == null || _selectable.IsInteractable();
    }

    private void PlayClip(AudioClip clip, float volumeScale)
    {
        if (_audioSource == null || clip == null) return;

        _audioSource.pitch = Random.Range(0.95f, 1.05f);
        _audioSource.PlayOneShot(clip, _volume * volumeScale);
    }
}
