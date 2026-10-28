using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 보급품 배치 (L4D의 AI 디렉터처럼 할 때마다 조금씩 다르게 놓는다)
//  - 출발 안전가옥 탁자 위: 탄약 더미, 구급상자, 수류탄 (항상)
//  - 진행 경로 중간중간: 길가에 보급품 묶음. 무엇이 나올지는 매번 랜덤
//  - 피날레 광장: 탄약 더미 + 구급상자 + 수류탄 (오래 버티기 위해)
// AIDirector가 시작할 때 붙인다. 맵 정보는 CityMapInfo에서 읽는다.
public class SupplySpawner : MonoBehaviour
{
    [Header("경로 중간 보급")]
    [SerializeField] private int _caches = 5;                       // 길가 보급품 묶음 수
    [SerializeField, Range(0f, 1f)] private float _medkitChance = 0.45f;
    [SerializeField, Range(0f, 1f)] private float _grenadeChance = 0.55f;
    [SerializeField] private int _routeAmmoPiles = 2;               // 경로 중간 탄약 더미 수

    [Header("피날레 광장")]
    [SerializeField] private int _finaleMedkits = 2;
    [SerializeField] private int _finaleGrenades = 2;

    private CityMapInfo _map;
    private Transform _root;
    private Transform _radio;
    private readonly List<Vector3> _placed = new List<Vector3>();

    public static SupplySpawner Create(CityMapInfo map, Transform radio)
    {
        GameObject go = new GameObject("Supplies");
        SupplySpawner spawner = go.AddComponent<SupplySpawner>();
        spawner._map = map;
        spawner._radio = radio;
        spawner._root = go.transform;
        spawner.SpawnAll();
        return spawner;
    }

    private void SpawnAll()
    {
        if (_map == null)
        {
            return;
        }

        int count = 0;
        count += SpawnSafeRoom();
        count += SpawnRoute();
        count += SpawnFinale();
        Debug.Log("[SupplySpawner] 보급품 " + count + "개를 놓았습니다.");
    }

    // ---------- 출발 안전가옥 ----------
    private int SpawnSafeRoom()
    {
        // 맵을 만들 때 탁자 위에 놓았던 장식용 상자 자리에 진짜 보급품을 놓는다.
        Transform crate = FindChild(_map.transform, "AmmoCrate");
        Transform medBox = FindChild(_map.transform, "MedKitBox");
        if (crate != null && medBox != null)
        {
            Vector3 ammoPos = Bottom(crate);
            Vector3 medPos = Bottom(medBox);
            Hide(crate);
            Hide(medBox);
            Vector3 along = (medPos - ammoPos).normalized;
            Place(PickupType.Ammo, ammoPos, 0f);
            Place(PickupType.Medkit, medPos - along * 0.12f, 0f);
            Place(PickupType.Grenade, medPos + along * 0.42f, 0f);
            return 3;
        }

        // 장식 상자가 없으면 안전가옥 바닥에
        BoxCollider area = _map.StartSafeRoomArea;
        if (area == null)
        {
            return 0;
        }

        Vector3 center = area.bounds.center;
        int count = 0;
        count += TryPlaceNear(PickupType.Ammo, center + new Vector3(-2f, 0f, -3f), 1.5f) ? 1 : 0;
        count += TryPlaceNear(PickupType.Medkit, center + new Vector3(2f, 0f, -3f), 1.5f) ? 1 : 0;
        count += TryPlaceNear(PickupType.Grenade, center + new Vector3(2.5f, 0f, -2f), 1.5f) ? 1 : 0;
        return count;
    }

    // ---------- 진행 경로 중간 ----------
    private int SpawnRoute()
    {
        Transform[] route = _map.RoutePoints;
        if (route == null || route.Length < 4)
        {
            return 0;
        }

        // 출발(0)과 피날레(마지막)는 빼고, 경로를 고르게 나눠 묶음 자리를 정한다.
        int first = 1;
        int last = route.Length - 2;
        int count = 0;
        List<int> ammoSlots = new List<int>();
        for (int i = 0; i < _routeAmmoPiles; i++)
        {
            ammoSlots.Add(Mathf.RoundToInt((i + 1f) * _caches / (_routeAmmoPiles + 1f)) - 1);
        }

        for (int c = 0; c < _caches; c++)
        {
            // 0~1 사이 진행도에 랜덤을 조금 섞는다.
            float progress = (c + 0.5f + Random.Range(-0.3f, 0.3f)) / _caches;
            float routeT = Mathf.Lerp(first, last, Mathf.Clamp01(progress));
            int index = Mathf.Clamp(Mathf.FloorToInt(routeT), first, last);
            Vector3 a = route[index].position;
            Vector3 b = route[Mathf.Min(index + 1, route.Length - 1)].position;
            Vector3 point = Vector3.Lerp(a, b, routeT - index);
            Vector3 direction = (b - a).sqrMagnitude > 0.01f ? (b - a).normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, direction) * (Random.value < 0.5f ? -1f : 1f);
            Vector3 spot = point + side * Random.Range(2.5f, 4.5f);

            bool ammo = ammoSlots.Contains(c);
            bool medkit = Random.value < _medkitChance;
            bool grenade = Random.value < _grenadeChance;
            if (!ammo && !medkit && !grenade)
            {
                // 빈 묶음은 없게: 둘 중 하나
                if (Random.value < 0.5f) medkit = true; else grenade = true;
            }

            if (ammo && TryPlaceNear(PickupType.Ammo, spot, 2.5f)) count++;
            if (medkit && TryPlaceNear(PickupType.Medkit, spot + direction * 1.2f, 2.5f)) count++;
            if (grenade && TryPlaceNear(PickupType.Grenade, spot - direction * 1.2f, 2.5f)) count++;
        }

        return count;
    }

    // ---------- 피날레 광장 ----------
    private int SpawnFinale()
    {
        Transform[] route = _map.RoutePoints;
        if (route == null || route.Length < 2)
        {
            return 0;
        }

        // 광장 입구 쪽(무전기 앞)에 모아 둔다.
        Vector3 plaza = route[route.Length - 1].position;
        Vector3 back = (route[route.Length - 2].position - plaza).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, back);
        int count = 0;
        Vector3 cache = plaza + back * 3f + side * 5f;
        count += TryPlaceNear(PickupType.Ammo, cache, 3f) ? 1 : 0;
        for (int i = 0; i < _finaleMedkits; i++)
        {
            count += TryPlaceNear(PickupType.Medkit, cache + back * 1.3f + side * (i * 0.8f), 3f) ? 1 : 0;
        }

        for (int i = 0; i < _finaleGrenades; i++)
        {
            count += TryPlaceNear(PickupType.Grenade, cache - back * 1.3f + side * (i * 0.8f), 3f) ? 1 : 0;
        }

        // 반대편에도 탄약 더미 하나
        count += TryPlaceNear(PickupType.Ammo, plaza + back * 3f - side * 5f, 3f) ? 1 : 0;
        return count;
    }

    // ===================== 놓기 =====================

    // 원하는 자리 근처에서 걸어갈 수 있고(NavMesh), 벽/차에 묻히지 않고, 다른 보급품과 겹치지 않는 곳을 찾아 놓는다.
    private bool TryPlaceNear(PickupType type, Vector3 desired, float radius)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 jitter = attempt == 0 ? Vector2.zero : Random.insideUnitCircle * radius;
            Vector3 candidate = desired + new Vector3(jitter.x, 0f, jitter.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                continue;
            }

            Vector3 position = hit.position;
            if (!IsFree(position))
            {
                continue;
            }

            Place(type, position, Random.Range(0f, 360f));
            return true;
        }

        return false;
    }

    private bool IsFree(Vector3 position)
    {
        // 무전기 바로 옆은 피한다. (E 키가 겹치지 않게)
        if (_radio != null && Vector3.Distance(Flat(_radio.position), Flat(position)) < 4f)
        {
            return false;
        }

        foreach (Vector3 other in _placed)
        {
            if (Vector3.Distance(Flat(other), Flat(position)) < 0.7f)
            {
                return false;
            }
        }

        // 바닥 위 0.3~1.2m 사이에 벽이나 차가 있으면 안 된다.
        return !Physics.CheckCapsule(position + Vector3.up * 0.35f, position + Vector3.up * 1.2f, 0.3f, ~0, QueryTriggerInteraction.Ignore);
    }

    private void Place(PickupType type, Vector3 position, float yaw)
    {
        Pickup.Create(type, position, yaw, _root);
        _placed.Add(position);
    }

    // ===================== 도우미 =====================

    private static Vector3 Bottom(Transform t)
    {
        Renderer renderer = t.GetComponent<Renderer>();
        if (renderer == null)
        {
            return t.position;
        }

        Bounds b = renderer.bounds;
        return new Vector3(b.center.x, b.min.y, b.center.z);
    }

    private static void Hide(Transform t)
    {
        foreach (Renderer renderer in t.GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = false;
        }
    }

    private static Vector3 Flat(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }
}
