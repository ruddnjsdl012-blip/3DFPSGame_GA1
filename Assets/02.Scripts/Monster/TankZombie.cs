using UnityEngine;

// 탱크: 크고 느리지만 아주 단단하다.
//  - 나타나면 포효하고 화면에 경고를 띄운다.
//  - 걸을 때마다 땅이 울린다. (가까우면 화면이 흔들림)
//  - 주먹에 맞으면 크게 밀려 날아간다.
//  - 쓰러뜨리면 탄약과 구급상자를 떨어뜨린다.
// ZombieVariants.Apply가 붙인다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Zombie))]
public class TankZombie : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _knockbackForce = 11f;
    [SerializeField, Min(0f)] private float _knockbackLift = 5f;
    [SerializeField, Min(0.5f)] private float _strideLength = 2.4f;

    private Zombie _zombie;
    private CharacterController _controller;
    private float _stepDistance;
    private bool _roared;

    private void Awake()
    {
        _zombie = GetComponent<Zombie>();
        _controller = GetComponent<CharacterController>();
        _zombie.AttackLanded += HandleAttackLanded;
        _zombie.Died += HandleDied;
    }

    private void Start()
    {
        Roar();
    }

    private void Roar()
    {
        if (_roared)
        {
            return;
        }

        _roared = true;
        GameAudio.Play2D("tank_roar", 0.9f, 0.03f, 2f);
        CameraShake.AddAt(transform.position, 0.5f, 40f);

        UI_ZombieStatusHud hud = FindAnyObjectByType<UI_ZombieStatusHud>();
        if (hud != null)
        {
            hud.ShowAlert("탱크가 나타났다!", "TANK INCOMING!", "총알을 아끼지 말고 수류탄을 던져라", "USE YOUR BOMBS");
        }
    }

    private void Update()
    {
        if (_zombie.IsDead || _controller == null || _controller.enabled == false || GameManager.IsPlaying == false)
        {
            return;
        }

        // 발소리 + 땅 울림
        Vector3 velocity = _controller.velocity;
        float speed = new Vector2(velocity.x, velocity.z).magnitude;
        _stepDistance += speed * Time.deltaTime;
        if (_stepDistance >= _strideLength)
        {
            _stepDistance = 0f;
            GameAudio.PlayAt("tank_step", transform.position, 0.9f, 0.08f, 0.05f, 45f);
            CameraShake.AddAt(transform.position, 0.22f, 18f);
        }
    }

    private void HandleAttackLanded(Zombie zombie, GameObject player)
    {
        GameAudio.Play2D("tank_hit", 0.95f, 0.05f);
        CameraShake.Add(0.75f);

        Vector3 direction = player.transform.position - transform.position;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
        PlayerKnockback.Push(player, direction * _knockbackForce + Vector3.up * _knockbackLift);
    }

    private void HandleDied(Zombie zombie)
    {
        GameAudio.PlayAt("tank_step", transform.position, 1f, 0.02f, 0f, 50f);
        CameraShake.AddAt(transform.position, 0.6f, 25f);

        // 보상: 탄약과 구급상자
        PickupManager.DropReward(transform.position + transform.right * 0.8f, PickupType.Ammo, 90);
        PickupManager.DropReward(transform.position - transform.right * 0.8f, PickupType.Medkit, 50);
    }

    private void OnDestroy()
    {
        if (_zombie != null)
        {
            _zombie.AttackLanded -= HandleAttackLanded;
            _zombie.Died -= HandleDied;
        }
    }
}
