using UnityEngine;
using System;

public class Player : MonoBehaviour, IDamageable
{
    [Header("스탯")]
    [SerializeField] private float _maxHealth = 100f;
    private float _health;

    public float MaxHealth => _maxHealth;
    public float Health => _health;

    // Action 자료형: 함수(들)를 저장할 수 있다.
    public event Action OnDamaged = null;
    public event Action OnHealed = null;   // 구급상자로 회복했을 때 (UI_PlayerStats가 구독)

    private void Start()
    {
        _health = _maxHealth;
    }

    // 체력 회복 (구급상자). 최대 체력을 넘지 않는다.
    public void Heal(float amount)
    {
        if (_health <= 0f || amount <= 0f) return;

        _health = Mathf.Min(_maxHealth, _health + amount);
        OnHealed?.Invoke();
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (_health <= 0f) return;

        _health -= damageInfo.Value;

        OnDamaged?.Invoke();
        GameSounds.Play2D("player_hurt", 0.8f, 0.08f);

        Debug.Log($"플레이어가 맞았다. 대미지 {damageInfo.Value}, 남은 체력 {_health}");
        if (_health <= 0f)
        {
            Debug.Log("플레이어가 쓰러졌다!");
            GameManager.Instance.GameOver();
        }
    }
}
