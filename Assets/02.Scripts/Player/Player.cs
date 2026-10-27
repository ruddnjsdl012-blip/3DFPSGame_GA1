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

    // 맞았을 때 공격한 쪽 방향 (피격 방향 표시용)
    public event Action<Vector3> DamagedFrom;

    private void Awake()
    {
        _health = _maxHealth;

        // 발소리/심장 박동 소리 컴포넌트가 없으면 붙인다.
        if (GetComponent<PlayerAudio>() == null)
        {
            gameObject.AddComponent<PlayerAudio>();
        }

        // 피격 방향 표시
        UI_DamageIndicator.Create(this);
    }

    public bool NeedsHealing => _health > 0f && _health < _maxHealth;

    // 구급상자를 주웠을 때. 실제로 회복한 양을 돌려준다.
    public float Heal(float amount)
    {
        if (_health <= 0f || amount <= 0f)
        {
            return 0f;
        }

        float before = _health;
        _health = Mathf.Min(_maxHealth, _health + amount);
        return _health - before;
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
        GameAudio.Play2D("player_hurt", 0.85f, 0.06f, 0.15f);
        DamagedFrom?.Invoke(damageInfo.HitNormal);

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