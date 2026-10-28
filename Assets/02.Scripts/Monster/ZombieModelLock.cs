using UnityEngine;

// 좀비가 맞을 때(Hit 동작) 땅에 파묻히는 것을 막는다. Zombie가 Start에서 붙인다.
//
// 원인: Mixamo 동작(Zombie_Reaction_Hit 등)은 몸의 높이를 '무게중심' 기준으로 가져와서,
//       몸을 숙이는 동작에서 몸 전체가 아래로 내려가 발이 땅속에 들어간다.
//       (근본 해결: Tools > L4D 맵 > 좀비 애니메이션 땅 묻힘 고치기 → 동작의 Root Y를 Bake Into Pose + Original로)
//
// 이 스크립트는 그 위에 한 번 더 막아 준다.
//  1. 애니메이터의 Root Motion을 끄고, 모델이 처음 자리에서 미끄러지지 않게 위치를 고정한다.
//  2. 매 프레임 동작이 끝난 뒤, 몸에서 가장 낮은 뼈(발, 엉덩이, 머리, 손)가 바닥보다 아래로 내려갔으면
//     그만큼 모델을 위로 올린다. (쓰러진 시체도 바닥 위에 눕는다)
[DefaultExecutionOrder(900)]
public class ZombieModelLock : MonoBehaviour
{
    private const float MinBoneHeight = 0.05f;  // 발목 뼈는 발바닥보다 조금 위에 있다.

    private static readonly HumanBodyBones[] LowBones =
    {
        HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.Hips, HumanBodyBones.Head,
        HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.Spine, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
    };

    private Animator _animator;
    private Transform _model;
    private Vector3 _baseLocalPosition;
    private Transform[] _bones;
    private float _lift;
    private float _ground;
    private float _groundTimer;

    private void Start()
    {
        _animator = GetComponentInChildren<Animator>();
        if (_animator == null || _animator.transform == transform)
        {
            enabled = false;  // 모델이 따로 없으면 할 일이 없다.
            return;
        }

        _animator.applyRootMotion = false;
        _model = _animator.transform;
        _baseLocalPosition = _model.localPosition;

        if (_animator.isHuman)
        {
            _bones = new Transform[LowBones.Length];
            for (int i = 0; i < LowBones.Length; i++)
            {
                _bones[i] = _animator.GetBoneTransform(LowBones[i]);
            }
        }
    }

    private void LateUpdate()
    {
        if (_model == null)
        {
            return;
        }

        // 1. 처음 자리로 (Root Motion으로 밀려난 것 되돌리기)
        _model.localPosition = _baseLocalPosition;
        if (_bones == null)
        {
            return;
        }

        // 2. 가장 낮은 뼈가 바닥 아래면 그만큼 올린다.
        float lowest = float.MaxValue;
        foreach (Transform bone in _bones)
        {
            if (bone != null && bone.position.y < lowest)
            {
                lowest = bone.position.y;
            }
        }

        // 바닥 높이는 0.15초마다 잰다. (좀비가 많아도 가볍게)
        _groundTimer -= Time.deltaTime;
        if (_groundTimer <= 0f)
        {
            _groundTimer = 0.15f;
            _ground = GroundHeight();
        }

        float ground = _ground;
        float scale = transform.lossyScale.y;
        float need = Mathf.Max(0f, ground + MinBoneHeight * scale - lowest);

        // 갑자기 튀지 않게 부드럽게 (올릴 때는 빠르게, 내릴 때는 천천히)
        _lift = need > _lift ? Mathf.Lerp(_lift, need, 1f - Mathf.Exp(-30f * Time.deltaTime)) : Mathf.MoveTowards(_lift, need, Time.deltaTime * 1.5f);
        if (_lift > 0.001f)
        {
            _model.position += Vector3.up * _lift;
        }
    }

    // 좀비 발밑 바닥 높이 (좀비 자신은 무시)
    private float GroundHeight()
    {
        Vector3 from = transform.position + Vector3.up * 1f;
        RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
        float best = transform.position.y;
        float bestDistance = float.MaxValue;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<Zombie>() != null)
            {
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                best = hit.point.y;
            }
        }

        return best;
    }
}
