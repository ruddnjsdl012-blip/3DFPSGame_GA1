using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    private static GameManager _instance;
    public static GameManager Instance => _instance;
    public static bool IsPlaying => _instance != null && _instance._state == GameState.Playing;

    [Header("화면 연결")]
    [SerializeField] private TextMeshProUGUI _stateText;
    [SerializeField] private TextMeshProUGUI _hintText;
    [SerializeField] private Button _restartButton;
    [SerializeField] private CursorManager _cursorManager;
    [Header("시작 연출")]
    [Tooltip("켜면 준비 2초 대신 3, 2, 1을 각각 1초 보여 줍니다.")]
    [SerializeField] private bool _useCountdown;
    [Header("승리 조건")]
    [Tooltip("이 수만큼 좀비를 처치하거나, 도착 안전가옥에 들어가면 승리한다.")]
    [SerializeField, Min(1)] private int _targetKills = 50;

    private readonly HashSet<Zombie> _livingZombies = new HashSet<Zombie>();
    private GameState _state = GameState.Ready;
    private bool _restarting;
    private int _killCount;

    public GameState State => _state;
    public int RemainingZombies => _livingZombies.Count;
    public int KillCount => _killCount;
    public int TargetKills => _targetKills;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
        Time.timeScale = 1f;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        _state = GameState.Ready;
        Time.timeScale = 1f;

        if (_stateText == null || _restartButton == null || _cursorManager == null)
        {
            Debug.LogError("GameManager: State Text, Restart Button, Cursor Manager 연결을 확인하세요.", this);
            enabled = false;
            return;
        }

        _restartButton.gameObject.SetActive(false);
        SetHint(null);
        ShowMessage("준비", Color.white);
    }

    private void Start()
    {
        // 시작할 때 한 번만 검색한다. 죽을 때의 알림으로 남은 수를 줄인다.
        Zombie[] zombies = FindObjectsByType<Zombie>(FindObjectsSortMode.None);

        foreach (Zombie zombie in zombies)
        {
            _livingZombies.Add(zombie);
        }

        // 씬에 스폰 매니저가 없으면 만든다. (스포너는 플레이할 때마다 무작위 위치에 생긴다)
        if (FindAnyObjectByType<ZombieSpawnManager>() == null)
        {
            new GameObject("ZombieSpawnManager").AddComponent<ZombieSpawnManager>();
        }

        _cursorManager.LockCursor();
        StartCoroutine(StartSequence_Coroutine());
    }

    private void Update()
    {
        if (_state == GameState.GameOver || _state == GameState.Clear)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                RestartGame();
            }

            return;
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            TogglePause();
        }
    }

    private IEnumerator StartSequence_Coroutine()
    {
        if (_useCountdown)
        {
            for (int number = 3; number > 0; number--)
            {
                yield return StartCoroutine(ShowText_Coroutine(number.ToString(), 1f));
            }
        }
        else
        {
            yield return StartCoroutine(ShowText_Coroutine("준비", 2f));
        }

        yield return StartCoroutine(ShowText_Coroutine("시작!", 0.5f));

        if (_state != GameState.Ready)
        {
            yield break;
        }

        _stateText.gameObject.SetActive(false);
        ChangeState(GameState.Playing);
    }

    private IEnumerator ShowText_Coroutine(string text, float duration)
    {
        ShowMessage(text, Color.white);
        yield return new WaitForSeconds(duration);
    }

    public void GameOver()
    {
        if (_state == GameState.GameOver || _state == GameState.Clear)
        {
            return;
        }

        ShowResult(GameState.GameOver, "게임 오버", Color.red);
    }

    // 플레이어가 도착 안전가옥에 들어오면 SafeRoomGoal이 부른다.
    public void CompleteEscape()
    {
        if (_state != GameState.Playing)
        {
            return;
        }

        ShowResult(GameState.Clear, "탈출 성공!", Color.green);
    }

    // 스포너가 새로 만든 좀비를 등록한다.
    public void RegisterZombie(Zombie zombie)
    {
        if (zombie != null)
        {
            _livingZombies.Add(zombie);
        }
    }

    public void NotifyZombieDefeated(Zombie zombie)
    {
        // 같은 좀비가 두 번 알려도 처치 수는 한 번만 늘어난다.
        if (zombie == null || _livingZombies.Remove(zombie) == false)
        {
            return;
        }

        _killCount++;

        if (_killCount >= _targetKills && _state == GameState.Playing)
        {
            ShowResult(GameState.Clear, "승리!", Color.green);
        }
    }

    private void ShowResult(GameState state, string message, Color color)
    {
        StopAllCoroutines();
        ChangeState(state);
        Time.timeScale = 0f;
        ShowMessage(message, color);
        _restartButton.gameObject.SetActive(true);
        SetHint("R키로도 다시 시작할 수 있습니다");
        _cursorManager.UnlockCursor();
    }

    public void TogglePause()
    {
        if (_state == GameState.Playing)
        {
            ChangeState(GameState.Pause);
            Time.timeScale = 0f;
            ShowMessage("일시 정지", Color.white);
            SetHint("P키: 계속 하기");
            _cursorManager.UnlockCursor();
        }
        else if (_state == GameState.Pause)
        {
            Time.timeScale = 1f;
            ChangeState(GameState.Playing);
            _stateText.gameObject.SetActive(false);
            SetHint(null);
            _cursorManager.LockCursor();
        }
    }

    // 씬에 저장한 Button.OnClick과 R키가 같은 함수를 사용한다.
    public void RestartGame()
    {
        if (_restarting || (_state != GameState.GameOver && _state != GameState.Clear))
        {
            return;
        }

        _restarting = true;
        StopAllCoroutines();
        _restartButton.interactable = false;
        Time.timeScale = 1f;
        _cursorManager.LockCursor();
        SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }

    private void ShowMessage(string message, Color color)
    {
        _stateText.text = message;
        _stateText.color = color;
        _stateText.gameObject.SetActive(true);
    }

    private void SetHint(string message)
    {
        if (_hintText == null)
        {
            return;
        }

        bool show = string.IsNullOrEmpty(message) == false;
        _hintText.gameObject.SetActive(show);

        if (show)
        {
            _hintText.text = message;
        }
    }

    private void ChangeState(GameState nextState)
    {
        Debug.Log($"게임 상태: {_state} -> {nextState}");
        _state = nextState;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            Time.timeScale = 1f;
        }
    }
}