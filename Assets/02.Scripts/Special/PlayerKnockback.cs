using UnityEngine;

// 플레이어를 밀어낸다. (탱크에게 맞았을 때) 밀리는 속도는 빠르게 줄어든다.
public class PlayerKnockback : MonoBehaviour
{
    private CharacterController _controller;
    private Vector3 _velocity;

    public static void Push(GameObject player, Vector3 velocity)
    {
        PlayerKnockback knockback = player.GetComponent<PlayerKnockback>();
        if (knockback == null)
        {
            knockback = player.AddComponent<PlayerKnockback>();
        }

        knockback._velocity = velocity;
    }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (_velocity.sqrMagnitude < 0.01f || _controller == null || !_controller.enabled)
        {
            return;
        }

        _controller.Move(_velocity * Time.deltaTime);
        _velocity = Vector3.MoveTowards(_velocity, Vector3.zero, 30f * Time.deltaTime);
    }
}
