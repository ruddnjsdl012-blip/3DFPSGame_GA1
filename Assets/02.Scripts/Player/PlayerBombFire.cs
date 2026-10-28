using UnityEngine;

// 마우스 올느쪽 버튼을 누르면 시선이 바라보는 방향으로 수류탄을 던지고 싶다.
public class PlayerBombFire : MonoBehaviour
{
    [SerializeField] private Transform _firePoint;
    [SerializeField] private Bomb _bombPrefab;
    [SerializeField] private float _throwPower = 15f;

    private PlayerInventory _inventory;  // 수류탄 개수 (없으면 무제한)

    private void Start()
    {
        _inventory = GetComponent<PlayerInventory>();
    }

    private void Update()
    {
        if (GameManager.Instance.State != GameState.Playing) return;

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
