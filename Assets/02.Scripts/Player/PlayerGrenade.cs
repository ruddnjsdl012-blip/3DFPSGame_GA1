using UnityEngine;
using UnityEngine.UI;

// 수류탄 들기와 던지기 (L4D 방식)
//  - 4번: 수류탄을 손에 든다. (총은 화면 아래로 내려가고, 오른손에 수류탄이 보인다)  1번: 다시 총
//  - 왼쪽 클릭을 누르고 있으면 오른팔을 뒤로 젖히며 힘을 모은다. 오래 누를수록 멀리 날아간다.
//    누르는 동안 날아갈 길(포물선)과 떨어질 자리가 보인다.
//  - 손을 떼면 오른팔을 앞으로 휘둘러 던진다. 던지고 나면 다시 총을 든다.
//  - 오른쪽 클릭: 힘 모으기 취소 / 총으로 돌아가기
// 실제 수류탄 만들기는 PlayerBombFire.Throw()가 한다. 개수는 PlayerInventory.
public class PlayerGrenade : MonoBehaviour
{
    private enum State
    {
        Gun,       // 총을 들고 있다.
        Holding,   // 수류탄을 들고 있다.
        Charging,  // 힘을 모으는 중 (왼쪽 클릭 누르는 중)
        Throwing,  // 던지는 동작
    }

    [Header("던지는 힘")]
    [SerializeField] private float _chargeTime = 1.2f;           // 이만큼 누르면 최대 힘
    [SerializeField, Range(0.1f, 1f)] private float _minPower = 0.35f;  // 짧게 눌렀을 때 (PlayerBombFire 힘의 배율)
    [SerializeField] private float _maxPower = 1.6f;             // 끝까지 모았을 때
    [SerializeField] private float _upward = 0.18f;              // 살짝 위로 던진다.

    [Header("동작 시간")]
    [SerializeField] private float _equipTime = 0.3f;
    [SerializeField] private float _releaseDelay = 0.08f;        // 휘두르기 시작 후 이만큼 뒤에 손을 떠난다.
    [SerializeField] private float _throwTime = 0.45f;           // 던지는 동작 전체

    private State _state = State.Gun;
    private PlayerBombFire _bombFire;
    private PlayerInventory _inventory;
    private GrenadeArmView _arm;
    private float _charge;       // 0 ~ 1
    private float _timer;
    private bool _released;
    private float _throwCharge;

    private LineRenderer _arc;
    private Transform _marker;
    private GameObject _chargeBar;
    private RectTransform _chargeFill;

    public bool IsEquipped => _state != State.Gun;
    public float Charge => _charge;
    public float EquipBlend { get; private set; }  // 0 = 총, 1 = 수류탄 (FirstPersonBodyView가 총을 내린다)

    // 팔 동작 단계 (GrenadeArmView가 읽는다)
    public float WindUp { get; private set; }      // 0 ~ 1 뒤로 젖힌 정도
    public float Swing { get; private set; }       // 0 ~ 1 앞으로 휘두른 정도
    public bool GrenadeInHand => _state == State.Holding || _state == State.Charging || (_state == State.Throwing && !_released);

    private void Start()
    {
        _bombFire = GetComponent<PlayerBombFire>();
        if (_bombFire == null)
        {
            _bombFire = FindAnyObjectByType<PlayerBombFire>();
        }

        _inventory = GetComponent<PlayerInventory>();
        _arm = GrenadeArmView.Create(this);
        BuildArc();
        BuildChargeBar();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        EquipBlend = Mathf.MoveTowards(EquipBlend, IsEquipped ? 1f : 0f, dt / _equipTime);

        if (GameManager.Instance.State != GameState.Playing)
        {
            ShowAim(false);
            return;
        }

        bool mouseFree = Cursor.visible;
        bool healing = _inventory != null && _inventory.IsHealing;

        switch (_state)
        {
            case State.Gun:
                if (Input.GetKeyDown(KeyCode.Alpha4) && !healing)
                {
                    TryEquip();
                }

                break;

            case State.Holding:
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetButtonDown("Fire2") || healing)
                {
                    Unequip();
                }
                else if (!mouseFree && Input.GetButtonDown("Fire1") && EquipBlend > 0.9f)
                {
                    _state = State.Charging;
                    _charge = 0f;
                    GameSounds.Play2D("reload_bolt_pull", 0.35f);  // 핀 뽑는 소리 대신
                }

                break;

            case State.Charging:
                _charge = Mathf.Clamp01(_charge + dt / _chargeTime);
                if (Input.GetButtonDown("Fire2") || Input.GetKeyDown(KeyCode.Alpha1))
                {
                    _state = State.Holding;  // 취소: 다시 들고 있는 자세로
                    _charge = 0f;
                }
                else if (!Input.GetButton("Fire1"))
                {
                    BeginThrow();
                }

                break;

            case State.Throwing:
                _timer += dt;
                if (!_released && _timer >= _releaseDelay)
                {
                    Release();
                }

                if (_timer >= _throwTime)
                {
                    // 남은 수류탄이 있어도 L4D처럼 다시 총을 든다.
                    _state = State.Gun;
                }

                break;
        }

        UpdatePose(dt);
        ShowAim(_state == State.Charging);
    }

    private void TryEquip()
    {
        if (_inventory != null && _inventory.Grenades <= 0)
        {
            _inventory.TryUseGrenade();  // "수류탄이 없다" 안내만 띄운다. (개수가 0이라 줄지 않는다)
            return;
        }

        _state = State.Holding;
        _charge = 0f;
        GameSounds.Play2D("pickup_bomb", 0.5f);
    }

    private void Unequip()
    {
        _state = State.Gun;
        _charge = 0f;
    }

    private void BeginThrow()
    {
        _state = State.Throwing;
        _timer = 0f;
        _released = false;
        _throwCharge = _charge;
    }

    private void Release()
    {
        _released = true;
        if (_inventory != null && !_inventory.TryUseGrenade())
        {
            return;
        }

        if (_bombFire != null)
        {
            _bombFire.Throw(PowerMultiplier(_throwCharge), _upward);
        }
    }

    private float PowerMultiplier(float charge)
    {
        // 처음엔 빨리 오르고 끝으로 갈수록 천천히 (짧게 눌러도 어느 정도는 날아간다)
        float eased = 1f - (1f - charge) * (1f - charge);
        return Mathf.Lerp(_minPower, _maxPower, eased);
    }

    // 팔 동작 값: 들기 → 젖히기(힘 모으기) → 휘두르기 → 내리기
    private void UpdatePose(float dt)
    {
        float windTarget = _state == State.Charging ? Mathf.Lerp(0.35f, 1f, _charge) : 0f;
        if (_state == State.Throwing)
        {
            // 휘두르기: 빠르게 앞으로, 그 뒤 천천히 돌아온다.
            float t = _timer / _throwTime;
            Swing = t < 0.3f ? Mathf.SmoothStep(0f, 1f, t / 0.3f) : Mathf.SmoothStep(1f, 0f, (t - 0.3f) / 0.7f);
            WindUp = Mathf.MoveTowards(WindUp, 0f, dt * 12f);
        }
        else
        {
            Swing = Mathf.MoveTowards(Swing, 0f, dt * 4f);
            WindUp = Mathf.MoveTowards(WindUp, windTarget, dt * 5f);
        }
    }

    // ===================== 날아갈 길 보여 주기 =====================

    private void BuildArc()
    {
        GameObject go = new GameObject("GrenadeArc");
        _arc = go.AddComponent<LineRenderer>();
        _arc.positionCount = 0;
        _arc.widthMultiplier = 0.035f;
        _arc.numCapVertices = 2;
        _arc.useWorldSpace = true;
        _arc.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _arc.receiveShadows = false;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            _arc.sharedMaterial = new Material(shader);
        }

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.2f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.15f), new GradientAlphaKey(0.8f, 1f) });
        _arc.colorGradient = gradient;

        // 떨어질 자리 표시 (납작한 원)
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "GrenadeLandMarker";
        Destroy(marker.GetComponent<Collider>());
        marker.transform.localScale = new Vector3(0.6f, 0.005f, 0.6f);
        Renderer renderer = marker.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (shader != null)
        {
            renderer.sharedMaterial = new Material(shader);
            renderer.sharedMaterial.color = new Color(1f, 0.55f, 0.15f, 0.55f);
        }

        _marker = marker.transform;
        ShowAim(false);
    }

    private void ShowAim(bool show)
    {
        if (_arc == null)
        {
            return;
        }

        _arc.enabled = show;
        _marker.gameObject.SetActive(show);
        if (_chargeBar != null)
        {
            _chargeBar.SetActive(show);
        }

        if (!show || _bombFire == null)
        {
            return;
        }

        // 던질 때와 같은 계산으로 포물선을 미리 그린다.
        _bombFire.GetLaunch(PowerMultiplier(_charge), _upward, out Vector3 start, out Vector3 velocity);
        Vector3 gravity = Physics.gravity;
        const int maxPoints = 60;
        const float step = 0.05f;
        Vector3[] points = new Vector3[maxPoints];
        int count = 0;
        Vector3 position = start;
        bool landed = false;
        points[count++] = position;
        for (int i = 1; i < maxPoints; i++)
        {
            Vector3 next = position + velocity * step + 0.5f * gravity * step * step;
            velocity += gravity * step;
            if (Physics.Linecast(position, next, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.transform.IsChildOf(transform))
            {
                points[count++] = hit.point;
                _marker.position = hit.point + hit.normal * 0.02f;
                _marker.up = hit.normal;
                landed = true;
                break;
            }

            points[count++] = next;
            position = next;
        }

        _marker.gameObject.SetActive(landed);
        _arc.positionCount = count;
        _arc.SetPositions(points);

        if (_chargeFill != null)
        {
            _chargeFill.anchorMax = new Vector2(Mathf.Max(0.03f, _charge), 1f);
        }
    }

    // 조준점 아래 힘 막대
    private void BuildChargeBar()
    {
        GameObject canvasObject = new GameObject("UI_GrenadeCharge", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 17;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _chargeBar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        RectTransform bar = (RectTransform)_chargeBar.transform;
        bar.SetParent(canvasObject.transform, false);
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
        bar.anchoredPosition = new Vector2(0f, -70f);
        bar.sizeDelta = new Vector2(180f, 10f);
        Image back = _chargeBar.GetComponent<Image>();
        back.color = new Color(0f, 0f, 0f, 0.55f);
        back.raycastTarget = false;

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        _chargeFill = (RectTransform)fill.transform;
        _chargeFill.SetParent(bar, false);
        _chargeFill.anchorMin = Vector2.zero;
        _chargeFill.anchorMax = new Vector2(0f, 1f);
        _chargeFill.offsetMin = new Vector2(2f, 2f);
        _chargeFill.offsetMax = new Vector2(-2f, -2f);
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = new Color(1f, 0.65f, 0.2f);
        fillImage.raycastTarget = false;
        _chargeBar.SetActive(false);
    }
}
