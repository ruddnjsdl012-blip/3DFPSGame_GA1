using UnityEngine;

// 플레이어 몸에서 나는 소리: 발소리(걷기/달리기), 점프, 착지, 체력이 낮을 때 심장 박동.
// Player가 시작할 때 자동으로 붙인다.
[DisallowMultipleComponent]
public class PlayerAudio : MonoBehaviour
{
    [Header("발소리")]
    [SerializeField, Min(0.5f)] private float _walkStride = 2.1f;
    [SerializeField, Min(0.5f)] private float _runStride = 2.8f;
    [SerializeField, Min(0f)] private float _runSpeed = 9f;
    [SerializeField, Range(0f, 1f)] private float _stepVolume = 0.55f;

    [Header("심장 박동")]
    [Tooltip("체력이 이 비율보다 낮으면 심장 소리가 들린다.")]
    [SerializeField, Range(0f, 1f)] private float _heartbeatBelow = 0.35f;

    private CharacterController _controller;
    private Player _player;
    private AudioSource _heartbeat;
    private float _distance;
    private bool _wasGrounded = true;
    private float _airTime;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _player = GetComponent<Player>();
    }

    private void Start()
    {
        _heartbeat = GameAudio.CreateLoop("player_heartbeat", transform, 0f, false);
    }

    private void Update()
    {
        bool playing = GameManager.IsPlaying;
        UpdateHeartbeat(playing);

        if (playing == false || _controller == null)
        {
            return;
        }

        float dt = Time.deltaTime;
        bool grounded = _controller.isGrounded;
        Vector3 velocity = _controller.velocity;
        float speed = new Vector2(velocity.x, velocity.z).magnitude;

        // 걸은 거리만큼 발소리
        if (grounded && speed > 1.5f)
        {
            _distance += speed * dt;
            bool running = speed > _runSpeed;
            if (_distance >= (running ? _runStride : _walkStride))
            {
                _distance = 0f;
                GameAudio.Play2D("step", _stepVolume * (running ? 1.15f : 1f), 0.08f);
            }
        }

        // 점프 / 착지
        if (_wasGrounded && grounded == false && velocity.y > 2f)
        {
            GameAudio.Play2D("player_jump", 0.5f, 0.05f);
        }

        if (grounded == false)
        {
            _airTime += dt;
        }
        else
        {
            if (_wasGrounded == false && _airTime > 0.25f)
            {
                GameAudio.Play2D("player_land", Mathf.Clamp(_airTime, 0.35f, 0.9f), 0.05f);
                _distance = 0f;
            }

            _airTime = 0f;
        }

        _wasGrounded = grounded;
    }

    private void UpdateHeartbeat(bool playing)
    {
        if (_heartbeat == null || _player == null)
        {
            return;
        }

        float ratio = _player.MaxHealth > 0f ? _player.Health / _player.MaxHealth : 1f;
        bool low = playing && ratio > 0f && ratio < _heartbeatBelow;
        if (low)
        {
            float danger = 1f - ratio / _heartbeatBelow; // 0 ~ 1
            _heartbeat.volume = Mathf.Lerp(0.25f, 0.85f, danger) * GameAudio.SfxScale;
            _heartbeat.pitch = Mathf.Lerp(1f, 1.35f, danger);
            if (_heartbeat.isPlaying == false)
            {
                _heartbeat.Play();
            }
        }
        else if (_heartbeat.isPlaying)
        {
            _heartbeat.Stop();
        }
    }
}
