using UnityEngine;

// 발소리, 점프, 착지 소리. (GunFeedback이 Player에 자동으로 붙인다)
public class PlayerFootsteps : MonoBehaviour
{
    [SerializeField] private float _stepDistance = 2.2f;  // 이만큼 걸을 때마다 발소리
    [SerializeField] private float _volume = 0.35f;

    private CharacterController _controller;
    private float _distance;
    private bool _wasGrounded = true;
    private float _airTime;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (_controller == null || GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        bool grounded = _controller.isGrounded;
        Vector3 velocity = _controller.velocity;
        float speed = new Vector2(velocity.x, velocity.z).magnitude;

        if (grounded)
        {
            if (!_wasGrounded && _airTime > 0.25f)
            {
                GameSounds.Play2D("player_land", _volume * 1.3f);
            }

            _airTime = 0f;
            _distance += speed * Time.deltaTime;
            if (_distance >= _stepDistance)
            {
                _distance = 0f;
                GameSounds.Play2D("step", _volume, 0.1f);
            }
        }
        else
        {
            if (_wasGrounded && velocity.y > 1f)
            {
                GameSounds.Play2D("player_jump", _volume);
            }

            _airTime += Time.deltaTime;
        }

        _wasGrounded = grounded;
    }
}
