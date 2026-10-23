using System.Collections.Generic;
using UnityEngine;

// Stack: 대기 중인 객체.
// HashSet: 대여 중인 객체.
// 현재 Bomb의 충돌 코드는 Destroy를 사용한다. 재사용은 Return 호출이 필요하다.
public class BombPool : MonoBehaviour
{
    // 현재 Bomb은 폭발 이펙트를 직접 관리한다.
    // 이 풀에서 이펙트도 대여할 때만 별도 프리팹을 지정한다.
    [SerializeField] private ParticleSystem _explosionEffectPrefab;

    private Stack<Bomb> _availableBombs;
    private Stack<PooledBombExplosion> _availableEffects;

    private readonly Dictionary<Bomb, Collider[]> _ownerColliders =
        new Dictionary<Bomb, Collider[]>();

    private readonly HashSet<Bomb> _activeBombs =
        new HashSet<Bomb>();

    private readonly HashSet<PooledBombExplosion> _activeEffects =
        new HashSet<PooledBombExplosion>();

    private GameObject _poolRoot;
    private Bomb _prefab;

    private bool _initialized;
    private bool _disposing;

    public int Capacity { get; private set; }

    public int ActiveBombCount => _activeBombs.Count;

    public int AvailableBombCount =>
        _availableBombs == null ? 0 : _availableBombs.Count;

    public int ActiveEffectCount => _activeEffects.Count;

    public int AvailableEffectCount =>
        _availableEffects == null ? 0 : _availableEffects.Count;

    public bool Initialize(Bomb prefab, int capacity)
    {
        capacity = Mathf.Max(1, capacity);

        if (_initialized)
        {
            return _prefab == prefab && Capacity == capacity;
        }

        if (prefab == null
            || prefab.GetComponent<Rigidbody>() == null
            || prefab.GetComponent<SphereCollider>() == null)
        {
            Debug.LogError(
                "BombPool: 폭탄 프리팹에 Rigidbody와 SphereCollider가 필요합니다.",
                this);

            return false;
        }

        _prefab = prefab;
        Capacity = capacity;

        _availableBombs = new Stack<Bomb>(Capacity);
        _availableEffects = new Stack<PooledBombExplosion>(Capacity);

        // 플레이어 이동과 회전에 끌려가지 않도록 월드 루트에 둔다.
        _poolRoot = new GameObject("BombPool_Objects");
        _poolRoot.SetActive(false);

        for (int i = 0; i < Capacity; i++)
        {
            Bomb bomb = Instantiate(prefab, _poolRoot.transform);

            bomb.name = "Bomb_" + (i + 1).ToString("00");
            bomb.gameObject.SetActive(false);

            _availableBombs.Push(bomb);

            if (_explosionEffectPrefab == null)
            {
                continue;
            }

            ParticleSystem particles = Instantiate(
                _explosionEffectPrefab,
                _poolRoot.transform);

            particles.name = "BombExplosion_" + (i + 1).ToString("00");
            particles.gameObject.SetActive(false);

            PooledBombExplosion effect =
                particles.GetComponent<PooledBombExplosion>();

            if (effect == null)
            {
                effect = particles.gameObject.AddComponent<PooledBombExplosion>();
            }

            effect.Initialize(this);
            _availableEffects.Push(effect);
        }

        _initialized = true;
        _poolRoot.SetActive(true);

        return true;
    }

    public bool TrySpawn(
        Vector3 position,
        Quaternion rotation,
        Vector3 impulse,
        Collider[] ownerColliders,
        out Bomb bomb)
    {
        bomb = null;

        if (_initialized == false || _disposing)
        {
            return false;
        }

        while (_availableBombs.Count > 0)
        {
            Bomb candidate = _availableBombs.Pop();

            if (candidate == null)
            {
                continue;
            }

            _activeBombs.Add(candidate);

            Rigidbody body = candidate.GetComponent<Rigidbody>();
            Collider bombCollider = candidate.GetComponent<SphereCollider>();

            candidate.transform.SetPositionAndRotation(position, rotation);

            ResetVelocity(body);
            _ownerColliders[candidate] = ownerColliders;

            candidate.gameObject.SetActive(true);

            SetOwnerCollision(bombCollider, ownerColliders, true);
            body.AddForce(impulse, ForceMode.Impulse);

            bomb = candidate;
            return true;
        }

        // 풀이 비어 있으면 새로 만들지 않고 실패를 반환한다.
        return false;
    }

    internal void Explode(
        Bomb bomb,
        Vector3 position,
        Quaternion rotation)
    {
        if (_disposing
            || bomb == null
            || _activeBombs.Contains(bomb) == false)
        {
            return;
        }

        while (_availableEffects.Count > 0)
        {
            PooledBombExplosion effect = _availableEffects.Pop();

            if (effect == null)
            {
                continue;
            }

            _activeEffects.Add(effect);
            effect.PlayAt(position, rotation);
            break;
        }

        Return(bomb);
    }

    public void Return(Bomb bomb)
    {
        // 다른 풀의 객체 및 이미 반납한 객체는 받지 않는다.
        if (_disposing
            || bomb == null
            || _activeBombs.Remove(bomb) == false)
        {
            return;
        }

        Collider[] ownerColliders;

        if (_ownerColliders.TryGetValue(bomb, out ownerColliders))
        {
            SetOwnerCollision(
                bomb.GetComponent<SphereCollider>(),
                ownerColliders,
                false);

            _ownerColliders.Remove(bomb);
        }

        ResetVelocity(bomb.GetComponent<Rigidbody>());

        bomb.gameObject.SetActive(false);
        _availableBombs.Push(bomb);
    }

    internal void Return(PooledBombExplosion effect)
    {
        if (_disposing
            || effect == null
            || _activeEffects.Remove(effect) == false)
        {
            return;
        }

        effect.Deactivate();
        _availableEffects.Push(effect);
    }

    private static void ResetVelocity(Rigidbody body)
    {
#if UNITY_6000_0_OR_NEWER
        body.linearVelocity = Vector3.zero;
#else
        body.velocity = Vector3.zero;
#endif
        body.angularVelocity = Vector3.zero;
    }

    private static void SetOwnerCollision(
        Collider bombCollider,
        Collider[] ownerColliders,
        bool ignore)
    {
        if (ownerColliders == null)
        {
            return;
        }

        for (int i = 0; i < ownerColliders.Length; i++)
        {
            Collider ownerCollider = ownerColliders[i];

            if (ownerCollider == null || ownerCollider == bombCollider)
            {
                continue;
            }

            Physics.IgnoreCollision(bombCollider, ownerCollider, ignore);
        }
    }

    private void OnDestroy()
    {
        _disposing = true;

        _activeBombs.Clear();
        _activeEffects.Clear();
        _ownerColliders.Clear();

        // 소유자나 씬이 제거될 때 실제 객체를 정리한다.
        if (_poolRoot != null)
        {
            Destroy(_poolRoot);
        }
    }
}