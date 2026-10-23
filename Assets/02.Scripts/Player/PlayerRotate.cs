using UnityEngine;

// 좌우 회전은 플레이어 한 곳에서만 처리한다.
[DefaultExecutionOrder(-50)]
public class PlayerRotate : MonoBehaviour
{
    private const float FullRotationAngle = 360f;

    [SerializeField] private float _rotationSpeed = 3f;

    private float _horizontalAngle;

    private void Awake()
    {
        _horizontalAngle = transform.localEulerAngles.y;
    }

    private void Update()
    {
        if (GameManager.IsPlaying == false)
        {
            return;
        }

        // Mouse X는 프레임 사이의 마우스 이동량이다.
        float mouseX = Input.GetAxis("Mouse X");
        _horizontalAngle = Mathf.Repeat(
            _horizontalAngle + mouseX * _rotationSpeed,
            FullRotationAngle);

        transform.localRotation = Quaternion.Euler(0f, _horizontalAngle, 0f);
    }
}
