using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UI_Minimap : MonoBehaviour
{
    [SerializeField] private MinimapCamera _minimapCamera;
    [SerializeField] private Button _zoomInButton;
    [SerializeField] private Button _zoomOutButton;
    private bool _boundIn;
    private bool _boundOut;

    private void OnEnable()
    {
        if (_minimapCamera == null)
            _minimapCamera = FindFirstObjectByType<MinimapCamera>(FindObjectsInactive.Include);
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            int direction = GetZoomButtonDirection(button);
            if (direction > 0 && _zoomInButton == null) _zoomInButton = button;
            if (direction < 0 && _zoomOutButton == null) _zoomOutButton = button;
        }
        if (_zoomInButton != null && !HasPersistentZoom(_zoomInButton, "ZoomIn"))
        {
            _zoomInButton.onClick.AddListener(ZoomIn);
            _boundIn = true;
        }
        if (_zoomOutButton != null && !HasPersistentZoom(_zoomOutButton, "ZoomOut"))
        {
            _zoomOutButton.onClick.AddListener(ZoomOut);
            _boundOut = true;
        }
        if (_minimapCamera != null) _minimapCamera.ZoomChanged += RefreshButtons;
        RefreshButtons(0f);
    }

    private void OnDisable()
    {
        if (_boundIn && _zoomInButton != null) _zoomInButton.onClick.RemoveListener(ZoomIn);
        if (_boundOut && _zoomOutButton != null) _zoomOutButton.onClick.RemoveListener(ZoomOut);
        _boundIn = _boundOut = false;
        if (_minimapCamera != null) _minimapCamera.ZoomChanged -= RefreshButtons;
    }

    private bool HasPersistentZoom(Button button, string method)
    {
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentListenerState(i) == UnityEngine.Events.UnityEventCallState.Off) continue;
            Object target = button.onClick.GetPersistentTarget(i);
            if ((target == this || target == _minimapCamera) && button.onClick.GetPersistentMethodName(i) == method)
                return true;
        }
        return false;
    }

    public void ZoomIn() { if (_minimapCamera != null) _minimapCamera.ZoomIn(); }
    public void ZoomOut() { if (_minimapCamera != null) _minimapCamera.ZoomOut(); }

    private void RefreshButtons(float unused)
    {
        if (_zoomInButton != null) _zoomInButton.interactable = _minimapCamera != null && _minimapCamera.CanZoomIn;
        if (_zoomOutButton != null) _zoomOutButton.interactable = _minimapCamera != null && _minimapCamera.CanZoomOut;
    }

    public static int GetZoomButtonDirection(Button button)
    {
        if (button == null) return 0;
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            int direction = DirectionFromText(label.text);
            if (direction != 0) return direction;
        }
        foreach (Text label in button.GetComponentsInChildren<Text>(true))
        {
            int direction = DirectionFromText(label.text);
            if (direction != 0) return direction;
        }
        return DirectionFromText(button.name);
    }

    private static int DirectionFromText(string value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        value = value.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "");
        value = value.Replace("＋", "+").Replace("－", "-").Replace("−", "-").Replace("–", "-").Replace("—", "-");
        while (value.Contains("<"))
        {
            int begin = value.IndexOf('<');
            int end = value.IndexOf('>', begin);
            if (end < 0) break;
            value = value.Remove(begin, end - begin + 1);
        }
        if (value == "+" || value.Contains("zoomin") || value.Contains("plus") || value.Contains("확대")) return 1;
        if (value == "-" || value.Contains("zoomout") || value.Contains("minus") || value.Contains("축소")) return -1;
        return 0;
    }
}
