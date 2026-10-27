using UnityEngine;

// 무언가에 부딛히면 폭발 이펙트를 남기고 사라지는 폭탄
public class Bomb : MonoBehaviour
{
    [SerializeField] private ParticleSystem _explosionEffectPrefab;
    [SerializeField] private float _explosionRange = 4.5f;
    [SerializeField] private float _damage = 100;


    // 다른 콜라이더와 부딛힌 순간 실행되는 메서드
    private void OnCollisionEnter(Collision collision)
    {
        ParticleSystem effect = Instantiate(_explosionEffectPrefab, transform.position, Quaternion.identity);
        effect.Play();

        ApplyAreaDamage();

        // Todo. 오브젝트 풀링 적용
        // 폭탄을 삭제한다.
        Destroy(gameObject);
    }

    private void ApplyAreaDamage()
    {
        // 0. 주위에 있는 드럼통들을 모두 찾는다.
        // Physics.OverlapSpere(원점, 반지름, 찾을 레이어들)
        Collider[] colliders = Physics.OverlapSphere(transform.position, _explosionRange, LayerMask.GetMask("Drum", "Zombie"));

        // 1. 드럼통과 좀비들에게 대미지를 준다.
        foreach (Collider col in colliders)
        {
            DamageInfo damageInfo = new()
            {
                Value = _damage,
                HitPoint = col.ClosestPoint(transform.position),  // 폭발 중심에서 가장 가까운 콜라이더 표면 위치
                HitNormal = (transform.position - col.ClosestPoint(transform.position)).normalized
            };


            col.GetComponent<IDamageable>()?.TakeDamage(damageInfo);
        }
    }
}
