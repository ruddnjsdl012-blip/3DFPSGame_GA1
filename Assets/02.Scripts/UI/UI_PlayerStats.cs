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

        // 문제 : 체력값이 변하지 않았음에도 초당 60회 이상 캔버스를 불필요하게 다시 그린다.ㅏ -> 성능 낭비가 일어난다.
        // 요고 하나 정도는 괜찮아 보이지만.. 이런 불필요한 연산이 다른 UI에서 누적될수록 느려진다.
        // 해결 : 값이 바뀌엇을때만 캔버스 갱신을 한다.

        // 이떄 쓰는게 옵저버 패턴 
        // C#에서는 이것으르 Action으로 구현할 수 있다.
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