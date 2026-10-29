using UnityEngine;

// 1인칭 무기 모델 불러오기
//  - Resources/Weapons/<이름> 프리팹 (Tools > L4D 맵 > 도끼·리볼버 모델 설정이 만든다)
//    손잡이(Grip)가 원점, +Z가 앞(총구/도끼날), +Y가 위(도끼 머리)
//  - 없으면 기본 도형으로 간단한 모양을 만든다. (그래도 게임은 된다)
public static class WeaponModels
{
    public static Transform Create(string name)
    {
        GameObject prefab = Resources.Load<GameObject>("Weapons/" + name);
        GameObject instance;
        if (prefab != null)
        {
            instance = Object.Instantiate(prefab);
        }
        else
        {
            Debug.LogWarning("[WeaponModels] Resources/Weapons/" + name + " 이 없어 간단한 모양으로 대신합니다. Tools > L4D 맵 > 도끼·리볼버 모델 설정을 눌러 주세요.");
            instance = name == "Axe" ? FallbackAxe() : FallbackRevolver();
        }

        instance.name = "FP_" + name;
        // 1인칭 모델: 충돌, 물리, 그림자 없음
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
        {
            Object.Destroy(collider);
        }

        foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true))
        {
            Object.Destroy(body);
        }

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        return instance.transform;
    }

    public static Transform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
            {
                return t;
            }
        }

        return null;
    }

    private static GameObject FallbackAxe()
    {
        GameObject root = new GameObject("Axe");
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.22f, 0f), new Vector3(0.035f, 0.32f, 0.035f), new Color(0.35f, 0.22f, 0.12f));
        Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.5f, 0.06f), new Vector3(0.02f, 0.12f, 0.16f), new Color(0.55f, 0.56f, 0.58f));
        Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.5f, -0.04f), new Vector3(0.035f, 0.06f, 0.05f), new Color(0.4f, 0.4f, 0.42f));
        Marker(root.transform, "Blade", new Vector3(0f, 0.5f, 0.14f));
        return root;
    }

    private static GameObject FallbackRevolver()
    {
        GameObject root = new GameObject("Revolver");
        Color metal = new Color(0.18f, 0.18f, 0.2f);
        Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.035f, 0.07f), new Vector3(0.03f, 0.04f, 0.1f), metal);
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.045f, 0.19f), new Vector3(0.018f, 0.08f, 0.018f), metal, new Vector3(90f, 0f, 0f));
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.035f, 0.09f), new Vector3(0.045f, 0.022f, 0.045f), metal, new Vector3(90f, 0f, 0f));
        Part(root.transform, PrimitiveType.Cube, new Vector3(0f, -0.03f, 0f), new Vector3(0.028f, 0.1f, 0.04f), new Color(0.3f, 0.18f, 0.1f), new Vector3(-15f, 0f, 0f));
        Marker(root.transform, "Muzzle", new Vector3(0f, 0.045f, 0.27f));
        return root;
    }

    private static void Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Color color, Vector3 euler = default)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(euler);
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = SpecialEffects.GetMaterial(color, 0f);
    }

    private static void Marker(Transform parent, string name, Vector3 position)
    {
        Transform marker = new GameObject(name).transform;
        marker.SetParent(parent, false);
        marker.localPosition = position;
    }
}
