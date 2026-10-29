using UnityEngine;
using UnityEngine.AI;

public enum SpecialType
{
    Runner,   // 러너: 작고 노랗다. 아주 빠르지만 약하다. 무리 지어 달려온다. (WWZ)
    Boomer,   // 부머: 뚱뚱하고 초록색. 가까이서 토하거나 터지면 화면이 가려지고 좀비 무리가 몰려온다.
    Spitter,  // 스피터: 보라색. 멀리서 산성을 뱉어 웅덩이를 만든다. 웅덩이 위에 있으면 점점 아프다.
    Tank,     // 탱크: 크고 붉다. 체력이 아주 높고 맞아도 멈칫하지 않는다. 맞으면 날아간다.
}

// 일반 좀비 프리팹에 붙여서 특수 좀비로 바꾼다. 겉모습은 SpecialZombieLook (전용 모델 또는 변이), 능력은 여기서.
// AIDirector가 SpecialZombie.Make(좀비, 종류)로 만든다.
public class SpecialZombie : MonoBehaviour
{
    private SpecialType _type;
    private bool _customModel;   // 넣어 둔 전용 모델을 쓰는 중이면 색과 모양을 바꾸지 않는다.
    private Zombie _zombie;
    private NavMeshAgent _agent;
    private Animator _animator;
    private Transform _player;
    private float _abilityTimer;
    private float _pauseTimer;
    private float _stepTimer;
    private float _soundTimer;

    public SpecialType Type => _type;
    public Zombie Zombie => _zombie;
    public bool IsUsingAbility => _pauseTimer > 0f;

    public static string DisplayName(SpecialType type)
    {
        switch (type)
        {
            case SpecialType.Runner: return "러너";
            case SpecialType.Boomer: return "부머";
            case SpecialType.Spitter: return "스피터";
            default: return "탱크";
        }
    }

    // Instantiate 바로 뒤에 부른다.
    public static SpecialZombie Make(Zombie zombie, SpecialType type)
    {
        SpecialZombie special = zombie.gameObject.AddComponent<SpecialZombie>();
        special._type = type;
        special._zombie = zombie;
        special.Setup();
        return special;
    }

    private void Setup()
    {
        _agent = GetComponent<NavMeshAgent>();
        name = "Special_" + _type;

        // 1. 넣어 둔 특수 좀비 모델이 있으면 그 모델로 바꾼다. (Resources/SpecialZombies/종류)
        _customModel = SpecialZombieLook.TrySwapModel(gameObject, _type);
        _animator = GetComponentInChildren<Animator>();

        switch (_type)
        {
            case SpecialType.Runner:
                //                    체력  속도  공격력 범위  간격  멈칫
                _zombie.SetupSpecial(18f, 10f, 6f, 1.5f, 0.8f, true);
                transform.localScale *= 0.85f;
                Tint(new Color(1f, 0.88f, 0.5f), 0.2f);
                SetAnimatorSpeed(1.5f);
                _agent.acceleration = 30f;
                _agent.angularSpeed = 720f;
                break;

            case SpecialType.Boomer:
                _zombie.SetupSpecial(25f, 4.5f, 5f, 1.6f, 1.4f, true);
                ScaleModel(new Vector3(1.15f, 1f, 1.15f));
                Tint(new Color(0.7f, 0.95f, 0.5f), 0.2f);
                SetAnimatorSpeed(0.8f);
                _abilityTimer = 3f;
                break;

            case SpecialType.Spitter:
                _zombie.SetupSpecial(40f, 5.5f, 4f, 1.5f, 1.2f, true);
                ScaleModel(new Vector3(0.92f, 1.05f, 0.92f));
                Tint(new Color(0.8f, 0.6f, 1f), 0.25f);
                _abilityTimer = 2.5f;
                break;

            case SpecialType.Tank:
                _zombie.SetupSpecial(700f, 6.2f, 25f, 2.6f, 0.8f, false);  // 공격 간격 1.6초 → 0.8초 (대신 한 방 30 → 25)
                transform.localScale *= 1.5f;
                Tint(new Color(0.95f, 0.55f, 0.48f), 0.15f);
                SetAnimatorSpeed(0.85f);
                _agent.acceleration = 20f;
                _zombie.Attacked += OnTankHit;
                break;
        }

        // 2. 모델이 없으면 일반 좀비를 변이시킨다. (뼈 크기, 자세, 몸에 붙는 덩어리)
        if (!_customModel)
        {
            SpecialZombieLook.Mutate(gameObject, _type);
        }

        _zombie.Died += OnDied;
        ZombieHealthBar healthBar = GetComponent<ZombieHealthBar>();
        if (healthBar != null)
        {
            healthBar.SetVisible(_type == SpecialType.Tank);
        }
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        _player = player != null ? player.transform : null;

        switch (_type)
        {
            case SpecialType.Runner:
                GameSounds.PlayAt("runner_shriek", transform.position, 1f, 0.6f, 60f);
                break;
            case SpecialType.Tank:
                GameSounds.Play2D("tank_roar", 1f, 0.02f);
                CameraShake.ShakeMain(0.5f);
                break;
        }

        _soundTimer = Random.Range(2f, 5f);
    }

    private void Update()
    {
        if (_zombie == null || _zombie.IsDead || _player == null || GameManager.Instance.State != GameState.Playing)
        {
            return;
        }

        float dt = Time.deltaTime;

        // 능력을 쓰는 동안 잠깐 멈춘다.
        if (_pauseTimer > 0f)
        {
            _pauseTimer -= dt;
            FacePlayer(dt);
            if (_pauseTimer <= 0f && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
            }
        }

        UpdateSounds(dt);

        switch (_type)
        {
            case SpecialType.Boomer:
                UpdateBoomer(dt);
                break;
            case SpecialType.Spitter:
                UpdateSpitter(dt);
                break;
            case SpecialType.Tank:
                UpdateTank(dt);
                break;
        }
    }

    // ===================== 부머 =====================

    private void UpdateBoomer(float dt)
    {
        _abilityTimer -= dt;
        float distance = Vector3.Distance(transform.position, _player.position);
        if (_abilityTimer > 0f || distance > 5.5f || !CanSeePlayer())
        {
            return;
        }

        // 가까이 오면 토한다.
        _abilityTimer = 15f;
        Pause(1.2f);
        GameSounds.PlayAt("boomer_gurgle", transform.position, 1f, 0.3f);
        SpecialEffects.VomitStream(Mouth(), _player.position + Vector3.up * 1.2f);
        SpecialEffects.Bile(transform.position);
    }

    // ===================== 스피터 =====================

    private void UpdateSpitter(float dt)
    {
        _abilityTimer -= dt;
        float distance = Vector3.Distance(transform.position, _player.position);
        if (_abilityTimer > 0f || distance < 3.5f || distance > 18f || !CanSeePlayer())
        {
            return;
        }

        // 멀리서 산성 덩어리를 뱉는다. 플레이어가 움직이는 쪽을 조금 앞서 노린다.
        _abilityTimer = Random.Range(8f, 11f);
        Pause(0.9f);
        GameSounds.PlayAt("spitter_spit", transform.position, 1f, 0.3f);
        CharacterController controller = _player.GetComponent<CharacterController>();
        Vector3 lead = controller != null ? Vector3.ProjectOnPlane(controller.velocity, Vector3.up) * 0.6f : Vector3.zero;
        AcidBall.Launch(Mouth(), _player.position + lead, 1f);
    }

    // ===================== 탱크 =====================

    private void UpdateTank(float dt)
    {
        // 휘두를 때는 동작을 빠르게, 걸을 때는 묵직하게
        if (_animator != null)
        {
            _animator.speed = _zombie.State == ZombieState.Attack ? 1.7f : 0.9f;
        }

        // 쿵쿵 걷는 소리와 땅 울림
        if (_agent.velocity.sqrMagnitude > 1f)
        {
            _stepTimer -= dt;
            if (_stepTimer <= 0f)
            {
                _stepTimer = 0.55f;
                GameSounds.PlayAt("tank_step", transform.position, 1f, 0.2f, 60f);
                CameraShake.ShakeAt(transform.position, 0.22f, 22f);
            }
        }

        DirectorHUD hud = DirectorHUD.Instance;
        if (hud != null)
        {
            hud.SetBoss("탱크", _zombie.Health / _zombie.MaxHealth);
        }
    }

    // 탱크에게 맞으면 크게 날아가고 화면이 흔들린다.
    private void OnTankHit(Zombie zombie)
    {
        Vector3 direction = _player.position - transform.position;
        direction.y = 0f;
        PlayerKnockback.Push(_player.gameObject, direction.normalized * 16f);
        CameraShake.ShakeMain(0.9f);
        GameSounds.Play2D("tank_hit", 1f, 0.05f);
    }

    // ===================== 죽을 때 =====================

    private void OnDied(Zombie zombie)
    {
        zombie.Died -= OnDied;
        switch (_type)
        {
            case SpecialType.Boomer:
                // 펑! 가까이 있으면 담즙을 뒤집어쓴다.
                GameSounds.PlayAt("boomer_burst", transform.position, 1f, 0f, 50f);
                CameraShake.ShakeAt(transform.position, 0.5f, 12f);
                SpecialEffects.Burst(transform.position + Vector3.up, new Color(0.45f, 0.75f, 0.2f), 3.5f);
                if (_player != null && Vector3.Distance(transform.position, _player.position) < 4.5f)
                {
                    SpecialEffects.Bile(transform.position);
                }

                HideModel();
                break;

            case SpecialType.Spitter:
                // 죽은 자리에 작은 산성 웅덩이
                AcidPuddle.Create(transform.position, 1.6f, 3.5f);
                break;

            case SpecialType.Tank:
                zombie.Attacked -= OnTankHit;
                if (DirectorHUD.Instance != null)
                {
                    DirectorHUD.Instance.SetBoss(null, 0f);
                    DirectorHUD.Instance.ShowAlert("탱크 처치!", new Color(1f, 0.85f, 0.4f), 2.5f);
                }

                CameraShake.ShakeAt(transform.position, 0.6f, 20f);
                break;
        }
    }

    private void OnDestroy()
    {
        if (_type == SpecialType.Tank && DirectorHUD.Instance != null && (_zombie == null || !_zombie.IsDead))
        {
            DirectorHUD.Instance.SetBoss(null, 0f);
        }
    }

    // ===================== 도우미 =====================

    private void UpdateSounds(float dt)
    {
        _soundTimer -= dt;
        if (_soundTimer > 0f)
        {
            return;
        }

        _soundTimer = Random.Range(3f, 6f);
        switch (_type)
        {
            case SpecialType.Boomer:
                GameSounds.PlayAt("boomer_gurgle", transform.position, 0.6f, 1f);
                break;
            case SpecialType.Runner:
                if (Random.value < 0.3f)
                {
                    GameSounds.PlayAt("runner_shriek", transform.position, 0.7f, 1.5f);
                }

                break;
        }
    }

    private void Pause(float seconds)
    {
        _pauseTimer = seconds;
        if (_agent.isOnNavMesh)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }
    }

    private void FacePlayer(float dt)
    {
        Vector3 look = _player.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), dt * 8f);
        }
    }

    private bool CanSeePlayer()
    {
        Vector3 from = Mouth();
        Vector3 to = _player.position + Vector3.up * 1.2f;
        if (Physics.Linecast(from, to, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == _player || hit.transform.IsChildOf(_player);
        }

        return true;
    }

    private Vector3 Mouth()
    {
        if (_animator != null && _animator.isHuman)
        {
            Transform head = _animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                return head.position + transform.forward * 0.25f;
            }
        }

        return transform.position + Vector3.up * 1.6f * transform.lossyScale.y + transform.forward * 0.4f;
    }

    private void ScaleModel(Vector3 scale)
    {
        if (_customModel)
        {
            return;
        }

        Transform model = _animator != null ? _animator.transform : transform;
        model.localScale = Vector3.Scale(model.localScale, scale);
    }

    private void SetAnimatorSpeed(float speed)
    {
        if (_animator != null)
        {
            _animator.speed = speed;
        }
    }

    private void HideModel()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = false;
        }
    }

    // 색을 입힌다. 어두운 밤에도 알아보게 살짝 빛나게 한다.
    private void Tint(Color color, float glow)
    {
        if (_customModel)
        {
            return;
        }

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer))
            {
                continue;
            }

            foreach (Material material in renderer.materials)
            {
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", material.GetColor("_BaseColor") * color);
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", material.GetColor("_Color") * color);
                }

                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * glow);
                }
            }
        }
    }
}
