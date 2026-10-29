using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 1인칭 화면 (GunFeedback이 Player에 자동으로 붙인다)
//
//  - 플레이어 몸(Player/Model)은 화면에서 숨기고 그림자만 남긴다. → 달릴 때 몸이 앞을 가리지 않는다.
//  - 대신 군인 모델을 하나 더 만들어 카메라에 붙인다(1인칭 모델). 카메라와 함께 움직이므로
//    총이 늘 화면에 보이고, 쏘는 동작과 재장전 동작도 카메라 앞에서 보인다.
//  - 1인칭 모델은 늘 '총을 겨누고 서 있는' 자세에서 쏘기/재장전만 하고, 달릴 때는 살짝 흔들린다.
//  - 머리와 다리는 줄여서 안 보이게 한다.
//
// 총 위치가 어색하면 플레이 중 인스펙터의 View Offset / Reload Offset / Reload Tilt를 바꿔 맞춘다.
public class FirstPersonBodyView : MonoBehaviour
{
    [Header("1인칭 모델 위치 (카메라 기준, 자동 계산 값에 더함)")]
    [SerializeField] private Vector3 _viewOffset = Vector3.zero;

    [Header("재장전할 때 (동작이 화면 안에 들어오게 올리고 기울인다)")]
    [SerializeField] private Vector3 _reloadOffset = Vector3.zero;
    [SerializeField] private Vector3 _reloadTilt = new Vector3(-8f, 0f, 0f);
    [Tooltip("재장전하는 동안 오른손(총)을 붙잡아 둘 화면 속 자리 (카메라 기준). 손이 화면 밖으로 나가지 않게 모델을 따라 옮긴다.")]
    [SerializeField] private Vector3 _reloadHandTarget = new Vector3(0.1f, -0.2f, 0.45f);
    [SerializeField] private float _handFollowSpeed = 10f;

    [Header("총구 (자동으로 총 끝을 찾는다. 어긋나면 카메라 기준으로 더해서 맞춘다)")]
    [SerializeField] private Vector3 _muzzleAdjust = Vector3.zero;

    [Header("달릴 때 흔들림")]
    [SerializeField] private float _bobAmount = 0.03f;
    [SerializeField] private float _bobSpeed = 11f;

    [Header("치료할 때 (총을 내린다)")]
    [SerializeField] private Vector3 _healOffset = new Vector3(0f, -0.3f, -0.15f);
    [SerializeField] private Vector3 _healTilt = new Vector3(15f, 0f, 0f);

    [Header("쏠 때 총이 뒤로 튐")]
    [SerializeField] private float _kickBack = 0.05f;     // 한 발마다 총이 뒤로 밀리는 거리 (m)
    [SerializeField] private float _kickUp = 0.02f;       // 위로 들리는 거리 (m)
    [SerializeField] private float _kickPitch = 3f;       // 총구가 들리는 각도 (도)
    [SerializeField] private float _kickReturn = 16f;     // 돌아오는 빠르기

    private float _kick;  // 0 ~ 1.6, 쏠 때 올라가고 빠르게 줄어든다.
    private float _kickRoll;
    private PlayerInventory _inventory;
    private PlayerGrenade _grenade;
    private bool _hiddenForGrenade;
    private PlayerWeapons _weapons;
    private float _rifleBlend = 1f;
    private float _healBlend;
    private Animator _bodyAnimator;
    private Animator _viewAnimator;
    private Transform _view;
    private Transform _camera;
    private Transform _viewHand;
    private Vector3 _baseOffset;
    private float _reloadBlend;
    private float _bobTime;
    private float _moveBlend;
    private Transform _viewHead;
    private Vector3 _handFollow;
    private bool _warned;
    private Renderer[] _viewRenderers;
    private readonly Dictionary<SkinnedMeshRenderer, bool[]> _keptVertices = new Dictionary<SkinnedMeshRenderer, bool[]>();
    private Transform _muzzleBone;     // 총 끝 점이 붙어 움직이는 뼈 (또는 총 오브젝트)
    private Vector3 _muzzleLocal;      // 그 뼈 기준 총 끝 위치
    private int _calibrateFrames = 3;   // 몇 프레임 뒤 (서 있는 자세가 잡힌 뒤) 머리 위치를 재서 눈에 맞춘다.

    // 총구 화염을 붙일 1인칭 모델의 오른손
    public Transform ViewHand => _viewHand;

    // 1인칭 모델 (GrenadeArmView가 복제해서 오른팔만 쓴다)
    public Transform ViewRoot => _view;
    public bool IsCalibrated => _view != null && _calibrateFrames <= 0;
    public Vector3 NeutralLocalPosition => _baseOffset + _viewOffset;

    // 1인칭 총의 총구(총 끝) 위치. 아직 못 찾았으면 false
    public bool TryGetMuzzle(out Vector3 position)
    {
        if (_muzzleBone == null || _calibrateFrames > 0)
        {
            position = Vector3.zero;
            return false;
        }

        position = _muzzleBone.TransformPoint(_muzzleLocal) + _camera.TransformVector(_muzzleAdjust);
        return true;
    }

    private void Start()
    {
        _bodyAnimator = GetComponentInChildren<Animator>();
        Camera camera = Camera.main;
        if (_bodyAnimator == null || camera == null)
        {
            enabled = false;
            return;
        }

        _camera = camera.transform;
        _inventory = GetComponent<PlayerInventory>();

        // 1인칭 모델을 놓을 자리: 몸에서 Model과 눈(CameraPosition)의 차이 만큼
        Transform eye = transform.Find("CameraPosition");
        Vector3 eyeLocal = eye != null ? eye.localPosition : transform.InverseTransformPoint(_camera.position);
        Vector3 modelLocal = transform.InverseTransformPoint(_bodyAnimator.transform.position);
        _baseOffset = modelLocal - eyeLocal;

        // 1. 1인칭 모델 만들기 (몸을 복제해서 카메라 아래에)
        GameObject copy = Instantiate(_bodyAnimator.gameObject, _camera);
        copy.name = "FirstPersonModel";
        _view = copy.transform;
        _view.localPosition = _baseOffset + _viewOffset;
        _view.localRotation = Quaternion.identity;
        _view.localScale = _bodyAnimator.transform.lossyScale;
        _viewAnimator = copy.GetComponent<Animator>();
        _viewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (MonoBehaviour script in copy.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Destroy(script);  // 애니메이션 이벤트 받는 스크립트 등은 필요 없다.
        }

        foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;  // 그림자는 몸이 만든다.
            renderer.receiveShadows = false;
            if (renderer is SkinnedMeshRenderer skinned)
            {
                skinned.updateWhenOffscreen = true;  // 카메라에 아주 가까워도 사라지지 않게
            }
        }

        if (_viewAnimator.isHuman)
        {
            _viewHand = _viewAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            _viewHead = _viewAnimator.GetBoneTransform(HumanBodyBones.Head);
        }

        // 손(아래팔부터 손가락까지)과 총만 남기고 몸통, 머리, 다리는 지운다.
        KeepOnlyHands(copy);

        // 위치를 맞추기 전에는 보이지 않게 한다.
        SetViewVisible(false);

        // 2. 몸은 숨기고 그림자만
        foreach (Renderer renderer in _bodyAnimator.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }

    // GunFeedback이 한 발 쏠 때마다 부른다. (1인칭 모델은 달리는 중에도 쏘는 동작을 한다)
    public void OnShot()
    {
        if (_viewAnimator != null)
        {
            _viewAnimator.SetTrigger("Attack");
        }

        _kick = Mathf.Min(_kick + 1f, 1.6f);
        _kickRoll = Random.Range(-1f, 1f);
    }

    private void LateUpdate()
    {
        if (_viewAnimator == null)
        {
            return;
        }

        // 몸의 애니메이터 값을 따라 한다. 1인칭 모델은 늘 서 있는 자세(MoveMotion 0)
        bool hasReload = HasParameter(_bodyAnimator, "IsReloading");
        bool reloading = hasReload && _bodyAnimator.GetBool("IsReloading");
        if (!hasReload && !_warned && Input.GetKeyDown(KeyCode.R))
        {
            _warned = true;
            Debug.LogWarning("[FirstPersonBodyView] PlayerAC에 IsReloading이 없습니다. 플레이를 멈추고 Tools > L4D 맵 > 재장전 동작 적용을 눌러 주세요.");
        }
        if (HasParameter(_viewAnimator, "IsReloading"))
        {
            _viewAnimator.SetBool("IsReloading", reloading);
        }

        _viewAnimator.SetFloat("MoveMotion", 0f);

        // 재장전 중에는 동작이 화면 안에 들어오도록 올리고 기울인다.
        _reloadBlend = Mathf.MoveTowards(_reloadBlend, reloading ? 1f : 0f, Time.deltaTime * 4f);
        float reload = Mathf.SmoothStep(0f, 1f, _reloadBlend);

        // 달릴 때 위아래/좌우로 살짝 흔들린다.
        float move = _bodyAnimator.GetFloat("MoveMotion");
        _moveBlend = Mathf.MoveTowards(_moveBlend, move, Time.deltaTime * 5f);
        _bobTime += Time.deltaTime * _bobSpeed * _moveBlend;
        Vector3 bob = new Vector3(Mathf.Sin(_bobTime) * _bobAmount, -Mathf.Abs(Mathf.Cos(_bobTime)) * _bobAmount, 0f) * _moveBlend;

        // 재장전 동작은 손을 아래로 내리므로, 오른손이 화면 속 정한 자리에 머물도록 모델을 따라 옮긴다.
        if (_viewHand != null && _calibrateFrames <= 0)
        {
            if (reload > 0.01f)
            {
                Vector3 hand = _camera.InverseTransformPoint(_viewHand.position);
                _handFollow += (_reloadHandTarget - hand) * Mathf.Clamp01(_handFollowSpeed * Time.deltaTime) * reload;
            }
            else
            {
                _handFollow = Vector3.MoveTowards(_handFollow, Vector3.zero, Time.deltaTime * 1.5f);
            }
        }

        // 구급상자로 치료하는 동안에는 총을 화면 아래로 내린다.
        bool healing = _inventory != null && _inventory.IsHealing;
        _healBlend = Mathf.MoveTowards(_healBlend, healing ? 1f : 0f, Time.deltaTime * 4f);
        float heal = Mathf.SmoothStep(0f, 1f, _healBlend);

        // 수류탄을 들면 총은 화면 아래로 내렸다가 아예 숨긴다. (오른손과 수류탄은 GrenadeArmView가 따로 보여 준다)
        if (_grenade == null)
        {
            _grenade = GetComponent<PlayerGrenade>();
        }

        float grenade = _grenade != null ? Mathf.SmoothStep(0f, 1f, _grenade.EquipBlend) : 0f;

        // AR(3번)이 아닌 무기를 들면 AR도 내렸다가 숨긴다. (도끼, 리볼버는 WeaponArmView가 보여 준다)
        if (_weapons == null)
        {
            _weapons = GetComponent<PlayerWeapons>();
        }

        if (_weapons != null)
        {
            _rifleBlend = Mathf.MoveTowards(_rifleBlend, _weapons.Raise(WeaponSlot.Rifle), Time.deltaTime * 6f);
            grenade = Mathf.Max(grenade, 1f - Mathf.SmoothStep(0f, 1f, _rifleBlend));
        }
        heal = Mathf.Max(heal, grenade * 1.5f);  // 치료 자세보다 더 많이 내린다.
        bool hideGun = grenade > 0.35f;
        if (hideGun != _hiddenForGrenade && _calibrateFrames <= 0)
        {
            _hiddenForGrenade = hideGun;
            SetViewVisible(!hideGun);
        }

        // 쏠 때 총이 뒤로 튀었다가 돌아온다.
        _kick = Mathf.Lerp(_kick, 0f, 1f - Mathf.Exp(-_kickReturn * Time.deltaTime));
        Vector3 kickOffset = new Vector3(0f, _kickUp, -_kickBack) * _kick;
        Vector3 kickTilt = new Vector3(-_kickPitch * _kick, 0f, _kickRoll * 2f * _kick);

        _view.localPosition = _baseOffset + _viewOffset + _reloadOffset * reload + _handFollow + bob + _healOffset * heal + kickOffset;
        _view.localRotation = Quaternion.Euler(_reloadTilt * reload + _healTilt * heal + kickTilt);

        Calibrate();

        // 머리와 다리는 안 보이게 줄인다. (애니메이션이 매 프레임 다시 키우므로 매번)
        Shrink(HumanBodyBones.Head);
        Shrink(HumanBodyBones.LeftUpperLeg);
        Shrink(HumanBodyBones.RightUpperLeg);
    }

    // 1인칭 모델의 머리(눈)가 카메라 바로 뒤에 오도록 모델을 옮긴다.
    // 그러면 몸은 카메라 뒤와 아래에 있고, 앞으로 내민 팔과 총만 화면에 보인다.
    private void Calibrate()
    {
        if (_calibrateFrames <= 0)
        {
            return;
        }

        _calibrateFrames--;
        if (_calibrateFrames > 0)
        {
            return;
        }

        if (_viewHead != null)
        {
            Vector3 eye = _camera.position - _camera.forward * 0.08f;  // 머리 가운데는 눈보다 조금 뒤
            Vector3 shift = _camera.InverseTransformVector(eye - _viewHead.position);
            _baseOffset += shift;
            _view.localPosition += shift;
        }

        FindMuzzle();
        SetViewVisible(true);
    }

    // 총구 찾기: 1인칭 모델의 총(없으면 손) 메시 점들 중 시선 방향으로 가장 앞에 있는 점 = 총 끝.
    // 그 점을 가장 많이 따라 움직이는 뼈에 붙여 두면 쏘는 동작/흔들림에도 총 끝을 따라간다.
    private void FindMuzzle()
    {
        Vector3 eye = _camera.position;
        Vector3 forward = _camera.forward;
        float best = float.MinValue;
        Transform bestBone = null;
        Vector3 bestPoint = Vector3.zero;

        foreach (Renderer renderer in _viewRenderers ?? new Renderer[0])
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                Mesh mesh = skinned.sharedMesh;
                Transform[] bones = skinned.bones;
                if (mesh == null || !mesh.isReadable || bones == null || bones.Length == 0)
                {
                    continue;
                }

                Vector3[] vertices = mesh.vertices;
                BoneWeight[] weights = mesh.boneWeights;
                Matrix4x4[] bindposes = mesh.bindposes;
                if (weights.Length != vertices.Length || bindposes.Length != bones.Length)
                {
                    continue;
                }

                Matrix4x4[] skin = new Matrix4x4[bones.Length];
                bool[] gunBone = new bool[bones.Length];
                bool hasGunBone = false;
                for (int i = 0; i < bones.Length; i++)
                {
                    skin[i] = bones[i] != null ? bones[i].localToWorldMatrix * bindposes[i] : Matrix4x4.identity;
                    gunBone[i] = bones[i] != null && IsGunName(bones[i].name);
                    hasGunBone |= gunBone[i];
                }

                _keptVertices.TryGetValue(skinned, out bool[] kept);
                for (int v = 0; v < vertices.Length; v++)
                {
                    int index = weights[v].boneIndex0;
                    if (index < 0 || index >= bones.Length || bones[index] == null)
                    {
                        continue;
                    }

                    // 총 뼈가 있으면 총의 점만, 없으면 남겨 둔(손과 총) 점만 본다.
                    if (hasGunBone ? !gunBone[index] : (kept != null && !kept[v]))
                    {
                        continue;
                    }

                    Vector3 world = Skin(skin, weights[v], vertices[v]);
                    float reach = Vector3.Dot(world - eye, forward);
                    if (reach > best)
                    {
                        best = reach;
                        bestBone = bones[index];
                        bestPoint = world;
                    }
                }
            }
            else if (renderer != null)
            {
                // 손에 따로 붙은 총 메시
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null && filter.sharedMesh.isReadable)
                {
                    foreach (Vector3 local in filter.sharedMesh.vertices)
                    {
                        Vector3 world = renderer.transform.TransformPoint(local);
                        float reach = Vector3.Dot(world - eye, forward);
                        if (reach > best)
                        {
                            best = reach;
                            bestBone = renderer.transform;
                            bestPoint = world;
                        }
                    }
                }
                else
                {
                    Bounds b = renderer.bounds;
                    float extent = Mathf.Abs(forward.x) * b.extents.x + Mathf.Abs(forward.y) * b.extents.y + Mathf.Abs(forward.z) * b.extents.z;
                    Vector3 world = b.center + forward * extent;
                    float reach = Vector3.Dot(world - eye, forward);
                    if (reach > best)
                    {
                        best = reach;
                        bestBone = renderer.transform;
                        bestPoint = world;
                    }
                }
            }
        }

        if (bestBone != null)
        {
            _muzzleBone = bestBone;
            _muzzleLocal = bestBone.InverseTransformPoint(bestPoint);
            Debug.Log("[FirstPersonBodyView] 총구를 찾았습니다: " + bestBone.name + " (화면 앞으로 " + best.ToString("0.00") + "m)");
        }
        else
        {
            Debug.LogWarning("[FirstPersonBodyView] 총구를 찾지 못해 오른손 앞에서 총구 화염을 냅니다. (군인 모델의 Read/Write를 켜 주세요)");
        }
    }

    private static Vector3 Skin(Matrix4x4[] skin, BoneWeight w, Vector3 v)
    {
        Vector3 result = skin[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0;
        if (w.weight1 > 0f)
        {
            result += skin[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1;
        }

        if (w.weight2 > 0f)
        {
            result += skin[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2;
        }

        if (w.weight3 > 0f)
        {
            result += skin[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
        }

        float total = w.weight0 + w.weight1 + w.weight2 + w.weight3;
        return total > 0.0001f ? result / total : skin[w.boneIndex0].MultiplyPoint3x4(v);
    }

    // 1인칭 모델의 메시에서 손과 총에 붙은 삼각형만 남긴다.
    // (메시의 각 점이 가장 많이 따라 움직이는 뼈를 보고, 그 뼈가 아래팔/손/손가락/총이면 남긴다)
    private void KeepOnlyHands(GameObject copy)
    {
        HashSet<Transform> keep = new HashSet<Transform>();
        if (_viewAnimator.isHuman)
        {
            AddSubtree(keep, _viewAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm));
            AddSubtree(keep, _viewAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm));
        }

        foreach (Transform t in copy.GetComponentsInChildren<Transform>(true))
        {
            if (IsGunName(t.name))
            {
                AddSubtree(keep, t);
            }
        }

        List<Renderer> visible = new List<Renderer>();
        foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                if (keep.Count == 0 || !TrimMesh(skinned, keep, out bool[] keptVertex))
                {
                    visible.Add(renderer);  // 잘라낼 수 없으면 그대로 둔다.
                }
                else if (skinned.sharedMesh.triangles.Length > 0)
                {
                    visible.Add(renderer);
                    _keptVertices[skinned] = keptVertex;
                }
                else
                {
                    renderer.enabled = false;
                }
            }
            else if (keep.Contains(renderer.transform) || IsGunName(renderer.name))
            {
                visible.Add(renderer);  // 손에 붙은 총 같은 따로 된 메시
            }
            else
            {
                renderer.enabled = false;  // 헬멧 같은 따로 된 메시
            }
        }

        _viewRenderers = visible.ToArray();
    }

    private static bool TrimMesh(SkinnedMeshRenderer skinned, HashSet<Transform> keep, out bool[] keepVertex)
    {
        keepVertex = null;
        Mesh source = skinned.sharedMesh;
        if (source == null || !source.isReadable)
        {
            Debug.LogWarning("[FirstPersonBodyView] " + skinned.name + " 메시를 읽을 수 없어 손만 남기지 못했습니다. (모델의 Read/Write를 켜 주세요)");
            return false;
        }

        Transform[] bones = skinned.bones;
        BoneWeight[] weights = source.boneWeights;
        if (bones == null || weights == null || weights.Length != source.vertexCount)
        {
            return false;
        }

        keepVertex = new bool[weights.Length];
        int kept = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            int index = weights[i].boneIndex0;
            keepVertex[i] = index >= 0 && index < bones.Length && bones[index] != null && keep.Contains(bones[index]);
            if (keepVertex[i])
            {
                kept++;
            }
        }

        if (kept == 0)
        {
            return false;
        }

        Mesh trimmed = Instantiate(source);
        trimmed.name = source.name + "_Hands";
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            int[] triangles = source.GetTriangles(sub);
            List<int> result = new List<int>(triangles.Length);
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                // 세 점 중 두 점 이상이 손/총이면 남긴다. (손목 경계가 뜯어지지 않게)
                int count = (keepVertex[triangles[t]] ? 1 : 0) + (keepVertex[triangles[t + 1]] ? 1 : 0) + (keepVertex[triangles[t + 2]] ? 1 : 0);
                if (count >= 2)
                {
                    result.Add(triangles[t]);
                    result.Add(triangles[t + 1]);
                    result.Add(triangles[t + 2]);
                }
            }

            trimmed.SetTriangles(result, sub);
        }

        skinned.sharedMesh = trimmed;
        return true;
    }

    private static void AddSubtree(HashSet<Transform> set, Transform root)
    {
        if (root == null)
        {
            return;
        }

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            set.Add(t);
        }
    }

    private static bool IsGunName(string name)
    {
        string lower = name.ToLowerInvariant();
        return lower.Contains("gun") || lower.Contains("rifle") || lower.Contains("weapon") || lower.Contains("prop")
               || lower.Contains("muzzle") || lower.Contains("magazine");
    }

    private void SetViewVisible(bool visible)
    {
        if (_viewRenderers == null)
        {
            return;
        }

        foreach (Renderer renderer in _viewRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }

    private void Shrink(HumanBodyBones bone)
    {
        if (!_viewAnimator.isHuman)
        {
            return;
        }

        Transform t = _viewAnimator.GetBoneTransform(bone);
        if (t != null)
        {
            t.localScale = Vector3.one * 0.0001f;
        }
    }

    private static bool HasParameter(Animator animator, string name)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == name)
            {
                return true;
            }
        }

        return false;
    }

    private void OnDestroy()
    {
        if (_view != null)
        {
            Destroy(_view.gameObject);
        }
    }
}
