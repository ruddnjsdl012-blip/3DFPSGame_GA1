using UnityEngine;

// 잠깐 나타났다 사라지는 효과: 작은 크기 → 큰 크기로 커졌다가 → 0으로 줄어들고 지워진다.
public class EffectAnimator : MonoBehaviour
{
    private Vector3 _from;
    private Vector3 _to;
    private float _growTime;
    private float _life;
    private float _time;

    public static EffectAnimator Add(GameObject target, Vector3 from, Vector3 to, float growTime, float life)
    {
        EffectAnimator effect = target.AddComponent<EffectAnimator>();
        effect._from = from;
        effect._to = to;
        effect._growTime = Mathf.Max(0.01f, growTime);
        effect._life = Mathf.Max(effect._growTime + 0.01f, life);
        target.transform.localScale = from;
        return effect;
    }

    private void Update()
    {
        _time += Time.deltaTime;
        if (_time < _growTime)
        {
            transform.localScale = Vector3.Lerp(_from, _to, _time / _growTime);
        }
        else
        {
            float t = (_time - _growTime) / (_life - _growTime);
            transform.localScale = Vector3.Lerp(_to, Vector3.zero, t * t);
        }

        if (_time >= _life)
        {
            Destroy(gameObject);
        }
    }
}
