using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// 플레이어 재장전 동작(Mixamo Reloading)을 PlayerAC에 넣는 도구.
// 메뉴: Tools > L4D 맵 > 재장전 동작 적용
//  1. Reloading.fbx: Humanoid(Create From This Model), Loop Time 끔, Root Transform 3곳 Bake Into Pose + Original (16.6.1과 같게)
//  2. 상체만 움직이는 아바타 마스크 UpperBodyMask (다리는 달리기/서 있기 그대로)
//  3. PlayerAC에 UpperBody 레이어: Empty ↔ Reload (Bool 파라미터 IsReloading)
//     Reload 상태의 빠르기는 PlayerGunFire의 재장전 시간에 맞춘다.
[InitializeOnLoad]
public static class ReloadAnimationSetup
{
    private const string MaskPath = "Assets/07.Animations/Player/UpperBodyMask.mask";
    private const string LayerName = "UpperBody";

    static ReloadAnimationSetup()
    {
        EditorApplication.delayCall += AskOnce;
    }

    private static void AskOnce()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string key = "ReloadAnimationSetup.Asked." + Application.dataPath;
        if (EditorPrefs.GetBool(key, false))
        {
            return;
        }

        EditorPrefs.SetBool(key, true);
        if (EditorUtility.DisplayDialog("재장전 동작", "플레이어에 재장전 동작(Reloading)을 지금 넣을까요?\n나중에 Tools > L4D 맵 > 재장전 동작 적용으로도 할 수 있습니다.", "지금 적용", "나중에"))
        {
            Apply();
        }
    }

    [MenuItem("Tools/L4D 맵/재장전 동작 적용", priority = 2)]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("재장전 동작", "플레이를 멈춘 뒤에 실행해 주세요.", "확인");
            return;
        }

        List<string> log = new List<string>();

        // ---------- 1. Reloading.fbx 가져오기 설정 ----------
        string clipPath = FindModelPath("Reloading");
        if (clipPath == null)
        {
            EditorUtility.DisplayDialog("재장전 동작", "Reloading.fbx를 찾지 못했습니다. (Assets/07.Animations/Player)", "확인");
            return;
        }

        ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(clipPath);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.SaveAndReimport();

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        if (clips.Length > 0)
        {
            ModelImporterClipAnimation clip = clips[0];
            clip.name = "Reloading";
            clip.loopTime = false;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        AnimationClip reloadClip = AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        Avatar reloadAvatar = AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<Avatar>().FirstOrDefault();
        if (reloadClip == null)
        {
            EditorUtility.DisplayDialog("재장전 동작", "Reloading.fbx 안에서 동작을 찾지 못했습니다.", "확인");
            return;
        }

        log.Add("✔ Reloading: Humanoid, Loop Time 끔, Bake Into Pose + Original (길이 " + reloadClip.length.ToString("0.0") + "초)");
        if (reloadAvatar == null || !reloadAvatar.isHuman)
        {
            log.Add("⚠ Reloading의 Humanoid 아바타가 올바르지 않습니다. Rig 탭의 Configure...를 확인해 주세요.");
        }

        // ---------- 씬의 플레이어 ----------
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() || !OpenSceneWithPlayer())
        {
            return;
        }

        PlayerMove player = Object.FindAnyObjectByType<PlayerMove>();
        Animator animator = player.GetComponentInChildren<Animator>();
        AnimatorController controller = animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
        if (controller == null)
        {
            EditorUtility.DisplayDialog("재장전 동작", "Player/Model의 Animator에 PlayerAC가 없습니다. 16장을 먼저 적용해 주세요.", "확인");
            return;
        }

        if (animator.avatar == null || !animator.avatar.isHuman)
        {
            log.Add("⚠ 군인이 Humanoid가 아닙니다. 16.6처럼 Soldier_demo를 Humanoid로 바꿔야 재장전 동작이 입혀집니다.");
        }

        float reloadTime = 3f;
        PlayerGunFire gun = player.GetComponent<PlayerGunFire>();
        if (gun != null)
        {
            SerializedProperty time = new SerializedObject(gun).FindProperty("_reloadingTime");
            if (time != null && time.floatValue > 0.1f)
            {
                reloadTime = time.floatValue;
            }
        }

        // ---------- 2. 상체 아바타 마스크 ----------
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, MaskPath);
        }

        for (AvatarMaskBodyPart part = 0; part < AvatarMaskBodyPart.LastBodyPart; part++)
        {
            bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head
                         || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                         || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers
                         || part == AvatarMaskBodyPart.LeftHandIK || part == AvatarMaskBodyPart.RightHandIK;
            mask.SetHumanoidBodyPartActive(part, upper);
        }

        EditorUtility.SetDirty(mask);
        log.Add("✔ UpperBodyMask: 몸통, 머리, 두 팔, 손가락만 (다리와 Root 제외)");

        // ---------- 3. PlayerAC에 UpperBody 레이어 ----------
        if (controller.parameters.All(p => p.name != "IsReloading"))
        {
            controller.AddParameter("IsReloading", AnimatorControllerParameterType.Bool);
        }

        AnimatorControllerLayer[] layers = controller.layers;
        for (int i = layers.Length - 1; i >= 0; i--)
        {
            if (layers[i].name == LayerName)
            {
                controller.RemoveLayer(i);
            }
        }

        controller.AddLayer(LayerName);
        layers = controller.layers;
        int index = layers.Length - 1;
        layers[index].defaultWeight = 1f;
        layers[index].avatarMask = mask;
        layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
        controller.layers = layers;

        AnimatorStateMachine machine = controller.layers[index].stateMachine;
        AnimatorState empty = machine.AddState("Empty", new Vector3(300f, 0f, 0f));
        machine.defaultState = empty;
        AnimatorState reload = machine.AddState("Reload", new Vector3(300f, 140f, 0f));
        reload.motion = reloadClip;
        reload.speed = reloadClip.length / reloadTime;  // 재장전 시간에 딱 맞게 끝난다.

        AnimatorStateTransition toReload = empty.AddTransition(reload);
        toReload.hasExitTime = false;
        toReload.hasFixedDuration = true;
        toReload.duration = 0.15f;
        toReload.AddCondition(AnimatorConditionMode.If, 0f, "IsReloading");

        AnimatorStateTransition toEmpty = reload.AddTransition(empty);
        toEmpty.hasExitTime = false;
        toEmpty.hasFixedDuration = true;
        toEmpty.duration = 0.2f;
        toEmpty.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsReloading");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        log.Add("✔ " + AssetDatabase.GetAssetPath(controller) + "에 UpperBody 레이어 (Weight 1, Mask UpperBodyMask)");
        log.Add("✔ Empty → Reload (IsReloading 켬), Reload → Empty (IsReloading 끔), Reload 빠르기 " + reload.speed.ToString("0.00") + " (재장전 " + reloadTime + "초에 맞춤)");

        string body = "재장전 동작을 넣었습니다. ▶ Play 후 R로 확인해 보세요.\n\n" + string.Join("\n", log);
        Debug.Log("[재장전 동작] " + body);
        EditorUtility.DisplayDialog("재장전 동작", body, "확인");
    }

    private static string FindModelPath(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Model"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(Path.GetFileNameWithoutExtension(path), name, System.StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }

    private static bool OpenSceneWithPlayer()
    {
        if (Object.FindAnyObjectByType<PlayerMove>() != null)
        {
            return true;
        }

        foreach (string path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Where(p => !p.StartsWith("Assets/Backup_"))
                     .OrderBy(p => Path.GetFileNameWithoutExtension(p) == "GameScene" ? 0 : 1))
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (Object.FindAnyObjectByType<PlayerMove>() != null)
            {
                return true;
            }
        }

        EditorUtility.DisplayDialog("재장전 동작", "Player가 있는 씬을 찾지 못했습니다. GameScene을 연 뒤 다시 실행해 주세요.", "확인");
        return false;
    }
}
