using UnityEngine;

// 기존 Resources 모델을 우선 사용하고, 없는 무기는 간단한 입체 모델로 만든다.
public static class WeaponModels
{
    public static Transform Create(string name)
    {
        GameObject prefab = Resources.Load<GameObject>("Weapons/" + name);
        GameObject instance;
        if (prefab != null) instance = Object.Instantiate(prefab);
        else if (name == "Axe") instance = FallbackAxe();
        else if (name == "Revolver") instance = FallbackRevolver();
        else instance = FallbackLongGun(name);
        instance.name = "FP_" + name;
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            Object.Destroy(collider);
        }
        foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
            Object.Destroy(body);
        }
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }
        instance.SetActive(true);
        return instance.transform;
    }

    public static string ModelName(WeaponSlot slot)
    {
        switch (slot)
        {
            case WeaponSlot.Axe: return "Axe";
            case WeaponSlot.Revolver: return "Revolver";
            case WeaponSlot.Smg: return "SMG";
            case WeaponSlot.Shotgun: return "Shotgun";
            default: return "Rifle";
        }
    }

    public static Transform CreateDisplay(WeaponSlot slot, Transform parent)
    {
        Transform root = new GameObject("WorldWeapon_" + slot).transform;
        root.SetParent(parent, false);
        Transform model = Create(ModelName(slot));
        model.SetParent(root, false);
        model.localRotation = Quaternion.Euler(0f, 0f, 90f);
        foreach (MonoBehaviour script in model.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
        foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        model.gameObject.SetActive(true);
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return root;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        float desired = slot == WeaponSlot.Revolver ? 0.4f : slot == WeaponSlot.Axe ? 0.75f : 0.9f;
        model.localScale *= desired / Mathf.Max(0.01f, Mathf.Max(bounds.size.x, bounds.size.z));
        bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        model.position += root.position + Vector3.up * 0.035f - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        return root;
    }

    public static Transform Find(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name) return child;
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

    private static GameObject FallbackLongGun(string kind)
    {
        GameObject root = new GameObject(kind);
        Color metal = new Color(0.13f, 0.15f, 0.17f);
        Color stock = kind == "Shotgun" ? new Color(0.34f, 0.2f, 0.11f) : new Color(0.2f, 0.25f, 0.22f);
        bool shotgun = kind == "Shotgun";
        bool smg = kind == "SMG";
        float receiverLength = smg ? 0.23f : 0.33f;
        float muzzle = smg ? 0.38f : shotgun ? 0.69f : 0.57f;
        Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.065f, 0.11f), new Vector3(0.065f, 0.09f, receiverLength), metal);
        Part(root.transform, PrimitiveType.Cube, new Vector3(0f, -0.035f, 0f), new Vector3(0.045f, 0.14f, 0.07f), stock, new Vector3(-15f, 0f, 0f));
        Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.045f, -0.19f), new Vector3(0.06f, 0.11f, smg ? 0.15f : 0.25f), stock);
        float barrelStart = smg ? 0.2f : 0.27f;
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.085f, (barrelStart + muzzle) * 0.5f),
            new Vector3(0.027f, (muzzle - barrelStart) * 0.5f, 0.027f), metal, new Vector3(90f, 0f, 0f));
        if (shotgun)
        {
            Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.025f, 0.39f), new Vector3(0.025f, 0.2f, 0.025f), metal, new Vector3(90f, 0f, 0f));
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.035f, 0.36f), new Vector3(0.08f, 0.085f, 0.16f), stock);
        }
        else
        {
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, -0.075f, 0.12f), new Vector3(0.045f, smg ? 0.2f : 0.17f, 0.075f), metal, new Vector3(-12f, 0f, 0f));
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.05f, smg ? 0.23f : 0.32f), new Vector3(0.075f, 0.08f, smg ? 0.1f : 0.18f), stock);
        }
        Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.125f, muzzle - 0.045f), new Vector3(0.02f, 0.04f, 0.02f), metal);
        Marker(root.transform, "Muzzle", new Vector3(0f, 0.085f, muzzle));
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
