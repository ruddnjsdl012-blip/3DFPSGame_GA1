using Input = GameInput;
using UnityEngine;

// 좌우 회전은 플레이어 한 곳에서만 처리한다.
[DefaultExecutionOrder(-50)]
public class PlayerRotate : MonoBehaviour
{
    private const float FullRotationAngle = 360f;

    [SerializeField] private float _rotationSpeed = 3f;

    private float _horizontalAngle;

    // 정조준 등에서 마우스 감도를 바꿀 때 쓰는 배율 (1 = 그대로)
    public float SensitivityMultiplier { get; set; } = 1f;

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
        // 설정의 마우스 감도를 적용한다.
        float mouseX = Input.GetAxis("Mouse X") * GameSettings.MouseSensitivity;
        _horizontalAngle = Mathf.Repeat(
            _horizontalAngle + mouseX * _rotationSpeed * SensitivityMultiplier,
            FullRotationAngle);

        transform.localRotation = Quaternion.Euler(0f, _horizontalAngle, 0f);
    }

    // 총 반동: 좌우로 시점을 돌린다.
    public void AddRecoil(float yawDegrees)
    {
        _horizontalAngle = Mathf.Repeat(_horizontalAngle + yawDegrees, FullRotationAngle);
        transform.localRotation = Quaternion.Euler(0f, _horizontalAngle, 0f);
    }
}
