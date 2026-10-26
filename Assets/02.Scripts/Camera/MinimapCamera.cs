using UnityEngine;

// 미니맵 카메라 : 플레이어 머리 위에서 아래를 내려다보며 따라다닌다.
public class MinimapCamera : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private float _height = 8f;

    private void LateUpdate()
    {
        // 1. 대상의 머리 위로 위치를 옮긴다.
        transform.position = _target.position + Vector3.up * _height;
        // 2.  아래를 보되, 대상이 쳐다보는 방향으로 카메라도 회전한다.
        transform.eulerAngles = new Vector3(90f, _target.eulerAngles.y, 0f);
    }
}
