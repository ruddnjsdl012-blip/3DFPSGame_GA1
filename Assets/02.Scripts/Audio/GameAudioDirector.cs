using UnityEngine;

// 게임 전체 분위기 소리: 밤 환경음(바람, 낮은 울림), 호드/레벨업 알림음, 승리/패배 음악.
// GameManager가 시작할 때 자동으로 만든다.
[DisallowMultipleComponent]
public class GameAudioDirector : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float _ambienceVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float _stingerVolume = 0.8f;

    private AudioSource _ambience;
    private ZombieSpawnManager _spawner;
    private GameState _lastState = GameState.Ready;
    private float _ambienceTarget;

    private void Start()
    {
        _ambience = GameAudio.CreateLoop("amb_night_loop", transform, 0f, false);
        if (_ambience != null)
        {
            _ambience.Play();
        }

        _ambienceTarget = _ambienceVolume;
        TryBindSpawner();
    }

    private void TryBindSpawner()
    {
        if (_spawner != null)
        {
            return;
        }

        _spawner = FindAnyObjectByType<ZombieSpawnManager>();
        if (_spawner != null)
        {
            _spawner.HordeStarted += HandleHorde;
            _spawner.LevelChanged += HandleLevelUp;
        }
    }

    private void HandleHorde(int count)
    {
        GameAudio.Play2D("sting_horde", _stingerVolume, 0f);
    }

    private void HandleLevelUp(int level)
    {
        GameAudio.Play2D("sting_levelup", _stingerVolume * 0.7f, 0f);
    }

    private void Update()
    {
        TryBindSpawner();

        GameManager game = GameManager.Instance;
        if (game != null && game.State != _lastState)
        {
            GameState state = game.State;
            if (state == GameState.Clear)
            {
                GameAudio.Play2D("sting_victory", _stingerVolume, 0f);
                _ambienceTarget = _ambienceVolume * 0.3f;
            }
            else if (state == GameState.GameOver)
            {
                GameAudio.Play2D("sting_defeat", _stingerVolume, 0f);
                _ambienceTarget = _ambienceVolume * 0.3f;
            }
            else if (state == GameState.Pause)
            {
                _ambienceTarget = _ambienceVolume * 0.5f;
            }
            else
            {
                _ambienceTarget = _ambienceVolume;
            }

            _lastState = state;
        }

        if (_ambience != null)
        {
            // 일시 정지/게임 종료 때도 부드럽게 줄어들도록 unscaled 시간 사용
            float target = _ambienceTarget * GameAudio.MusicScale;
            _ambience.volume = Mathf.MoveTowards(_ambience.volume, target, Time.unscaledDeltaTime * 0.5f);
        }
    }

    private void OnDestroy()
    {
        if (_spawner != null)
        {
            _spawner.HordeStarted -= HandleHorde;
            _spawner.LevelChanged -= HandleLevelUp;
        }
    }
}
