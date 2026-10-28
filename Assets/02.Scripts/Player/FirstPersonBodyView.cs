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

    [Header("달릴 때 흔들림")]
    [SerializeField] private float _bobAmount = 0.03f;
    [SerializeField] private float _bobSpeed = 11f;

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
    private int _calibrateFrames = 3;   // 몇 프레임 뒤 (서 있는 자세가 잡힌 뒤) 머리 위치를 재서 눈에 맞춘다.

    // 총구 화염을 붙일 1인칭 모델의 오른손
    public Transform ViewHand => _viewHand;

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

        // 위치를 맞추기 전에는 보이지 않게 한다.
        _viewRenderers = copy.GetComponentsInChildren<Renderer>(true);
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

        _view.localPosition = _baseOffset + _viewOffset + _reloadOffset * reload + _handFollow + bob;
        _view.localRotation = Quaternion.Euler(_reloadTilt * reload);

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

        SetViewVisible(true);
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
