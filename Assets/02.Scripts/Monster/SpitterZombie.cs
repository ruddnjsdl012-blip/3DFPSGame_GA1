using UnityEngine;

// 스피터: 멀리서 산성액 덩어리를 포물선으로 뱉는다.
//  - 플레이어가 적당히 떨어져 있고 보이면 멈춰 서서 뱉는다. (가까이 오면 그냥 할퀸다)
//  - 산성액이 떨어진 곳에는 몇 초 동안 웅덩이가 남아, 그 안에 있으면 계속 피해를 입는다.
//  - 플레이어가 움직이는 방향을 조금 앞서 노리므로 계속 움직이는 게 좋다.
// ZombieVariants.Apply가 붙인다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Zombie))]
public class SpitterZombie : MonoBehaviour
{
    [SerializeField, Min(1f)] private float _minRange = 4.5f;
    [SerializeField, Min(2f)] private float _maxRange = 20f;
    [SerializeField, Min(0.5f)] private float _cooldown = 4f;
    [Tooltip("멈춰 선 뒤 뱉기까지 걸리는 시간")]
    [SerializeField, Min(0f)] private float _windup = 0.7f;
    [SerializeField, Min(0f)] private float _hitDamage = 8f;
    [Tooltip("플레이어 움직임을 얼마나 앞서 노릴지 (0 = 지금 위치, 1 = 도착할 때 위치)")]
    [SerializeField, Range(0f, 1f)] private float _leadAmount = 0.6f;

    private Zombie _zombie;
    private ZombieAnimator _animator;
    private float _cooldownTimer;
    private float _windupTimer = -1f;
    private CharacterController _targetBody;

    private void Awake()
    {
        _zombie = GetComponent<Zombie>();
        _cooldownTimer = Random.Range(1.5f, 3f);
    }

    private void Update()
    {
        if (_zombie.IsDead || GameManager.IsPlaying == false)
        {
            _zombie.HoldPosition = false;
            return;
        }

        GameObject player = _zombie.Target;
        if (player == null || (_zombie.State != ZombieState.Trace && _zombie.State != ZombieState.Attack))
        {
            _zombie.HoldPosition = false;
            _windupTimer = -1f;
            return;
        }

        if (_animator == null) _animator = GetComponent<ZombieAnimator>();
        if (_targetBody == null) _targetBody = player.GetComponentInParent<CharacterController>();

        _cooldownTimer -= Time.deltaTime;
        Vector3 mouth = MouthPosition();
        Vector3 target = player.transform.position;
        float distance = Vector3.Distance(transform.position, target);
        bool inRange = distance >= _minRange && distance <= _maxRange;
        bool canSee = inRange && HasLineOfSight(mouth, player);

        // 쏠 수 있는 거리에서 보이면 멈춰 서고, 아니면 다가간다.
        _zombie.HoldPosition = canSee;
        if (canSee == false)
        {
            _windupTimer = -1f;
            return;
        }

        if (_windupTimer < 0f)
        {
            if (_cooldownTimer <= 0f)
            {
                _windupTimer = _windup; // 뱉을 준비 (가래 끌어올리기)
                GameAudio.PlayAt("spitter_spit", mouth, 0.85f, 0.08f, 0.3f, 35f);
            }

            return;
        }

        _windupTimer -= Time.deltaTime;
        if (_windupTimer > 0f)
        {
            return;
        }

        _windupTimer = -1f;
        _cooldownTimer = _cooldown * Random.Range(0.85f, 1.2f);
        Spit(mouth, player);
    }

    private void Spit(Vector3 mouth, GameObject player)
    {
        if (_animator != null) _animator.PlayAttack();

        Vector3 aim = player.transform.position;
        float flightTime = Mathf.Clamp(Vector3.Distance(mouth, aim) / 13f, 0.55f, 1.4f);
        if (_targetBody != null)
        {
            Vector3 v = _targetBody.velocity;
            v.y = 0f;
            aim += v * flightTime * _leadAmount;
        }

        // 발밑 바닥을 노린다.
        if (Physics.Raycast(aim + Vector3.up * 1.5f, Vector3.down, out RaycastHit ground, 6f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && ground.collider.transform.root != player.transform.root)
        {
            aim = ground.point;
        }

        Vector3 velocity = (aim - mouth) / flightTime - 0.5f * AcidProjectile.Gravity * flightTime * Vector3.up;
        AcidProjectile.Launch(mouth, velocity, player, _hitDamage);
    }

    private Vector3 MouthPosition()
    {
        float height = 1.55f * transform.lossyScale.y;
        return transform.position + Vector3.up * height + transform.forward * 0.4f;
    }

    // 입에서 플레이어 머리까지 벽이 없는지
    private bool HasLineOfSight(Vector3 from, GameObject player)
    {
        Vector3 to = player.transform.position + Vector3.up * 1.2f;
        Vector3 direction = to - from;
        float length = direction.magnitude;
        if (length < 0.01f) return true;

        RaycastHit[] hits = Physics.RaycastAll(from, direction / length, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.root == player.transform.root) continue;
            if (hit.collider.GetComponentInParent<Zombie>() != null) continue; // 다른 좀비 너머로는 뱉을 수 있다.
            return false;
        }

        return true;
    }
}
