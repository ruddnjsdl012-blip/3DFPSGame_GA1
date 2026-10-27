using System;
using System.Collections;
using UnityEngine;

// 좀비 스포너: 일정 간격마다 랜덤한 스폰 포인트에 좀비를 생성한다.
// - 동시에 살아 있는 좀비 수는 _maxCount까지만 허용한다.
// - 좀비가 죽으면 Zombie.Died 이벤트를 통해 살아 있는 수를 줄인다. (옵저버 패턴)
// - 살아 있는 수가 바뀔 때마다 OnCountChanged 이벤트로 UI 같은 관찰자에게 알린다.
public class ZombieSpawner : MonoBehaviour
{
    [Header("스폰 설정")]
    [SerializeField] private Zombie _zombiePrefab;
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private float _spawnInterval = 3f;   // 스폰 간격(초)
    [SerializeField] private int _maxCount = 10;          // 동시에 살아 있을 수 있는 최대 좀비 수

    private int _aliveCount = 0;

    public int AliveCount => _aliveCount;
    public int MaxCount => _maxCount;

    // 살아 있는 좀비 수가 바뀔 때마다 발행한다.
    public event Action OnCountChanged = null;

    private void Start()
    {
        StartCoroutine(Spawn_Coroutine());
    }

    private IEnumerator Spawn_Coroutine()
    {
        WaitForSeconds wait = new WaitForSeconds(_spawnInterval);

        while (true)
        {
            yield return wait;

            // 게임오버가 되면 더 이상 스폰하지 않는다.
            if (GameManager.Instance.State == GameState.GameOver) yield break;

            // 준비 중에는 스폰하지 않고 다음 턴을 기다린다.
            if (GameManager.Instance.State != GameState.Playing) continue;

            // 최대 수에 도달했으면 빈자리가 날 때까지 기다린다.
            if (_aliveCount >= _maxCount) continue;

            Spawn();
        }
    }

    private void Spawn()
    {
        if (_spawnPoints == null || _spawnPoints.Length == 0)
        {
            Debug.LogWarning("ZombieSpawner: 스폰 포인트가 없습니다.");
            return;
        }

        // Random.Range(int, int)의 최대값은 포함되지 않는다.
        Transform point = _spawnPoints[UnityEngine.Random.Range(0, _spawnPoints.Length)];

        Zombie zombie = Instantiate(_zombiePrefab, point.position, point.rotation);
        zombie.Died += OnZombieDied;
        _aliveCount++;
        OnCountChanged?.Invoke();

        Debug.Log($"좀비 스폰: {_aliveCount}/{_maxCount}");
    }

    private void OnZombieDied(Zombie zombie)
    {
        zombie.Died -= OnZombieDied;
        _aliveCount--;
        OnCountChanged?.Invoke();

        Debug.Log($"좀비 사망: {_aliveCount}/{_maxCount}");
    }
}
