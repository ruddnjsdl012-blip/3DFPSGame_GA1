using System;
using UnityEngine;

// The existing 0/9 shortcuts and UI buttons share clamped orthographic zoom.
[RequireComponent(typeof(Camera))]
public class MinimapCamera : MonoBehaviour
{
    private Camera _camera;
    [SerializeField] private Transform _target;
    [SerializeField] private float _height = 8f;
    [Header("미니맵 줌")]
    [SerializeField, Min(0.1f)] private float _minZoom = 2f;
    [SerializeField, Min(0.1f)] private float _maxZoom = 40f;
    [SerializeField, Min(0.1f)] private float _zoomStep = 2f;

    public event Action<float> ZoomChanged;
    public float CurrentZoom { get { return GetCamera().orthographicSize; } }
    public bool CanZoomIn { get { return CurrentZoom > MinimumZoom + 0.001f; } }
    public bool CanZoomOut { get { return CurrentZoom < MaximumZoom - 0.001f; } }
    private float MinimumZoom { get { return Mathf.Max(0.1f, _minZoom); } }
    private float MaximumZoom { get { return Mathf.Max(MinimumZoom, _maxZoom); } }

    private Camera GetCamera()
    {
        if (_camera == null) _camera = GetComponent<Camera>();
        return _camera;
    }

    private void Awake()
    {
        GetCamera().orthographic = true;
        SetZoom(CurrentZoom);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0)) ZoomIn();
        else if (Input.GetKeyDown(KeyCode.Alpha9)) ZoomOut();
    }

    public void ZoomIn() { SetZoom(CurrentZoom - Mathf.Max(0.1f, _zoomStep)); }
    public void ZoomOut() { SetZoom(CurrentZoom + Mathf.Max(0.1f, _zoomStep)); }

    public void SetZoom(float size)
    {
        if (float.IsNaN(size) || float.IsInfinity(size)) return;
        Camera camera = GetCamera();
        camera.orthographicSize = Mathf.Clamp(size, MinimumZoom, MaximumZoom);
        ZoomChanged?.Invoke(camera.orthographicSize);
    }

    private void LateUpdate()
    {
        if (_target == null) return;
        transform.position = _target.position + Vector3.up * _height;
        transform.eulerAngles = new Vector3(90f, _target.eulerAngles.y, 0f);
    }

    private void OnValidate()
    {
        _minZoom = Mathf.Max(0.1f, _minZoom);
        _maxZoom = Mathf.Max(_minZoom, _maxZoom);
        _zoomStep = Mathf.Max(0.1f, _zoomStep);
    }
}
