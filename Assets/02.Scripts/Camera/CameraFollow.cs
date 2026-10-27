using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform _target;

    private void Awake()
    {
        if (_target == null)
        {
            Debug.LogWarning("[CameraFollow] 카메라 타겟이 없습니다!!");
        }
    }

    private void LateUpdate()
    {
        transform.position = _target.position;
    }
}