using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class UI_PlayerStats : MonoBehaviour
{
    [SerializeField] private Slider _healthSlider;
    [SerializeField] private Slider _staminaSlider;
    [SerializeField] private Player _player;
    [SerializeField] private PlayerMove _playerMove;

    private float _lastHealth = -1f;
    private float _lastMaxHealth = -1f;
    private float _lastStamina = -1f;

    private void Awake()
    {
        if (_healthSlider == null || _staminaSlider == null
                                  || _player == null || _playerMove == null)
        {
            Debug.LogError("UI_PlayerStats: Player, Player Move와 두 Slider를 연결하세요.", this);
            enabled = false;
            return;
        }

        _healthSlider.minValue = 0f;
        _healthSlider.maxValue = 1f;
        _healthSlider.wholeNumbers = false;

        _staminaSlider.minValue = 0f;
        _staminaSlider.maxValue = 1f;
        _staminaSlider.wholeNumbers = false;
    }

    private void OnEnable()
    {
        _lastHealth = -1f;
        _lastMaxHealth = -1f;
        _lastStamina = -1f;
    }

    private void Update()
    {
        if (_player == null || _playerMove == null
                            || _healthSlider == null || _staminaSlider == null)
        {
            return;
        }

        float health = _player.Health;
        float maxHealth = _player.MaxHealth;

        if (health != _lastHealth || maxHealth != _lastMaxHealth)
        {
            _lastHealth = health;
            _lastMaxHealth = maxHealth;
            _healthSlider.SetValueWithoutNotify(
                maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f);
        }

        float stamina = _playerMove.Stamina;

        if (stamina != _lastStamina)
        {
            _lastStamina = stamina;
            _staminaSlider.SetValueWithoutNotify(Mathf.Clamp01(stamina / PlayerMove.MaxStamina));
        }
    }
}