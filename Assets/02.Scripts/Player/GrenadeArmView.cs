using System.Collections.Generic;
using UnityEngine;

// 수류탄을 들었을 때 화면에 보이는 '오른손 + 수류탄' (PlayerGrenade가 붙인다)
//
//  - 1인칭 모델(FirstPersonBodyView)을 하나 더 복제해서 오른손과 손목 쪽 팔만 남긴다. (왼팔, 총은 지운다)
//  - 손에는 코드로 만든 수류탄 모양(GrenadeModel)을 쥐어 준다.
//  - 팔은 '손이 갈 자리'를 정해 주면 어깨/팔꿈치를 알아서 굽히는 방식(2관절 IK)으로 움직인다.
//      들기     : 손이 화면 오른쪽 아래
//      힘 모으기: 손이 오른쪽 귀 옆 뒤로 (오래 누를수록 더 뒤로)
//      던지기   : 손이 화면 가운데 앞으로 쭉
//    팔꿈치는 늘 오른쪽 아래를 향해서 잘린 팔 단면이 화면에 보이지 않는다.
//  - 그림자를 만드는 몸(3인칭)의 오른팔도 같은 자리로 움직인다.
// 손 위치가 어색하면 플레이 중 인스펙터의 Hold / Wind Up / Throw 값(카메라 기준 x 오른쪽, y 위, z 앞)을 바꿔 맞춘다.
public class GrenadeArmView : MonoBehaviour
{
    [Header("손이 갈 자리 (카메라 기준 m: x 오른쪽, y 위, z 앞)")]
    [SerializeField] private Vector3 _holdTarget = new Vector3(0.17f, -0.17f, 0.36f);
    [SerializeField] private Vector3 _windUpTarget = new Vector3(0.3f, 0.06f, 0.06f);
    [SerializeField] private Vector3 _throwTarget = new Vector3(0.02f, -0.02f, 0.55f);
    [SerializeField] private Vector3 _elbowHint = new Vector3(0.55f, -0.75f, -0.1f);  // 팔꿈치가 향할 쪽

    [Header("팔 모델 위치 (1인칭 모델 기준에 더함)")]
    [SerializeField] private Vector3 _offset = new Vector3(0.04f, -0.02f, 0.12f);
    [SerializeField] private float _handRadius = 0.2f;  // 손에서 이보다 먼 메시(총)는 지운다. (모델 크기 기준)

    [Header("수류탄")]
    [SerializeField] private float _grenadeHeight = 0.1f;
    [SerializeField] private Vector3 _grenadeTilt = new Vector3(-20f, 15f, 15f);

    private PlayerGrenade _grenade;
    private FirstPersonBodyView _body;
    private Transform _camera;
    private Transform _arm;
    private Animator _animator;
    private Transform _upper;
    private Transform _lower;
    private Transform _hand;
    private Transform _finger;
    private Renderer[] _renderers;
    private Transform _grenadeVisual;
    private bool _built;
    private bool _visible;

    private Animator _bodyAnimator;  // 그림자를 만드는 3인칭 몸

    public static GrenadeArmView Create(PlayerGrenade grenade)
    {
        GrenadeArmView view = grenade.gameObject.AddComponent<GrenadeArmView>();
        view._grenade = grenade;
        return view;
    }

    private void Start()
    {
        _body = GetComponent<FirstPersonBodyView>();
        Camera camera = Camera.main;
        _camera = camera != null ? camera.transform : null;
        _bodyAnimator = GetComponentInChildren<Animator>();
        if (_bodyAnimator != null)
        {
            // 화면에 안 보여도(그림자만) 매 프레임 동작을 다시 잡아야 팔 돌리기가 쌓이지 않는다.
            _bodyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        // 카메라 바로 앞의 손이 잘려 보이지 않게 (FPS 게임은 보통 0.01~0.05)
        if (camera != null && camera.nearClipPlane > 0.05f)
        {
            camera.nearClipPlane = 0.05f;
        }
    }

    private void LateUpdate()
    {
        if (_grenade == null || _camera == null)
        {
            return;
        }

        // 1인칭 모델이 눈 위치에 맞춰진 뒤에 복제한다.
        if (!_built)
        {
            if (_body == null || !_body.IsCalibrated)
            {
                return;
            }

            Build();
            if (!_built)
            {
                enabled = false;
                return;
            }
        }

        float blend = _grenade.EquipBlend;
        SetVisible(blend > 0.01f);
        if (blend <= 0.01f)
        {
            return;
        }

        // 지금 손이 가야 할 자리 (카메라 기준) → 들 때는 화면 아래에서 올라온다.
        float rise = 1f - Mathf.SmoothStep(0f, 1f, blend);
        Vector3 local = Vector3.Lerp(_holdTarget, _windUpTarget, Mathf.SmoothStep(0f, 1f, _grenade.WindUp));
        local = Vector3.Lerp(local, _throwTarget, _grenade.Swing);
        local += new Vector3(0.05f, -0.35f, -0.05f) * rise;
        Vector3 target = _camera.TransformPoint(local);
        Vector3 hint = _camera.TransformPoint(_elbowHint);

        // 그림자 몸의 오른팔도 같은 자리로 (조금만 섞는다)
        if (_bodyAnimator != null && _bodyAnimator.isHuman)
        {
            SolveArm(_bodyAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm), _bodyAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm),
                _bodyAnimator.GetBoneTransform(HumanBodyBones.RightHand), target, hint, blend);
        }

        _arm.localPosition = _body.NeutralLocalPosition + _offset;
        _arm.localRotation = Quaternion.identity;
        if (_animator != null)
        {
            _animator.SetFloat("MoveMotion", 0f);
        }

        SolveArm(_upper, _lower, _hand, target, hint, 1f);
        PlaceGrenade();
    }

    // ===================== 2관절 IK: 손을 target에, 팔꿈치는 hint 쪽으로 =====================

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

    // 수류탄은 손바닥 자리에, 늘 반듯하게 (카메라 기준으로 살짝 기울여서)
    private void PlaceGrenade()
    {
        if (_grenadeVisual == null)
        {
            return;
        }

        bool show = _grenade.GrenadeInHand;
        if (_grenadeVisual.gameObject.activeSelf != show)
        {
            _grenadeVisual.gameObject.SetActive(show);
        }

        if (!show || _hand == null)
        {
            return;
        }

        Vector3 palm = _finger != null ? Vector3.Lerp(_hand.position, _finger.position, 0.7f) : _hand.position + _camera.forward * 0.05f;
        _grenadeVisual.rotation = _camera.rotation * Quaternion.Euler(_grenadeTilt);
        // 수류탄 몸통 가운데(높이의 40%)가 손바닥에 오게
        _grenadeVisual.position = palm - _grenadeVisual.up * (_grenadeHeight * 0.4f) + _camera.up * 0.01f;
    }

    private void SetVisible(bool visible)
    {
        if (_visible == visible)
        {
            return;
        }

        _visible = visible;
        foreach (Renderer renderer in _renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }

        if (_grenadeVisual != null && !visible)
        {
            _grenadeVisual.gameObject.SetActive(false);
        }
    }

    // ===================== 오른손 모델 만들기 =====================

    private void Build()
    {
        _built = true;
        Transform source = _body.ViewRoot;
        if (source == null)
        {
            _built = false;
            return;
        }

        GameObject copy = Instantiate(source.gameObject, source.parent);
        copy.name = "FirstPersonGrenadeArm";
        _arm = copy.transform;
        _arm.localPosition = _body.NeutralLocalPosition + _offset;
        _arm.localRotation = Quaternion.identity;
        _arm.localScale = source.localScale;
        _animator = copy.GetComponent<Animator>();
        if (_animator == null || !_animator.isHuman)
        {
            Debug.LogWarning("[GrenadeArmView] 1인칭 모델이 Humanoid가 아니라 오른팔을 만들 수 없습니다.");
            Destroy(copy);
            _built = false;
            return;
        }

        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _upper = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        _lower = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        _hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        _finger = _animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);

        KeepRightHandOnly(copy);

        _grenadeVisual = GrenadeModel.Create(_grenadeHeight).transform;
        _grenadeVisual.name = "GrenadeInHand";
        _grenadeVisual.SetParent(_camera, true);
        foreach (Renderer renderer in _grenadeVisual.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        _grenadeVisual.gameObject.SetActive(false);
        _visible = true;
        SetVisible(false);
    }

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

        float radius = _handRadius * copy.transform.lossyScale.y / 0.7f;
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

    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 0.000001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector3.Distance(p, a + ab * t);
    }
}
