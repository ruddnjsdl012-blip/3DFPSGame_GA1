#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;

// R키/버튼의 씬 재시작을 위해 기존 Build Settings 목록을 보존하며 씬을 등록한다.
[InitializeOnLoad]
public static class Lesson11BuildSetup
{
    private const string ScenePath = "Assets/01.Scenes/GameScene.unity";

    static Lesson11BuildSetup()
    {
        EditorApplication.delayCall += EnsureProjectSetup;
    }

    private static void EnsureProjectSetup()
    {
        EnsureSceneRegistered();

        // 이미 설치한 TMP 설정은 보존한다. Essentials가 없는 프로젝트만 준비한다.
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || AssetDatabase.FindAssets("t:TMP_Settings", new[] { "Assets" }).Length > 0)
        {
            return;
        }

        const string template = "Assets/Editor/Lesson11_Complete_20261023/TMPSettingsTemplate.asset.bytes";
        const string destination = "Assets/08.Fonts/Resources/TMP Settings.asset";

        if (File.Exists(template) && File.Exists(destination) == false)
        {
            Directory.CreateDirectory("Assets/08.Fonts/Resources");
            File.Copy(template, destination);
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
        }
    }

    [MenuItem("Tools/Lesson 11/Register GameScene for Restart")]
    public static void EnsureSceneRegistered()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            return;
        }

        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        for (int index = 0; index < scenes.Count; index++)
        {
            if (scenes[index].path != ScenePath)
            {
                continue;
            }

            if (scenes[index].enabled == false)
            {
                scenes[index] = new EditorBuildSettingsScene(ScenePath, true);
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            return;
        }

        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
