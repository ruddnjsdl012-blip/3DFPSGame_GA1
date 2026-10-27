using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UI_HitEffect : MonoBehaviour
{
    [SerializeField] private float _flashTime = 0.06f;
    [SerializeField] private float _fadeTime = 0.3f;

    private Image _image;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _image.enabled = false;
    }

    private void Start()
    {
        Player player = GameObject.FindAnyObjectByType<Player>();
        if (player != null)
        {
            player.OnDamaged += OnPlayerDamaged;
        }
    }

    private void OnPlayerDamaged()
    {
        Fade(0.6f);
    }


    public void Flash(float alpha)
    {
        SetAlpha(alpha);

        StartCoroutine(Flash_Coroutine());
    }

    private IEnumerator Flash_Coroutine()
    {
        _image.enabled = true;

        yield return new WaitForSeconds(_flashTime);

        _image.enabled = false;
    }

    public void Fade(float alpha)
    {
        SetAlpha(alpha);

        StartCoroutine(Fade_Coroutine());
        // 2. 0.4에서 서서히 0으로 가서 사라지게 한다.
    }

    private IEnumerator Fade_Coroutine()
    {
        _image.enabled = true;

        float elpasedTime = 0f;

        float startAlpha = _image.color.a;

        while (elpasedTime < _fadeTime)
        {
            float alpha = Mathf.Lerp(startAlpha, 0f, elpasedTime / _fadeTime);

            SetAlpha(alpha);

            elpasedTime += Time.deltaTime;
            yield return null; // 한 프레임씩 쉰다!
        }

        SetAlpha(0f);
        _image.enabled = false;
    }

    private void SetAlpha(float alpha)
    {
        Color color = _image.color;
        color.a = alpha;
        _image.color = color;
    }
}
