using System.Collections.Generic;
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
    private float _minBoneHeight = 0.05f;  // 발목 뼈는 발바닥보다 조금 위에 있다. (Generic이면 발끝 뼈까지 보므로 거의 0)

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
    private static bool _reported;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _reported = false;
    }

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

        // 뼈 모으기: Humanoid면 발/엉덩이/머리/손, Generic이면 메시에 붙은 모든 뼈
        List<Transform> bones = new List<Transform>();
        if (_animator.isHuman)
        {
            foreach (HumanBodyBones bone in LowBones)
            {
                Transform t = _animator.GetBoneTransform(bone);
                if (t != null)
                {
                    bones.Add(t);
                }
            }
        }

        if (bones.Count == 0)
        {
            _minBoneHeight = 0.01f;
            foreach (SkinnedMeshRenderer skinned in _model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                foreach (Transform t in skinned.bones)
                {
                    if (t != null && !bones.Contains(t))
                    {
                        bones.Add(t);
                    }
                }
            }
        }

        _bones = bones.Count > 0 ? bones.ToArray() : null;
        if (_bones == null)
        {
            Debug.LogWarning("[ZombieModelLock] " + name + ": 모델의 뼈를 찾지 못해 땅 묻힘 보정을 할 수 없습니다.");
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
        float need = Mathf.Max(0f, ground + _minBoneHeight * scale - lowest);

        // 갑자기 튀지 않게 부드럽게 (올릴 때는 빠르게, 내릴 때는 천천히)
        _lift = need > _lift ? Mathf.Lerp(_lift, need, 1f - Mathf.Exp(-30f * Time.deltaTime)) : Mathf.MoveTowards(_lift, need, Time.deltaTime * 1.5f);
        if (_lift > 0.001f)
        {
            _model.position += Vector3.up * _lift;
        }

        // 많이 내려간 동작이 있으면 어떤 동작인지 한 번 알려 준다. (원인 찾기용)
        if (need > 0.12f * scale && !_reported)
        {
            _reported = true;
            string clip = "?";
            AnimatorClipInfo[] infos = _animator.GetCurrentAnimatorClipInfo(0);
            if (infos.Length > 0 && infos[0].clip != null)
            {
                clip = infos[0].clip.name;
            }

            Debug.Log("[ZombieModelLock] '" + clip + "' 동작에서 좀비 몸이 바닥 아래로 " + need.ToString("0.00") + "m 내려가서 올려 주었습니다. "
                      + "Tools > L4D 맵 > 좀비 애니메이션 땅 묻힘 고치기를 누르면 동작 자체가 고쳐집니다.");
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
