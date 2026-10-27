using UnityEngine;

// 플레이어를 밀어낸다. (탱크 주먹, 부머 폭발)
// PlayerMove의 이동과 따로 CharacterController를 움직이고, 밀리는 힘은 금방 줄어든다.
// PlayerKnockback.Push(플레이어, 힘)로 부르면 플레이어에 자동으로 붙는다.
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerKnockback : MonoBehaviour
{
    private const float Gravity = -20f;

    [Tooltip("밀리는 힘이 줄어드는 빠르기")]
    [SerializeField, Min(0.1f)] private float _drag = 4.5f;

    private CharacterController _controller;
    private Vector3 _velocity;

    public bool IsPushed => _velocity.sqrMagnitude > 0.25f;

    public static void Push(GameObject player, Vector3 velocity)
    {
        if (player == null)
        {
            return;
        }

        CharacterController controller = player.GetComponentInParent<CharacterController>();
        if (controller == null)
        {
            return;
        }

        PlayerKnockback knockback = controller.GetComponent<PlayerKnockback>();
        if (knockback == null)
        {
            knockback = controller.gameObject.AddComponent<PlayerKnockback>();
        }

        knockback._velocity += velocity;
    }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (_velocity.sqrMagnitude < 0.0004f || _controller == null || _controller.enabled == false)
        {
            _velocity = Vector3.zero;
            return;
        }

        float dt = Time.deltaTime;
        Vector3 horizontal = new Vector3(_velocity.x, 0f, _velocity.z) * Mathf.Exp(-_drag * dt);
        float vertical = _velocity.y > 0f ? _velocity.y + Gravity * dt : 0f;
        _velocity = new Vector3(horizontal.x, Mathf.Max(0f, vertical), horizontal.z);

        CollisionFlags flags = _controller.Move(_velocity * dt);
        if ((flags & CollisionFlags.Above) != 0)
        {
            _velocity.y = 0f;
        }

        if ((flags & CollisionFlags.Sides) != 0)
        {
            // 벽에 부딪히면 수평 힘을 많이 잃는다.
            _velocity.x *= 0.5f;
            _velocity.z *= 0.5f;
        }
    }
}
