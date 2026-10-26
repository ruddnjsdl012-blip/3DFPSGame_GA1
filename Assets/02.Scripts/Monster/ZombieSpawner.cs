using UnityEngine;

// 좀비가 나오는 한 지점. ZombieSpawnManager가 플레이할 때마다 맵 곳곳의 무작위 위치에 만든다.
// (Scene 뷰에서 빨간 원으로 보인다)
[DisallowMultipleComponent]
public class ZombieSpawner : MonoBehaviour
{
    [Tooltip("이 반경 안의 무작위 위치에 좀비가 나온다.")]
    [SerializeField, Min(0f)] private float _radius = 1.5f;
    [Tooltip("한 번 좀비를 만든 뒤 다시 만들 때까지 쉬는 시간")]
    [SerializeField, Min(0f)] private float _cooldown = 2f;

    private float _readyTime;

    public float Radius => _radius;
    public bool IsReady => Time.time >= _readyTime;

    public void Setup(float radius, float cooldown)
    {
        _radius = radius;
        _cooldown = cooldown;
    }

    public void MarkUsed()
    {
        _readyTime = Time.time + _cooldown;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, _radius);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);
    }
}
