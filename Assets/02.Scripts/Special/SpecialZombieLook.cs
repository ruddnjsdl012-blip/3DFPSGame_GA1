using System.Collections.Generic;
using UnityEngine;

// 특수 좀비의 겉모습
//
//  방법 1) 모델 교체: Resources/SpecialZombies/ 폴더에 Tank, Boomer, Spitter, Runner 이름의 프리팹이 있으면
//          일반 좀비 모델 대신 그 모델을 쓴다. (Humanoid 모델이면 좀비 동작이 그대로 입혀진다)
//          Tools > L4D 맵 > 특수 좀비 모델 설정 이 Assets/10.SpecialZombies의 FBX로 이 프리팹을 만들어 준다.
//
//  방법 2) 모델이 없으면 일반 좀비 모델을 '변이'시킨다. (뼈 크기 바꾸기 + 몸에 붙는 덩어리)
//          탱크   : 거대한 상체와 고릴라 팔, 작은 머리, 어깨의 바위 같은 덩어리, 등뼈 가시, 붉은 눈
//          부머   : 부풀어 오른 배, 몸 곳곳의 빛나는 고름 주머니
//          스피터 : 길게 늘어난 목, 큰 머리, 턱 밑에서 꿈틀거리는 산성 주머니 (초록 빛)
//          러너   : 마른 몸, 앞으로 숙인 자세, 노랗게 빛나는 눈
public class SpecialZombieLook : MonoBehaviour
{
    private const string ResourceFolder = "SpecialZombies/";

    private struct BoneScale
    {
        public Transform Bone;
        public Vector3 Original;
        public float Factor;
    }

    private class BoneLean
    {
        public Transform Bone;
        public float Angle;
        public Quaternion Applied;  // 지난 프레임에 기울인 결과 (애니메이션이 안 움직였으면 또 기울이지 않는다)
    }

    private SpecialType _type;
    private Animator _animator;
    private readonly List<BoneScale> _scales = new List<BoneScale>();
    private readonly List<BoneLean> _leans = new List<BoneLean>();
    private readonly List<Transform> _pulses = new List<Transform>();
    private readonly List<Vector3> _pulseBase = new List<Vector3>();
    private bool _built;
    private float _time;

    // ===================== 방법 1: 모델 교체 =====================

    // 교체할 모델이 있으면 바꾸고 true. (Instantiate 바로 뒤, 좀비 Start 전에 부른다)
    public static bool TrySwapModel(GameObject zombie, SpecialType type)
    {
        GameObject prefab = Resources.Load<GameObject>(ResourceFolder + type);
        if (prefab == null)
        {
            return false;
        }

        Animator old = zombie.GetComponentInChildren<Animator>();
        if (old == null || old.gameObject == zombie)
        {
            Debug.LogWarning("[SpecialZombieLook] 좀비 모델이 자식 오브젝트가 아니라서 교체할 수 없습니다.");
            return false;
        }

        GameObject model = Instantiate(prefab, old.transform.parent);
        model.name = "Model_" + type;
        model.transform.localPosition = old.transform.localPosition;
        model.transform.localRotation = old.transform.localRotation;

        Animator animator = model.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            animator = model.AddComponent<Animator>();
        }

        if (animator.avatar == null || !animator.avatar.isHuman)
        {
            Debug.LogWarning("[SpecialZombieLook] " + type + " 모델이 Humanoid가 아니라 좀비 동작이 입혀지지 않습니다. Tools > L4D 맵 > 특수 좀비 모델 설정을 눌러 주세요.");
        }

        // 좀비 동작(애니메이터 컨트롤러)을 그대로 쓴다.
        animator.runtimeAnimatorController = old.runtimeAnimatorController;
        animator.applyRootMotion = old.applyRootMotion;
        animator.cullingMode = old.cullingMode;

        // 키를 원래 좀비와 같게 맞춘다.
        float oldHeight = Height(old.gameObject);
        float newHeight = Height(model);
        if (oldHeight > 0.1f && newHeight > 0.1f)
        {
            model.transform.localScale *= oldHeight / newHeight;
        }

        // 공격 애니메이션 이벤트(HitEvent)를 받는 스크립트
        if (animator.GetComponent<ZombieHitEvent>() == null)
        {
            animator.gameObject.AddComponent<ZombieHitEvent>();
        }

        // 원래 모델은 끈다. (Zombie가 Start에서 새 모델의 애니메이터를 찾는다)
        old.gameObject.SetActive(false);
        Destroy(old.gameObject);
        return true;
    }

    private static float Height(GameObject root)
    {
        bool found = false;
        Bounds bounds = new Bounds();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
        {
            if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer))
            {
                continue;
            }

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found ? bounds.size.y : 0f;
    }

    // ===================== 방법 2: 변이 =====================

    public static SpecialZombieLook Mutate(GameObject zombie, SpecialType type)
    {
        SpecialZombieLook look = zombie.AddComponent<SpecialZombieLook>();
        look._type = type;
        look._animator = zombie.GetComponentInChildren<Animator>();
        look.Setup();
        return look;
    }

    private void Setup()
    {
        if (_animator == null || !_animator.isHuman)
        {
            return;  // Humanoid가 아니면 뼈를 못 찾으므로 붙는 덩어리만 몸 기준으로 붙인다.
        }

        switch (_type)
        {
            case SpecialType.Tank:
                Scale(HumanBodyBones.Spine, 1.15f);
                Scale(HumanBodyBones.Chest, 1.35f);        // 가슴을 키우면 팔, 목, 머리도 함께 커진다.
                Scale(HumanBodyBones.Neck, 0.8f);
                Scale(HumanBodyBones.Head, 0.7f);          // 그래서 머리는 다시 줄여 작게
                Scale(HumanBodyBones.LeftUpperArm, 1.25f);
                Scale(HumanBodyBones.RightUpperArm, 1.25f);
                Scale(HumanBodyBones.LeftHand, 1.25f);
                Scale(HumanBodyBones.RightHand, 1.25f);
                Lean(HumanBodyBones.Chest, 14f);           // 앞으로 구부정하게
                break;

            case SpecialType.Boomer:
                Scale(HumanBodyBones.Spine, 1.7f);         // 배가 부푼다.
                Scale(HumanBodyBones.Chest, 0.62f);        // 그 위(가슴, 팔, 머리)는 원래 크기로 되돌린다.
                Scale(HumanBodyBones.Head, 0.92f);
                Lean(HumanBodyBones.Spine, -7f);           // 배를 내밀고 뒤로 젖힌 자세
                break;

            case SpecialType.Spitter:
                Scale(HumanBodyBones.Chest, 0.9f);
                Scale(HumanBodyBones.Neck, 1.7f);          // 목이 길게
                Scale(HumanBodyBones.Head, 0.78f);
                Scale(HumanBodyBones.LeftUpperArm, 0.85f);
                Scale(HumanBodyBones.RightUpperArm, 0.85f);
                Scale(HumanBodyBones.LeftLowerArm, 1.2f);  // 가늘고 긴 팔
                Scale(HumanBodyBones.RightLowerArm, 1.2f);
                Lean(HumanBodyBones.Neck, 18f);            // 목을 앞으로 쭉 뺀다.
                break;

            case SpecialType.Runner:
                Scale(HumanBodyBones.Spine, 0.95f);
                Scale(HumanBodyBones.Chest, 0.88f);
                Scale(HumanBodyBones.Head, 0.95f);
                Scale(HumanBodyBones.LeftUpperArm, 0.92f);
                Scale(HumanBodyBones.RightUpperArm, 0.92f);
                Lean(HumanBodyBones.Spine, 22f);           // 잔뜩 숙이고 달린다.
                break;
        }
    }

    private void Scale(HumanBodyBones bone, float factor)
    {
        Transform t = _animator.GetBoneTransform(bone);
        if (t != null)
        {
            _scales.Add(new BoneScale { Bone = t, Original = t.localScale, Factor = factor });
        }
    }

    private void Lean(HumanBodyBones bone, float angle)
    {
        Transform t = _animator.GetBoneTransform(bone);
        if (t != null)
        {
            _leans.Add(new BoneLean { Bone = t, Angle = angle, Applied = new Quaternion(0f, 0f, 0f, 0f) });
        }
    }

    // 애니메이션이 뼈를 움직인 뒤에 크기와 기울기를 덮어쓴다.
    private void LateUpdate()
    {
        foreach (BoneScale scale in _scales)
        {
            if (scale.Bone != null)
            {
                scale.Bone.localScale = scale.Original * scale.Factor;
            }
        }

        // 좀비 몸의 오른쪽 축을 기준으로 앞으로 숙인다. (뼈의 축 방향과 상관없이)
        // (화면 밖이라 애니메이션이 멈춘 프레임에는 다시 기울이지 않는다. 계속 쌓이면 몸이 돌아가 버린다)
        foreach (BoneLean lean in _leans)
        {
            if (lean.Bone == null || Quaternion.Angle(lean.Bone.localRotation, lean.Applied) < 0.01f)
            {
                continue;
            }

            lean.Bone.rotation = Quaternion.AngleAxis(lean.Angle, transform.right) * lean.Bone.rotation;
            lean.Applied = lean.Bone.localRotation;
        }

        if (!_built)
        {
            _built = true;
            BuildAttachments();
        }

        // 고름/산성 주머니가 꿈틀거린다.
        _time += Time.deltaTime;
        for (int i = 0; i < _pulses.Count; i++)
        {
            if (_pulses[i] != null)
            {
                float pulse = 1f + 0.12f * Mathf.Sin(_time * 4f + i * 1.7f);
                _pulses[i].localScale = _pulseBase[i] * pulse;
            }
        }
    }

    // ===================== 몸에 붙는 덩어리 =====================

    private void BuildAttachments()
    {
        float s = transform.lossyScale.y;
        Transform head = Bone(HumanBodyBones.Head);
        Transform chest = Bone(HumanBodyBones.Chest) ?? Bone(HumanBodyBones.Spine);
        Transform spine = Bone(HumanBodyBones.Spine);
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Vector3 up = transform.up;
        Vector3 headPos = head != null ? head.position : transform.position + up * 1.6f * s;
        Vector3 chestPos = chest != null ? chest.position : transform.position + up * 1.25f * s;
        Vector3 spinePos = spine != null ? spine.position : transform.position + up * 1.05f * s;

        switch (_type)
        {
            case SpecialType.Tank:
                {
                    Color rock = new Color(0.32f, 0.27f, 0.25f);
                    Transform leftShoulder = Bone(HumanBodyBones.LeftUpperArm);
                    Transform rightShoulder = Bone(HumanBodyBones.RightUpperArm);
                    Vector3 leftPos = leftShoulder != null ? leftShoulder.position : chestPos - right * 0.22f * s + up * 0.1f * s;
                    Vector3 rightPos = rightShoulder != null ? rightShoulder.position : chestPos + right * 0.22f * s + up * 0.1f * s;
                    // 어깨 위 울퉁불퉁한 덩어리
                    for (int i = 0; i < 3; i++)
                    {
                        Attach(PrimitiveType.Cube, leftShoulder ?? chest, leftPos + up * (0.06f + i * 0.04f) * s - right * i * 0.04f * s,
                            Vector3.one * (0.2f - i * 0.04f) * s, Quaternion.Euler(20f * i, 35f * i, 15f), rock, 0f);
                        Attach(PrimitiveType.Cube, rightShoulder ?? chest, rightPos + up * (0.06f + i * 0.04f) * s + right * i * 0.04f * s,
                            Vector3.one * (0.2f - i * 0.04f) * s, Quaternion.Euler(-20f * i, -35f * i, -15f), rock, 0f);
                    }

                    // 등에 튀어나온 뼈 가시
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 position = chestPos - forward * 0.17f * s + up * (0.12f - i * 0.1f) * s;
                        Attach(PrimitiveType.Cylinder, chest, position, new Vector3(0.035f, 0.09f, 0.035f) * s,
                            Quaternion.LookRotation(up, -forward), new Color(0.85f, 0.8f, 0.7f), 0f);
                    }

                    Eyes(head, headPos, s * 0.75f, new Color(1f, 0.15f, 0.05f));
                    break;
                }

            case SpecialType.Boomer:
                {
                    Color pus = new Color(0.75f, 0.85f, 0.25f);
                    for (int i = 0; i < 9; i++)
                    {
                        // 배와 등, 옆구리 곳곳에
                        float angle = i * 40f + Random.Range(-15f, 15f);
                        Vector3 around = Quaternion.AngleAxis(angle, up) * forward;
                        Vector3 position = spinePos + around * Random.Range(0.18f, 0.24f) * s + up * Random.Range(-0.12f, 0.25f) * s;
                        float size = Random.Range(0.07f, 0.13f) * s;
                        Transform blob = Attach(PrimitiveType.Sphere, spine, position, Vector3.one * size, Quaternion.identity, pus, 0.9f);
                        AddPulse(blob);
                    }

                    break;
                }

            case SpecialType.Spitter:
                {
                    Color acid = new Color(0.5f, 1f, 0.15f);
                    // 턱 밑 산성 주머니
                    Transform sac = Attach(PrimitiveType.Sphere, head, headPos - up * 0.12f * s + forward * 0.06f * s,
                        new Vector3(0.14f, 0.12f, 0.14f) * s, Quaternion.identity, acid, 1.6f);
                    AddPulse(sac);
                    // 입가의 빛
                    if (head != null)
                    {
                        GameObject light = new GameObject("AcidGlow");
                        light.transform.SetParent(head, false);
                        light.transform.position = headPos + forward * 0.2f * s;
                        Light l = light.AddComponent<Light>();
                        l.type = LightType.Point;
                        l.color = acid;
                        l.range = 2.5f;
                        l.intensity = 1.5f;
                        l.shadows = LightShadows.None;
                    }

                    // 목을 따라 작은 혹
                    for (int i = 0; i < 3; i++)
                    {
                        Transform bump = Attach(PrimitiveType.Sphere, Bone(HumanBodyBones.Neck) ?? head,
                            Vector3.Lerp(chestPos, headPos, 0.35f + i * 0.2f) - forward * 0.05f * s + right * (i % 2 == 0 ? 0.04f : -0.04f) * s,
                            Vector3.one * 0.05f * s, Quaternion.identity, acid * 0.8f, 1.2f);
                        AddPulse(bump);
                    }

                    Eyes(head, headPos, s, acid);
                    break;
                }

            case SpecialType.Runner:
                Eyes(head, headPos, s, new Color(1f, 0.85f, 0.2f));
                break;
        }
    }

    // 빛나는 두 눈
    private void Eyes(Transform head, Vector3 headPos, float s, Color color)
    {
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Vector3 up = transform.up;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 position = headPos + forward * 0.1f * s + up * 0.04f * s + right * side * 0.035f * s;
            Attach(PrimitiveType.Sphere, head, position, Vector3.one * 0.025f * s, Quaternion.identity, color, 4f);
        }
    }

    private Transform Attach(PrimitiveType shape, Transform parent, Vector3 worldPosition, Vector3 worldSize, Quaternion localRotation, Color color, float emission)
    {
        GameObject part = SpecialEffects.Shape(shape, color, emission);
        part.name = "Mutation";
        Transform t = part.transform;
        t.SetParent(parent != null ? parent : transform, true);
        t.position = worldPosition;
        t.localRotation = localRotation;

        // 부모(뼈)가 커지거나 줄어든 만큼 되돌려서 원하는 크기로
        Vector3 parentScale = t.parent.lossyScale;
        t.localScale = new Vector3(worldSize.x / Mathf.Max(0.0001f, parentScale.x), worldSize.y / Mathf.Max(0.0001f, parentScale.y), worldSize.z / Mathf.Max(0.0001f, parentScale.z));
        return t;
    }

    private void AddPulse(Transform t)
    {
        _pulses.Add(t);
        _pulseBase.Add(t.localScale);
    }

    private Transform Bone(HumanBodyBones bone)
    {
        return _animator != null && _animator.isHuman ? _animator.GetBoneTransform(bone) : null;
    }
}
