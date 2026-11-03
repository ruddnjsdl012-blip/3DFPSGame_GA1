using UnityEngine;

// 고장 난 형광등처럼 제목이 가끔 깜빡이고, 아주 가끔 옆으로 찢어지듯 흔들린다.
[RequireComponent(typeof(CanvasGroup))]
public class UI_TitleFlicker : MonoBehaviour
{
    [SerializeField] private float _minInterval = 1.5f;
    [SerializeField] private float _maxInterval = 5f;
    [SerializeField] private float _flickerDuration = 0.25f;
    [SerializeField] private float _minAlpha = 0.25f;

    [Header("글리치 (선택)")]
    [SerializeField] private RectTransform _glitchTarget;
    [SerializeField] private float _glitchChance = 0.35f;
    [SerializeField] private float _glitchOffset = 12f;

    private CanvasGroup _canvasGroup;
    private float _nextFlickerTime;
    private float _flickerEndTime;
    private bool _isGlitching;
    private Vector2 _glitchBasePosition;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_glitchTarget != null) _glitchBasePosition = _glitchTarget.anchoredPosition;
        ScheduleNext();
    }

    private void Update()
    {
        float now = Time.unscaledTime;

        if (now >= _nextFlickerTime && now > _flickerEndTime)
        {
            _flickerEndTime = now + _flickerDuration;
            _isGlitching = _glitchTarget != null && Random.value < _glitchChance;
            ScheduleNext();
        }

        bool isFlickering = now < _flickerEndTime;
        _canvasGroup.alpha = isFlickering ? Random.Range(_minAlpha, 1f) : 1f;

        if (_glitchTarget == null) return;

        _glitchTarget.anchoredPosition = isFlickering && _isGlitching
            ? _glitchBasePosition + new Vector2(Random.Range(-_glitchOffset, _glitchOffset), 0f)
            : _glitchBasePosition;
    }

    private void ScheduleNext()
    {
        _nextFlickerTime = Time.unscaledTime + Random.Range(_minInterval, _maxInterval);
    }
}
