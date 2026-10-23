using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public class UI_HitEffect : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float _maxAlpha = 0.4f;
    [SerializeField, Min(0f)] private float _fadeTime = 0.3f;

    private Image _image;
    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _image.raycastTarget = false;
        SetAlpha(0f);
    }

    public void Play()
    {
        if (isActiveAndEnabled == false || _image == null)
        {
            return;
        }

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        if (_fadeTime <= 0f)
        {
            SetAlpha(0f);
            return;
        }

        _fadeCoroutine = StartCoroutine(Fade_Coroutine());
    }

    private IEnumerator Fade_Coroutine()
    {
        float elapsed = 0f;
        SetAlpha(_maxAlpha);

        while (elapsed < _fadeTime)
        {
            yield return null;

            elapsed += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(_maxAlpha, 0f, elapsed / _fadeTime));
        }

        SetAlpha(0f);
        _fadeCoroutine = null;
    }

    private void SetAlpha(float alpha)
    {
        if (_image == null)
        {
            return;
        }

        Color color = _image.color;
        color.a = alpha;
        _image.color = color;
        _image.enabled = alpha > 0f;
    }

    private void OnDisable()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        SetAlpha(0f);
    }
}