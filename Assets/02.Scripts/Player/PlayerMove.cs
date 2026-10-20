using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    private const float Gravity = -20f;
    private float _yVelocity = 0f; // 위아래 속도 매 프레임마다 중력가속도가 붙는다.
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private float _jumpPower = 10f;


    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        // 1. 키보드 입력을 받는다.
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // 2. 이동 방향을 구하고 대각선 속도를 보정한다.
        Vector3 direction = new Vector3(h, 0f, v);
        direction.Normalize(); // 대각선이어도 방향의 길이 1로 바꾸기 위해
        direction = transform.TransformDirection(direction);
        // 로컬 좌표계 :  한 대상을 중심
        // 월드 좌표계 :  세상을 중심 

        // 중력을 누적한다.
        if (_characterController.isGrounded == false)
        {
            _yVelocity += Gravity * Time.deltaTime;
        }

        // 만약에 점프키를 누르면 점프를 한다. 
        if (Input.GetButtonDown("Jump") && _characterController.isGrounded)
        {
            _yVelocity = _jumpPower;
        }

        // 속도를 구한다
        Vector3 velocity = direction * _moveSpeed;
        velocity.y = _yVelocity;

        // 3. 속도(방향과 속력)에 따라 이동한다.
        // transform.position += direction * _moveSpeed * Time.deltaTime;
        _characterController.Move(velocity * Time.deltaTime);
    }
}