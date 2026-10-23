using UnityEngine;

public class Player : MonoBehaviour, IDamageable
{
    [Header("스탯")]
    [SerializeField] private float _maxHealth = 100f;

    [Header("UI")]
    [SerializeField] private UI_HitEffect _hitEffect;

    private float _health;

    public float MaxHealth => _maxHealth;
    public float Health => _health;

    private void Awake()
    {
        _health = _maxHealth;
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (_health <= 0f)
        {
            return;
        }

        _health = Mathf.Max(0f, _health - damageInfo.Value);

        if (_hitEffect != null)
        {
            _hitEffect.Play();
        }

        Debug.Log($"플레이어가 맞았다. 대미지 {damageInfo.Value}, 남은 체력 {_health}");

        if (_health <= 0f)
        {
            Debug.Log("플레이어가 쓰러졌다.");
        }
    }
}