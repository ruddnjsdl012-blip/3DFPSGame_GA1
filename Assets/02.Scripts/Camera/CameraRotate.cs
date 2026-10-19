using UnityEngine;

// 역할: 마우스 이동에 따라 카메라를 회전한다.
public class CameraRotate : MonoBehaviour
{
    private const float MinVerticalAngle = -90f;
    private const float MaxVerticalAngle = 90f;
    private const float FullRotationAngle = 360f;

    [SerializeField] private float _rotationSpeed = 3f;

    private float _horizontalAngle;
    private float _verticalAngle;

    private void Awake()
    {
        // 씬에 설정된 카메라의 시작 각도를 유지한다.
        Vector3 rotation = transform.eulerAngles;

        _horizontalAngle = rotation.y;
        _verticalAngle = Mathf.DeltaAngle(0f, rotation.x);
        _verticalAngle = Mathf.Clamp(
            _verticalAngle,
            MinVerticalAngle,
            MaxVerticalAngle);
    }

    private void Update()
    {
        // 마우스 이동량을 읽는다.
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // 좌우 회전은 계속 가능하도록 누적한다.
        _horizontalAngle += mouseX * _rotationSpeed;
        _horizontalAngle = Mathf.Repeat(
            _horizontalAngle,
            FullRotationAngle);

        // 상하 회전만 -90도에서 90도 사이로 제한한다.
        _verticalAngle -= mouseY * _rotationSpeed;
        _verticalAngle = Mathf.Clamp(
            _verticalAngle,
            MinVerticalAngle,
            MaxVerticalAngle);

        // X축에 상하 회전, Y축에 좌우 회전을 적용한다.
        transform.eulerAngles = new Vector3(
            _verticalAngle,
            _horizontalAngle,
            0f);
    }
}