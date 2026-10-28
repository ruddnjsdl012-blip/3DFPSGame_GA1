using UnityEngine;

// 상처에서 피가 뚝뚝 떨어진다. 맞은 자리에서 가장 가까운 뼈에 붙어서 몸을 따라 움직인다.
public class BloodDrip : MonoBehaviour
{
    private const int MaxActive = 16;
    private static int _active;

    private Color _color;
    private float _life;
    private float _timer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _active = 0;
    }

    public static void Begin(Transform bone, Vector3 worldPoint, Color color, float seconds)
    {
        if (_active >= MaxActive || bone == null)
        {
            return;
        }

        GameObject go = new GameObject("BloodDrip");
        go.transform.SetParent(bone, true);
        go.transform.position = worldPoint;
        BloodDrip drip = go.AddComponent<BloodDrip>();
        drip._color = color;
        drip._life = seconds;
        drip._timer = Random.Range(0.05f, 0.15f);
        _active++;
    }

    private void Update()
    {
        _life -= Time.deltaTime;
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            // 처음엔 자주, 갈수록 드물게
            _timer = Random.Range(0.08f, 0.2f) + Mathf.Max(0f, 0.6f - _life) * 0.3f;
            BloodEffects.EmitDrip(transform.position, _color);
        }

        if (_life <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        _active = Mathf.Max(0, _active - 1);
    }
}
