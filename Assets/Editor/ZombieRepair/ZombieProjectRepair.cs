#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Repairs the existing gameplay scene after importing an unrelated FPS demo.
// Scene geometry is never rebuilt by this command.
public static class ZombieProjectRepair
{
    private const string GameScene = "Assets/01.Scenes/GameScene.unity";
    private static readonly string[] MaterialFolders =
    {
        "Assets/ZombieCity/Materials",
        "Assets/FreeModels_ImportFixed_20261026/Materials",
        "Assets/09.Materials"
    };

    [MenuItem("Tools/Zombie Game/Repair Play and Map", false, 1)]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogWarning("[Zombie Repair] Play 모드를 종료하고 컴파일이 끝난 뒤 실행하세요.");
            return;
        }

        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScene);
        if (sceneAsset == null)
        {
            Debug.LogError("[Zombie Repair] Assets/01.Scenes/GameScene.unity 파일을 찾을 수 없습니다.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != GameScene)
        {
            // Protect unsaved work before selecting the actual game rather than an imported demo.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        }

        string backup = Path.Combine("ZombieRepairBackups", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        Directory.CreateDirectory(backup);
        File.Copy(GameScene, Path.Combine(backup, "GameScene.unity"), true);

        // A demo scene left open must not become the Play entry point.
        EditorSceneManager.playModeStartScene = sceneAsset;
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int sceneIndex = scenes.FindIndex(item => item.path == GameScene);
        if (sceneIndex < 0) scenes.Add(new EditorBuildSettingsScene(GameScene, true));
        else scenes[sceneIndex] = new EditorBuildSettingsScene(GameScene, true);
        EditorBuildSettings.scenes = scenes.ToArray();

        Time.timeScale = 1f;
        AudioListener.pause = false;
        EditorApplication.isPaused = false;

        int repairedMaterials = RepairMaterials(backup);
        int reimportedModels = RepairMissingModelMeshes(scene);
        int remainingMissing = CountMissingMeshes(scene);
        AssetDatabase.SaveAssets();

        Player player = FindPlayer(scene);
        if (player != null)
        {
            Camera camera = player.GetComponentInChildren<Camera>(true);
            if (camera != null && SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.in2DMode = false;
                SceneView.lastActiveSceneView.LookAt(camera.transform.position + camera.transform.forward * 8f,
                    camera.transform.rotation, 10f, false, true);
            }
            Selection.activeGameObject = player.gameObject;
        }

        string result = $"[Zombie Repair] GameScene 실행 경로 복구. 재질 {repairedMaterials}개, 모델 재임포트 {reimportedModels}개.";
        if (remainingMissing > 0)
            Debug.LogWarning(result + $" 메시가 없는 오브젝트 {remainingMissing}개는 원본 모델 파일을 확인해야 합니다.", sceneAsset);
        else
            Debug.Log(result + " Play를 누르고 게임 시작 버튼을 선택하세요.", sceneAsset);
    }

    private static int RepairMaterials(string backup)
    {
        RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
        bool universal = pipeline != null && pipeline.GetType().Name.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0;
        if (pipeline != null && !universal)
        {
            Debug.LogWarning("[Zombie Repair] 이 복구는 Built-in/URP 맵 재질을 지원합니다. 현재 렌더 파이프라인 설정을 확인하세요.");
            return 0;
        }

        Shader target = Shader.Find(universal ? "Universal Render Pipeline/Lit" : "Standard");
        if (target == null)
        {
            Debug.LogWarning("[Zombie Repair] 현재 파이프라인의 Lit 셰이더를 찾지 못했습니다.");
            return 0;
        }

        List<string> folders = new List<string>();
        foreach (string folder in MaterialFolders)
            if (AssetDatabase.IsValidFolder(folder)) folders.Add(folder);
        if (folders.Count == 0) return 0;

        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", folders.ToArray()))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) continue;
            string shaderName = material.shader != null ? material.shader.name : string.Empty;
            bool missing = material.shader == null || shaderName == "Hidden/InternalErrorShader";
            bool mismatch = universal ? shaderName == "Standard" : shaderName.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal);
            if (!missing && !mismatch) continue;

            string backupPath = Path.Combine(backup, path);
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
            File.Copy(path, backupPath, true);

            Texture texture = SavedTexture(material, "_BaseMap") ?? SavedTexture(material, "_MainTex");
            Color color = SavedColor(material, "_BaseColor", SavedColor(material, "_Color", Color.white));
            Color emission = SavedColor(material, "_EmissionColor", Color.black);
            Undo.RecordObject(material, "Repair Zombie Map Material");
            material.shader = target;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
            if (emission.maxColorComponent > 0f) material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
            changed++;
        }
        return changed;
    }

    // Saved properties survive a missing shader, unlike Material.HasProperty lookups.
    private static Texture SavedTexture(Material material, string name)
    {
        SerializedProperty array = new SerializedObject(material).FindProperty("m_SavedProperties.m_TexEnvs");
        if (array == null) return null;
        for (int i = 0; i < array.arraySize; i++)
        {
            SerializedProperty entry = array.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue == name)
                return entry.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
        }
        return null;
    }

    private static Color SavedColor(Material material, string name, Color fallback)
    {
        SerializedProperty array = new SerializedObject(material).FindProperty("m_SavedProperties.m_Colors");
        if (array == null) return fallback;
        for (int i = 0; i < array.arraySize; i++)
        {
            SerializedProperty entry = array.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("first").stringValue == name)
                return entry.FindPropertyRelative("second").colorValue;
        }
        return fallback;
    }

    private static int RepairMissingModelMeshes(Scene scene)
    {
        HashSet<string> models = new HashSet<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null) continue;
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(filter.gameObject);
                if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/ZombieCity/Kenney/", StringComparison.Ordinal)
                    && File.Exists(path)) models.Add(path);
            }
        }
        foreach (string path in models)
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        return models.Count;
    }

    private static int CountMissingMeshes(Scene scene)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh == null) count++;
        return count;
    }

    private static Player FindPlayer(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Player player = root.GetComponentInChildren<Player>(true);
            if (player != null) return player;
        }
        return null;
    }
}
#endif
