using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class SupplySpawner : MonoBehaviour
{
    [Header("경로 중간 보급")]
    [SerializeField] private int _caches = 5;
    [SerializeField, Range(0f, 1f)] private float _medkitChance = 0.45f;
    [SerializeField, Range(0f, 1f)] private float _grenadeChance = 0.55f;
    [SerializeField] private int _routeAmmoPiles = 2;
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
        if (map != null) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, map.gameObject.scene);
        SupplySpawner spawner = go.AddComponent<SupplySpawner>();
        spawner._map = map;
        spawner._radio = radio;
        spawner._root = go.transform;
        spawner.SpawnAll();
        return spawner;
    }

    private void SpawnAll()
    {
        if (_map == null) return;
        int count = WeaponSupplyStations.Spawn(_map, _root, _radio);
        count += SpawnSafeRoom("AmmoCrate", "MedKitBox", true);
        count += SpawnSafeRoom("MidAmmoCrate", "MidMedKitBox", false);
        count += SpawnRoute();
        count += SpawnFinale();
        Debug.Log("[SupplySpawner] 무기/보급품 " + count + "개 배치 완료.");
    }

    private int SpawnSafeRoom(string crateName, string medName, bool fallback)
    {
        Transform crate = FindChild(_map.transform, crateName);
        Transform medBox = FindChild(_map.transform, medName);
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
        if (!fallback || _map.StartSafeRoomArea == null) return 0;
        Vector3 center = _map.StartSafeRoomArea.bounds.center;
        int count = 0;
        count += TryPlaceNear(PickupType.Ammo, center + new Vector3(-2f, 0f, -3f), 1.5f) ? 1 : 0;
        count += TryPlaceNear(PickupType.Medkit, center + new Vector3(2f, 0f, -3f), 1.5f) ? 1 : 0;
        count += TryPlaceNear(PickupType.Grenade, center + new Vector3(2.5f, 0f, -2f), 1.5f) ? 1 : 0;
        return count;
    }

    private int SpawnRoute()
    {
        Transform[] route = _map.RoutePoints;
        if (route == null || route.Length < 4) return 0;
        int first = 1;
        int last = route.Length - 2;
        int count = 0;
        int caches = Mathf.Max(_caches, Mathf.RoundToInt(_map.RouteLength / 55f));
        int ammoPiles = Mathf.Max(_routeAmmoPiles, Mathf.RoundToInt(caches / 3.5f));
        List<int> ammoSlots = new List<int>();
        for (int i = 0; i < ammoPiles; i++) ammoSlots.Add(Mathf.RoundToInt((i + 1f) * caches / (ammoPiles + 1f)) - 1);
        for (int c = 0; c < caches; c++)
        {
            float progress = (c + 0.5f + Random.Range(-0.3f, 0.3f)) / caches;
            float routeT = Mathf.Lerp(first, last, Mathf.Clamp01(progress));
            int index = Mathf.Clamp(Mathf.FloorToInt(routeT), first, last);
            if (route[index] == null || route[Mathf.Min(index + 1, route.Length - 1)] == null) continue;
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
                if (Random.value < 0.5f) medkit = true; else grenade = true;
            }
            if (ammo && TryPlaceNear(PickupType.Ammo, spot, 2.5f)) count++;
            if (medkit && TryPlaceNear(PickupType.Medkit, spot + direction * 1.2f, 2.5f)) count++;
            if (grenade && TryPlaceNear(PickupType.Grenade, spot - direction * 1.2f, 2.5f)) count++;
        }
        return count;
    }

    private int SpawnFinale()
    {
        Transform[] route = _map.RoutePoints;
        if (route == null || route.Length < 2) return 0;
        Vector3 plaza = route[route.Length - 1].position;
        Vector3 back = (route[route.Length - 2].position - plaza).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, back);
        Vector3 cache = plaza + back * 3f + side * 5f;
        int count = TryPlaceNear(PickupType.Ammo, cache, 3f) ? 1 : 0;
        for (int i = 0; i < _finaleMedkits; i++) count += TryPlaceNear(PickupType.Medkit, cache + back * 1.3f + side * (i * 0.8f), 3f) ? 1 : 0;
        for (int i = 0; i < _finaleGrenades; i++) count += TryPlaceNear(PickupType.Grenade, cache - back * 1.3f + side * (i * 0.8f), 3f) ? 1 : 0;
        count += TryPlaceNear(PickupType.Ammo, plaza + back * 3f - side * 5f, 3f) ? 1 : 0;
        return count;
    }

    private bool TryPlaceNear(PickupType type, Vector3 desired, float radius)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 jitter = attempt == 0 ? Vector2.zero : Random.insideUnitCircle * radius;
            Vector3 candidate = desired + new Vector3(jitter.x, 0f, jitter.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas) || !IsFree(hit.position)) continue;
            Place(type, hit.position, Random.Range(0f, 360f));
            return true;
        }
        return false;
    }

    private bool IsFree(Vector3 position)
    {
        if (_radio != null && Vector3.Distance(Flat(_radio.position), Flat(position)) < 4f) return false;
        foreach (Vector3 other in _placed)
        {
            if (Vector3.Distance(Flat(other), Flat(position)) < 0.7f) return false;
        }
        foreach (Pickup pickup in Pickup.All)
        {
            if (pickup != null && Mathf.Abs(pickup.transform.position.y - position.y) < 1.5f
                && Vector3.Distance(Flat(pickup.transform.position), Flat(position)) < 0.7f)
            {
                return false;
            }
        }
        return !Physics.CheckCapsule(position + Vector3.up * 0.35f, position + Vector3.up * 1.2f, 0.3f, ~0, QueryTriggerInteraction.Ignore);
    }

    private void Place(PickupType type, Vector3 position, float yaw)
    {
        Pickup.Create(type, position, yaw, _root);
        _placed.Add(position);
    }

    private static Vector3 Bottom(Transform target)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null) return target.position;
        Bounds bounds = renderer.bounds;
        return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    }

    private static void Hide(Transform target)
    {
        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
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
            if (child.name == name) return child;
        }
        return null;
    }
}
