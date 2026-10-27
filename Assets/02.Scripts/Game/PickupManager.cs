using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 보급품(탄약 상자, 구급상자, 수류탄 상자)을 맵에 놓고, 좀비가 죽을 때 가끔 떨어뜨리게 한다.
//  - 보급 지점: 시작 위치에서 안전가옥까지 가는 길목 몇 곳에 탄약 + 구급상자/수류탄을 함께 둔다. (L4D 느낌)
//  - 흩어진 보급품: 맵 곳곳에 무작위로 둔다. (플레이할 때마다 위치가 바뀜)
//  - 맵에 놓인 보급품은 주우면 일정 시간 뒤 같은 자리에 다시 생긴다.
//  - 좀비 드롭: 탄약이 모자라거나 체력이 낮을수록 그 보급품이 더 잘 나온다.
// GameManager가 시작할 때 자동으로 만든다. (씬에 직접 두면 그것을 쓴다)
[DisallowMultipleComponent]
public class PickupManager : MonoBehaviour
{
    [Header("보급 지점 (안전가옥 가는 길목)")]
    [Tooltip("시작 위치 → 안전가옥 경로의 이 비율 지점마다 보급품을 둔다.")]
    [SerializeField] private float[] _routeStops = { 0.3f, 0.55f, 0.8f };

    [Header("흩어진 보급품 개수")]
    [SerializeField, Min(0)] private int _scatteredAmmo = 5;
    [SerializeField, Min(0)] private int _scatteredMedkits = 2;
    [SerializeField, Min(0)] private int _scatteredBombs = 2;
    [SerializeField, Min(1f)] private float _minSpacing = 14f;
    [SerializeField, Min(0f)] private float _minDistanceFromStart = 10f;

    [Header("양 / 다시 생기는 시간 (초)")]
    [SerializeField, Min(1)] private int _ammoAmount = 60;
    [SerializeField, Min(1)] private int _medkitAmount = 50;
    [SerializeField, Min(1)] private int _bombAmount = 2;
    [SerializeField, Min(0f)] private float _ammoRespawn = 45f;
    [SerializeField, Min(0f)] private float _medkitRespawn = 90f;
    [SerializeField, Min(0f)] private float _bombRespawn = 75f;

    [Header("좀비 드롭")]
    [SerializeField, Range(0f, 1f)] private float _dropAmmoChance = 0.15f;
    [SerializeField, Range(0f, 1f)] private float _dropMedkitChance = 0.04f;
    [SerializeField, Range(0f, 1f)] private float _dropBombChance = 0.03f;
    [SerializeField, Min(1)] private int _dropAmmoAmount = 30;
    [SerializeField, Min(1)] private int _dropMedkitAmount = 25;
    [SerializeField, Min(1)] private int _dropBombAmount = 1;
    [Tooltip("떨어뜨린 보급품이 사라지기까지의 시간")]
    [SerializeField, Min(5f)] private float _dropLifetime = 30f;

    private static PickupManager _instance;

    private Transform _player;
    private Player _playerHealth;
    private PlayerGunFire _gun;
    private PlayerBombFire _bombs;
    private Transform _mapRoot;
    private Transform _dropRoot;
    private readonly List<Vector3> _placed = new List<Vector3>();

    // NavMesh 무작위 위치 고르기용 (삼각형 넓이 누적값)
    private Vector3[] _navVertices;
    private int[] _navIndices;
    private float[] _navAreas;
    private float _navTotalArea;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    // 탱크 같은 강한 좀비를 쓰러뜨렸을 때 확실히 주는 보상
    public static void DropReward(Vector3 position, PickupType type, int amount)
    {
        if (_instance == null)
        {
            return;
        }

        if (_instance.TryGetGround(position, out Vector3 ground) == false)
        {
            ground = position;
        }

        Pickup.Create(type, amount, ground, 0f, _instance._dropLifetime * 2f, false, _instance._dropRoot);
    }

    // 좀비가 죽을 때 Zombie가 부른다.
    public static void OnZombieKilled(Vector3 position)
    {
        if (_instance != null)
        {
            _instance.TryDrop(position);
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    private IEnumerator Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("PickupManager: 'Player' 태그가 붙은 오브젝트가 없어 보급품을 놓지 않습니다.", this);
            yield break;
        }

        _player = player.transform;
        _playerHealth = player.GetComponentInChildren<Player>();
        if (_playerHealth == null) _playerHealth = FindAnyObjectByType<Player>();
        _gun = FindAnyObjectByType<PlayerGunFire>();
        _bombs = FindAnyObjectByType<PlayerBombFire>();

        _mapRoot = new GameObject("MapPickups").transform;
        _mapRoot.SetParent(transform, false);
        _dropRoot = new GameObject("DroppedPickups").transform;
        _dropRoot.SetParent(transform, false);

        // 좀비 길 지도(NavMesh)가 만들어질 때까지 기다린다. (ZombieSpawnManager가 만든다)
        float wait = 0f;
        while (ZombieNavMesh.IsAvailable == false && wait < 12f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return null;
        PlaceMapPickups();
    }

    // ----- 맵 배치 -----

    private void PlaceMapPickups()
    {
        Vector3 start = _player.position;
        bool hasNav = PrepareNavSampling(start, out Vector3 startNav);

        int count = 0;

        // 1) 안전가옥 가는 길목의 보급 지점
        if (hasNav && SafeRoomGoal.Current != null)
        {
            NavMeshPath path = new NavMeshPath();
            if (NavMesh.SamplePosition(SafeRoomGoal.Current.Position, out NavMeshHit goalHit, 4f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(startNav, goalHit.position, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete)
            {
                for (int i = 0; i < _routeStops.Length; i++)
                {
                    Vector3 stop = PointAlongPath(path.corners, Mathf.Clamp01(_routeStops[i]));
                    if (TryFindSpotNear(stop, 4f, out Vector3 a) == false) continue;
                    Place(PickupType.Ammo, a);
                    count++;

                    // 짝수 번째는 구급상자, 홀수 번째는 수류탄
                    PickupType second = i % 2 == 0 ? PickupType.Medkit : PickupType.Bombs;
                    if (TryFindSpotNear(a, 1.6f, out Vector3 b, 0.9f))
                    {
                        Place(second, b);
                        count++;
                    }
                }
            }
        }

        // 2) 맵 곳곳에 흩어진 보급품
        count += Scatter(PickupType.Ammo, _scatteredAmmo, start, startNav, hasNav);
        count += Scatter(PickupType.Medkit, _scatteredMedkits, start, startNav, hasNav);
        count += Scatter(PickupType.Bombs, _scatteredBombs, start, startNav, hasNav);

        Debug.Log($"PickupManager: 보급품 {count}개를 맵에 놓았습니다." + (hasNav ? string.Empty : " (NavMesh 없음 - 플레이어 주변에 배치)"));
    }

    private int Scatter(PickupType type, int amount, Vector3 start, Vector3 startNav, bool hasNav)
    {
        int placed = 0;
        NavMeshPath path = new NavMeshPath();
        for (int attempt = 0; attempt < 400 && placed < amount; attempt++)
        {
            Vector3 point;
            if (hasNav)
            {
                point = RandomNavPoint();
                if (NavMesh.CalculatePath(startNav, point, NavMesh.AllAreas, path) == false
                    || path.status != NavMeshPathStatus.PathComplete)
                {
                    continue; // 닫힌 건물 안이나 지붕 위처럼 갈 수 없는 곳
                }
            }
            else
            {
                Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(8f, 40f);
                if (TryGetGround(start + new Vector3(ring.x, 0f, ring.y), out point) == false) continue;
            }

            Vector3 flat = point - start;
            flat.y = 0f;
            float spacing = attempt < 250 ? _minSpacing : _minSpacing * 0.5f; // 자리가 모자라면 간격을 줄인다.
            if (flat.magnitude < _minDistanceFromStart || IsTooClose(point, spacing) || HasRoom(point) == false)
            {
                continue;
            }

            Place(type, point);
            placed++;
        }

        return placed;
    }

    private void Place(PickupType type, Vector3 position)
    {
        // NavMesh는 바닥보다 살짝 떠 있으므로 실제 바닥에 맞춘다.
        if (TryGetGround(position, out Vector3 ground) && Mathf.Abs(ground.y - position.y) < 1f)
        {
            position = ground;
        }

        _placed.Add(position);
        switch (type)
        {
            case PickupType.Medkit:
                Pickup.Create(type, _medkitAmount, position, _medkitRespawn, 0f, false, _mapRoot);
                break;
            case PickupType.Bombs:
                Pickup.Create(type, _bombAmount, position, _bombRespawn, 0f, false, _mapRoot);
                break;
            default:
                Pickup.Create(type, _ammoAmount, position, _ammoRespawn, 0f, false, _mapRoot);
                break;
        }
    }

    // ----- 좀비 드롭 -----

    private void TryDrop(Vector3 zombiePosition)
    {
        if (GameManager.IsPlaying == false || _dropRoot == null)
        {
            return;
        }

        // 부족할수록 잘 나온다.
        float ammo = _dropAmmoChance;
        if (_gun != null)
        {
            if (_gun.TotalAmmo < _gun.MaxBulletCount * 2) ammo *= 2f;
            if (_gun.ReserveAmmo <= 0) ammo *= 1.5f;
            if (_gun.NeedsAmmo == false) ammo *= 0.3f;
        }

        float medkit = _dropMedkitChance;
        if (_playerHealth != null && _playerHealth.MaxHealth > 0f)
        {
            float ratio = _playerHealth.Health / _playerHealth.MaxHealth;
            if (ratio < 0.4f) medkit *= 2.5f;
            if (_playerHealth.NeedsHealing == false) medkit *= 0.3f;
        }

        float bomb = _bombs != null && _bombs.NeedsBombs ? _dropBombChance : _dropBombChance * 0.3f;
        ammo = Mathf.Min(ammo, 0.6f);

        float roll = Random.value;
        PickupType type;
        int amount;
        if (roll < ammo)
        {
            type = PickupType.Ammo;
            amount = _dropAmmoAmount;
        }
        else if (roll < ammo + medkit)
        {
            type = PickupType.Medkit;
            amount = _dropMedkitAmount;
        }
        else if (roll < ammo + medkit + bomb)
        {
            type = PickupType.Bombs;
            amount = _dropBombAmount;
        }
        else
        {
            return;
        }

        if (TryGetGround(zombiePosition, out Vector3 ground) == false)
        {
            ground = zombiePosition;
        }

        Pickup.Create(type, amount, ground, 0f, _dropLifetime, true, _dropRoot);
    }

    // ----- 위치 찾기 -----

    private bool PrepareNavSampling(Vector3 start, out Vector3 startNav)
    {
        startNav = start;
        if (ZombieNavMesh.IsAvailable == false
            || NavMesh.SamplePosition(start, out NavMeshHit hit, 3f, NavMesh.AllAreas) == false)
        {
            return false;
        }

        startNav = hit.position;
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        _navVertices = triangulation.vertices;
        _navIndices = triangulation.indices;
        int triangles = _navIndices.Length / 3;
        if (triangles == 0)
        {
            return false;
        }

        _navAreas = new float[triangles];
        _navTotalArea = 0f;
        for (int i = 0; i < triangles; i++)
        {
            Vector3 a = _navVertices[_navIndices[i * 3]];
            Vector3 b = _navVertices[_navIndices[i * 3 + 1]];
            Vector3 c = _navVertices[_navIndices[i * 3 + 2]];
            _navTotalArea += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            _navAreas[i] = _navTotalArea;
        }

        return _navTotalArea > 0f;
    }

    // NavMesh 전체에서 넓이에 비례해 고르게 한 점을 고른다.
    private Vector3 RandomNavPoint()
    {
        float pick = Random.value * _navTotalArea;
        int index = System.Array.BinarySearch(_navAreas, pick);
        if (index < 0) index = ~index;
        index = Mathf.Clamp(index, 0, _navAreas.Length - 1);

        Vector3 a = _navVertices[_navIndices[index * 3]];
        Vector3 b = _navVertices[_navIndices[index * 3 + 1]];
        Vector3 c = _navVertices[_navIndices[index * 3 + 2]];
        float u = Random.value;
        float v = Random.value;
        if (u + v > 1f)
        {
            u = 1f - u;
            v = 1f - v;
        }

        return a + (b - a) * u + (c - a) * v;
    }

    private static Vector3 PointAlongPath(Vector3[] corners, float fraction)
    {
        if (corners.Length == 0) return Vector3.zero;
        if (corners.Length == 1) return corners[0];

        float total = 0f;
        for (int i = 1; i < corners.Length; i++) total += Vector3.Distance(corners[i - 1], corners[i]);

        float target = total * fraction;
        for (int i = 1; i < corners.Length; i++)
        {
            float segment = Vector3.Distance(corners[i - 1], corners[i]);
            if (target <= segment && segment > 0f)
            {
                return Vector3.Lerp(corners[i - 1], corners[i], target / segment);
            }

            target -= segment;
        }

        return corners[corners.Length - 1];
    }

    // 기준점 근처에서 비어 있고 다른 보급품과 겹치지 않는 자리를 찾는다. (길 한가운데보다 가장자리 쪽을 선호)
    private bool TryFindSpotNear(Vector3 center, float radius, out Vector3 spot, float spacing = 3f)
    {
        for (int i = 0; i < 30; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);
            if (ZombieNavMesh.IsAvailable)
            {
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) == false) continue;
                candidate = hit.position;
            }
            else if (TryGetGround(candidate, out candidate) == false)
            {
                continue;
            }

            if (HasRoom(candidate) && IsTooClose(candidate, spacing) == false)
            {
                spot = candidate;
                return true;
            }
        }

        spot = center;
        return false;
    }

    private bool IsTooClose(Vector3 point, float spacing)
    {
        foreach (Vector3 placed in _placed)
        {
            if ((placed - point).sqrMagnitude < spacing * spacing) return true;
        }

        return false;
    }

    // 상자나 벽 속에 묻히지 않는지
    private bool HasRoom(Vector3 ground)
    {
        return Physics.CheckSphere(ground + Vector3.up * 0.55f, 0.4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) == false;
    }

    // 위에서 아래로 광선을 쏴서 바닥을 찾는다. (플레이어/좀비/굴러다니는 물체는 제외)
    private bool TryGetGround(Vector3 point, out Vector3 ground)
    {
        ground = point;
        RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 2f, Vector3.down, 8f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (RaycastHit hit in hits)
        {
            Collider collider = hit.collider;
            if (collider.GetComponentInParent<Zombie>() != null) continue;
            if (_player != null && collider.transform.root == _player.root) continue;
            if (collider.attachedRigidbody != null && collider.attachedRigidbody.isKinematic == false) continue;
            if (hit.normal.y < 0.6f) return false;

            ground = hit.point;
            return true;
        }

        return false;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
