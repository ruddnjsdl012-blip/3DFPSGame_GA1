using UnityEngine;

// 역할: 마우스 좌우 이동에 따라 플레이어를 회전한다.
public class PlayerRotate : MonoBehaviour
{
    private const float FullRotationAngle = 360f;

    [SerializeField] private float _rotationSpeed = 3f;

    private float _horizontalAngle;

    private void Awake()
    {
        _horizontalAngle = transform.eulerAngles.y;
    }

    private void Update()
    {
        float mouseX = Input.GetAxis("Mouse X");

        _horizontalAngle += mouseX * _rotationSpeed;
        _horizontalAngle = Mathf.Repeat(
            _horizontalAngle,
            FullRotationAngle);

        // 플레이어는 Y축으로만 회전한다.
        transform.eulerAngles = new Vector3(
            0f,
            _horizontalAngle,
            0f);
    }
}