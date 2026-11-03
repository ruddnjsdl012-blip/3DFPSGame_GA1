using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ESC를 누르면 일시정지 팝업을 열고 닫는다.
// 팝업 오브젝트는 꺼져 있으므로, 이 컴포넌트는 항상 켜져 있는 Canvas에 붙인다.
public class UI_OptionPopup : MonoBehaviour
{
    [SerializeField] private GameObject _popup;
    [SerializeField] private Button _continueButton;
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _quitButton;
    [SerializeField] private CursorManager _cursorManager;

    public bool IsOpen => _popup != null && _popup.activeSelf;

    private void Awake()
    {
        if (_continueButton != null)
        {
            _continueButton.onClick.AddListener(Close);
        }

        if (_restartButton != null)
        {
            _restartButton.onClick.AddListener(Restart);
        }

        if (_quitButton != null)
        {
            _quitButton.onClick.AddListener(Quit);
        }

        if (_popup != null)
        {
            _popup.SetActive(false);
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (IsOpen)
        {
            Close();
        }
        else if (GameManager.Instance.State == GameState.Playing)
        {
            Open();
        }
    }

    public void Open()
    {
        _popup.SetActive(true);
        Time.timeScale = 0f;
        if (_cursorManager != null) _cursorManager.UnlockCursor();
    }

    public void Close()
    {
        _popup.SetActive(false);
        Time.timeScale = 1f;
        if (_cursorManager != null) _cursorManager.LockCursor();
    }

    // 지금 씬을 처음부터 다시 불러온다. 멈춘 시간은 먼저 되돌린다.
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // 빌드에서는 게임을 끄고, 에디터에서는 플레이 모드를 끝낸다.
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
