using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 게임을 시작할 때 맵의 콜라이더로 NavMesh(좀비가 걸어 다닐 수 있는 길 지도)를 만든다.
// 따로 패키지를 설치하거나 Navigation 창에서 Bake하지 않아도 된다.
// 씬에 이미 구워 둔 NavMesh가 있으면 그것을 그대로 쓴다.
[DisallowMultipleComponent]
public class ZombieNavMesh : MonoBehaviour
{
    private NavMeshData _data;
    private NavMeshDataInstance _instance;

    // 플레이어 쪽 NavMesh가 준비됐는지
    public static bool IsAvailable { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsAvailable = false;
    }

    // 걸을 수 있는 바닥과 장애물(벽, 컨테이너 등)로 NavMesh를 만든다. 몇 프레임에 걸쳐 진행된다.
    public IEnumerator Build(Bounds area, Transform player)
    {
        IsAvailable = false;

        if (HasExistingNavMesh(player))
        {
            Debug.Log("ZombieNavMesh: 씬에 있는 NavMesh를 사용합니다.");
            IsAvailable = true;
            yield break;
        }

        List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(area, Physics.DefaultRaycastLayers, NavMeshCollectGeometry.PhysicsColliders,
            0, new List<NavMeshBuildMarkup>(), sources);

        // 움직이는 것(플레이어, 좀비, 굴러다니는 물체)과 트리거는 길 지도에 넣지 않는다.
        sources.RemoveAll(source => ShouldIgnore(source.component, player));

        if (sources.Count == 0)
        {
            Debug.LogWarning("ZombieNavMesh: 바닥 콜라이더를 찾지 못해 NavMesh를 만들지 못했습니다. 좀비는 직선으로 이동합니다.");
            yield break;
        }

        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0); // Humanoid
        _data = new NavMeshData(settings.agentTypeID);
        _instance = NavMesh.AddNavMeshData(_data);

        float startTime = Time.realtimeSinceStartup;
        AsyncOperation operation = NavMeshBuilder.UpdateNavMeshDataAsync(_data, settings, sources, area);
        while (operation != null && operation.isDone == false)
        {
            yield return null;
        }

        IsAvailable = NavMesh.SamplePosition(player.position, out _, 3f, NavMesh.AllAreas);
        Debug.Log($"ZombieNavMesh: NavMesh 생성 완료 ({(Time.realtimeSinceStartup - startTime) * 1000f:0}ms, 소스 {sources.Count}개)"
                  + (IsAvailable ? string.Empty : " - 플레이어 근처에 길이 없습니다. 맵 범위를 확인하세요."));
    }

    private static bool HasExistingNavMesh(Transform player)
    {
        return player != null && NavMesh.SamplePosition(player.position, out _, 3f, NavMesh.AllAreas);
    }

    private static bool ShouldIgnore(Component component, Transform player)
    {
        if (component == null)
        {
            return false;
        }

        if (player != null && component.transform.root == player.root)
        {
            return true;
        }

        if (component.GetComponentInParent<Zombie>() != null)
        {
            return true;
        }

        Collider collider = component as Collider;
        if (collider != null)
        {
            if (collider.isTrigger)
            {
                return true;
            }

            if (collider.attachedRigidbody != null && collider.attachedRigidbody.isKinematic == false)
            {
                return true;
            }
        }

        return false;
    }

    private void OnDestroy()
    {
        // 씬을 다시 시작하면 새로 만들기 때문에, 이전 NavMesh는 지운다.
        if (_instance.valid)
        {
            _instance.Remove();
        }

        if (_data != null)
        {
            Destroy(_data);
        }

        IsAvailable = false;
    }
}
