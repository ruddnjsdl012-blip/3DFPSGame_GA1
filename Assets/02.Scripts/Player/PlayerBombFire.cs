using UnityEngine;

// PlayerGrenade가 4번으로 들고 왼쪽 클릭을 떼면 Throw를 부른다. 우클릭은 PlayerShove 전용이다.
public class PlayerBombFire : MonoBehaviour
{
    [SerializeField] private Transform _firePoint;
    [SerializeField] private Bomb _bombPrefab;
    [SerializeField] private float _throwPower = 15f;

    public Bomb BombPrefab => _bombPrefab;

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

}
