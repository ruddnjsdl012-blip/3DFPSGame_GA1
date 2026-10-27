using Input = GameInput;
using UnityEngine;

// CameraPosition에 하나만 붙인다. 좌우는 Player를 상속하고 상하만 회전한다.
[DefaultExecutionOrder(-40)]
public class CameraRotate : MonoBehaviour
{
    private const float MinVerticalAngle = -90f;
    private const float MaxVerticalAngle = 90f;

    [SerializeField] private float _rotationSpeed = 3f;

    private float _verticalAngle;

    // 이번 프레임에 마우스로 움직인 위아래 각도 (+: 아래로). 반동 회복 계산에 쓴다.
    public float LastMouseDelta { get; private set; }

    // 정조준 등에서 마우스 감도를 바꿀 때 쓰는 배율 (1 = 그대로)
    public float SensitivityMultiplier { get; set; } = 1f;

    private void Awake()
    {
        _verticalAngle = Mathf.Clamp(
            Mathf.DeltaAngle(0f, transform.localEulerAngles.x),
            MinVerticalAngle,
            MaxVerticalAngle);
    }

    private void Update()
    {
        if (GameManager.IsPlaying == false)
        {
            return;
        }

        // 부모의 좌우 회전과 충돌하지 않도록 로컬 X축만 갱신한다.
        // 설정의 마우스 감도와 위아래 반전을 적용한다.
        float mouseY = Input.GetAxis("Mouse Y") * GameSettings.MouseSensitivity * (GameSettings.InvertY ? -1f : 1f);
        float before = _verticalAngle;
        _verticalAngle = Mathf.Clamp(
            _verticalAngle - mouseY * _rotationSpeed * SensitivityMultiplier,
            MinVerticalAngle,
            MaxVerticalAngle);
        LastMouseDelta = _verticalAngle - before;

        transform.localRotation = Quaternion.Euler(_verticalAngle, 0f, 0f);
    }

    // 총 반동: 음수면 위로, 양수면 아래로 시점을 돌린다.
    public void AddRecoil(float pitchDegrees)
    {
        _verticalAngle = Mathf.Clamp(_verticalAngle + pitchDegrees, MinVerticalAngle, MaxVerticalAngle);
        transform.localRotation = Quaternion.Euler(_verticalAngle, 0f, 0f);
    }
}
