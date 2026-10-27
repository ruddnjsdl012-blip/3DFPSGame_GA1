using UnityEngine;
using UnityEngine.UI;

// 미니맵 UI: +/- 버튼을 누르면 미니맵 카메라를 확대/축소한다.
public class UI_Minimap : MonoBehaviour
{
    [SerializeField] private MinimapCamera _minimapCamera;
    [SerializeField] private Button _zoomInButton;
    [SerializeField] private Button _zoomOutButton;

    private void Start()
    {
        _zoomInButton.onClick.AddListener(_minimapCamera.ZoomIn);
        _zoomOutButton.onClick.AddListener(_minimapCamera.ZoomOut);
    }

    private void OnDestroy()
    {
        _zoomInButton.onClick.RemoveListener(_minimapCamera.ZoomIn);
        _zoomOutButton.onClick.RemoveListener(_minimapCamera.ZoomOut);
    }
}
