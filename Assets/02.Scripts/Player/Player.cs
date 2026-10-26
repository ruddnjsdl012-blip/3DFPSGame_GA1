using UnityEngine;
using System;
public class Player : MonoBehaviour, IDamageable
{
    [Header("스탯")]
    [SerializeField] private float _maxHealth = 100f;

    [Header("UI")]
    [SerializeField] private UI_HitEffect _hitEffect;

    private float _health;

    public float MaxHealth => _maxHealth;
    public float Health => _health;
    // Action자료형은 함수들을 저장 할 수 잇다.
    public Action OnDamaged = null;

    private void Awake()
    {
        _health = _maxHealth;
    }

    // 기존 총/좀비/폭탄의 IDamageable 호출을 그대로 지원한다.
    public void TakeDamage(DamageInfo damageInfo)
    {
        TryTakeDamage(damageInfo);
    }

    public bool TryTakeDamage(DamageInfo damageInfo)
    {
        if (_health <= 0f || GameManager.IsPlaying == false)
        {
            return false;
        }

        _health = Mathf.Max(0f, _health - damageInfo.Value);

        if (_hitEffect != null)
        {
            _hitEffect.Play();
        }

        Debug.Log($"플레이어가 맞았다. 대미지 {damageInfo.Value}, 남은 체력 {_health}");

        if (_health <= 0f)
        {
            GameManager.Instance.GameOver();
        }

        return true;
    }
}