using UnityEngine;

// 도착 안전가옥. 플레이어가 이 상자 안으로 들어오면 탈출 성공(승리).
// Tools > Zombie City 메뉴가 안전가옥 안에 자동으로 만든다.
[RequireComponent(typeof(BoxCollider))]
[DisallowMultipleComponent]
public class SafeRoomGoal : MonoBehaviour
{
    [Tooltip("이 시간(초) 동안 안에 머물러야 탈출 성공")]
    [SerializeField, Min(0f)] private float _enterTime = 0.3f;

    private BoxCollider _area;
    private Transform _player;
    private float _insideTime;

    public static SafeRoomGoal Current { get; private set; }

    public Vector3 Position => _area != null ? _area.bounds.center : transform.position;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Current = null;
    }

    private void Awake()
    {
        _area = GetComponent<BoxCollider>();
        _area.isTrigger = true; // 길 지도와 스포너 바닥 검사에서 제외된다.
        Current = this;
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        _player = player != null ? player.transform : null;
    }

    private void Update()
    {
        if (_player == null || GameManager.IsPlaying == false || GameManager.Instance == null)
        {
            return;
        }

        if (_area.bounds.Contains(_player.position))
        {
            _insideTime += Time.deltaTime;
            if (_insideTime >= _enterTime)
            {
                GameManager.Instance.CompleteEscape();
            }
        }
        else
        {
            _insideTime = 0f;
        }
    }

    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    private void OnDrawGizmos()
    {
        BoxCollider area = GetComponent<BoxCollider>();
        if (area == null)
        {
            return;
        }

        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(area.center, area.size);
    }
}
