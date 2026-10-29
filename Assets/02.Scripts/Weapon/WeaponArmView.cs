using System.Collections.Generic;
using UnityEngine;

// 1인칭 '오른손 + 들고 있는 물건' (도끼, 리볼버). PlayerAxe / PlayerRevolver가 하나씩 만든다.
//  - 1인칭 모델(FirstPersonBodyView)을 복제해서 오른손과 손목 쪽 팔만 남긴다. (GrenadeArmView와 같은 방법)
//  - 주인 스크립트가 매 프레임 '손이 갈 자리(HandTarget)'와 '물건의 기울기(ItemRotation)'를 정해 주면
//    팔은 2관절 IK로 그 자리에 가고, 물건은 손바닥에 붙어 그 기울기로 보인다.
//  - 물건(Item)은 손잡이(Grip)가 원점인 모델이어야 한다. (WeaponModelSetup이 그렇게 만든다)
public class WeaponArmView : MonoBehaviour
{
    // ---- 주인 스크립트가 매 프레임 정한다 (카메라 기준: x 오른쪽, y 위, z 앞) ----
    public Vector3 HandTarget = new Vector3(0.18f, -0.18f, 0.4f);
    public Vector3 ElbowHint = new Vector3(0.55f, -0.75f, -0.1f);
    public Quaternion ItemRotation = Quaternion.identity;   // 카메라 기준 물건 회전
    public Vector3 ItemOffset = Vector3.zero;               // 손바닥에서 조금 옮기기 (카메라 기준)
    public bool Visible;
    public float Raise = 1f;                                // 0 = 화면 아래로 내림, 1 = 올림

    private Transform _camera;
    private FirstPersonBodyView _body;
    private Transform _arm;
    private Animator _animator;
    private Transform _upper;
    private Transform _lower;
    private Transform _hand;
    private Transform _finger;
    private Renderer[] _renderers = new Renderer[0];
    private Transform _item;
    private bool _built;
    private bool _shown;
    private const float HandRadius = 0.2f;

    public Transform Item => _item;
    public bool IsBuilt => _built;

    public static WeaponArmView Create(GameObject player, Transform item)
    {
        WeaponArmView view = player.AddComponent<WeaponArmView>();
        view._item = item;
        return view;
    }

    private void Start()
    {
        _body = GetComponent<FirstPersonBodyView>();
        _camera = Camera.main != null ? Camera.main.transform : null;
        if (_item != null && _camera != null)
        {
            _item.SetParent(_camera, false);
            _item.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (_camera == null)
        {
            return;
        }

        if (!_built)
        {
            if (_body == null || !_body.IsCalibrated)
            {
                return;
            }

            Build();
        }

        bool show = Visible && Raise > 0.001f;
        SetShown(show);
        if (!show)
        {
            return;
        }

        float low = 1f - Mathf.SmoothStep(0f, 1f, Raise);
        Vector3 local = HandTarget + new Vector3(0.05f, -0.35f, -0.05f) * low;
        Vector3 target = _camera.TransformPoint(local);
        Vector3 hint = _camera.TransformPoint(ElbowHint);
        Quaternion rotation = _camera.rotation * Quaternion.Euler(35f * low, 0f, 0f) * ItemRotation;

        if (_arm != null)
        {
            _arm.localPosition = _body.NeutralLocalPosition + new Vector3(0.04f, -0.02f, 0.12f);
            _arm.localRotation = Quaternion.identity;
            if (_animator != null)
            {
                _animator.SetFloat("MoveMotion", 0f);
            }

            SolveArm(_upper, _lower, _hand, target, hint, 1f);
        }

        if (_item != null)
        {
            Vector3 palm = target;
            if (_hand != null)
            {
                palm = _finger != null ? Vector3.Lerp(_hand.position, _finger.position, 0.6f) : _hand.position + _camera.forward * 0.05f;
            }

            _item.rotation = rotation;
            _item.position = palm + _camera.rotation * ItemOffset;
        }
    }

    private void SetShown(bool show)
    {
        if (_shown == show)
        {
            return;
        }

        _shown = show;
        if (_animator != null)
        {
            _animator.enabled = show;  // 안 보일 때는 팔 애니메이션도 쉰다. (가볍게)
        }

        foreach (Renderer renderer in _renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = show;
            }
        }

        if (_item != null)
        {
            _item.gameObject.SetActive(show);
        }
    }

    private void Build()
    {
        _built = true;
        Transform source = _body.ViewRoot;
        if (source == null)
        {
            return;
        }

        GameObject copy = Instantiate(source.gameObject, source.parent);
        copy.name = "FirstPersonWeaponArm";
        _arm = copy.transform;
        _arm.localScale = source.localScale;
        _animator = copy.GetComponent<Animator>();
        if (_animator == null || !_animator.isHuman)
        {
            Destroy(copy);
            _arm = null;
            return;  // 팔 없이 물건만 보인다.
        }

        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _upper = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        _lower = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        _hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        _finger = _animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        KeepRightHandOnly(copy);
        _shown = true;
        SetShown(false);
    }

    // ===================== 2관절 IK (GrenadeArmView와 같다) =====================

    private static void SolveArm(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 hint, float weight)
    {
        if (upper == null || lower == null || hand == null || weight <= 0f)
        {
            return;
        }

        Vector3 a = upper.position;
        Vector3 b = lower.position;
        Vector3 c = hand.position;
        float upperLength = Vector3.Distance(a, b);
        float lowerLength = Vector3.Distance(b, c);
        if (upperLength < 0.0001f || lowerLength < 0.0001f)
        {
            return;
        }

        // 다른 자세와 섞을 때를 위해 손이 갈 자리를 지금 손 위치와 섞는다.
        target = Vector3.Lerp(c, target, weight);
        float reach = Mathf.Clamp(Vector3.Distance(a, target), 0.01f, (upperLength + lowerLength) * 0.999f);

        // 1. 팔꿈치 굽힘: 어깨~손 거리가 reach가 되도록
        float currentAC = Vector3.Distance(a, c);
        float currentElbow = Vector3.Angle(a - b, c - b);
        float wantedElbow = Mathf.Acos(Mathf.Clamp((upperLength * upperLength + lowerLength * lowerLength - reach * reach) / (2f * upperLength * lowerLength), -1f, 1f)) * Mathf.Rad2Deg;
        Vector3 bendAxis = Vector3.Cross(b - a, c - b);
        if (bendAxis.sqrMagnitude < 0.000001f)
        {
            bendAxis = Vector3.Cross(b - a, hint - a);  // 팔이 쭉 펴져 있으면 hint 쪽으로 굽힌다.
        }

        if (bendAxis.sqrMagnitude > 0.000001f && currentAC > 0.0001f)
        {
            lower.rotation = Quaternion.AngleAxis(currentElbow - wantedElbow, bendAxis.normalized) * lower.rotation;
        }

        // 2. 어깨를 돌려 손이 target 쪽을 향하게
        c = hand.position;
        upper.rotation = Quaternion.FromToRotation(c - a, target - a) * upper.rotation;

        // 3. 어깨~손 축을 중심으로 돌려 팔꿈치가 hint 쪽을 향하게
        Vector3 axis = (target - a).normalized;
        Vector3 elbowDir = Vector3.ProjectOnPlane(lower.position - a, axis);
        Vector3 hintDir = Vector3.ProjectOnPlane(hint - a, axis);
        if (elbowDir.sqrMagnitude > 0.000001f && hintDir.sqrMagnitude > 0.000001f)
        {
            float twist = Vector3.SignedAngle(elbowDir, hintDir, axis);
            upper.rotation = Quaternion.AngleAxis(twist, axis) * upper.rotation;
        }
    }

    // ===================== 오른손만 남기기 (GrenadeArmView와 같다) =====================

    // 손과 손목 쪽 아래팔만 남긴다. (손에서 먼 총 부분, 팔꿈치 쪽은 지운다)
    private void KeepRightHandOnly(GameObject copy)
    {
        HashSet<Transform> keep = new HashSet<Transform>();
        if (_lower != null)
        {
            foreach (Transform t in _lower.GetComponentsInChildren<Transform>(true))
            {
                string lower = t.name.ToLowerInvariant();
                if (!(lower.Contains("gun") || lower.Contains("rifle") || lower.Contains("weapon") || lower.Contains("prop")
                      || lower.Contains("muzzle") || lower.Contains("magazine")))
                {
                    keep.Add(t);
                }
            }
        }

        float radius = HandRadius * copy.transform.lossyScale.y / 0.7f;
        List<Renderer> visible = new List<Renderer>();
        foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is SkinnedMeshRenderer skinned && Trim(skinned, keep, radius))
            {
                visible.Add(renderer);
            }
            else
            {
                renderer.enabled = false;  // 따로 된 총 메시 등
            }
        }

        _renderers = visible.ToArray();
    }

    private bool Trim(SkinnedMeshRenderer skinned, HashSet<Transform> keep, float radius)
    {
        Mesh source = skinned.sharedMesh;
        Transform[] bones = skinned.bones;
        if (source == null || !source.isReadable || bones == null || bones.Length == 0 || _hand == null)
        {
            return false;
        }

        Vector3[] vertices = source.vertices;
        BoneWeight[] weights = source.boneWeights;
        Matrix4x4[] bindposes = source.bindposes;
        if (weights.Length != vertices.Length || bindposes.Length != bones.Length)
        {
            return false;
        }

        Matrix4x4[] skin = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            skin[i] = bones[i] != null ? bones[i].localToWorldMatrix * bindposes[i] : Matrix4x4.identity;
        }

        Vector3 hand = _hand.position;
        Vector3 elbow = _lower != null ? _lower.position : hand;
        bool[] keepVertex = new bool[vertices.Length];
        for (int v = 0; v < vertices.Length; v++)
        {
            int index = weights[v].boneIndex0;
            if (index < 0 || index >= bones.Length || bones[index] == null || !keep.Contains(bones[index]))
            {
                continue;
            }

            Vector3 world = skin[index].MultiplyPoint3x4(vertices[v]);

            // 아래팔은 손목 쪽 60%만 (팔꿈치 쪽 잘린 단면이 보이지 않게)
            Vector3 forearm = hand - elbow;
            float along = forearm.sqrMagnitude > 0.000001f ? Vector3.Dot(world - elbow, forearm) / forearm.sqrMagnitude : 1f;
            bool nearHand = Vector3.Distance(world, hand) < radius;
            bool wristSide = along > 0.4f && DistanceToSegment(world, elbow, hand) < radius * 0.6f;
            keepVertex[v] = nearHand || wristSide;
        }

        Mesh trimmed = Instantiate(source);
        int kept = 0;
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            if (IsWeaponMaterial(skinned, sub))
            {
                trimmed.SetTriangles(new List<int>(), sub);
                continue;
            }

            int[] triangles = source.GetTriangles(sub);
            List<int> result = new List<int>(triangles.Length);
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                if (keepVertex[triangles[t]] && keepVertex[triangles[t + 1]] && keepVertex[triangles[t + 2]])
                {
                    result.Add(triangles[t]);
                    result.Add(triangles[t + 1]);
                    result.Add(triangles[t + 2]);
                }
            }

            kept += result.Count;
            trimmed.SetTriangles(result, sub);
        }

        skinned.sharedMesh = trimmed;
        return kept > 0;
    }

    // 군인 모델에 같이 들어 있는 총(재질 이름에 weapon/gun/rifle)은 통째로 뺀다.
    private static bool IsWeaponMaterial(SkinnedMeshRenderer skinned, int sub)
    {
        Material[] materials = skinned.sharedMaterials;
        if (sub >= materials.Length || materials[sub] == null)
        {
            return false;
        }

        string name = materials[sub].name.ToLowerInvariant();
        return name.Contains("weapon") || name.Contains("gun") || name.Contains("rifle");
    }

    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 0.000001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector3.Distance(p, a + ab * t);
    }
}
