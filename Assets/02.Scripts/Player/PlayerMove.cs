using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMove : MonoBehaviour
{
    private const float Gravity = -20f;
    private const float GroundedYVelocity = -2f;
    private const int MaxJumpCount = 2;

    [Header("기존 이동 설정")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private float _jumpPower = 10f;

    [Header("질주")]
    [SerializeField, Min(0f)] private float _sprintSpeed = 12f;

    [Header("스태미나 설정")]
    [SerializeField, Min(1f)] private float _maxStamina = 100f;
    [SerializeField, Min(0f)] private float _sprintStaminaCostPerSecond = 20f;
    [SerializeField, Min(0f)] private float _jumpStaminaCost = 20f;
    [SerializeField, Min(0f)] private float _staminaRecoveryPerSecond = 15f;
    [SerializeField, Min(0f)] private float _staminaRecoveryDelay = 1f;

    [Header("현재 스태미나: 실행 중 확인")]
    [SerializeField] private float _currentStamina;

    private float _yVelocity = 0f;
    private float _staminaRecoveryTimer;
    private int _jumpCount;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();

        if (_characterController == null)
        {
            Debug.LogError("Player에 CharacterController가 필요합니다.", this);
            enabled = false;
            return;
        }

        _currentStamina = _maxStamina;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        if (_characterController.enabled == false || deltaTime <= 0f)
        {
            return;
        }

        // 기존 키보드 입력과 플레이어 기준 이동 방향을 유지한다.
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3(h, 0f, v);
        direction.Normalize();
        direction = transform.TransformDirection(direction);

        // 상승 중에는 이전 프레임의 접지 정보로 점프 횟수를 초기화하지 않는다.
        bool isGrounded = _characterController.isGrounded && _yVelocity <= 0f;

        if (isGrounded)
        {
            _jumpCount = 0;
            _yVelocity = GroundedYVelocity;
        }
        else
        {
            _yVelocity += Gravity * deltaTime;

            // 발판에서 걸어 떨어졌을 때도 공중 점프는 한 번만 허용한다.
            if (_jumpCount == 0)
            {
                _jumpCount = 1;
            }
        }

        _staminaRecoveryTimer = Mathf.Max(0f, _staminaRecoveryTimer - deltaTime);

        // 지상 점프와 공중 점프 모두 같은 점프력과 스태미나 비용을 적용한다.
        bool canJump = _jumpCount < MaxJumpCount
                       && _currentStamina > 0f
                       && _currentStamina >= _jumpStaminaCost;
        bool hasJumped = Input.GetButtonDown("Jump") && canJump;

        if (hasJumped)
        {
            _yVelocity = _jumpPower;
            _jumpCount++;
            UseStamina(_jumpStaminaCost);
        }

        // 왼쪽 Shift를 누른 채 이동할 때만 질주하고 스태미나를 소모한다.
        bool wantsToSprint = Input.GetKey(KeyCode.LeftShift)
                             && direction.sqrMagnitude > 0f;
        bool isSprinting = wantsToSprint && _currentStamina > 0f;
        float currentMoveSpeed = isSprinting ? _sprintSpeed : _moveSpeed;

        if (isSprinting)
        {
            UseStamina(_sprintStaminaCostPerSecond * deltaTime);
        }
        else if (wantsToSprint == false && hasJumped == false
                 && _staminaRecoveryTimer <= 0f)
        {
            // Shift를 놓거나 이동을 멈추면 마지막 소모 후 일정 시간이 지나 회복한다.
            // 고갈 후 Shift를 계속 누를 때 달리기와 걷기가 반복되는 현상도 막는다.
            _currentStamina = Mathf.Min(
                _maxStamina,
                _currentStamina + _staminaRecoveryPerSecond * deltaTime);
        }

        // 기존 CharacterController 이동에 질주 속도와 점프 속도를 함께 적용한다.
        Vector3 velocity = direction * currentMoveSpeed;
        velocity.y = _yVelocity;

        CollisionFlags collisionFlags = _characterController.Move(velocity * deltaTime);

        // 천장에 닿으면 상승을 멈추고 다음 프레임부터 중력으로 내려온다.
        if ((collisionFlags & CollisionFlags.Above) != CollisionFlags.None
            && _yVelocity > 0f)
        {
            _yVelocity = 0f;
        }
    }

    private void UseStamina(float amount)
    {
        _currentStamina = Mathf.Max(0f, _currentStamina - amount);
        _staminaRecoveryTimer = _staminaRecoveryDelay;
    }
}