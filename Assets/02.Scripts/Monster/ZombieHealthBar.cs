using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[RequireComponent(typeof(Zombie))]
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class ZombieHealthBar : MonoBehaviour
{
    [SerializeField]
    private Transform _canvasTransform;

    [FormerlySerializedAs("_slider")]
    [SerializeField] private Slider _healthSlider;

    private Zombie _zombie;
    private Camera _camera;
    private float _lastHealth = -1f;
    private float _lastMaxHealth = -1f;

    private void Awake()
    {
        _zombie = GetComponent<Zombie>();
        _camera = Camera.main;

        if (_healthSlider == null)
        {
            _healthSlider = GetComponentInChildren<Slider>(true);
        }

        if (_canvasTransform == null && _healthSlider != null)
        {
            Canvas canvas = _healthSlider.GetComponentInParent<Canvas>();

            if (canvas != null)
            {
                _canvasTransform = canvas.transform;
            }
        }

        if (_zombie == null || _healthSlider == null
                            || _canvasTransform == null || _canvasTransform == transform)
        {
            Debug.LogError("ZombieHealthBar: 좀비 루트에서 HealthBarCanvas와 HealthSlider를 연결하세요.", this);
            enabled = false;
            return;
        }

        _healthSlider.minValue = 0f;
        _healthSlider.maxValue = 1f;
        _healthSlider.wholeNumbers = false;
    }

    private void OnEnable()
    {
        _lastHealth = -1f;
        _lastMaxHealth = -1f;
    }

    private void LateUpdate()
    {
        if (_zombie == null || _healthSlider == null || _canvasTransform == null)
        {
            return;
        }

        float health = _zombie.Health;
        float maxHealth = _zombie.MaxHealth;

        if (health != _lastHealth || maxHealth != _lastMaxHealth)
        {
            _lastHealth = health;
            _lastMaxHealth = maxHealth;
            _healthSlider.SetValueWithoutNotify(
                maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f);
        }

        if (_camera == null)
        {
            _camera = Camera.main;
        }

        if (_camera != null)
        {
            _canvasTransform.forward = _camera.transform.forward;
        }
    }
}