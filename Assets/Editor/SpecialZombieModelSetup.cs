using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 특수 좀비 전용 모델 넣기
// 메뉴: Tools > L4D 맵 > 특수 좀비 모델 설정
//
//  1. Assets/10.SpecialZombies 폴더에 모델(FBX)을 넣는다. 파일 이름에 종류가 들어가면 된다.
//       예) Tank.fbx, Boomer.fbx, Spitter.fbx, Runner.fbx  (또는 tank_mutant.fbx 처럼)
//     Mixamo(무료)의 캐릭터를 "FBX for Unity, T-pose, Skin 포함"으로 받으면 바로 쓸 수 있다.
//  2. 이 메뉴를 누르면 모델을 Humanoid로 바꾸고, 텍스처/재질을 꺼내고,
//     Assets/10.SpecialZombies/Resources/SpecialZombies/<종류>.prefab 을 만든다.
//  3. 게임에서 특수 좀비가 나올 때 이 프리팹으로 모델이 바뀐다. (좀비 동작은 그대로)
//     프리팹을 지우면 다시 '변이된 일반 좀비'로 나온다.
public static class SpecialZombieModelSetup
{
    private const string Folder = "Assets/10.SpecialZombies";
    private const string PrefabFolder = Folder + "/Resources/SpecialZombies";

    private static readonly string[] Types = { "Tank", "Boomer", "Spitter", "Runner" };

    [MenuItem("Tools/L4D 맵/특수 좀비 모델 설정", priority = 20)]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("특수 좀비 모델", "플레이를 멈춘 뒤에 실행해 주세요.", "확인");
            return;
        }

        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder("Assets", "10.SpecialZombies");
        }

        List<string> log = new List<string>();
        string[] models = AssetDatabase.FindAssets("t:Model", new[] { Folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (string type in Types)
        {
            string path = models.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).ToLowerInvariant().Contains(type.ToLowerInvariant()));
            if (path == null)
            {
                log.Add("· " + type + ": 모델 없음 → 변이된 일반 좀비로 나옵니다.");
                continue;
            }

            log.Add(SetupModel(type, path));
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string body = "Assets/10.SpecialZombies 폴더의 모델을 확인했습니다.\n\n" + string.Join("\n", log)
                      + "\n\n모델을 넣으려면 파일 이름에 Tank / Boomer / Spitter / Runner 가 들어가게 해서 이 폴더에 넣고 다시 눌러 주세요.";
        Debug.Log("[특수 좀비 모델] " + body);
        EditorUtility.DisplayDialog("특수 좀비 모델", body, "확인");
    }

    private static string SetupModel(string type, string path)
    {
        // 1. Humanoid로 (좀비 애니메이션을 그대로 입히기 위해)
        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();

        // 2. 모델 안에 들어 있는 텍스처를 꺼낸다. (Mixamo FBX는 텍스처가 안에 들어 있다)
        string textureFolder = Folder + "/Textures_" + type;
        if (!AssetDatabase.IsValidFolder(textureFolder))
        {
            AssetDatabase.CreateFolder(Folder, "Textures_" + type);
        }

        importer.ExtractTextures(textureFolder);
        importer.materialLocation = ModelImporterMaterialLocation.External;  // 재질도 파일로 꺼내서 URP 재질로 바뀌게
        importer.SaveAndReimport();

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        string warn = avatar == null || !avatar.isHuman ? "  ⚠ Humanoid 뼈를 못 찾았습니다. Rig 탭 > Configure...를 확인해 주세요." : string.Empty;

        // 3. Resources 프리팹 만들기
        CreateFolders(PrefabFolder);
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        instance.name = type;
        Animator animator = instance.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            animator = instance.AddComponent<Animator>();
        }

        animator.avatar = avatar;
        animator.applyRootMotion = false;
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                skinned.updateWhenOffscreen = true;
            }
        }

        string prefabPath = PrefabFolder + "/" + type + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);
        return "✔ " + type + ": " + Path.GetFileName(path) + " → " + prefabPath + warn;
    }

    private static void CreateFolders(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    [MenuItem("Tools/L4D 맵/특수 좀비 모델 지우기 (변이 좀비로 되돌리기)", priority = 21)]
    public static void Clear()
    {
        if (AssetDatabase.IsValidFolder(PrefabFolder) && EditorUtility.DisplayDialog("특수 좀비 모델", "만들어 둔 특수 좀비 프리팹을 지울까요? (FBX 모델은 남겨 둡니다)", "지우기", "취소"))
        {
            AssetDatabase.DeleteAsset(PrefabFolder);
            AssetDatabase.Refresh();
        }
    }
}
