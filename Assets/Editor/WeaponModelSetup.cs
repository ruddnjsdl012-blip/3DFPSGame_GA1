using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// 도끼와 리볼버 모델을 1인칭 무기로 쓸 수 있게 준비한다.
// 메뉴: Tools > L4D 맵 > 도끼·리볼버 모델 설정 (처음 한 번은 자동으로 물어본다)
//
//  1. 원래 재질(빌트인 Standard)은 URP에서 분홍색이 되므로, 같은 텍스처로 URP Lit 재질을 새로 만든다.
//     (원래 에셋은 건드리지 않는다. 새 재질은 Assets/10.Weapons/Materials)
//  2. 모델의 메시 점들을 보고 방향을 자동으로 맞춘다.
//       리볼버: 가장 긴 쪽 = 총신, 끝이 가는 쪽 = 총구(+Z), 손잡이가 내려간 쪽 = 아래(-Y)
//       도끼  : 가장 긴 쪽 = 자루, 넓은 쪽 = 머리(+Y), 날이 더 튀어나온 쪽 = 날(+Z)
//     손잡이(Grip)가 원점이 되게 옮기고, 총구(Muzzle) / 도끼날(Blade) 표시를 붙인다.
//  3. Assets/10.Weapons/Resources/Weapons/Axe.prefab, Revolver.prefab 으로 저장 → 게임에서 이걸 불러 쓴다.
// 모양이나 방향이 어색하면 이 프리팹을 열어 Model의 회전/위치를 고치면 된다. (메뉴를 다시 누르면 새로 만든다)
[InitializeOnLoad]
public static class WeaponModelSetup
{
    private const string Folder = "Assets/10.Weapons";
    private const string MaterialFolder = Folder + "/Materials";
    private const string PrefabFolder = Folder + "/Resources/Weapons";

    private const string AxePrefabGuid = "e048a401410aa9f4791cdd97e15735ca";       // Assets/Axe/axe.prefab
    private const string RevolverPrefabGuid = "20d6fada1e8caa1488140d8433a67d6b";  // Assets/Reichsrevolver_M1879/Prefabs/ReichsrevolverM1879.prefab

    static WeaponModelSetup()
    {
        EditorApplication.delayCall += AskOnce;
    }

    private static void AskOnce()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string key = "WeaponModelSetup.Asked." + Application.dataPath;
        if (EditorPrefs.GetBool(key, false))
        {
            return;
        }

        EditorPrefs.SetBool(key, true);
        if (EditorUtility.DisplayDialog("도끼·리볼버", "도끼와 리볼버 모델을 1인칭 무기로 준비할까요? (URP 재질, 방향, 손잡이 위치)\n나중에 Tools > L4D 맵 > 도끼·리볼버 모델 설정으로도 할 수 있습니다.", "지금 설정", "나중에"))
        {
            Setup();
        }
    }

    [MenuItem("Tools/L4D 맵/도끼·리볼버 모델 설정", priority = 22)]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("도끼·리볼버", "플레이를 멈춘 뒤에 실행해 주세요.", "확인");
            return;
        }

        EnsureFolder(MaterialFolder);
        EnsureFolder(PrefabFolder);
        List<string> log = new List<string>();
        log.Add(Build("Axe", FindPrefab(AxePrefabGuid, "axe"), true));
        log.Add(Build("Revolver", FindPrefab(RevolverPrefabGuid, "ReichsrevolverM1879"), false));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string body = string.Join("\n", log) + "\n\n▶ Play 후 1번 도끼, 2번 리볼버, 3번 AR, 4번 수류탄";
        Debug.Log("[도끼·리볼버] " + body);
        EditorUtility.DisplayDialog("도끼·리볼버", body, "확인");
    }

    private static string FindPrefab(string guid, string name)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (!string.IsNullOrEmpty(path) && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            return path;
        }

        return AssetDatabase.FindAssets(name + " t:Prefab")
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => !p.StartsWith(Folder) && Path.GetFileNameWithoutExtension(p).ToLowerInvariant() == name.ToLowerInvariant());
    }

    // ===================== 무기 하나 만들기 =====================

    private static string Build(string name, string sourcePath, bool isAxe)
    {
        if (string.IsNullOrEmpty(sourcePath))
        {
            return "⚠ " + name + ": 원본 프리팹을 찾지 못했습니다. (게임에서는 간단한 모양으로 대신합니다)";
        }

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        MakeMeshesReadable(source);

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.name = "Model";
        model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        model.transform.localScale = Vector3.one;

        // 필요 없는 것 지우기: 스크립트, 충돌, (도끼는) 애니메이터
        foreach (MonoBehaviour script in model.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Object.DestroyImmediate(script);
        }

        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(collider);
        }

        if (isAxe)
        {
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
            {
                Object.DestroyImmediate(animator);
            }
        }
        else
        {
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        int converted = ConvertMaterials(model, isAxe ? 0.15f : 0.75f, isAxe ? 0.35f : 0.55f);

        // 방향 맞추기
        List<Vector3> points = CollectPoints(model);
        if (points.Count < 10)
        {
            Object.DestroyImmediate(model);
            return "⚠ " + name + ": 메시를 읽지 못했습니다.";
        }

        Quaternion facing;
        Vector3 grip;
        Vector3 tip;
        string detail = isAxe ? AnalyzeAxe(points, out facing, out grip, out tip) : AnalyzeRevolver(points, out facing, out grip, out tip);

        GameObject root = new GameObject(name);
        Quaternion inverse = Quaternion.Inverse(facing);
        model.transform.SetParent(root.transform, false);
        model.transform.localRotation = inverse;
        model.transform.localPosition = -(inverse * grip);

        Transform marker = new GameObject(isAxe ? "Blade" : "Muzzle").transform;
        marker.SetParent(root.transform, false);
        marker.localPosition = inverse * (tip - grip);
        Transform gripMarker = new GameObject("Grip").transform;
        gripMarker.SetParent(root.transform, false);

        string path = PrefabFolder + "/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return "✔ " + name + ": " + Path.GetFileName(sourcePath) + " → " + path + " (재질 " + converted + "개 URP로, " + detail + ")";
    }

    // ===================== 방향 분석 =====================

    private static List<Vector3> CollectPoints(GameObject model)
    {
        List<Vector3> points = new List<Vector3>();
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            Renderer renderer = filter.GetComponent<Renderer>();
            if (filter.sharedMesh == null || renderer == null || !renderer.enabled)
            {
                continue;
            }

            Matrix4x4 matrix = filter.transform.localToWorldMatrix;
            foreach (Vector3 v in filter.sharedMesh.vertices)
            {
                points.Add(matrix.MultiplyPoint3x4(v));
            }
        }

        foreach (SkinnedMeshRenderer skinned in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh baked = new Mesh();
            skinned.BakeMesh(baked);
            Matrix4x4 matrix = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
            foreach (Vector3 v in baked.vertices)
            {
                points.Add(matrix.MultiplyPoint3x4(v));
            }

            Object.DestroyImmediate(baked);
        }

        return points;
    }

    // 점들의 상자에서 길이 순서대로 축 세 개 (가장 긴 축, 중간, 가장 짧은 축)
    private static void Axes(List<Vector3> points, out Vector3 longAxis, out Vector3 midAxis, out Vector3 shortAxis)
    {
        Vector3 min = points[0], max = points[0];
        foreach (Vector3 p in points)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }

        Vector3 size = max - min;
        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
        int[] order = new[] { 0, 1, 2 }.OrderByDescending(i => size[i]).ToArray();
        longAxis = axes[order[0]];
        midAxis = axes[order[1]];
        shortAxis = axes[order[2]];
    }

    private static string AnalyzeRevolver(List<Vector3> points, out Quaternion facing, out Vector3 grip, out Vector3 muzzle)
    {
        Axes(points, out Vector3 l, out Vector3 h, out Vector3 w);
        float lMin = points.Min(p => Vector3.Dot(p, l)), lMax = points.Max(p => Vector3.Dot(p, l));
        float length = lMax - lMin;

        // 양 끝 12% 중 위아래 폭이 좁은 쪽이 총신 끝 (총구)
        List<Vector3> endA = points.Where(p => Vector3.Dot(p, l) < lMin + length * 0.12f).ToList();
        List<Vector3> endB = points.Where(p => Vector3.Dot(p, l) > lMax - length * 0.12f).ToList();
        float spreadA = Spread(endA, h), spreadB = Spread(endB, h);
        bool muzzleAtMax = spreadB < spreadA;
        Vector3 forward = muzzleAtMax ? l : -l;
        List<Vector3> muzzleEnd = muzzleAtMax ? endB : endA;

        float barrelH = muzzleEnd.Average(p => Vector3.Dot(p, h));
        // 뒤쪽 45%에서 총신 높이보다 더 멀리 내려간 쪽이 손잡이 (아래)
        List<Vector3> rear = points.Where(p => muzzleAtMax ? Vector3.Dot(p, l) < lMin + length * 0.45f : Vector3.Dot(p, l) > lMax - length * 0.45f).ToList();
        float rearMin = rear.Min(p => Vector3.Dot(p, h)), rearMax = rear.Max(p => Vector3.Dot(p, h));
        bool gripBelowMin = barrelH - rearMin > rearMax - barrelH;
        Vector3 down = gripBelowMin ? -h : h;
        float extreme = gripBelowMin ? rearMin : rearMax;

        // 손잡이 가운데 (총신 아래 30~80% 깊이)
        List<Vector3> gripPoints = rear.Where(p =>
        {
            float depth = Mathf.Abs(Vector3.Dot(p, h) - barrelH) / Mathf.Max(0.0001f, Mathf.Abs(extreme - barrelH));
            bool belowSide = (Vector3.Dot(p, h) - barrelH) * (extreme - barrelH) > 0f;
            return belowSide && depth > 0.3f && depth < 0.8f;
        }).ToList();
        grip = gripPoints.Count > 0 ? Average(gripPoints) : Average(rear);

        Vector3 muzzleCenter = Average(muzzleEnd);
        float tipL = muzzleAtMax ? lMax : lMin;
        muzzle = muzzleCenter + l * (tipL - Vector3.Dot(muzzleCenter, l));

        facing = Quaternion.LookRotation(forward, -down);
        return "길이 " + length.ToString("0.00") + "m";
    }

    private static string AnalyzeAxe(List<Vector3> points, out Quaternion facing, out Vector3 grip, out Vector3 blade)
    {
        Axes(points, out Vector3 l, out Vector3 h, out Vector3 w);
        float lMin = points.Min(p => Vector3.Dot(p, l)), lMax = points.Max(p => Vector3.Dot(p, l));
        float length = lMax - lMin;

        // 양 끝 22% 중 옆으로 넓은 쪽이 도끼 머리
        List<Vector3> endA = points.Where(p => Vector3.Dot(p, l) < lMin + length * 0.22f).ToList();
        List<Vector3> endB = points.Where(p => Vector3.Dot(p, l) > lMax - length * 0.22f).ToList();
        bool headAtMax = Spread(endB, h) > Spread(endA, h);
        Vector3 up = headAtMax ? l : -l;
        List<Vector3> head = headAtMax ? endB : endA;

        // 자루 가운데 높이 → 머리에서 더 멀리 튀어나온 쪽이 날
        List<Vector3> handle = points.Where(p => Mathf.Abs(Vector3.Dot(p, l) - (lMin + lMax) * 0.5f) < length * 0.1f).ToList();
        float handleH = handle.Count > 0 ? handle.Average(p => Vector3.Dot(p, h)) : 0f;
        float headMin = head.Min(p => Vector3.Dot(p, h)), headMax = head.Max(p => Vector3.Dot(p, h));
        bool edgeAtMax = headMax - handleH > handleH - headMin;
        Vector3 edge = edgeAtMax ? h : -h;

        // 손잡이: 자루 끝에서 15% 지점
        float gripL = headAtMax ? lMin + length * 0.15f : lMax - length * 0.15f;
        List<Vector3> near = points.Where(p => Mathf.Abs(Vector3.Dot(p, l) - gripL) < length * 0.05f).ToList();
        grip = near.Count > 0 ? Average(near) : Average(points);

        // 날 끝 가운데
        List<Vector3> edgePoints = head.Where(p => edgeAtMax ? Vector3.Dot(p, h) > headMax - (headMax - headMin) * 0.15f : Vector3.Dot(p, h) < headMin + (headMax - headMin) * 0.15f).ToList();
        blade = edgePoints.Count > 0 ? Average(edgePoints) : Average(head);

        facing = Quaternion.LookRotation(edge, up);
        return "길이 " + length.ToString("0.00") + "m";
    }

    private static float Spread(List<Vector3> points, Vector3 axis)
    {
        if (points.Count == 0)
        {
            return 0f;
        }

        return points.Max(p => Vector3.Dot(p, axis)) - points.Min(p => Vector3.Dot(p, axis));
    }

    private static Vector3 Average(List<Vector3> points)
    {
        Vector3 sum = Vector3.zero;
        foreach (Vector3 p in points)
        {
            sum += p;
        }

        return points.Count > 0 ? sum / points.Count : Vector3.zero;
    }

    // ===================== 재질 =====================

    private static int ConvertMaterials(GameObject model, float metallic, float smoothness)
    {
        Dictionary<Material, Material> converted = new Dictionary<Material, Material>();
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material old = materials[i];
                if (old == null)
                {
                    continue;
                }

                if (!converted.TryGetValue(old, out Material urp))
                {
                    urp = ToUrp(old, metallic, smoothness);
                    converted[old] = urp;
                }

                materials[i] = urp;
            }

            renderer.sharedMaterials = materials;
        }

        return converted.Count;
    }

    private static Material ToUrp(Material old, float metallic, float smoothness)
    {
        string path = MaterialFolder + "/" + old.name + "_URP.mat";
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null || old.shader == lit)
        {
            return old;
        }

        Texture main = GetTexture(old, "_MainTex", "_BaseMap", "_AlbedoMap");
        Texture normal = GetTexture(old, "_BumpMap", "_NormalMap");
        string folder = Path.GetDirectoryName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(old)) ?? string.Empty)?.Replace('\\', '/');
        if (main == null)
        {
            main = GuessTexture(folder, old.name, "albedo", "diffuse", "basecolor");
        }

        if (normal == null)
        {
            normal = GuessTexture(folder, old.name, "norm");
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(lit) { name = old.name + "_URP" };
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = lit;
        material.SetTexture("_BaseMap", main);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", smoothness);
        if (normal != null)
        {
            MakeNormalMap(normal);
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture GetTexture(Material material, params string[] names)
    {
        foreach (string name in names)
        {
            if (material.HasProperty(name) && material.GetTexture(name) != null)
            {
                return material.GetTexture(name);
            }
        }

        return null;
    }

    // 재질 이름과 비슷한 텍스처를 같은 에셋 폴더에서 찾는다. (예: ..._1024_mat → ..._diffuse_1024)
    private static Texture GuessTexture(string folder, string materialName, params string[] kinds)
    {
        if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
        {
            return null;
        }

        string lowerName = materialName.ToLowerInvariant();
        string size = new string(lowerName.Where(char.IsDigit).ToArray());
        string prefix = lowerName.Split('_')[0];
        Texture best = null;
        int bestScore = -1;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (!kinds.Any(k => file.Contains(k)))
            {
                continue;
            }

            int score = 0;
            if (file.StartsWith(prefix)) score += 2;
            if (lowerName.Contains("shell") == file.Contains("shell")) score += 2;
            if (lowerName.Contains("bloody") == file.Contains("bloody")) score += 2;
            if (size.Length > 0 && file.Contains(size.Length > 4 ? size.Substring(size.Length - 4) : size)) score += 1;
            if (score > bestScore)
            {
                bestScore = score;
                best = AssetDatabase.LoadAssetAtPath<Texture>(path);
            }
        }

        return best;
    }

    private static void MakeNormalMap(Texture texture)
    {
        TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
    }

    // ===================== 공통 =====================

    private static void MakeMeshesReadable(GameObject source)
    {
        HashSet<string> paths = new HashSet<string>();
        foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh != null)
            {
                paths.Add(AssetDatabase.GetAssetPath(filter.sharedMesh));
            }
        }

        foreach (SkinnedMeshRenderer skinned in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skinned.sharedMesh != null)
            {
                paths.Add(AssetDatabase.GetAssetPath(skinned.sharedMesh));
            }
        }

        foreach (string path in paths)
        {
            if (AssetImporter.GetAtPath(path) is ModelImporter importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
