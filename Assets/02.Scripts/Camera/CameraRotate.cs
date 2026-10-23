using UnityEngine;

// CameraPosition에 하나만 붙인다. 좌우는 Player를 상속하고 상하만 회전한다.
[DefaultExecutionOrder(-40)]
public class CameraRotate : MonoBehaviour
{
    private const float MinVerticalAngle = -90f;
    private const float MaxVerticalAngle = 90f;

    [SerializeField] private float _rotationSpeed = 3f;

    private float _verticalAngle;

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
        float mouseY = Input.GetAxis("Mouse Y");
        _verticalAngle = Mathf.Clamp(
            _verticalAngle - mouseY * _rotationSpeed,
            MinVerticalAngle,
            MaxVerticalAngle);

        transform.localRotation = Quaternion.Euler(_verticalAngle, 0f, 0f);
    }
}
