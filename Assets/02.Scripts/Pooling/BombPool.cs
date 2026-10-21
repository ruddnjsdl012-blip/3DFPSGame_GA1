using System.Collections.Generic;
using UnityEngine;

// Stack: 대기 중인 객체.
// HashSet: 대여 중인 객체.
public class BombPool : MonoBehaviour
{
    private Stack<Bomb> _availableBombs;
    private Stack<PooledBombExplosion> _availableEffects;

    private readonly HashSet<Bomb> _activeBombs = new HashSet<Bomb>();

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
        if (_initialized)
        {
            return _prefab == prefab && Capacity == capacity;
        }

        if (prefab == null
            || prefab.GetComponent<Rigidbody>() == null
            || prefab.GetComponent<SphereCollider>() == null
            || prefab.ExplosionEffectPrefab == null)
        {
            Debug.LogError(
                "BombPool: 폭탄 프리팹에 Rigidbody, SphereCollider, 폭발 이펙트가 필요합니다.",
                this);

            return false;
        }

        _prefab = prefab;
        Capacity = Mathf.Max(1, capacity);

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
            bomb.Initialize(this);

            _availableBombs.Push(bomb);

            ParticleSystem particles = Instantiate(
                prefab.ExplosionEffectPrefab,
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

            candidate.Launch(
                position,
                rotation,
                impulse,
                ownerColliders);

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

        bomb.Deactivate();
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

    private void OnDestroy()
    {
        _disposing = true;

        _activeBombs.Clear();
        _activeEffects.Clear();

        // 소유자나 씬이 제거될 때 실제 객체를 정리한다.
        if (_poolRoot != null)
        {
            Destroy(_poolRoot);
        }
    }
}