using UnityEngine;

// 도시 맵의 구역 정보. Tools > L4D 맵 > 도시 맵 만들기가 CityMap에 붙여 준다.
// 다음 단계(AI 디렉터, 호드, 피날레)가 이 정보를 읽어서 쓴다.
[DefaultExecutionOrder(-1000)]
public class CityMapInfo : MonoBehaviour
{
    [Header("출발")]
    [SerializeField] private Transform _startPoint;          // 플레이어가 시작하는 자리 (출발 안전가옥 안)
    [SerializeField] private BoxCollider _startSafeRoomArea; // 출발 안전가옥 안쪽 범위 (나가면 게임 진행 시작)

    [Header("진행 경로 (출발 → 피날레 순서)")]
    [SerializeField] private Transform[] _routePoints;

    [Header("좀비가 나오는 자리")]
    [SerializeField] private Transform[] _spawnPoints;

    [Header("중간 안전가옥 (안에서는 좀비가 새로 나오지 않는다)")]
    [SerializeField] private BoxCollider[] _midSafeRooms;

    [Header("피날레")]
    [SerializeField] private BoxCollider _finaleArea;        // 피날레 광장 범위
    [SerializeField] private Transform _extractionPoint;    // 구조 지점 (헬기장)

    public Transform StartPoint => _startPoint;
    public BoxCollider StartSafeRoomArea => _startSafeRoomArea;
    public Transform[] RoutePoints => _routePoints;
    public Transform[] SpawnPoints => _spawnPoints;
    public BoxCollider FinaleArea => _finaleArea;
    public BoxCollider[] MidSafeRooms => _midSafeRooms;

    private void Awake()
    {
        CityMapNavigation.Prepare(this);
        if (GetComponent<CityMapAtmosphere>() == null)
        {
            gameObject.AddComponent<CityMapAtmosphere>();
        }
    }

    // 이 자리가 중간 안전가옥 안인가?
    public bool InMidSafeRoom(Vector3 position)
    {
        if (_midSafeRooms == null)
        {
            return false;
        }

        foreach (BoxCollider room in _midSafeRooms)
        {
            if (room != null && room.bounds.Contains(position + Vector3.up * 0.5f))
            {
                return true;
            }
        }

        return false;
    }

    // 진행 경로 전체 길이 (m) - 맵 크기에 맞춰 좀비/보급품 수를 늘릴 때 쓴다.
    public float RouteLength
    {
        get
        {
            float length = 0f;
            for (int i = 0; _routePoints != null && i < _routePoints.Length - 1; i++)
            {
                if (_routePoints[i] != null && _routePoints[i + 1] != null)
                {
                    length += Vector3.Distance(_routePoints[i].position, _routePoints[i + 1].position);
                }
            }

            return length;
        }
    }
    public Transform ExtractionPoint => _extractionPoint;

    // 진행 경로에서 가장 가까운 지점의 번호 (0 = 출발, 마지막 = 피날레)
    public int GetRouteIndex(Vector3 position)
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < _routePoints.Length; i++)
        {
            if (_routePoints[i] == null)
            {
                continue;
            }

            float distance = (_routePoints[i].position - position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        for (int i = 0; _routePoints != null && i < _routePoints.Length - 1; i++)
        {
            if (_routePoints[i] != null && _routePoints[i + 1] != null)
            {
                Gizmos.DrawLine(_routePoints[i].position + Vector3.up, _routePoints[i + 1].position + Vector3.up);
            }
        }

        Gizmos.color = Color.red;
        if (_spawnPoints != null)
        {
            foreach (Transform point in _spawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position + Vector3.up, 0.6f);
                }
            }
        }
    }
}
