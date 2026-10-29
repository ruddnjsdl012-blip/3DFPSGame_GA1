using UnityEngine;

// 마우스 올느쪽 버튼을 누르면 시선이 바라보는 방향으로 수류탄을 던지고 싶다.
// → 지금은 PlayerGrenade가 있으면 4번으로 수류탄을 들고, 왼쪽 클릭을 누르고 있다 떼서 던진다. (Throw를 부른다)
public class PlayerBombFire : MonoBehaviour
{
    [SerializeField] private Transform _firePoint;
    [SerializeField] private Bomb _bombPrefab;
    [SerializeField] private float _throwPower = 15f;

    private PlayerInventory _inventory;  // 수류탄 개수 (없으면 무제한)
    private PlayerGrenade _grenade;      // 4번 들기 + 길게 눌러 멀리 던지기

    public Bomb BombPrefab => _bombPrefab;  // 손에 든 수류탄 모양 (GrenadeArmView)

    private void Start()
    {
        _inventory = GetComponent<PlayerInventory>();

        _grenade = FindAnyObjectByType<PlayerGrenade>();  // PlayerGunFire가 Player에 붙여 둔다.
    }

    private void Update()
    {
        if (GameManager.Instance.State != GameState.Playing) return;

        // 4번으로 들고 왼쪽 클릭으로 던지는 방식을 쓰면 오른쪽 클릭 바로 던지기는 쓰지 않는다.
        if (_grenade != null) return;

        // 1. 마우스 오른쪽 버튼 입력 받기
        if (Input.GetButtonDown("Fire2"))
        {
            // 치료 중에는 못 던지고, 수류탄이 있을 때만 하나 써서 던진다.
            if (_inventory == null)
            {
                Shoot();
            }
            else if (!_inventory.IsHealing && _inventory.TryUseGrenade())
            {
                Shoot();
            }
        }

    }

    // 던질 자리와 처음 속도 (PlayerGrenade가 포물선을 미리 그릴 때도 쓴다)
    // power: 원래 던지는 힘(_throwPower)의 배율, upward: 위로 얼마나 띄울지
    public void GetLaunch(float power, float upward, out Vector3 start, out Vector3 velocity)
    {
        Transform view = Camera.main.transform;
        start = _firePoint != null ? _firePoint.position : view.position + view.forward * 0.5f;
        Vector3 direction = (view.forward + Vector3.up * upward).normalized;
        Rigidbody body = _bombPrefab != null ? _bombPrefab.GetComponent<Rigidbody>() : null;
        float mass = body != null ? Mathf.Max(0.01f, body.mass) : 1f;
        velocity = direction * (_throwPower * power / mass);  // Impulse 힘 / 질량 = 속도
    }

    public void Throw(float power, float upward)
    {
        GetLaunch(power, upward, out Vector3 start, out Vector3 velocity);
        Bomb bomb = Instantiate(_bombPrefab, start, Random.rotation);
        GrenadeModel.ReplaceVisual(bomb.gameObject);  // 날아가는 것도 손에 든 것과 같은 수류탄 모양
        GameSounds.Play2D("bomb_throw", 0.7f);

        Rigidbody rigidbody = bomb.GetComponent<Rigidbody>();
        rigidbody.AddForce(velocity * rigidbody.mass, ForceMode.Impulse);
        rigidbody.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);  // 빙글빙글 돌며 날아간다.
    }

    private void Shoot()
    {
        // 2. 발사 위치에서 수류탄 생성하기
        Bomb bomb = Instantiate(_bombPrefab, _firePoint.position, Quaternion.identity);
        GameSounds.Play2D("bomb_throw", 0.7f);

        // 3. 생성된 수류탄을 시선 방향으로 던지기
        Rigidbody rigidbody = bomb.GetComponent<Rigidbody>();
        rigidbody.AddForce(Camera.main.transform.forward * _throwPower, ForceMode.Impulse);
    }

}
