using UnityEngine;


// 미니맵 카메라: 플레이어 머리 위에서 아래를 내려다보며 따라다닌다.
public class MinimapCamera : MonoBehaviour
{
    private const float ZoomStep = 1f;

    private Camera _camera;
    private bool _originalCameraEnabled;
    private bool _initialized;
    private float _nextRenderTime;

    [SerializeField] private Transform _target;
    [SerializeField] private float _height = 8f;
    [SerializeField] private float _minZoom = 3f;
    [SerializeField] private float _maxZoom = 20f;
    [SerializeField, Range(5f, 60f)] private float _refreshRate = 15f;


    private void Start()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            enabled = false;
            return;
        }

        _originalCameraEnabled = _camera.enabled;
        _initialized = true;
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
        if (_camera == null) return;

        _camera.orthographicSize = Mathf.Clamp(size, _minZoom, _maxZoom);
        _nextRenderTime = 0f;
    }

    private void LateUpdate()
    {
        if (_target == null || _camera == null) return;

        // 1. 대상의 머리 위로 위치를 옮긴다.
        transform.position = _target.position + (Vector3.up * _height);

        // 2. 아래를 보되, 대상이 쳐다보는 방향으로 카메라도 회전한다.
        transform.eulerAngles = new Vector3(90, _target.eulerAngles.y, 0f);

        // RenderTexture는 마지막 화면을 유지한다. URP의 정상 렌더링 경로로 초당 15회만 갱신한다.
        if (_originalCameraEnabled && _camera.targetTexture != null)
        {
            bool render = Time.unscaledTime >= _nextRenderTime;
            _camera.enabled = render;
            if (render)
            {
                _nextRenderTime = Time.unscaledTime + 1f / Mathf.Max(1f, _refreshRate);
            }
        }
    }

    private void OnDisable()
    {
        if (_initialized && _camera != null)
        {
            _camera.enabled = _originalCameraEnabled;
            _nextRenderTime = 0f;
        }
    }

}
