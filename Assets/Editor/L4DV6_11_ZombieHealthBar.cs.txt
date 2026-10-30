using UnityEngine;
using UnityEngine.UI;

public class ZombieHealthBar : MonoBehaviour
{
    [SerializeField] private Slider _slider;

    private Zombie _zombie;
    private GameObject _barObject;
    private Camera _camera;
    private float _lastRatio = -1f;

    private void Awake()
    {
        CacheReferences();
        SpecialZombie special = GetComponent<SpecialZombie>();
        SetVisible(special != null && special.Type == SpecialType.Tank);
    }

    private void CacheReferences()
    {
        if (_zombie == null)
        {
            _zombie = GetComponent<Zombie>();
        }

        if (_slider != null && _barObject == null)
        {
            Canvas canvas = _slider.GetComponentInParent<Canvas>(true);
            _barObject = canvas != null && canvas.gameObject != gameObject ? canvas.gameObject : _slider.gameObject;
            _slider.interactable = false;
        }
    }

    // 탱크를 제외한 모든 좀비는 Canvas와 LateUpdate를 모두 멈춘다.
    // 탱크의 화면 상단 보스 체력바(DirectorHUD)는 별도로 유지한다.
    public void SetVisible(bool visible)
    {
        CacheReferences();
        SpecialZombie special = GetComponent<SpecialZombie>();
        visible = visible && special != null && special.Type == SpecialType.Tank
            && _zombie != null && _slider != null;
        if (_barObject != null && _barObject.activeSelf != visible)
        {
            _barObject.SetActive(visible);
        }

        enabled = visible;
        if (visible)
        {
            _camera = Camera.main;
            _lastRatio = -1f;
        }
    }

    private void LateUpdate()
    {
        if (_zombie == null || _slider == null || _zombie.IsDead)
        {
            SetVisible(false);
            return;
        }

        float ratio = Mathf.Clamp01(_zombie.Health / Mathf.Max(1f, _zombie.MaxHealth));
        if (!Mathf.Approximately(ratio, _lastRatio))
        {
            _slider.SetValueWithoutNotify(ratio);
            _lastRatio = ratio;
        }

        if (_camera == null)
        {
            _camera = Camera.main;
        }

        if (_camera != null && _barObject != null)
        {
            _barObject.transform.forward = _camera.transform.forward;
        }
    }
}
