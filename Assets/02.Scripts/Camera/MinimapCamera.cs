using UnityEngine;


// 미니맵 카메라: 플레이어 머리 위에서 아래를 내려다보며 따라다닌다.
public class MinimapCamera : MonoBehaviour
{
    private const float ZoomStep = 1f;

    private Camera _camera;

    [SerializeField] private Transform _target;
    [SerializeField] private float _height = 8f;
    [SerializeField] private float _minZoom = 3f;
    [SerializeField] private float _maxZoom = 20f;


    private void Start()
    {
        _camera = GetComponent<Camera>();
    }


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            ZoomIn();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            ZoomOut();
        }
    }

    public void ZoomIn()
    {
        SetZoom(_camera.orthographicSize - ZoomStep);
    }

    public void ZoomOut()
    {
        SetZoom(_camera.orthographicSize + ZoomStep);
    }

    private void SetZoom(float size)
    {
        _camera.orthographicSize = Mathf.Clamp(size, _minZoom, _maxZoom);
    }

    private void LateUpdate()
    {
        // 1. 대상의 머리 위로 위치를 옮긴다.
        transform.position = _target.position + (Vector3.up * _height);

        // 2. 아래를 보되, 대상이 쳐다보는 방향으로 카메라도 회전한다.
        transform.eulerAngles = new Vector3(90, _target.eulerAngles.y, 0f);
    }

}
