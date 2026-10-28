using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// AI 디렉터 (Left 4 Dead) + 대규모 좀비 무리 (World War Z)
//
//  1. 대기   : 출발 안전가옥 안에서는 좀비가 나오지 않는다. 문 밖으로 나가면 시작한다.
//  2. 빌드업 : 플레이어 주변의 보이지 않는 곳에서 좀비가 조금씩 나와 몰려온다.
//  3. 피크   : 플레이어가 많이 맞아서 긴장도가 높아지면 잠깐 좀비를 멈춘다.
//  4. 휴식   : 숨 돌릴 시간. 좀비가 거의 나오지 않는다.
//  5. 호드   : 일정 시간마다 경적과 함께 좀비 무리가 한꺼번에 몰려온다.
//  6. 피날레 : 광장의 무전기에서 E로 구조를 부르면, 헬기가 올 때까지 거대한 무리(WWZ)가 계속 몰려온다.
//  7. 탈출   : 헬기가 오면 헬기장으로 달려가 탈출한다.
//
// 맵 정보(스폰 포인트, 경로, 안전가옥, 광장)는 CityMap의 CityMapInfo에서 읽는다.
// 원래 ZombieSpawner는 AI 디렉터가 대신하므로 시작할 때 끈다.
public class AIDirector : MonoBehaviour
{
    private enum Phase
    {
        Waiting,
        BuildUp,
        Peak,
        Relax,
        Finale,
        Escape,
        Done,
    }

    [Header("연결")]
    [SerializeField] private Zombie _zombiePrefab;
    [SerializeField] private CityMapInfo _map;

    [Header("평소 (빌드업)")]
    [SerializeField] private int _wanderers = 6;               // 길가에 미리 서 있는 좀비 수
    [SerializeField] private float _spawnInterval = 2.5f;      // 좀비가 하나씩 나오는 간격 (초)
    [SerializeField] private int _maxCommons = 14;             // 평소에 동시에 쫓아오는 최대 수
    [SerializeField] private float _spawnMinDistance = 18f;    // 이보다 가까운 곳에는 만들지 않는다.
    [SerializeField] private float _spawnMaxDistance = 55f;    // 이보다 먼 곳에는 만들지 않는다.

    [Header("긴장도 (피크와 휴식)")]
    [SerializeField] private float _peakIntensity = 0.75f;     // 긴장도가 이만큼 오르면 피크
    [SerializeField] private float _relaxTime = 18f;           // 피크 뒤에 숨 돌리는 시간 (초)

    [Header("호드")]
    [SerializeField] private Vector2 _hordeInterval = new Vector2(55f, 80f);
    [SerializeField] private int _hordeSize = 16;
    [SerializeField] private int _maxAlive = 32;

    [Header("피날레 (World War Z 무리)")]
    [SerializeField] private float _finaleTime = 100f;         // 헬기가 올 때까지 버티는 시간 (초)
    [SerializeField] private int _finaleMaxAlive = 40;
    [SerializeField] private float _finaleSpawnInterval = 1.2f;
    [SerializeField] private float _swarmInterval = 25f;       // 대규모 무리가 몰려오는 간격 (초)
    [SerializeField] private int _swarmSize = 20;
    [SerializeField] private float _extractionRadius = 5f;

    private readonly List<Zombie> _alive = new List<Zombie>();
    private readonly HashSet<Zombie> _hunters = new HashSet<Zombie>();  // 플레이어를 쫓는 좀비 (길가에 서 있는 좀비 제외)
    private Phase _phase = Phase.Waiting;
    private Transform _player;
    private Player _playerHealth;
    private Camera _camera;
    private DirectorHUD _hud;
    private AudioSource _audio;
    private AudioClip _hornClip;
    private AudioClip _alarmClip;
    private Transform _radio;
    private Light _beacon;

    private float _intensity;          // 0 ~ 1. 맞을수록 오르고 시간이 지나면 내려간다.
    private float _lastHealth;
    private float _calmTimer;
    private float _spawnTimer;
    private float _phaseTimer;
    private float _hordeTimer;
    private float _swarmTimer;
    private float _cleanupTimer;
    private int _kills;
    private int _hordeQueue;
    private float _hordeQueueTimer;
    private Vector3 _hordeOrigin;

    private void Awake()
    {
        // 원래 스포너는 AI 디렉터가 대신한다. (Start 전에 꺼야 스폰 코루틴이 시작되지 않는다)
        foreach (ZombieSpawner spawner in FindObjectsByType<ZombieSpawner>(FindObjectsSortMode.None))
        {
            spawner.enabled = false;
        }
    }

    private void Start()
    {
        if (_map == null)
        {
            _map = FindAnyObjectByType<CityMapInfo>();
        }

        PlayerMove move = FindAnyObjectByType<PlayerMove>();
        _player = move != null ? move.transform : null;
        _playerHealth = FindAnyObjectByType<Player>();
        _camera = Camera.main;

        if (_map == null || _player == null || _zombiePrefab == null)
        {
            Debug.LogError("[AIDirector] CityMapInfo, Player, Zombie Prefab 연결을 확인하세요.", this);
            enabled = false;
            return;
        }

        _lastHealth = _playerHealth != null ? _playerHealth.Health : 0f;
        _radio = FindChild(_map.transform, "Radio");

        _hud = DirectorHUD.Create();
        _hud.SetObjective("안전가옥을 나가 광장의 무전기로 가라");

        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;
        _hornClip = GameSounds.Get("sting_horde");
        if (_hornClip == null)
        {
            _hornClip = CreateHorn();
        }

        // 밤거리 환경음 (계속 반복)
        AudioClip ambience = GameSounds.Get("amb_night_loop");
        if (ambience != null)
        {
            AudioSource loop = gameObject.AddComponent<AudioSource>();
            loop.clip = ambience;
            loop.loop = true;
            loop.volume = 0.35f;
            loop.spatialBlend = 0f;
            loop.Play();
        }
        _alarmClip = CreateAlarm();

        SpawnWanderers();
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing || _phase == Phase.Done)
        {
            return;
        }

        float dt = Time.deltaTime;
        UpdateIntensity(dt);
        CleanUp(dt);
        UpdateHordeQueue(dt);
        _hud.SetInfo($"좀비 {_alive.Count}   처치 {_kills}");

        switch (_phase)
        {
            case Phase.Waiting:
                if (!InsideStartRoom())
                {
                    BeginRun();
                }

                break;

            case Phase.BuildUp:
                SpawnCommons(dt, _spawnInterval);
                _hordeTimer -= dt;
                if (_hordeTimer <= 0f)
                {
                    StartHorde(_hordeSize, "호드가 몰려온다!");
                    _hordeTimer = Random.Range(_hordeInterval.x, _hordeInterval.y);
                }

                if (_intensity >= _peakIntensity)
                {
                    ChangePhase(Phase.Peak, 5f);
                }

                break;

            case Phase.Peak:
                // 피크: 새 좀비를 만들지 않고, 지금 있는 좀비와 싸우게 한다.
                _phaseTimer -= dt;
                if (_phaseTimer <= 0f)
                {
                    ChangePhase(Phase.Relax, _relaxTime);
                }

                break;

            case Phase.Relax:
                // 휴식: 아주 가끔만 나온다.
                SpawnCommons(dt, _spawnInterval * 4f);
                _phaseTimer -= dt;
                if (_phaseTimer <= 0f)
                {
                    ChangePhase(Phase.BuildUp, 0f);
                }

                break;

            case Phase.Finale:
                UpdateFinale(dt);
                break;

            case Phase.Escape:
                UpdateEscape(dt);
                break;
        }

        CheckRadio();
    }

    // ===================== 진행 =====================

    private void BeginRun()
    {
        ChangePhase(Phase.BuildUp, 0f);
        _hordeTimer = Random.Range(35f, 50f);  // 첫 호드는 조금 일찍
        _hud.ShowAlert("생존자를 찾아라", new Color(1f, 0.9f, 0.7f), 2.5f);
    }

    private void ChangePhase(Phase next, float duration)
    {
        _phase = next;
        _phaseTimer = duration;
    }

    // 플레이어가 맞으면 긴장도가 오르고, 한동안 안 맞으면 내려간다.
    private void UpdateIntensity(float dt)
    {
        if (_playerHealth == null)
        {
            return;
        }

        float health = _playerHealth.Health;
        if (health < _lastHealth)
        {
            _intensity = Mathf.Clamp01(_intensity + (_lastHealth - health) / Mathf.Max(1f, _playerHealth.MaxHealth) * 2.5f);
            _calmTimer = 4f;
        }

        _lastHealth = health;
        _calmTimer -= dt;
        if (_calmTimer <= 0f)
        {
            _intensity = Mathf.Max(0f, _intensity - 0.05f * dt);
        }
    }

    // ===================== 좀비 만들기 =====================

    private void SpawnWanderers()
    {
        Transform[] route = _map.RoutePoints;
        if (route == null || route.Length < 3)
        {
            return;
        }

        // 출발점과 피날레는 빼고 경로 가운데에 흩어 놓는다. (가까이 가야 쫓아오는 좀비)
        for (int i = 0; i < _wanderers; i++)
        {
            Transform point = route[Random.Range(2, route.Length - 1)];
            Vector2 offset = Random.insideUnitCircle * 5f;
            SpawnZombie(point.position + new Vector3(offset.x, 0f, offset.y), false);
        }
    }

    private void SpawnCommons(float dt, float interval)
    {
        _spawnTimer -= dt;
        if (_spawnTimer > 0f)
        {
            return;
        }

        _spawnTimer = interval * Random.Range(0.7f, 1.3f);
        if (CountHunters() >= _maxCommons || _alive.Count >= _maxAlive)
        {
            return;
        }

        Transform point = PickSpawnPoint(false);
        if (point != null)
        {
            SpawnZombie(point.position, true);
        }
    }

    // 호드: 한 자리(보이지 않는 곳)에서 좀비가 짧은 간격으로 쏟아져 나온다.
    private void StartHorde(int size, string message)
    {
        Transform point = PickSpawnPoint(false);
        if (point == null)
        {
            return;
        }

        _hordeOrigin = point.position;
        _hordeQueue += size;
        _hordeQueueTimer = 0f;
        _hud.ShowAlert(message, new Color(1f, 0.2f, 0.15f), 3f);
        _audio.PlayOneShot(_hornClip, 0.9f);
    }

    private void UpdateHordeQueue(float dt)
    {
        if (_hordeQueue <= 0)
        {
            return;
        }

        int maxAlive = _phase == Phase.Finale || _phase == Phase.Escape ? _finaleMaxAlive : _maxAlive;
        _hordeQueueTimer -= dt;
        if (_hordeQueueTimer > 0f || _alive.Count >= maxAlive)
        {
            return;
        }

        _hordeQueueTimer = 0.15f;
        SpawnZombie(_hordeOrigin, true);
        _hordeQueue--;
    }

    private Zombie SpawnZombie(Vector3 position, bool hunt)
    {
        Vector2 jitter = Random.insideUnitCircle * 2f;
        Vector3 wanted = position + new Vector3(jitter.x, 0f, jitter.y);
        if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 4f, NavMesh.AllAreas))
        {
            return null;
        }

        Vector3 look = _player.position - hit.position;
        look.y = 0f;
        Quaternion rotation = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;
        Zombie zombie = Instantiate(_zombiePrefab, hit.position + Vector3.up, rotation);
        if (hunt)
        {
            zombie.BeginHunt();
            _hunters.Add(zombie);
        }

        zombie.Died += OnZombieDied;
        _alive.Add(zombie);
        return zombie;
    }

    private void OnZombieDied(Zombie zombie)
    {
        zombie.Died -= OnZombieDied;
        _alive.Remove(zombie);
        _hunters.Remove(zombie);
        _kills++;
    }

    // 평소: 플레이어에서 적당히 떨어져 있고 화면에 보이지 않는 스폰 포인트
    // 피날레: 광장 안의 스폰 포인트
    private Transform PickSpawnPoint(bool finale)
    {
        Transform[] points = _map.SpawnPoints;
        if (points == null || points.Length == 0)
        {
            return null;
        }

        List<Transform> hidden = new List<Transform>();
        List<Transform> visible = new List<Transform>();
        foreach (Transform point in points)
        {
            if (point == null)
            {
                continue;
            }

            if (finale)
            {
                if (_map.FinaleArea != null && InsideFlat(_map.FinaleArea.bounds, point.position))
                {
                    (IsVisible(point.position) ? visible : hidden).Add(point);
                }

                continue;
            }

            float distance = Vector3.Distance(point.position, _player.position);
            if (distance < _spawnMinDistance || distance > _spawnMaxDistance)
            {
                continue;
            }

            (IsVisible(point.position) ? visible : hidden).Add(point);
        }

        if (hidden.Count > 0)
        {
            return hidden[Random.Range(0, hidden.Count)];
        }

        return visible.Count > 0 ? visible[Random.Range(0, visible.Count)] : null;
    }

    private bool IsVisible(Vector3 position)
    {
        if (_camera == null)
        {
            return false;
        }

        Vector3 target = position + Vector3.up * 1.5f;
        Vector3 viewport = _camera.WorldToViewportPoint(target);
        if (viewport.z <= 0f || viewport.x < -0.1f || viewport.x > 1.1f || viewport.y < -0.1f || viewport.y > 1.1f)
        {
            return false;
        }

        // 가운데에 건물이 막고 있으면 보이지 않는다.
        return !Physics.Linecast(_camera.transform.position, target, ~0, QueryTriggerInteraction.Ignore);
    }

    private int CountHunters()
    {
        _hunters.RemoveWhere(z => z == null);
        return _hunters.Count;
    }

    // 플레이어에게서 아주 멀리 떨어진 좀비는 치운다. (L4D처럼 뒤에 남은 좀비 정리)
    private void CleanUp(float dt)
    {
        _cleanupTimer -= dt;
        if (_cleanupTimer > 0f)
        {
            return;
        }

        _cleanupTimer = 2f;
        for (int i = _alive.Count - 1; i >= 0; i--)
        {
            Zombie zombie = _alive[i];
            if (zombie == null)
            {
                _alive.RemoveAt(i);
                continue;
            }

            if (_phase != Phase.Finale && _phase != Phase.Escape && _hunters.Contains(zombie)
                && Vector3.Distance(zombie.transform.position, _player.position) > 90f && !IsVisible(zombie.transform.position))
            {
                zombie.Died -= OnZombieDied;
                _alive.RemoveAt(i);
                _hunters.Remove(zombie);
                Destroy(zombie.gameObject);
            }
        }
    }

    // ===================== 피날레 =====================

    private void CheckRadio()
    {
        if (_phase == Phase.Finale || _phase == Phase.Escape || _phase == Phase.Waiting)
        {
            _hud.SetPrompt(string.Empty);
            return;
        }

        bool inPlaza = _map.FinaleArea != null && InsideFlat(_map.FinaleArea.bounds, _player.position);
        if (!inPlaza)
        {
            _hud.SetPrompt(string.Empty);
            return;
        }

        Vector3 radio = _radio != null ? _radio.position : _map.ExtractionPoint.position;
        bool near = Vector3.Distance(Flat(radio), Flat(_player.position)) < 3f;
        _hud.SetObjective(near ? "무전기로 구조를 요청하라" : "광장 가운데의 무전기로 가라");
        _hud.SetPrompt(near ? "[E] 구조 요청" : string.Empty);
        if (near && Input.GetKeyDown(KeyCode.E))
        {
            StartFinale();
        }
    }

    private void StartFinale()
    {
        ChangePhase(Phase.Finale, _finaleTime);
        _swarmTimer = 8f;
        _hud.SetPrompt(string.Empty);
        _hud.SetObjective("구조 헬기가 올 때까지 버텨라!");
        _hud.ShowAlert("구조 요청 완료! 놈들이 몰려온다!", new Color(1f, 0.3f, 0.2f), 3.5f);
        _audio.PlayOneShot(_alarmClip, 0.8f);
        _audio.PlayOneShot(_hornClip, 0.9f);
    }

    private void UpdateFinale(float dt)
    {
        _phaseTimer -= dt;
        _hud.SetTimer($"헬기 도착까지 {Mathf.CeilToInt(Mathf.Max(0f, _phaseTimer))}초");

        // 쉬지 않고 광장 곳곳에서 나온다.
        _spawnTimer -= dt;
        if (_spawnTimer <= 0f && _alive.Count < _finaleMaxAlive)
        {
            _spawnTimer = _finaleSpawnInterval;
            Transform point = PickSpawnPoint(true);
            if (point != null)
            {
                SpawnZombie(point.position, true);
            }
        }

        // World War Z: 대규모 무리가 한쪽에서 파도처럼 밀려온다.
        _swarmTimer -= dt;
        if (_swarmTimer <= 0f)
        {
            _swarmTimer = _swarmInterval;
            Transform point = PickSpawnPoint(true);
            if (point != null)
            {
                _hordeOrigin = point.position;
                _hordeQueue += _swarmSize;
                _hud.ShowAlert("대규모 무리가 밀려온다!", new Color(1f, 0.15f, 0.1f), 3f);
                _audio.PlayOneShot(_hornClip, 1f);
            }
        }

        if (_phaseTimer <= 0f)
        {
            ChangePhase(Phase.Escape, 0f);
            _hud.SetTimer(string.Empty);
            _hud.SetObjective("헬기가 왔다! 헬기장으로 달려라!");
            _hud.ShowAlert("헬기 도착!", new Color(0.4f, 1f, 0.5f), 3f);
            _audio.PlayOneShot(_alarmClip, 0.8f);
            CreateBeacon();
        }
    }

    private void UpdateEscape(float dt)
    {
        // 탈출하는 동안에도 계속 몰려온다.
        _spawnTimer -= dt;
        if (_spawnTimer <= 0f && _alive.Count < _finaleMaxAlive)
        {
            _spawnTimer = _finaleSpawnInterval;
            Transform point = PickSpawnPoint(true);
            if (point != null)
            {
                SpawnZombie(point.position, true);
            }
        }

        if (_beacon != null)
        {
            _beacon.intensity = 6f + Mathf.Sin(Time.time * 6f) * 3f;
        }

        Vector3 extraction = _map.ExtractionPoint.position;
        if (Vector3.Distance(Flat(extraction), Flat(_player.position)) <= _extractionRadius)
        {
            _phase = Phase.Done;
            _hud.SetObjective(string.Empty);
            _hud.SetInfo($"처치 {_kills}");
            GameManager.Instance.GameClear();
        }
    }

    // 헬기장에 초록 신호 빛
    private void CreateBeacon()
    {
        GameObject beacon = new GameObject("ExtractionBeacon");
        beacon.transform.position = _map.ExtractionPoint.position + Vector3.up * 4f;
        _beacon = beacon.AddComponent<Light>();
        _beacon.type = LightType.Point;
        _beacon.color = new Color(0.3f, 1f, 0.4f);
        _beacon.range = 18f;
        _beacon.intensity = 6f;
    }

    private bool InsideStartRoom()
    {
        BoxCollider area = _map.StartSafeRoomArea;
        return area != null && area.bounds.Contains(_player.position);
    }

    // ===================== 소리 (파일 없이 코드로 만든다) =====================

    // 호드 경적: 낮게 울리는 사이렌
    private static AudioClip CreateHorn()
    {
        const int rate = 22050;
        const float length = 2.6f;
        int samples = (int)(rate * length);
        float[] data = new float[samples];
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            float frequency = 90f + 40f * Mathf.Sin(t * 1.3f);
            phase += frequency / rate;
            float envelope = Mathf.Clamp01(t * 4f) * Mathf.Clamp01((length - t) * 1.5f);
            float wave = Mathf.Sin(phase * 2f * Mathf.PI) * 0.6f + Mathf.Sin(phase * 4f * Mathf.PI) * 0.25f + Mathf.Sin(phase * 6.03f * Mathf.PI) * 0.15f;
            data[i] = wave * envelope * 0.8f;
        }

        AudioClip clip = AudioClip.Create("HordeHorn", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // 무전/경보: 짧은 삐삐 소리
    private static AudioClip CreateAlarm()
    {
        const int rate = 22050;
        const float length = 1.2f;
        int samples = (int)(rate * length);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            bool on = (int)(t * 5f) % 2 == 0;
            data[i] = on ? Mathf.Sign(Mathf.Sin(t * 880f * 2f * Mathf.PI)) * 0.25f : 0f;
        }

        AudioClip clip = AudioClip.Create("Alarm", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ===================== 도우미 =====================

    private static bool InsideFlat(Bounds bounds, Vector3 position)
    {
        return position.x >= bounds.min.x && position.x <= bounds.max.x && position.z >= bounds.min.z && position.z <= bounds.max.z;
    }

    private static Vector3 Flat(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }
}
