using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum WeaponSlot
{
    Axe = 1,       // 1번: 도끼 (근접)
    Revolver = 2,  // 2번: 리볼버 (6발, 예비탄 무한)
    Rifle = 3,     // 3번: AR (원래 총, PlayerGunFire)
    Grenade = 4,   // 4번: 수류탄 (PlayerGrenade)
}

// 무기 바꾸기 (1 도끼 / 2 리볼버 / 3 AR / 4 수류탄, 마우스 휠로도 바꾼다)
//  - 바꿀 때 지금 무기를 내리고(0.18초) 새 무기를 올린다(0.25초). 그동안은 쏠 수 없다.
//  - 수류탄은 던지고 나면 바로 전에 쓰던 무기로 돌아간다. (L4D)
//  - 화면 오른쪽 아래, 보급품 칸 위에 무기 칸 4개를 보여 준다.
// PlayerGunFire가 Player에 붙인다. 도끼(PlayerAxe), 리볼버(PlayerRevolver)도 여기서 붙인다.
public class PlayerWeapons : MonoBehaviour
{
    [SerializeField] private WeaponSlot _startWeapon = WeaponSlot.Rifle;
    [SerializeField] private float _lowerTime = 0.18f;
    [SerializeField] private float _raiseTime = 0.25f;

    private WeaponSlot _current;
    private WeaponSlot _pending;
    private WeaponSlot _previous = WeaponSlot.Rifle;
    private float _switchTimer;     // > 0 이면 바꾸는 중
    private bool _lowering;
    private PlayerGrenade _grenade;
    private PlayerGunFire _rifle;
    private PlayerRevolver _revolver;
    private PlayerAxe _axe;
    private PlayerInventory _inventory;

    private TextMeshProUGUI[] _slotTexts;
    private Image[] _slotBacks;

    public WeaponSlot Current => _current;
    public bool IsSwitching => _switchTimer > 0f;

    // 지금 무기가 쓸 수 있는 상태인가 (바꾸는 중이 아니고, 그 무기를 들고 있다)
    public bool IsReady(WeaponSlot slot) => _current == slot && !IsSwitching;

    // 무기별 화면 위치 (0 = 내려감, 1 = 올라옴) - 1인칭 모델들이 읽는다.
    public float Raise(WeaponSlot slot)
    {
        if (_current != slot)
        {
            return 0f;
        }

        if (!IsSwitching)
        {
            return 1f;
        }

        // 내리는 중: 1 → 0, 올리는 중: 0 → 1
        return _lowering ? Mathf.Clamp01(_switchTimer / _lowerTime) : 1f - Mathf.Clamp01(_switchTimer / _raiseTime);
    }

    private void Awake()
    {
        _rifle = GetComponent<PlayerGunFire>();
        _inventory = GetComponent<PlayerInventory>();
        // (?? 대신 if: 유니티 컴포넌트는 없을 때 '가짜 null'이라 ??가 제대로 동작하지 않는다)
        _revolver = GetComponent<PlayerRevolver>();
        if (_revolver == null)
        {
            _revolver = gameObject.AddComponent<PlayerRevolver>();
        }

        _axe = GetComponent<PlayerAxe>();
        if (_axe == null)
        {
            _axe = gameObject.AddComponent<PlayerAxe>();
        }
        _current = _startWeapon == WeaponSlot.Grenade ? WeaponSlot.Rifle : _startWeapon;
        _previous = _current;
    }

    private void Start()
    {
        _grenade = GetComponent<PlayerGrenade>();
        BuildSlotBar();
    }

    private void Update()
    {
        if (GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        UpdateSwitch(Time.deltaTime);

        // 수류탄을 던졌거나(또는 오른쪽 클릭으로 내렸으면) 전에 쓰던 무기로
        if (_current == WeaponSlot.Grenade && !IsSwitching && _grenade != null && !_grenade.IsEquipped)
        {
            Begin(_previous == WeaponSlot.Grenade ? WeaponSlot.Rifle : _previous, true);
        }

        bool healing = _inventory != null && _inventory.IsHealing;
        if (healing)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) Select(WeaponSlot.Axe);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) Select(WeaponSlot.Revolver);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) Select(WeaponSlot.Rifle);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) Select(WeaponSlot.Grenade);

        float wheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(wheel) > 0.01f && !Cursor.visible)
        {
            int next = (int)(_pending == 0 ? _current : _pending) + (wheel < 0f ? 1 : -1);
            if (next < 1) next = 3;
            if (next > 3) next = 1;  // 휠은 1~3 (수류탄은 4번 키로)
            Select((WeaponSlot)next);
        }

        RefreshSlotBar();
    }

    public void Select(WeaponSlot slot)
    {
        if (slot == _current && !IsSwitching)
        {
            return;
        }

        if (slot == WeaponSlot.Grenade && _inventory != null && _inventory.Grenades <= 0)
        {
            _inventory.TryUseGrenade();  // "수류탄이 없다" 안내만
            return;
        }

        Begin(slot, false);
    }

    private void Begin(WeaponSlot slot, bool skipLower)
    {
        // 지금 무기 정리
        if (_current == WeaponSlot.Rifle && _rifle != null) _rifle.CancelReload();
        if (_current == WeaponSlot.Revolver && _revolver != null) _revolver.CancelReload();
        if (_current == WeaponSlot.Grenade && _grenade != null && _grenade.IsEquipped) _grenade.Unequip();
        if (_current != WeaponSlot.Grenade) _previous = _current;

        _pending = slot;
        _lowering = !skipLower;
        _switchTimer = skipLower ? _raiseTime : _lowerTime;
        if (skipLower)
        {
            Arrive();
        }

        GameSounds.Play2D("weapon_switch", 0.5f, 0.08f);
    }

    private void UpdateSwitch(float dt)
    {
        if (_switchTimer <= 0f)
        {
            return;
        }

        _switchTimer -= dt;
        if (_switchTimer > 0f)
        {
            return;
        }

        if (_lowering)
        {
            // 다 내렸으면 새 무기를 올린다.
            _lowering = false;
            _switchTimer = _raiseTime;
            Arrive();
        }
        else
        {
            _switchTimer = 0f;
            _pending = 0;
        }
    }

    private void Arrive()
    {
        _current = _pending;
        if (_current == WeaponSlot.Grenade && _grenade != null && !_grenade.Equip())
        {
            _current = _previous;  // 수류탄이 없으면 돌아간다.
        }
    }

    // ===================== 오른쪽 아래 총알 표시 (UI_AmmoHud가 부른다) =====================

    // AR이 아닐 때 보여 줄 글자. AR이면 false (원래대로 PlayerGunFire 값을 보여 준다)
    public bool GetAmmoDisplay(out string big, out string small, out string hint, out Color color)
    {
        big = small = hint = string.Empty;
        color = Color.white;
        switch (_current)
        {
            case WeaponSlot.Revolver:
                big = _revolver.Rounds.ToString();
                small = "/ 무한";  // (∞ 기호는 한글 폰트에 없어서 글자로)
                color = _revolver.Rounds <= 0 ? new Color(1f, 0.25f, 0.2f) : (_revolver.Rounds <= 2 ? new Color(1f, 0.8f, 0.25f) : new Color(1f, 0.97f, 0.9f));
                hint = _revolver.IsReloading ? "재장전 중..." : (_revolver.Rounds <= 2 ? "[R] 재장전" : string.Empty);
                return true;
            case WeaponSlot.Axe:
                big = "도끼";
                small = string.Empty;
                hint = "왼쪽 클릭: 휘두르기";
                return true;
            case WeaponSlot.Grenade:
                big = (_inventory != null ? _inventory.Grenades : 0).ToString();
                small = "수류탄";
                hint = "누르고 있다 떼서 던지기";
                return true;
            default:
                return false;
        }
    }

    // ===================== 무기 칸 표시 =====================

    private void BuildSlotBar()
    {
        GameObject canvasObject = new GameObject("UI_WeaponSlots", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 16;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        TMP_FontAsset font = null;
        foreach (TextMeshProUGUI text in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text.font != null && text.font.HasCharacter('도', true, true))
            {
                font = text.font;
                break;
            }
        }

        string[] names = { "1 도끼", "2 리볼버", "3 AR", "4 수류탄" };
        _slotTexts = new TextMeshProUGUI[4];
        _slotBacks = new Image[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject back = new GameObject("Slot" + (i + 1), typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)back.transform;
            rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-40f - (3 - i) * 84f, 276f);
            rect.sizeDelta = new Vector2(78f, 34f);
            _slotBacks[i] = back.GetComponent<Image>();
            _slotBacks[i].raycastTarget = false;

            GameObject label = new GameObject("Label", typeof(RectTransform));
            RectTransform labelRect = (RectTransform)label.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = label.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }

            text.fontSize = 20f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.text = names[i];
            _slotTexts[i] = text;
        }

        RefreshSlotBar();
    }

    private WeaponSlot _shownSlot;

    private void RefreshSlotBar()
    {
        if (_slotBacks == null)
        {
            return;
        }

        WeaponSlot shown = IsSwitching && _pending != 0 ? _pending : _current;
        if (shown == _shownSlot)
        {
            return;  // 바뀌었을 때만 다시 그린다.
        }

        _shownSlot = shown;
        for (int i = 0; i < 4; i++)
        {
            bool active = (int)shown == i + 1;
            _slotBacks[i].color = active ? new Color(1f, 0.85f, 0.4f, 0.75f) : new Color(0f, 0f, 0f, 0.4f);
            _slotTexts[i].color = active ? new Color(0.1f, 0.08f, 0.05f) : new Color(1f, 1f, 1f, 0.75f);
            _slotTexts[i].fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}
