using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 플레이어의 기존 3인칭 몸/애니메이터와 WeaponModels의 동일한 도끼/권총을 사용한다.
public sealed class SurvivorVisual : MonoBehaviour
{
    private Animator _animator;
    private Transform _hand;
    private Transform _upper;
    private Transform _lower;
    private Transform _axe;
    private Transform _pistol;
    private Transform _rifle;
    private Transform _pistolMuzzle;
    private Transform _rifleMuzzle;
    private WeaponSlot _weapon = WeaponSlot.None;
    private Zombie _target;
    private readonly List<Renderer> _integratedGun = new List<Renderer>();
    private readonly List<Mesh> _createdMeshes = new List<Mesh>();
    private readonly List<MaterialSwap> _materialSwaps = new List<MaterialSwap>();
    private Material _hidden;
    private LineRenderer _tracer;
    private Transform _flash;
    private float _flashUntil;
    private float _swingAt = -10f;
    private float _kick;
    private bool _moveParameter;
    private bool _attackParameter;
    private bool _reloadParameter;
    private bool _built;
    private bool _reloading;
    private sealed class MaterialSwap
    {
        public Renderer Renderer;
        public Material[] Original;
        public Material[] Hidden;
    }

    public void Configure(Animator animator, int index)
    {
        _animator = animator;
        if (_animator != null)
        {
            _animator.applyRootMotion = false;
            _animator.fireEvents = false; // 플레이어의 공격/발소리 이벤트를 복제해 실행하지 않는다.
        }
    }

    private void Start()
    {
        if (_animator != null)
        {
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                if (parameter.name == "MoveMotion") _moveParameter = true;
                if (parameter.name == "Attack") _attackParameter = true;
                if (parameter.name == "IsReloading") _reloadParameter = true;
            }
            if (_animator.isHuman)
            {
                _hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
                _upper = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                _lower = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            }
            foreach (Renderer renderer in _animator.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.enabled = true;
            }
            PrepareIntegratedGun();
        }
        _axe = WeaponModels.Create("Axe");
        _pistol = WeaponModels.Create("Revolver");
        _pistolMuzzle = WeaponModels.Find(_pistol, "Muzzle");
        _axe.SetParent(transform, false);
        _pistol.SetParent(transform, false);
        _axe.gameObject.SetActive(false);
        _pistol.gameObject.SetActive(false);
        if (_integratedGun.Count == 0 && _materialSwaps.Count == 0)
        {
            _rifle = WeaponModels.Create("Rifle");
            _rifle.SetParent(transform, false);
            _rifleMuzzle = WeaponModels.Find(_rifle, "Muzzle");
            _rifle.gameObject.SetActive(false);
        }
        Material light = SpecialEffects.GetMaterial(new Color(1f, 0.74f, 0.28f, 0.8f), 1.5f);
        GameObject trail = new GameObject("AllyShotTrail");
        trail.transform.SetParent(transform, false);
        _tracer = trail.AddComponent<LineRenderer>();
        _tracer.sharedMaterial = light;
        _tracer.useWorldSpace = true;
        _tracer.positionCount = 2;
        _tracer.startWidth = 0.014f;
        _tracer.endWidth = 0.004f;
        _tracer.shadowCastingMode = ShadowCastingMode.Off;
        _tracer.receiveShadows = false;
        _tracer.enabled = false;
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "AllyMuzzleFlash";
        flash.transform.SetParent(transform, false);
        Collider collider = flash.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        flash.GetComponent<Renderer>().sharedMaterial = light;
        flash.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        _flash = flash.transform;
        flash.SetActive(false);
        _built = true;
        SetWeapon(WeaponSlot.Rifle);
    }

    public Vector3 Muzzle
    {
        get
        {
            if (_weapon == WeaponSlot.Revolver && _pistolMuzzle != null) return _pistolMuzzle.position;
            if (_weapon == WeaponSlot.Rifle && _rifleMuzzle != null) return _rifleMuzzle.position;
            Vector3 hand = _hand != null ? _hand.position : transform.position + Vector3.up * 1.3f;
            Vector3 direction = _target != null ? (_target.transform.position + Vector3.up - hand).normalized : transform.forward;
            return hand + direction * 0.55f;
        }
    }

    public void SetMotion(Vector3 velocity, Zombie target, WeaponSlot weapon, bool reloading)
    {
        _target = target;
        _reloading = reloading;
        if (!_built) return;
        SetWeapon(weapon);
        if (_animator != null)
        {
            if (_moveParameter) _animator.SetFloat("MoveMotion", Mathf.Clamp01(velocity.magnitude / 5.5f), 0.12f, Time.deltaTime);
            if (_reloadParameter) _animator.SetBool("IsReloading", reloading);
        }
    }

    private void SetWeapon(WeaponSlot weapon)
    {
        if (_weapon == weapon) return;
        _weapon = weapon;
        _axe.gameObject.SetActive(weapon == WeaponSlot.Axe);
        _pistol.gameObject.SetActive(weapon == WeaponSlot.Revolver);
        if (_rifle != null) _rifle.gameObject.SetActive(weapon == WeaponSlot.Rifle);
        foreach (Renderer renderer in _integratedGun) if (renderer != null) renderer.enabled = weapon == WeaponSlot.Rifle;
        foreach (MaterialSwap swap in _materialSwaps)
            if (swap.Renderer != null) swap.Renderer.sharedMaterials = weapon == WeaponSlot.Rifle ? swap.Original : swap.Hidden;
    }

    public void Attack()
    {
        _swingAt = Time.time;
        if (_animator != null && _attackParameter) _animator.SetTrigger("Attack");
    }

    public void Shot(Vector3 end)
    {
        if (!_built) return;
        _kick = 1f;
        _flashUntil = Time.time + 0.045f;
        _tracer.SetPosition(0, Muzzle);
        _tracer.SetPosition(1, end);
        _tracer.enabled = true;
        if (_animator != null && _attackParameter) _animator.SetTrigger("Attack");
    }

    private void LateUpdate()
    {
        if (!_built) return;
        _kick = Mathf.MoveTowards(_kick, 0f, Time.deltaTime * 10f);
        bool flash = Time.time < _flashUntil && GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
        _tracer.enabled = flash;
        _flash.gameObject.SetActive(flash);
        if (flash) { _flash.position = Muzzle; _flash.localScale = new Vector3(0.055f, 0.055f, 0.12f); _flash.rotation = transform.rotation; }
        Transform item = _weapon == WeaponSlot.Axe ? _axe : _weapon == WeaponSlot.Revolver ? _pistol : _rifle;
        if (item == null) return;
        float swing = Mathf.Clamp01((Time.time - _swingAt) / 0.6f);
        float chop = _weapon == WeaponSlot.Axe ? Mathf.Sin(swing * Mathf.PI) : 0f;
        Vector3 localHand = new Vector3(0.2f, 1.22f, 0.4f) + new Vector3(-0.13f, 0.2f, 0.15f) * chop;
        Vector3 target = transform.TransformPoint(localHand);
        if (_hand != null && _weapon != WeaponSlot.Rifle)
            SolveArm(target, transform.TransformPoint(new Vector3(0.65f, 0.8f, -0.2f)));
        Vector3 palm = _hand != null ? _hand.position : target;
        Vector3 forward = _target != null ? (_target.transform.position + Vector3.up * 1.1f - palm).normalized : transform.forward;
        if (forward.sqrMagnitude < 0.01f) forward = transform.forward;
        item.position = palm + forward * (0.015f - _kick * 0.025f);
        float reload = _reloading ? 25f : 0f;
        item.rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(_weapon == WeaponSlot.Axe ? -15f + chop * 105f : -4f * _kick,
            0f, _weapon == WeaponSlot.Axe ? -12f : reload);
    }

    private void SolveArm(Vector3 target, Vector3 hint)
    {
        if (_upper == null || _lower == null || _hand == null) return;
        Vector3 a = _upper.position, b = _lower.position, c = _hand.position;
        float upper = Vector3.Distance(a, b), lower = Vector3.Distance(b, c);
        if (upper < 0.001f || lower < 0.001f) return;
        float reach = Mathf.Clamp(Vector3.Distance(a, target), 0.01f, (upper + lower) * 0.999f);
        float current = Vector3.Angle(a - b, c - b);
        float wanted = Mathf.Acos(Mathf.Clamp((upper * upper + lower * lower - reach * reach) / (2f * upper * lower), -1f, 1f)) * Mathf.Rad2Deg;
        Vector3 axis = Vector3.Cross(b - a, c - b);
        if (axis.sqrMagnitude < 0.000001f) axis = Vector3.Cross(b - a, hint - a);
        if (axis.sqrMagnitude > 0.000001f) _lower.rotation = Quaternion.AngleAxis(current - wanted, axis.normalized) * _lower.rotation;
        _upper.rotation = Quaternion.FromToRotation(_hand.position - a, target - a) * _upper.rotation;
        Vector3 reachAxis = (target - a).normalized;
        Vector3 elbow = Vector3.ProjectOnPlane(_lower.position - a, reachAxis);
        Vector3 preferred = Vector3.ProjectOnPlane(hint - a, reachAxis);
        if (elbow.sqrMagnitude > 0.000001f && preferred.sqrMagnitude > 0.000001f)
            _upper.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(elbow, preferred, reachAxis), reachAxis) * _upper.rotation;
    }

    private static bool GunName(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower.Contains("weapon") || lower.Contains("gun") || lower.Contains("rifle") || lower.Contains("muzzle") || lower.Contains("magazine");
    }

    private bool GunBone(Transform bone)
    {
        while (bone != null && bone != _animator.transform)
        {
            if (GunName(bone.name)) return true;
            bone = bone.parent;
        }
        return false;
    }

    private void PrepareIntegratedGun()
    {
        Shader shader = Shader.Find("UI/Default");
        if (shader != null) { _hidden = new Material(shader); _hidden.color = Color.clear; }
        foreach (Renderer renderer in _animator.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is SkinnedMeshRenderer skin && SplitGun(skin)) continue;
            if (!(renderer is SkinnedMeshRenderer) && (GunName(renderer.name) || GunBone(renderer.transform)))
            { _integratedGun.Add(renderer); continue; }
            Material[] original = renderer.sharedMaterials;
            Material[] hidden = (Material[])original.Clone();
            bool found = false;
            for (int i = 0; i < original.Length; i++)
                if (_hidden != null && original[i] != null && GunName(original[i].name)) { hidden[i] = _hidden; found = true; }
            if (found) _materialSwaps.Add(new MaterialSwap { Renderer = renderer, Original = original, Hidden = hidden });
        }
    }

    private bool SplitGun(SkinnedMeshRenderer skin)
    {
        Mesh source = skin.sharedMesh;
        if (source == null || !source.isReadable) return false;
        BoneWeight[] weights = source.boneWeights;
        Transform[] bones = skin.bones;
        if (weights.Length != source.vertexCount || bones.Length == 0) return false;
        bool[] isGun = new bool[bones.Length];
        for (int i = 0; i < bones.Length; i++) isGun[i] = GunBone(bones[i]);
        Material[] materials = skin.sharedMaterials;
        List<int>[] bodyTriangles = new List<int>[source.subMeshCount];
        List<int>[] gunTriangles = new List<int>[source.subMeshCount];
        int totalGun = 0, totalBody = 0;
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            bool gunMaterial = sub < materials.Length && materials[sub] != null && GunName(materials[sub].name);
            bodyTriangles[sub] = new List<int>(); gunTriangles[sub] = new List<int>();
            int[] triangles = source.GetTriangles(sub);
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int count = 0;
                for (int j = 0; j < 3; j++)
                {
                    int bone = weights[triangles[i + j]].boneIndex0;
                    if (bone >= 0 && bone < isGun.Length && isGun[bone]) count++;
                }
                List<int> output = gunMaterial || count >= 2 ? gunTriangles[sub] : bodyTriangles[sub];
                output.Add(triangles[i]); output.Add(triangles[i + 1]); output.Add(triangles[i + 2]);
            }
            totalGun += gunTriangles[sub].Count; totalBody += bodyTriangles[sub].Count;
        }
        if (totalGun == 0) return false;
        if (totalBody == 0) { _integratedGun.Add(skin); return true; }
        Mesh bodyMesh = Instantiate(source), gunMesh = Instantiate(source);
        bodyMesh.name = source.name + "_AllyBody"; gunMesh.name = source.name + "_AllyGun";
        for (int sub = 0; sub < source.subMeshCount; sub++)
        { bodyMesh.SetTriangles(bodyTriangles[sub], sub, false); gunMesh.SetTriangles(gunTriangles[sub], sub, false); }
        skin.sharedMesh = bodyMesh;
        GameObject root = new GameObject("ExistingIntegratedRifle");
        root.transform.SetParent(skin.transform.parent, false);
        root.transform.localPosition = skin.transform.localPosition;
        root.transform.localRotation = skin.transform.localRotation;
        root.transform.localScale = skin.transform.localScale;
        SkinnedMeshRenderer gun = root.AddComponent<SkinnedMeshRenderer>();
        gun.sharedMesh = gunMesh; gun.sharedMaterials = materials; gun.bones = bones; gun.rootBone = skin.rootBone;
        gun.localBounds = skin.localBounds; gun.updateWhenOffscreen = true; gun.shadowCastingMode = ShadowCastingMode.On;
        _integratedGun.Add(gun); _createdMeshes.Add(bodyMesh); _createdMeshes.Add(gunMesh);
        return true;
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in _createdMeshes) if (mesh != null) Destroy(mesh);
        if (_hidden != null) Destroy(_hidden);
    }
}
