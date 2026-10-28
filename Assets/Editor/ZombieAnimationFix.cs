using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 좀비가 맞을 때 땅에 파묻히는 것 고치기 (애니메이션 가져오기 설정)
// 메뉴: Tools > L4D 맵 > 좀비 애니메이션 땅 묻힘 고치기
//
// Mixamo에서 받은 동작은 Root Transform Position (Y)가 'Center of Mass(무게중심)' 기준이라
// 몸을 숙이는 동작(맞기, 공격)에서 몸 전체가 아래로 내려가 발이 땅에 묻힌다.
// 좀비 애니메이터 컨트롤러에 들어 있는 모든 동작을
//   Root Transform Position (Y): Bake Into Pose 켬 + Based Upon: Original
// 로 바꾼다. (16.6.1에서 플레이어 동작에 한 것과 같은 설정. 앞뒤 이동(XZ)은 건드리지 않는다)
[InitializeOnLoad]
public static class ZombieAnimationFix
{
    static ZombieAnimationFix()
    {
        EditorApplication.delayCall += AskOnce;
    }

    private static void AskOnce()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string key = "ZombieAnimationFix.Asked." + Application.dataPath;
        if (EditorPrefs.GetBool(key, false))
        {
            return;
        }

        EditorPrefs.SetBool(key, true);
        if (EditorUtility.DisplayDialog("좀비 땅 묻힘", "좀비가 맞을 때 땅에 묻히지 않도록 좀비 동작의 Root Y 설정을 고칠까요?\n나중에 Tools > L4D 맵 > 좀비 애니메이션 땅 묻힘 고치기로도 할 수 있습니다.", "지금 고치기", "나중에"))
        {
            Fix();
        }
    }

    [MenuItem("Tools/L4D 맵/좀비 애니메이션 땅 묻힘 고치기", priority = 4)]
    public static void Fix()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("좀비 땅 묻힘", "플레이를 멈춘 뒤에 실행해 주세요.", "확인");
            return;
        }

        // 1. Zombie가 붙은 프리팹에서 애니메이터 컨트롤러를 찾는다.
        HashSet<AnimatorController> controllers = new HashSet<AnimatorController>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab == null || prefab.GetComponent<Zombie>() == null)
            {
                continue;
            }

            foreach (Animator animator in prefab.GetComponentsInChildren<Animator>(true))
            {
                if (animator.runtimeAnimatorController is AnimatorController controller)
                {
                    controllers.Add(controller);
                }
            }
        }

        if (controllers.Count == 0)
        {
            EditorUtility.DisplayDialog("좀비 땅 묻힘", "Zombie 프리팹의 애니메이터 컨트롤러를 찾지 못했습니다.", "확인");
            return;
        }

        // 2. 컨트롤러 안의 동작들이 들어 있는 모델 파일(FBX)을 모은다.
        Dictionary<string, HashSet<string>> clipsByModel = new Dictionary<string, HashSet<string>>();
        foreach (AnimatorController controller in controllers)
        {
            foreach (AnimationClip clip in controller.animationClips)
            {
                string path = AssetDatabase.GetAssetPath(clip);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter))
                {
                    continue;
                }

                if (!clipsByModel.TryGetValue(path, out HashSet<string> names))
                {
                    names = new HashSet<string>();
                    clipsByModel[path] = names;
                }

                names.Add(clip.name);
            }
        }

        // 3. Root Y: Bake Into Pose + Original
        List<string> log = new List<string>();
        foreach (KeyValuePair<string, HashSet<string>> pair in clipsByModel)
        {
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(pair.Key);
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            int changed = 0;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (!pair.Value.Contains(clip.name) && !pair.Value.Contains(clip.takeName))
                {
                    continue;
                }

                if (clip.lockRootHeightY && clip.keepOriginalPositionY)
                {
                    continue;
                }

                clip.lockRootHeightY = true;        // Bake Into Pose
                clip.keepOriginalPositionY = true;  // Based Upon: Original
                clip.heightFromFeet = false;
                changed++;
            }

            if (changed > 0)
            {
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                log.Add("✔ " + System.IO.Path.GetFileName(pair.Key) + " (" + string.Join(", ", pair.Value) + ")");
            }
        }

        string body = log.Count > 0
            ? "좀비 동작의 Root Y를 Bake Into Pose + Original로 바꿨습니다.\n\n" + string.Join("\n", log)
            : "좀비 동작이 이미 올바르게 설정되어 있습니다. (" + clipsByModel.Count + "개 파일 확인)";
        Debug.Log("[좀비 땅 묻힘] " + body);
        EditorUtility.DisplayDialog("좀비 땅 묻힘", body, "확인");
    }
}
