using UnityEngine;

// 역할: 현재 상대 위치를 유지하면서 대상을 따라간다.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform _target;

    private Vector3 _positionOffset;

    private void Awake()
    {
        if (_target == null)
        {
            Debug.LogError(
                "CameraFollow의 Target에 Nose를 연결해 주세요.",
                this);

            enabled = false;
            return;
        }

        // 대상의 회전을 기준으로 시작 상대 위치를 기억한다.
        _positionOffset = Quaternion.Inverse(_target.rotation)
                          * (transform.position - _target.position);
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            return;
        }

        // 대상이 이동하거나 회전해도 상대 위치를 유지한다.
        transform.position = _target.position
                             + _target.rotation * _positionOffset;
    }
}