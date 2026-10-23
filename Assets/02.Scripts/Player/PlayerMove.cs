using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    private const float Gravity = -20f;
    private const int MaxJumps = 2;
    public const float MaxStamina = 100f;

    private float _yVelocity = 0f;
    private int _jumpCount = 0;
    private float _stamina = 0f;

    public float Stamina => _stamina;

    [Header("스탯")]
    [SerializeField] private float _moveSpeed = 7f;
    [SerializeField] private float _sprintSpeed = 12f;
    [SerializeField] private float _jumpPower = 10f;
    [SerializeField] private float _staminaRecovery = 15f;
    [SerializeField] private float _sprintCost = 10f;
    [SerializeField] private float _jumpCost = 20f;

    [Header("컴포넌트")]
    [SerializeField] private CharacterController _characterController;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _stamina = MaxStamina;
    }

    private void Update()
    {
        bool usedStamina = false;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3(h, 0f, v);
        direction.Normalize();
        direction = transform.TransformDirection(direction);

        if (_characterController.isGrounded == false)
        {
            _yVelocity += Gravity * Time.deltaTime;
        }
        else
        {
            _yVelocity = -1f;
            _jumpCount = 0;
        }

        // 점프
        if (Input.GetButtonDown("Jump")
            && _jumpCount < MaxJumps
            && _stamina >= _jumpCost)
        {
            _yVelocity = _jumpPower;
            _jumpCount++;

            _stamina -= _jumpCost;
            usedStamina = true;
        }

        // 달리기
        float requiredStamina = _sprintCost * Time.deltaTime;

        bool isSprint = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                        && _stamina > requiredStamina;

        float currentSpeed = _moveSpeed;

        if (isSprint)
        {
            currentSpeed = _sprintSpeed;

            _stamina -= requiredStamina;
            usedStamina = true;
        }

        // 소비하지 않는 프레임에만 회복한다.
        if (usedStamina == false)
        {
            _stamina = Mathf.Min(
                MaxStamina,
                _stamina + _staminaRecovery * Time.deltaTime);
        }

        Vector3 velocity = direction * currentSpeed;
        velocity.y = _yVelocity;

        _characterController.Move(velocity * Time.deltaTime);
    }
}