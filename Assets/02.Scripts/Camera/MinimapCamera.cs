using UnityEngine;

// 미니맵 카메라 : 플레이어 머리 위에서 아래를 내려다보며 따라다닌다.
[RequireComponent(typeof(Camera))]
public class MinimapCamera : MonoBehaviour
{
    private Camera _camera;
    [SerializeField] private Transform _target;
    [SerializeField] private float _height = 8f;

    [Header("줌")]
    [SerializeField] private float _zoomStep = 1f;
    [SerializeField] private float _minSize = 3f;
    [SerializeField] private float _maxSize = 20f;

    private void Awake()
    {
        _camera = GetComponent<Camera>();

        if (_target == null)
        {
            Debug.LogError("MinimapCamera: Target에 플레이어를 연결하세요.", this);
            enabled = false;
            return;
        }

        // 원근 카메라는 orthographicSize가 적용되지 않으므로 직교로 맞춘다.
        _camera.orthographic = true;
        _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize, _minSize, _maxSize);
    }

    private void Update()
    {
        // 키보드 +/- (숫자패드 포함)로도 줌할 수 있다.
        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
        {
            ZoomIn();
        }
        else if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
        {
            ZoomOut();
        }
    }

    private void LateUpdate()
    {
        // 1. 대상의 머리 위로 위치를 옮긴다.
        transform.position = _target.position + Vector3.up * _height;
        // 2.  아래를 보되, 대상이 쳐다보는 방향으로 카메라도 회전한다.
        transform.eulerAngles = new Vector3(90f, _target.eulerAngles.y, 0f);
    }

    // 보이는 범위를 좁혀 크게 보여 준다.
    public void ZoomIn()
    {
        SetSize(_camera.orthographicSize - _zoomStep);
    }

    // 보이는 범위를 넓혀 작게 보여 준다.
    public void ZoomOut()
    {
        SetSize(_camera.orthographicSize + _zoomStep);
    }

    private void SetSize(float size)
    {
        _camera.orthographicSize = Mathf.Clamp(size, _minSize, _maxSize);
    }
}
