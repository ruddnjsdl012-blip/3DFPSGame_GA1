using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 플레이할 때마다 맵 곳곳의 무작위 위치에 좀비 스포너를 만들고, 시간이 지날수록
// 좀비를 더 자주, 더 많이, 더 강하게 만든다. 좀비는 플레이어의 '현재 위치' 주변
// 안 보이는 스포너에서 나오고, 가끔 호드(한꺼번에 몰려오는 무리)가 온다.
// 씬에 배치하지 않아도 GameManager가 시작할 때 자동으로 만든다.
// (빈 GameObject에 직접 붙여 두면 Inspector에서 값을 조절할 수 있다)
[DisallowMultipleComponent]
public class ZombieSpawnManager : MonoBehaviour
{
    [Header("좀비")]
    [Tooltip("비워 두면 씬에 있는 좀비 하나를 복제해서 쓴다.")]
    [SerializeField] private Zombie _zombiePrefab;

    [Header("스포너 위치 (플레이할 때마다 무작위)")]
    [Tooltip("맵 전체에 만들 스포너의 최대 개수 (맵이 좁으면 간격 때문에 더 적게 생긴다)")]
    [SerializeField, Min(1)] private int _spawnerCount = 30;
    [Tooltip("플레이어 시작 위치에서 이 거리보다 가까운 곳에는 만들지 않는다.")]
    [SerializeField, Min(0f)] private float _minDistanceFromPlayer = 15f;
    [Tooltip("좀비는 플레이어의 현재 위치에서 이 거리 범위 안에 있는 스포너에서 나온다.")]
    [SerializeField, Min(0f)] private float _spawnDistanceMin = 12f;
    [SerializeField, Min(1f)] private float _spawnDistanceMax = 45f;
    [Tooltip("스포너끼리 최소 간격")]
    [SerializeField, Min(0f)] private float _minSpacing = 9f;
    [Tooltip("플레이어가 서 있는 바닥과 높이가 이만큼 넘게 차이 나는 곳(지붕, 컨테이너 위 등)은 제외")]
    [SerializeField, Min(0.1f)] private float _maxHeightDifference = 0.8f;
    [Tooltip("맵 가장자리에서 이만큼 안쪽에만 만든다.")]
    [SerializeField, Min(0f)] private float _mapEdgeMargin = 2f;
    [Tooltip("맵 범위를 직접 정할 때 사용 (Size가 0이면 씬의 콜라이더로 자동 계산)")]
    [SerializeField] private Vector3 _areaCenter = Vector3.zero;
    [SerializeField] private Vector3 _areaSize = Vector3.zero;

    [Header("생성 속도")]
    [SerializeField, Min(0f)] private float _startDelay = 2f;
    [Tooltip("레벨 1일 때 좀비를 만드는 간격(초)")]
    [SerializeField, Min(0.1f)] private float _spawnInterval = 4f;
    [SerializeField, Min(0.1f)] private float _minSpawnInterval = 1.2f;
    [Tooltip("레벨이 오를 때마다 생성 간격에 곱하는 값 (작을수록 빨라짐)")]
    [SerializeField, Range(0.5f, 1f)] private float _intervalMultiplierPerLevel = 0.88f;
    [Tooltip("레벨 1일 때 동시에 살아 있을 수 있는 최대 좀비 수 (처음부터 있던 좀비 포함)")]
    [SerializeField, Min(1)] private int _maxAlive = 8;
    [SerializeField, Min(0)] private int _maxAlivePerLevel = 2;
    [SerializeField, Min(1)] private int _maxAliveLimit = 30;

    [Header("난이도 (시간이 지날수록 강해짐)")]
    [Tooltip("이 시간(초)마다 레벨이 1씩 오른다.")]
    [SerializeField, Min(1f)] private float _levelUpInterval = 30f;
    [Tooltip("레벨당 체력 증가율 (0.2 = 레벨마다 원래 체력의 +20%)")]
    [SerializeField, Min(0f)] private float _healthPerLevel = 0.2f;
    [Tooltip("레벨당 공격력 증가율 (0.1 = 레벨마다 원래 공격력의 +10%)")]
    [SerializeField, Min(0f)] private float _damagePerLevel = 0.1f;
    [SerializeField, Min(1)] private int _maxLevel = 20;
    [Tooltip("켜면 레벨이 오를 때 이미 살아 있는 좀비도 강해진다. 끄면 새로 나오는 좀비만 강해진다.")]
    [SerializeField] private bool _applyToLivingZombies = false;

    [Header("호드 (한꺼번에 몰려오는 무리)")]
    [SerializeField] private bool _enableHorde = true;
    [Tooltip("게임 시작 후 첫 호드까지의 시간(초)")]
    [SerializeField, Min(1f)] private float _firstHordeDelay = 60f;
    [Tooltip("호드 사이의 간격(초)")]
    [SerializeField, Min(5f)] private float _hordeInterval = 75f;
    [SerializeField, Min(1)] private int _hordeBaseSize = 6;
    [SerializeField, Min(0)] private int _hordeSizePerLevel = 2;
    [Tooltip("호드 좀비를 한 마리씩 내보내는 간격(초)")]
    [SerializeField, Min(0.05f)] private float _hordeSpawnGap = 0.3f;

    private readonly List<ZombieSpawner> _spawners = new List<ZombieSpawner>();
    private readonly List<Zombie> _spawned = new List<Zombie>();
    private Zombie _template;
    private GameObject _templateHolder;
    private Transform _player;
    private float _elapsed;
    private float _spawnTimer;
    private int _level = 1;
    private float _footOffset;
    private float _clearRadius = 0.4f;
    private float _clearHeight = 1.8f;
    private bool _ready;
    private float _nextHordeTime;
    private int _hordeRemaining;
    private float _hordeTimer;

    public event Action<int> LevelChanged;
    public event Action<int> HordeStarted; // 호드 좀비 수

    public int Level => _level;
    public float ElapsedTime => _elapsed;
    public float HealthMultiplier => 1f + _healthPerLevel * (_level - 1);
    public float DamageMultiplier => 1f + _damagePerLevel * (_level - 1);
    public IReadOnlyList<ZombieSpawner> Spawners => _spawners;

    private void Awake()
    {
        // 처음 좀비가 맞거나 죽기 전에 복제본을 떠 둔다.
        PrepareTemplate();
    }

    private IEnumerator Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        _player = player != null ? player.transform : null;

        if (_template == null)
        {
            Debug.LogWarning("ZombieSpawnManager: 복제할 좀비가 없습니다. Zombie Prefab을 연결하거나 씬에 좀비를 하나 두세요.", this);
            enabled = false;
            yield break;
        }

        if (_player == null)
        {
            Debug.LogWarning("ZombieSpawnManager: 'Player' 태그가 붙은 오브젝트가 없습니다.", this);
            enabled = false;
            yield break;
        }

        if (FindAnyObjectByType<UI_ZombieStatusHud>() == null)
        {
            UI_ZombieStatusHud.Create(this);
        }

        // 1) 좀비가 벽을 돌아갈 수 있도록 길 지도(NavMesh)를 만든다.
        ZombieNavMesh navMesh = GetComponent<ZombieNavMesh>();
        if (navMesh == null)
        {
            navMesh = gameObject.AddComponent<ZombieNavMesh>();
        }

        yield return StartCoroutine(navMesh.Build(GetNavMeshArea(), _player));

        // 2) 플레이어까지 걸어서 갈 수 있는 곳에만 스포너를 만든다.
        CreateSpawners();
        _spawnTimer = _startDelay;
        _nextHordeTime = _firstHordeDelay;
        _ready = _spawners.Count > 0;
    }

    // NavMesh를 만들 범위: 맵 전체, 단 너무 넓으면 플레이어 주변 400m까지만
    private Bounds GetNavMeshArea()
    {
        Bounds map = GetMapArea();
        map.Expand(new Vector3(4f, 4f, 4f));

        const float half = 200f;
        Vector3 min = map.min;
        Vector3 max = map.max;
        min.x = Mathf.Max(min.x, _player.position.x - half);
        min.z = Mathf.Max(min.z, _player.position.z - half);
        max.x = Mathf.Min(max.x, _player.position.x + half);
        max.z = Mathf.Min(max.z, _player.position.z + half);

        Bounds area = new Bounds();
        area.SetMinMax(min, max);
        return area;
    }

    // NavMesh 위에 있고, 그곳에서 플레이어까지 길이 이어져 있는지
    private bool IsReachable(Vector3 ground, Vector3 playerNavPosition, NavMeshPath path)
    {
        if (ZombieNavMesh.IsAvailable == false)
        {
            return true; // NavMesh가 없으면 검사하지 않는다.
        }

        if (NavMesh.SamplePosition(ground, out NavMeshHit hit, 1f, NavMesh.AllAreas) == false)
        {
            return false;
        }

        return NavMesh.CalculatePath(hit.position, playerNavPosition, NavMesh.AllAreas, path)
               && path.status == NavMeshPathStatus.PathComplete;
    }

    private void Update()
    {
        if (_ready == false || GameManager.IsPlaying == false)
        {
            return;
        }

        float dt = Time.deltaTime;
        _elapsed += dt;

        // 시간이 지나면 레벨이 오른다.
        int level = Mathf.Min(_maxLevel, 1 + Mathf.FloorToInt(_elapsed / _levelUpInterval));
        if (level != _level)
        {
            _level = level;
            Debug.Log($"좀비 레벨 {_level}: 체력 x{HealthMultiplier:0.00}, 공격력 x{DamageMultiplier:0.00}");

            if (_applyToLivingZombies)
            {
                foreach (Zombie zombie in FindObjectsByType<Zombie>(FindObjectsSortMode.None))
                {
                    zombie.ApplyStatMultiplier(HealthMultiplier, DamageMultiplier);
                }
            }

            LevelChanged?.Invoke(_level);
        }

        UpdateHorde(dt);

        _spawnTimer -= dt;
        if (_spawnTimer > 0f)
        {
            return;
        }

        _spawnTimer = CurrentSpawnInterval();

        if (CountAlive() < CurrentMaxAlive())
        {
            TrySpawn(false);
        }
    }

    // 일정 시간마다 좀비 무리를 짧은 간격으로 한꺼번에 내보낸다.
    private void UpdateHorde(float dt)
    {
        if (_enableHorde == false)
        {
            return;
        }

        if (_hordeRemaining <= 0 && _elapsed >= _nextHordeTime)
        {
            _hordeRemaining = _hordeBaseSize + _hordeSizePerLevel * (_level - 1);
            _hordeTimer = 0f;
            _nextHordeTime = _elapsed + _hordeInterval;
            Debug.Log($"호드 출현! {_hordeRemaining}마리");
            HordeStarted?.Invoke(_hordeRemaining);
        }

        if (_hordeRemaining <= 0)
        {
            return;
        }

        _hordeTimer -= dt;
        if (_hordeTimer > 0f)
        {
            return;
        }

        _hordeTimer = _hordeSpawnGap;
        // 호드는 최대 수를 무시하지만, 너무 많아지지 않게 상한은 둔다.
        if (CountAlive() < _maxAliveLimit + 15)
        {
            TrySpawn(true);
        }

        _hordeRemaining--;
    }

    private float CurrentSpawnInterval()
    {
        float interval = _spawnInterval * Mathf.Pow(_intervalMultiplierPerLevel, _level - 1);
        return Mathf.Max(_minSpawnInterval, interval);
    }

    private int CurrentMaxAlive()
    {
        return Mathf.Min(_maxAliveLimit, _maxAlive + _maxAlivePerLevel * (_level - 1));
    }

    private int CountAlive()
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.RemainingZombies;
        }

        _spawned.RemoveAll(zombie => zombie == null);
        return _spawned.Count;
    }

    // ----- 좀비 생성 -----

    private void TrySpawn(bool horde)
    {
        ZombieSpawner spawner = PickSpawner(horde);
        if (spawner == null)
        {
            return;
        }

        // 스포너 반경 안에서 비어 있는 자리를 찾는다.
        for (int attempt = 0; attempt < 6; attempt++)
        {
            // 마지막 시도는 스포너 중심(길 지도 위로 검사된 자리)을 쓴다.
            Vector2 offset = attempt < 5 ? UnityEngine.Random.insideUnitCircle * spawner.Radius : Vector2.zero;
            Vector3 point = spawner.transform.position + new Vector3(offset.x, 0f, offset.y);

            if (TryGetGround(point, spawner.transform.position.y + 2f, out Vector3 ground) == false
                || HasRoomFor(ground) == false)
            {
                continue;
            }

            if (ZombieNavMesh.IsAvailable && NavMesh.SamplePosition(ground, out _, 1f, NavMesh.AllAreas) == false)
            {
                continue; // 길 지도 밖(좁은 틈, 물체 위 등)
            }

            Quaternion facePlayer = Quaternion.identity;
            Vector3 toPlayer = _player.position - ground;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                facePlayer = Quaternion.LookRotation(toPlayer);
            }

            Zombie zombie = Instantiate(_template, ground + Vector3.up * (0.05f - _footOffset), facePlayer);
            zombie.name = _template.name + "_Spawned";
            zombie.ApplyStatMultiplier(HealthMultiplier, DamageMultiplier);
            zombie.BeginHunt();
            _spawned.Add(zombie);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterZombie(zombie);
            }

            spawner.MarkUsed();
            return;
        }
    }

    // 플레이어 현재 위치에서 적당히 떨어져 있고, 가능하면 화면에 보이지 않는 스포너를 고른다.
    private ZombieSpawner PickSpawner(bool horde)
    {
        Camera view = Camera.main;
        List<ZombieSpawner> hidden = new List<ZombieSpawner>();
        List<ZombieSpawner> visible = new List<ZombieSpawner>();
        ZombieSpawner nearestOutside = null;
        float nearestOutsideDistance = float.MaxValue;

        foreach (ZombieSpawner spawner in _spawners)
        {
            if (spawner == null || (horde == false && spawner.IsReady == false))
            {
                continue;
            }

            Vector3 position = spawner.transform.position;
            float distance = Vector3.Distance(position, _player.position);
            if (distance < _spawnDistanceMin)
            {
                continue;
            }

            if (distance > _spawnDistanceMax)
            {
                // 범위 안에 스포너가 하나도 없을 때를 위해 가장 가까운 것을 기억해 둔다.
                if (distance < nearestOutsideDistance)
                {
                    nearestOutsideDistance = distance;
                    nearestOutside = spawner;
                }

                continue;
            }

            if (IsOnScreen(view, position))
            {
                visible.Add(spawner);
            }
            else
            {
                hidden.Add(spawner);
            }
        }

        List<ZombieSpawner> pool = hidden.Count > 0 ? hidden : visible;
        if (pool.Count > 0)
        {
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        return nearestOutside;
    }

    private static bool IsOnScreen(Camera view, Vector3 position)
    {
        if (view == null)
        {
            return false;
        }

        Vector3 viewport = view.WorldToViewportPoint(position + Vector3.up);
        return viewport.z > 0f && viewport.x > -0.1f && viewport.x < 1.1f && viewport.y > -0.1f && viewport.y < 1.1f;
    }

    // ----- 스포너 만들기 -----

    private void CreateSpawners()
    {
        Vector3 playerGround = _player.position;
        if (TryGetGround(_player.position, _player.position.y + 1f, out Vector3 ground))
        {
            playerGround = ground;
        }

        Bounds area = GetMapArea();
        List<Vector3> points = new List<Vector3>();

        // 1차: 조건을 모두 지키며 찾는다. 2차: 조건을 조금 완화해서 부족한 만큼 채운다.
        FindPoints(points, area, playerGround, _minDistanceFromPlayer, _minSpacing, _maxHeightDifference, 1500);
        if (points.Count < _spawnerCount / 2)
        {
            FindPoints(points, area, playerGround, _minDistanceFromPlayer * 0.6f, _minSpacing * 0.6f, _maxHeightDifference * 2f, 1000);
        }

        // 그래도 못 찾으면 처음 좀비들이 서 있던 자리를 쓴다.
        if (points.Count == 0)
        {
            foreach (Zombie zombie in FindObjectsByType<Zombie>(FindObjectsSortMode.None))
            {
                points.Add(zombie.transform.position);
            }
        }

        GameObject root = new GameObject("ZombieSpawners");
        root.transform.SetParent(transform, false);
        for (int i = 0; i < points.Count; i++)
        {
            GameObject go = new GameObject($"ZombieSpawner_{i + 1:00}");
            go.transform.SetParent(root.transform, false);
            go.transform.position = points[i];
            _spawners.Add(go.AddComponent<ZombieSpawner>());
        }

        Debug.Log($"ZombieSpawnManager: 스포너 {_spawners.Count}개를 무작위 위치에 만들었습니다.");
    }

    private void FindPoints(List<Vector3> points, Bounds area, Vector3 playerGround,
        float minDistance, float spacing, float maxHeightDiff, int attempts)
    {
        NavMeshPath path = new NavMeshPath();
        Vector3 playerNavPosition = playerGround;
        if (ZombieNavMesh.IsAvailable
            && NavMesh.SamplePosition(playerGround, out NavMeshHit playerHit, 3f, NavMesh.AllAreas))
        {
            playerNavPosition = playerHit.position;
        }

        for (int i = 0; i < attempts && points.Count < _spawnerCount; i++)
        {
            // 맵 전체에서 고르게 고른다. (플레이어 시작 위치 근처는 제외)
            Vector3 candidate = new Vector3(
                UnityEngine.Random.Range(area.min.x + _mapEdgeMargin, area.max.x - _mapEdgeMargin),
                playerGround.y,
                UnityEngine.Random.Range(area.min.z + _mapEdgeMargin, area.max.z - _mapEdgeMargin));

            Vector3 flat = candidate - playerGround;
            flat.y = 0f;
            if (flat.sqrMagnitude < minDistance * minDistance)
            {
                continue;
            }

            if (TryGetGround(candidate, area.max.y + 5f, out Vector3 ground) == false)
            {
                continue;
            }

            if (Mathf.Abs(ground.y - playerGround.y) > maxHeightDiff || HasRoomFor(ground) == false)
            {
                continue;
            }

            // 닫힌 건물 안처럼 플레이어에게 갈 길이 없는 곳은 제외
            if (IsReachable(ground, playerNavPosition, path) == false)
            {
                continue;
            }

            bool tooClose = false;
            foreach (Vector3 point in points)
            {
                if ((point - ground).sqrMagnitude < spacing * spacing)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose == false)
            {
                points.Add(ground);
            }
        }
    }

    // 위에서 아래로 광선을 쏴서 걸을 수 있는 바닥을 찾는다. (플레이어/좀비는 바닥으로 치지 않는다)
    private bool TryGetGround(Vector3 point, float fromHeight, out Vector3 ground)
    {
        ground = point;
        Vector3 origin = new Vector3(point.x, fromHeight, point.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, fromHeight - point.y + 50f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0)
        {
            return false;
        }

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (IsActor(hit.collider))
            {
                continue;
            }

            if (hit.normal.y < 0.7f)
            {
                return false; // 경사가 너무 급하거나 벽이다.
            }

            if (hit.collider.attachedRigidbody != null && hit.collider.attachedRigidbody.isKinematic == false)
            {
                return false; // 굴러다니는 물체(드럼통, 상자) 위는 제외
            }

            ground = hit.point;
            return true;
        }

        return false;
    }

    // 좀비 크기만큼 비어 있는지 확인한다.
    private bool HasRoomFor(Vector3 ground)
    {
        Vector3 bottom = ground + Vector3.up * (_clearRadius + 0.1f);
        Vector3 top = ground + Vector3.up * Mathf.Max(_clearRadius + 0.1f, _clearHeight - _clearRadius);
        // 벽, 상자, 다른 좀비, 플레이어 등 무엇이든 겹치면 안 된다.
        return Physics.CheckCapsule(bottom, top, _clearRadius * 0.9f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) == false;
    }

    private bool IsActor(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (_player != null && collider.transform.root == _player.root)
        {
            return true;
        }

        return collider.GetComponentInParent<Zombie>() != null;
    }

    // 씬의 고정 콜라이더(벽, 건물 등)를 모두 감싸는 범위
    // 아주 넓고 납작한 바닥(지면 Plane 등)은 맵 크기를 부풀리므로 뺀다.
    private Bounds GetMapArea()
    {
        if (_areaSize.x > 0f && _areaSize.z > 0f)
        {
            return new Bounds(_areaCenter, new Vector3(_areaSize.x, Mathf.Max(_areaSize.y, 20f), _areaSize.z));
        }

        bool found = false;
        Bounds bounds = new Bounds(_player.position, new Vector3(80f, 10f, 80f));
        foreach (Collider collider in FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            if (collider.isTrigger || IsActor(collider) || collider.attachedRigidbody != null)
            {
                continue;
            }

            Vector3 size = collider.bounds.size;
            if (size.y <= 1.01f && size.x * size.z > 2500f)
            {
                continue;
            }

            if (found)
            {
                bounds.Encapsulate(collider.bounds);
            }
            else
            {
                bounds = collider.bounds;
                found = true;
            }
        }

        return bounds;
    }

    private void PrepareTemplate()
    {
        if (_template != null)
        {
            return;
        }

        Zombie source = _zombiePrefab;
        bool fromScene = false;
        if (source == null)
        {
            source = FindAnyObjectByType<Zombie>();
            fromScene = true;
        }

        if (source == null)
        {
            return;
        }

        if (fromScene)
        {
            // 꺼진 부모 아래에 복제해 두면 복제본은 동작하지 않고 원본 상태만 보관된다.
            _templateHolder = new GameObject("ZombieTemplate (inactive)");
            _templateHolder.SetActive(false);
            _templateHolder.transform.SetParent(transform, false);
            _template = Instantiate(source, _templateHolder.transform, true);
            _template.name = source.name;
        }
        else
        {
            _template = source;
        }

        // 바닥에 정확히 세우기 위해 CharacterController 크기를 읽는다.
        CharacterController controller = _template.GetComponent<CharacterController>();
        if (controller != null)
        {
            float scaleY = Mathf.Abs(_template.transform.lossyScale.y);
            float scaleXZ = Mathf.Max(Mathf.Abs(_template.transform.lossyScale.x), Mathf.Abs(_template.transform.lossyScale.z));
            _footOffset = (controller.center.y - controller.height * 0.5f) * scaleY;
            _clearRadius = Mathf.Max(0.1f, controller.radius * scaleXZ);
            _clearHeight = Mathf.Max(_clearRadius * 2f, controller.height * scaleY);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_areaSize.x > 0f && _areaSize.z > 0f)
        {
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.6f);
            Gizmos.DrawWireCube(_areaCenter, _areaSize);
        }
    }
}
