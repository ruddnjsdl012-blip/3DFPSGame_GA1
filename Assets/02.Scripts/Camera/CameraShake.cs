using UnityEngine;

// 카메라 흔들림. Main Camera에 붙는다. (GunFeedback이 없으면 자동으로 붙인다)
// CameraRotate(Update)와 CameraFollow(LateUpdate)가 카메라를 놓은 뒤, 마지막에 살짝 흔든다.
// 다음 프레임에는 두 스크립트가 다시 제자리에 놓으므로 흔들림이 쌓이지 않는다.
[DefaultExecutionOrder(1000)]
public class CameraShake : MonoBehaviour
{
    [SerializeField] private float _maxAngle = 1.6f;      // 가장 세게 흔들릴 때의 각도 (도)
    [SerializeField] private float _maxOffset = 0.035f;   // 가장 세게 흔들릴 때의 위치 (m)
    [SerializeField] private float _frequency = 25f;      // 흔들리는 빠르기
    [SerializeField] private float _decay = 2.2f;         // 흔들림이 줄어드는 빠르기 (1초에)

    private float _trauma;  // 0 ~ 1
    private float _seed;

    // 흔든다. amount: 0 ~ 1 (총 한 발 0.15, 폭발 0.6 정도)
    public void Shake(float amount)
    {
        _trauma = Mathf.Clamp01(_trauma + amount);
    }

    private void Awake()
    {
        _seed = Random.Range(0f, 100f);
    }

    private void LateUpdate()
    {
        if (_trauma <= 0f)
        {
            return;
        }

        _trauma = Mathf.Max(0f, _trauma - _decay * Time.deltaTime);
        float power = _trauma * _trauma;
        float time = Time.time * _frequency;
        float pitch = (Mathf.PerlinNoise(_seed, time) * 2f - 1f) * _maxAngle * power;
        float yaw = (Mathf.PerlinNoise(_seed + 10f, time) * 2f - 1f) * _maxAngle * power;
        float roll = (Mathf.PerlinNoise(_seed + 20f, time) * 2f - 1f) * _maxAngle * power;
        Vector3 offset = new Vector3(Mathf.PerlinNoise(_seed + 30f, time) * 2f - 1f, Mathf.PerlinNoise(_seed + 40f, time) * 2f - 1f, 0f) * (_maxOffset * power);

        transform.rotation *= Quaternion.Euler(pitch, yaw, roll);
        transform.position += transform.rotation * offset;
    }

    // 어디서든 카메라를 흔든다. (폭발 등)
    public static void ShakeMain(float amount)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        CameraShake shake = camera.GetComponent<CameraShake>();
        if (shake == null)
        {
            shake = camera.gameObject.AddComponent<CameraShake>();
        }

        shake.Shake(amount);
    }

    // 폭발처럼 위치가 있는 충격: 가까울수록 세게
    public static void ShakeAt(Vector3 position, float amount, float radius)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        float distance = Vector3.Distance(camera.transform.position, position);
        float falloff = Mathf.Clamp01(1f - distance / radius);
        if (falloff > 0f)
        {
            ShakeMain(amount * falloff);
        }
    }
}
