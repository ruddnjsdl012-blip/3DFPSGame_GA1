using System.Collections.Generic;
using UnityEngine;

// 수류탄을 들었을 때 화면에 보이는 '오른팔만' (PlayerGrenade가 붙인다)
//
//  - 1인칭 모델(FirstPersonBodyView)을 하나 더 복제해서, 오른쪽 아래팔과 손만 남긴다. (왼팔과 총은 지운다)
//  - 오른손에 수류탄(내 Bomb 프리팹 모양)을 쥐어 준다.
//  - 동작 파일 없이 뼈를 돌려서 움직인다:
//      들기     : 손을 화면 오른쪽 아래에 든다.
//      힘 모으기: 팔을 뒤로 높이 젖힌다. (오래 누를수록 더 젖힌다)
//      던지기   : 팔을 앞으로 크게 휘두른다.
//    뼈를 '카메라 기준'으로 돌리므로 모델의 뼈 축 방향과 상관없이 같은 방향으로 움직인다.
//  - 그림자를 만드는 몸(3인칭)의 오른팔도 같이 돌린다.
// 팔 위치가 어색하면 플레이 중 인스펙터의 각도 값을 바꿔 맞춘다.
public class GrenadeArmView : MonoBehaviour
{
    [Header("들고 있을 때 (도, 카메라 기준)")]
    [SerializeField] private float _holdUpperPitch = 12f;    // +: 팔을 아래로, -: 위로
    [SerializeField] private float _holdUpperYaw = -6f;      // +: 바깥(오른쪽)으로, -: 안쪽으로
    [SerializeField] private float _holdLowerPitch = -10f;   // 팔꿈치 굽힘

    [Header("힘 모으기 (뒤로 젖히기, 최대일 때)")]
    [SerializeField] private float _windUpperPitch = -75f;
    [SerializeField] private float _windUpperYaw = 12f;
    [SerializeField] private float _windLowerPitch = -40f;

    [Header("던지기 (앞으로 휘두르기, 최대일 때)")]
    [SerializeField] private float _swingUpperPitch = 45f;
    [SerializeField] private float _swingUpperYaw = -18f;
    [SerializeField] private float _swingLowerPitch = 20f;

    [Header("위치")]
    [SerializeField] private Vector3 _offset = new Vector3(0f, 0f, 0.04f);
    [SerializeField] private float _handRadius = 0.2f;  // 손에서 이보다 먼 메시(총)는 지운다. (모델 크기 기준)
    [SerializeField] private float _grenadeSize = 0.09f;

    private PlayerGrenade _grenade;
    private FirstPersonBodyView _body;
    private Transform _camera;
    private Transform _arm;
    private Animator _animator;
    private Transform _upper;
    private Transform _lower;
    private Transform _hand;
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
        _camera = Camera.main != null ? Camera.main.transform : null;
        _bodyAnimator = GetComponentInChildren<Animator>();
        if (_bodyAnimator != null)
        {
            // 화면에 안 보여도(그림자만) 매 프레임 동작을 다시 잡아야 팔 돌리기가 쌓이지 않는다.
            _bodyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
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

        // 그림자 몸의 오른팔도 같이 (수류탄을 들었을 때만)
        if (blend > 0.01f && _bodyAnimator != null && _bodyAnimator.isHuman)
        {
            ApplyPose(_bodyAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm), _bodyAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm), blend);
        }

        if (!_visible)
        {
            return;
        }

        // 들 때는 화면 아래에서 올라온다.
        float rise = 1f - Mathf.SmoothStep(0f, 1f, blend);
        _arm.localPosition = _body.NeutralLocalPosition + _offset + new Vector3(0.05f, -0.4f, 0f) * rise;
        _arm.localRotation = Quaternion.identity;

        if (_animator != null)
        {
            _animator.SetFloat("MoveMotion", 0f);
        }

        ApplyPose(_upper, _lower, 1f);

        if (_grenadeVisual != null)
        {
            _grenadeVisual.gameObject.SetActive(_grenade.GrenadeInHand);
        }
    }

    // 들기 + 젖히기 + 휘두르기를 섞어서 오른팔 뼈를 돌린다.
    private void ApplyPose(Transform upper, Transform lower, float weight)
    {
        if (upper == null)
        {
            return;
        }

        float wind = _grenade.WindUp;
        float swing = _grenade.Swing;
        float upperPitch = (_holdUpperPitch + _windUpperPitch * wind + _swingUpperPitch * swing) * weight;
        float upperYaw = (_holdUpperYaw + _windUpperYaw * wind + _swingUpperYaw * swing) * weight;
        float lowerPitch = (_holdLowerPitch + _windLowerPitch * wind + _swingLowerPitch * swing) * weight;

        Vector3 right = _camera.right;
        Vector3 up = _camera.up;
        upper.rotation = Quaternion.AngleAxis(upperPitch, right) * Quaternion.AngleAxis(upperYaw, up) * upper.rotation;
        if (lower != null)
        {
            lower.rotation = Quaternion.AngleAxis(lowerPitch, right) * lower.rotation;
        }
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

        if (_grenadeVisual != null)
        {
            _grenadeVisual.gameObject.SetActive(visible);
        }
    }

    // ===================== 오른팔 모델 만들기 =====================

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
            Debug.LogWarning("[GrenadeArmView] 1인칭 모델이 Humanoid가 아니라 오른팔 동작을 만들 수 없습니다.");
            Destroy(copy);
            _built = false;
            return;
        }

        _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        _upper = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        _lower = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        _hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);

        KeepRightHandOnly(copy);
        AttachGrenade();
        _visible = true;
        SetVisible(false);
    }

    // 오른쪽 아래팔과 손에 붙은 삼각형만 남기고, 손에서 먼 부분(총)도 지운다.
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

            // 손목~손가락은 손 근처만, 아래팔은 팔꿈치~손목 사이만 (멀리 있는 총 부분은 버린다)
            Vector3 world = skin[index].MultiplyPoint3x4(vertices[v]);
            float toSegment = DistanceToSegment(world, elbow, hand);
            keepVertex[v] = Vector3.Distance(world, hand) < radius || toSegment < radius * 0.6f;
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

    // 손바닥 쪽에 수류탄을 쥐어 준다. (내 Bomb 프리팹의 모양만 복사)
    private void AttachGrenade()
    {
        if (_hand == null)
        {
            return;
        }

        PlayerBombFire bombFire = FindAnyObjectByType<PlayerBombFire>();
        GameObject visual = null;
        if (bombFire != null && bombFire.BombPrefab != null)
        {
            visual = Instantiate(bombFire.BombPrefab.gameObject);
            // 모양만 남긴다. (부딪히면 터지는 스크립트, 물리, 충돌은 지운다)
            foreach (MonoBehaviour script in visual.GetComponentsInChildren<MonoBehaviour>(true))
            {
                DestroyImmediate(script);
            }

            foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
            {
                DestroyImmediate(body);
            }

            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            {
                DestroyImmediate(collider);
            }
        }

        if (visual == null)
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(visual.GetComponent<Collider>());
        }

        visual.name = "GrenadeInHand";
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 크기 맞추기
        Bounds bounds = new Bounds(visual.transform.position, Vector3.zero);
        bool found = false;
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
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

        float size = found ? Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) : 1f;
        visual.transform.localScale *= _grenadeSize / Mathf.Max(0.001f, size);

        // 손가락 쪽으로 조금 나간 자리 (손바닥)
        Transform finger = _animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        Vector3 palm = finger != null ? Vector3.Lerp(_hand.position, finger.position, 0.8f) : _hand.position + _camera.forward * 0.05f;
        palm -= _camera.up * 0.025f;
        visual.transform.SetParent(_hand, true);
        visual.transform.position = palm;
        _grenadeVisual = visual.transform;
        visual.SetActive(false);
    }
}
