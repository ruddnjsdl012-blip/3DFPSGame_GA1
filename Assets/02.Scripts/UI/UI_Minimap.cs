using UnityEngine;
using UnityEngine.UI;

// 미니맵의 +/- 버튼을 누르면 미니맵 카메라를 줌인/줌아웃한다.
[DisallowMultipleComponent]
public class UI_Minimap : MonoBehaviour
{
    [SerializeField] private MinimapCamera _minimapCamera;
    [SerializeField] private Button _zoomInButton;
    [SerializeField] private Button _zoomOutButton;

    private void Awake()
    {
        if (_minimapCamera == null || _zoomInButton == null || _zoomOutButton == null)
        {
            Debug.LogError("UI_Minimap: Minimap Camera와 +/- 버튼을 연결하세요.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_minimapCamera == null || _zoomInButton == null || _zoomOutButton == null)
        {
            return;
        }

        _zoomInButton.onClick.AddListener(_minimapCamera.ZoomIn);
        _zoomOutButton.onClick.AddListener(_minimapCamera.ZoomOut);
    }

    private void OnDisable()
    {
        if (_minimapCamera == null || _zoomInButton == null || _zoomOutButton == null)
        {
            return;
        }

        _zoomInButton.onClick.RemoveListener(_minimapCamera.ZoomIn);
        _zoomOutButton.onClick.RemoveListener(_minimapCamera.ZoomOut);
    }
}
