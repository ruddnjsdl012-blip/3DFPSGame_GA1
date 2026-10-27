using UnityEngine;

// 화면 흔들림 (탱크 발소리/주먹, 부머 폭발 등).
// 시점(위아래/좌우 회전)을 잠깐 흔들었다가 정확히 원래 각도로 돌려놓는다. (조준이 틀어지지 않음)
// CameraShake.Add(세기)로 부르면 처음 한 번 자동으로 만들어진다.
[DisallowMultipleComponent]
public class CameraShake : MonoBehaviour
{
    [Tooltip("세기 1일 때 최대 흔들림 각도 (도)")]
    [SerializeField, Min(0f)] private float _maxAngle = 2.5f;
    [Tooltip("흔들림이 줄어드는 속도 (초당)")]
    [SerializeField, Min(0.1f)] private float _decay = 1.6f;
    [SerializeField, Min(1f)] private float _frequency = 22f;

    private static CameraShake _instance;

    private CameraRotate _cameraRotate;
    private PlayerRotate _playerRotate;
    private float _trauma;
    private float _pitch;
    private float _yaw;
    private float _seed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _instance = null;
    }

    // strength: 0 ~ 1 (1 = 아주 강하게). 여러 번 부르면 더해진다.
    public static void Add(float strength)
    {
        if (strength <= 0f)
        {
            return;
        }

        if (_instance == null)
        {
            _instance = FindAnyObjectByType<CameraShake>();
            if (_instance == null)
            {
                _instance = new GameObject("CameraShake").AddComponent<CameraShake>();
            }
        }

        _instance._trauma = Mathf.Clamp01(_instance._trauma + strength);
    }

    // 위치에서 일어난 충격. 가까울수록 세게 흔들린다.
    public static void AddAt(Vector3 position, float strength, float radius)
    {
        Camera view = Camera.main;
        if (view == null || radius <= 0f)
        {
            return;
        }

        float distance = Vector3.Distance(view.transform.position, position);
        float falloff = 1f - Mathf.Clamp01(distance / radius);
        Add(strength * falloff * falloff);
    }

    private void Awake()
    {
        _seed = Random.Range(0f, 100f);
    }

    private void Update()
    {
        if (_trauma <= 0f && _pitch == 0f && _yaw == 0f)
        {
            return; // 흔들리지 않는 동안은 아무것도 하지 않는다.
        }

        if (_cameraRotate == null) _cameraRotate = FindAnyObjectByType<CameraRotate>();
        if (_playerRotate == null) _playerRotate = FindAnyObjectByType<PlayerRotate>();

        float dt = Time.deltaTime;
        _trauma = Mathf.MoveTowards(_trauma, 0f, _decay * dt);

        // 세기의 제곱에 비례해서 흔든다. (작은 충격은 살짝, 큰 충격은 확)
        float amount = _trauma * _trauma * _maxAngle;
        float time = Time.time * _frequency;
        float pitch = amount * (Mathf.PerlinNoise(_seed, time) * 2f - 1f);
        float yaw = amount * (Mathf.PerlinNoise(_seed + 37f, time) * 2f - 1f);

        // 지난 프레임과의 차이만큼만 돌린다. 흔들림이 끝나면 합이 0이 되어 원래 각도로 돌아온다.
        if (_cameraRotate != null) _cameraRotate.AddRecoil(pitch - _pitch);
        if (_playerRotate != null) _playerRotate.AddRecoil(yaw - _yaw);
        _pitch = pitch;
        _yaw = yaw;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
