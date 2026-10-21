using UnityEngine;

public class PlayerBombFire : MonoBehaviour
{
    [SerializeField] private Transform _firePoint;
    [SerializeField] private Bomb _bombPrefab;
    [SerializeField] private float _throwPower = 15f;

    private void Update()
    {
        // 마우스 오른쪽 버튼을 누른 순간 수류탄 발사
        if (Input.GetButtonDown("Fire2"))
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        // 발사 위치에 수류탄 생성
        Bomb bomb = Instantiate(
            _bombPrefab,
            _firePoint.position,
            Quaternion.identity
        );

        // 생성한 수류탄의 Rigidbody 가져오기
        Rigidbody rigidBody = bomb.GetComponent<Rigidbody>();

        // 카메라가 바라보는 방향으로 던지기
        rigidBody.AddForce(
            Camera.main.transform.forward * _throwPower,
            ForceMode.Impulse
        );
    }
}