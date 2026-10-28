using System;
using System.Collections;
using TMPro;
using UnityEngine;

public enum GameState
{
    Ready,
    Playing,
    GameOver,
    Clear       // 탈출 성공
}

public class GameManager : MonoBehaviour
{
    private static GameManager _instance = null;
    public static GameManager Instance => _instance;

    private GameState _state = GameState.Ready;
    public GameState State => _state;

    [SerializeField] private TextMeshProUGUI _stateText;
    [SerializeField] private Animator _playerAnimator;      // 게임 오버 때 군인을 서 있는 동작으로 돌린다.


    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    private void Start()
    {
        StartCoroutine(StartSequence_Coroutine());
    }

    private IEnumerator StartSequence_Coroutine()
    {
        _stateText.gameObject.SetActive(true);
        _stateText.text = "준비";
        yield return new WaitForSeconds(1.5f);
        _stateText.text = "시작!";
        yield return new WaitForSeconds(0.5f);
        _stateText.gameObject.SetActive(false);

        _state = GameState.Playing;
    }

    public void GameOver()
    {
        if (_state == GameState.Clear)
        {
            return;  // 이미 탈출했다.
        }

        _state = GameState.GameOver;

        _stateText.gameObject.SetActive(true);
        _stateText.text = "게임오버";
        _stateText.color = Color.red;
        GameSounds.Play2D("sting_defeat", 0.8f, 0f);

        _playerAnimator.SetFloat("MoveMotion", 0f);  // 달리던 중이었어도 서 있는 동작으로 돌린다.
    }

    // 탈출 성공. 구조 헬기에 타면 AIDirector가 부른다.
    public void GameClear()
    {
        if (_state != GameState.Playing)
        {
            return;
        }

        _state = GameState.Clear;

        _stateText.gameObject.SetActive(true);
        _stateText.text = "탈출 성공!";
        _stateText.color = Color.green;
        GameSounds.Play2D("sting_victory", 0.8f, 0f);

        _playerAnimator.SetFloat("MoveMotion", 0f);
    }

}
