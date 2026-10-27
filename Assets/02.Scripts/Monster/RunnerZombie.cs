using UnityEngine;

// 러너: 작고 빠르다. 체력은 낮지만 무리 지어 뛰어오면 위험하다.
// 쫓기 시작할 때 날카로운 비명을 질러서 다가오는 걸 알 수 있다.
// (몸을 숙이고 뛰는 동작은 ZombieAnimator가 속도를 보고 알아서 한다)
// ZombieVariants.Apply가 붙인다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Zombie))]
public class RunnerZombie : MonoBehaviour
{
    [Tooltip("이 거리 안에서 처음 쫓기 시작하면 비명을 지른다.")]
    [SerializeField, Min(1f)] private float _shriekDistance = 30f;

    private Zombie _zombie;
    private bool _shrieked;
    private float _nextShriekTime;

    private void Awake()
    {
        _zombie = GetComponent<Zombie>();
    }

    private void Update()
    {
        if (_zombie.IsDead || GameManager.IsPlaying == false || _zombie.Target == null)
        {
            return;
        }

        if (_zombie.State != ZombieState.Trace)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, _zombie.Target.transform.position);
        if (distance > _shriekDistance)
        {
            return;
        }

        // 처음 다가올 때 한 번, 그 뒤로는 가끔
        if (_shrieked == false || Time.time >= _nextShriekTime)
        {
            _shrieked = true;
            _nextShriekTime = Time.time + Random.Range(7f, 12f);
            GameAudio.PlayAt("runner_shriek", transform.position + Vector3.up * 1.5f, 0.8f, 0.1f, 0.8f, 45f);
        }
    }
}
