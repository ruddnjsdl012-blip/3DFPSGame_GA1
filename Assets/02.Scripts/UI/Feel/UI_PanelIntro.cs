using System.Collections;
using UnityEngine;

// 패널이 켜질 때마다 옆에서 밀려 들어오며 나타나고, 자식 항목들이 순서대로 뒤따라 들어온다.
// 자식 항목에 UI_ButtonFeel이 있으면 위치가 서로 겹치지 않도록 버튼 안쪽의 Visual을 움직이게 한다.
// 버튼 OnClick에서 GameObject.SetActive만 연결해도 화면 전환이 자연스럽게 보인다.
[RequireComponent(typeof(CanvasGroup))]
public class UI_PanelIntro : MonoBehaviour
{
    private const float BackOvershoot = 1.70158f;
    private const float BackOvershoot3 = BackOvershoot + 1f;

    [SerializeField] private float _duration = 0.35f;
    [SerializeField] private Vector2 _startOffset = new Vector2(-60f, 0f);

    [Header("자식 순차 등장 (선택)")]
    [SerializeField] private RectTransform[] _staggerItems;
    [SerializeField] private float _staggerDelay = 0.05f;
    [SerializeField] private float _itemOffsetX = -40f;

    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Vector2 _basePosition;
    private Vector2[] _itemBasePositions;
    private CanvasGroup[] _itemGroups;

    private void Awake()
    {
        _rectTransform = (RectTransform)transform;
        _canvasGroup = GetComponent<CanvasGroup>();
        _basePosition = _rectTransform.anchoredPosition;

        int count = _staggerItems != null ? _staggerItems.Length : 0;
        _itemBasePositions = new Vector2[count];
        _itemGroups = new CanvasGroup[count];
        for (int i = 0; i < count; i++)
        {
            _itemBasePositions[i] = _staggerItems[i].anchoredPosition;
            _itemGroups[i] = _staggerItems[i].GetComponent<CanvasGroup>();
            if (_itemGroups[i] == null) _itemGroups[i] = _staggerItems[i].gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        StopAllCoroutines();
        StartCoroutine(Intro_Coroutine());
    }

    private IEnumerator Intro_Coroutine()
    {
        _canvasGroup.interactable = false;
        int count = _itemGroups.Length;
        float total = _duration + _staggerDelay * count;

        for (float elapsed = 0f; elapsed < total; elapsed += Time.unscaledDeltaTime)
        {
            float panel = EaseOutBack(Mathf.Clamp01(elapsed / _duration));
            _rectTransform.anchoredPosition = Vector2.LerpUnclamped(_basePosition + _startOffset, _basePosition, panel);
            _canvasGroup.alpha = Mathf.Clamp01(elapsed / (_duration * 0.6f));

            for (int i = 0; i < count; i++)
            {
                float local = Mathf.Clamp01((elapsed - _staggerDelay * (i + 1)) / _duration);
                float eased = EaseOutBack(local);
                _staggerItems[i].anchoredPosition = _itemBasePositions[i] + new Vector2(_itemOffsetX * (1f - eased), 0f);
                _itemGroups[i].alpha = local;
            }

            yield return null;
        }

        _rectTransform.anchoredPosition = _basePosition;
        _canvasGroup.alpha = 1f;
        for (int i = 0; i < count; i++)
        {
            _staggerItems[i].anchoredPosition = _itemBasePositions[i];
            _itemGroups[i].alpha = 1f;
        }

        _canvasGroup.interactable = true;
    }

    // 목표를 살짝 지나쳤다가 돌아오는 곡선. 툭 튀어나오는 느낌을 준다.
    private static float EaseOutBack(float x)
    {
        return 1f + BackOvershoot3 * Mathf.Pow(x - 1f, 3f) + BackOvershoot * Mathf.Pow(x - 1f, 2f);
    }
}
