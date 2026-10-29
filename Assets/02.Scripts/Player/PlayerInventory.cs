using System;
using UnityEngine;

// 플레이어가 가지고 다니는 보급품과 줍기/사용 (L4D 방식)
//  - E: 가까이 있는 보급품 줍기 (탄약 더미는 예비 총알을 가득 채우고 그대로 남는다)
//  - Q: 구급상자 사용. 3초 동안 치료하고 잃은 체력의 80%를 회복한다. 치료 중에는 총을 쏠 수 없다.
//       치료 중에 Q나 왼쪽 클릭을 누르면 취소된다.
//  - 수류탄: 최대 3개. 4번으로 들고 왼쪽 클릭을 떼서 던진다. 우클릭은 밀치기.
// PlayerGunFire가 Player에 자동으로 붙인다. 화면 표시는 UI_SupplyHud.
public class PlayerInventory : MonoBehaviour
{
    [Header("구급상자")]
    [SerializeField] private int _maxMedkits = 1;
    [SerializeField] private int _startMedkits = 0;
    [SerializeField] private float _healTime = 3f;
    [SerializeField, Range(0f, 1f)] private float _healRatio = 0.8f;  // 잃은 체력 중 회복하는 비율

    [Header("수류탄")]
    [SerializeField] private int _maxGrenades = 3;
    [SerializeField] private int _startGrenades = 1;

    [Header("줍기")]
    [SerializeField] private float _pickupDistance = 2.2f;

    private Player _player;
    private PlayerGunFire _gun;
    private UI_SupplyHud _hud;
    private int _medkits;
    private int _grenades;
    private float _healTimer = -1f;
    private Pickup _nearest;

    // 보급품 수나 치료 상태가 바뀌면 알린다. (옵저버 패턴: UI_SupplyHud가 구독)
    public event Action OnChanged;

    public int Medkits => _medkits;
    public int MaxMedkits => _maxMedkits;
    public int Grenades => _grenades;
    public int MaxGrenades => _maxGrenades;
    public bool IsHealing => _healTimer >= 0f;
    public float HealProgress => IsHealing ? Mathf.Clamp01(_healTimer / _healTime) : 0f;

    public void InterruptHealing()
    {
        CancelHeal(false);
    }

    private void Awake()
    {
        _player = GetComponent<Player>();
        _gun = GetComponent<PlayerGunFire>();
        _medkits = Mathf.Clamp(_startMedkits, 0, _maxMedkits);
        _grenades = Mathf.Clamp(_startGrenades, 0, _maxGrenades);
    }

    private void Start()
    {
        _hud = UI_SupplyHud.Create(this);
    }

    // PlayerBombFire가 던지기 전에 부른다. 수류탄이 있으면 하나 쓰고 true
    public bool TryUseGrenade()
    {
        if (_grenades <= 0)
        {
            _hud.ShowToast("수류탄이 없다", new Color(1f, 0.4f, 0.3f));
            GameSounds.Play2D("gun_empty", 0.5f);
            return false;
        }

        _grenades--;
        OnChanged?.Invoke();
        return true;
    }

    private void Update()
    {
        if (GameManager.Instance.State != GameState.Playing)
        {
            CancelHeal(false);
            _hud.SetPrompt(string.Empty);
            return;
        }

        UpdateHeal();
        UpdatePickup();
    }

    // ===================== 줍기 =====================

    private void UpdatePickup()
    {
        Pickup nearest = FindNearest();
        if (nearest != _nearest)
        {
            if (_nearest != null)
            {
                _nearest.SetHighlight(false);
            }

            _nearest = nearest;
            if (_nearest != null)
            {
                _nearest.SetHighlight(true);
            }
        }

        if (_nearest == null || IsHealing)
        {
            _hud.SetPrompt(string.Empty);
            return;
        }

        string blocked = WhyCannotTake(_nearest);
        _hud.SetPrompt(blocked ?? "[E] " + _nearest.DisplayName + (_nearest.Type == PickupType.Ammo ? " 채우기" : " 줍기"));
        if (blocked == null && Input.GetKeyDown(KeyCode.E))
        {
            Take(_nearest);
        }
    }

    private Pickup FindNearest()
    {
        Pickup best = null;
        float bestDistance = _pickupDistance;
        Vector3 me = transform.position;
        foreach (Pickup pickup in Pickup.All)
        {
            Vector3 offset = pickup.transform.position - me;
            if (Mathf.Abs(offset.y) > 2.2f)
            {
                continue;
            }

            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = pickup;
            }
        }

        return best;
    }

    // 주울 수 없으면 그 이유, 주울 수 있으면 null
    private string WhyCannotTake(Pickup pickup)
    {
        switch (pickup.Type)
        {
            case PickupType.Ammo:
                return _gun != null && _gun.ReserveAmmo >= _gun.MaxReserveAmmo ? "탄약이 가득 찼다" : null;
            case PickupType.Medkit:
                return _medkits >= _maxMedkits ? "구급상자를 이미 가지고 있다" : null;
            default:
                return _grenades >= _maxGrenades ? $"수류탄이 가득 찼다 ({_grenades}/{_maxGrenades})" : null;
        }
    }

    private void Take(Pickup pickup)
    {
        switch (pickup.Type)
        {
            case PickupType.Ammo:
                int added = _gun != null ? _gun.AddAmmo(_gun.MaxReserveAmmo) : 0;
                GameSounds.Play2D("pickup_ammo", 0.9f);
                _hud.ShowToast("탄약 +" + added, new Color(1f, 0.85f, 0.4f));
                break;
            case PickupType.Medkit:
                _medkits++;
                GameSounds.Play2D("pickup_health", 0.9f);
                _hud.ShowToast("구급상자 획득  [Q] 사용", new Color(1f, 0.5f, 0.45f));
                break;
            default:
                _grenades++;
                GameSounds.Play2D("pickup_bomb", 0.9f);
                _hud.ShowToast($"수류탄 획득 ({_grenades}/{_maxGrenades})", new Color(0.6f, 1f, 0.5f));
                break;
        }

        if (!pickup.Reusable)
        {
            pickup.SetHighlight(false);
            Destroy(pickup.gameObject);
            _nearest = null;
        }

        OnChanged?.Invoke();
    }

    // ===================== 구급상자 =====================

    private void UpdateHeal()
    {
        if (IsHealing)
        {
            // Q를 다시 누르거나 쏘려고 하면 취소
            if (Input.GetKeyDown(KeyCode.Q) || (!Cursor.visible && Input.GetButtonDown("Fire1")))
            {
                CancelHeal(true);
                return;
            }

            _healTimer += Time.deltaTime;
            if (_healTimer >= _healTime)
            {
                FinishHeal();
            }

            return;
        }

        if (!Input.GetKeyDown(KeyCode.Q))
        {
            return;
        }

        if (_medkits <= 0)
        {
            _hud.ShowToast("구급상자가 없다", new Color(1f, 0.4f, 0.3f));
            return;
        }

        if (_player != null && _player.Health >= _player.MaxHealth)
        {
            _hud.ShowToast("체력이 가득 찼다", new Color(0.85f, 0.85f, 0.85f));
            return;
        }

        _healTimer = 0f;
        GameSounds.Play2D("pickup_health", 0.6f);
        OnChanged?.Invoke();
    }

    private void FinishHeal()
    {
        _healTimer = -1f;
        _medkits--;
        if (_player != null)
        {
            float missing = _player.MaxHealth - _player.Health;
            _player.Heal(missing * _healRatio);
        }

        GameSounds.Play2D("sting_levelup", 0.5f);
        _hud.ShowToast("치료 완료", new Color(0.5f, 1f, 0.55f));
        OnChanged?.Invoke();
    }

    private void CancelHeal(bool showMessage)
    {
        if (!IsHealing)
        {
            return;
        }

        _healTimer = -1f;
        if (showMessage)
        {
            _hud.ShowToast("치료 취소", new Color(0.85f, 0.85f, 0.85f));
        }

        OnChanged?.Invoke();
    }
}
