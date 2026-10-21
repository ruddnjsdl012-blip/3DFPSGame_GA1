using UnityEngine;

// 충돌 시 폭발 이펙트를 재생하고 풀로 돌아간다.
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class Bomb : MonoBehaviour
{
    [SerializeField]
    private ParticleSystem _explosionEffectPrefab;

    [SerializeField, Min(0.1f)]
    private float _maxFlightTime = 12f;

    private BombPool _pool;
    private Rigidbody _body;
    private Collider[] _colliders;
    private Collider[] _ownerColliders;

    private float _lifeRemaining;
    private bool _inFlight;

    public ParticleSystem ExplosionEffectPrefab => _explosionEffectPrefab;
    public bool IsInFlight => _inFlight;

    public float CollisionRadius
    {
        get
        {
            SphereCollider sphere = GetComponent<SphereCollider>();
            Vector3 scale = transform.lossyScale;

            return sphere.radius * Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        }
    }

    internal void Initialize(BombPool pool)
    {
        _pool = pool;
        _body = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>(true);
    }

    internal void Launch(
        Vector3 position,
        Quaternion rotation,
        Vector3 impulse,
        Collider[] owner)
    {
        transform.SetPositionAndRotation(position, rotation);

        _ownerColliders = owner;
        _lifeRemaining = Mathf.Max(0.1f, _maxFlightTime);
        _inFlight = true;

        gameObject.SetActive(true);

        _body.isKinematic = false;
        _body.useGravity = true;
        _body.linearVelocity = Vector3.zero;
        _body.angularVelocity = Vector3.zero;

        SetOwnerCollisionIgnored(true);

        _body.WakeUp();
        _body.AddForce(impulse, ForceMode.Impulse);
    }

    private void Update()
    {
        if (_inFlight == false)
        {
            return;
        }

        _lifeRemaining -= Time.deltaTime;

        // 충돌할 바닥이 없어도 일정 시간이 지나면 회수한다.
        if (_lifeRemaining <= 0f || transform.position.y < -1000f)
        {
            _pool.Return(this);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_inFlight == false || _pool == null)
        {
            return;
        }

        if (_ownerColliders != null)
        {
            for (int i = 0; i < _ownerColliders.Length; i++)
            {
                if (_ownerColliders[i] == collision.collider)
                {
                    return;
                }
            }
        }

        Vector3 position = transform.position;
        Quaternion rotation = Quaternion.identity;

        if (collision.contactCount > 0)
        {
            ContactPoint contact = collision.GetContact(0);

            position = contact.point + contact.normal * 0.03f;
            rotation = Quaternion.LookRotation(contact.normal);
        }

        _pool.Explode(this, position, rotation);
    }

    internal void Deactivate()
    {
        _inFlight = false;
        _lifeRemaining = 0f;

        _body.linearVelocity = Vector3.zero;
        _body.angularVelocity = Vector3.zero;

        SetOwnerCollisionIgnored(false);
        _ownerColliders = null;

        _body.Sleep();
        gameObject.SetActive(false);
    }

    private void SetOwnerCollisionIgnored(bool ignored)
    {
        if (_ownerColliders == null)
        {
            return;
        }

        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] == null)
            {
                continue;
            }

            for (int j = 0; j < _ownerColliders.Length; j++)
            {
                if (_ownerColliders[j] != null)
                {
                    Physics.IgnoreCollision(
                        _colliders[i],
                        _ownerColliders[j],
                        ignored);
                }
            }
        }
    }

    private void OnDisable()
    {
        // 외부에서 비활성화해도 풀로 반납한다.
        if (_inFlight && _pool != null)
        {
            _pool.Return(this);
        }
    }
}