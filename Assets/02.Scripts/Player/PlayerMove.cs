using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 5f;

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

        // 3. 방향과 속력에 따라 이동한다.
        transform.position += direction * _moveSpeed * Time.deltaTime;
    }
}