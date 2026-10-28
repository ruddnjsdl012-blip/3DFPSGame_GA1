using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// 2단계: AI 디렉터 + 호드 + 피날레 + 손전등을 씬에 붙이는 도구.
// 메뉴: Tools > L4D 맵 > 2단계: AI 디렉터 + 손전등 적용
[InitializeOnLoad]
public static class Step2DirectorSetup
{
    static Step2DirectorSetup()
    {
        EditorApplication.delayCall += AskOnce;
    }

    private static void AskOnce()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string key = "Step2DirectorSetup.Asked." + Application.dataPath;
        if (EditorPrefs.GetBool(key, false))
        {
            return;
        }

        EditorPrefs.SetBool(key, true);
        if (EditorUtility.DisplayDialog("L4D 2단계", "AI 디렉터(호드, 피날레)와 손전등(F)을 지금 GameScene에 붙일까요?\n나중에 Tools > L4D 맵 메뉴로도 할 수 있습니다.", "지금 적용", "나중에"))
        {
            Apply();
        }
    }

    [MenuItem("Tools/L4D 맵/2단계: AI 디렉터 + 손전등 적용", priority = 1)]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("L4D 2단계", "플레이를 멈춘 뒤에 실행해 주세요.", "확인");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() || !OpenSceneWithPlayer())
        {
            return;
        }

        System.Collections.Generic.List<string> log = new System.Collections.Generic.List<string>();
        Scene scene = SceneManager.GetActiveScene();

        CityMapInfo map = Object.FindAnyObjectByType<CityMapInfo>();
        if (map == null)
        {
            EditorUtility.DisplayDialog("L4D 2단계", "CityMap이 없습니다. 먼저 Tools > L4D 맵 > 도시 맵 만들기를 해 주세요.", "확인");
            return;
        }

        // 1. AI 디렉터
        AIDirector director = Object.FindAnyObjectByType<AIDirector>();
        if (director == null)
        {
            director = new GameObject("AIDirector").AddComponent<AIDirector>();
            SceneManager.MoveGameObjectToScene(director.gameObject, scene);
        }

        Zombie prefab = FindZombiePrefab();
        SerializedObject serialized = new SerializedObject(director);
        serialized.FindProperty("_map").objectReferenceValue = map;
        serialized.FindProperty("_zombiePrefab").objectReferenceValue = prefab;
        serialized.ApplyModifiedProperties();
        log.Add(prefab != null ? "✔ AIDirector 오브젝트 (Zombie Prefab: " + prefab.name + ")" : "⚠ Zombie 프리팹을 찾지 못했습니다. AIDirector의 Zombie Prefab 칸에 넣어 주세요.");

        // 2. 원래 스포너와 좀비 수 UI는 끈다. (AI 디렉터가 대신하고, 화면 왼쪽 위에 좀비 수를 보여 준다)
        foreach (ZombieSpawner spawner in Object.FindObjectsByType<ZombieSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            spawner.enabled = false;
            log.Add("✔ ZombieSpawner 끔 (AI 디렉터가 대신함)");
        }

        foreach (UI_ZombieCount count in Object.FindObjectsByType<UI_ZombieCount>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            count.gameObject.SetActive(false);
            log.Add("✔ UI_ZombieCount 숨김 (" + count.name + ")");
        }

        // 3. 손전등
        Camera camera = Camera.main;
        if (camera == null)
        {
            CameraFollow follow = Object.FindAnyObjectByType<CameraFollow>();
            camera = follow != null ? follow.GetComponent<Camera>() : null;
        }

        if (camera != null)
        {
            if (camera.GetComponent<PlayerFlashlight>() == null)
            {
                camera.gameObject.AddComponent<PlayerFlashlight>();
            }

            log.Add("✔ Main Camera에 PlayerFlashlight (F 키)");
        }
        else
        {
            log.Add("⚠ Main Camera를 찾지 못해 손전등을 붙이지 못했습니다.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add("✔ " + scene.path + " 저장");

        string body = "2단계를 적용했습니다. ▶ Play로 확인해 보세요.\n\n" + string.Join("\n", log);
        Debug.Log("[L4D 2단계] " + body);
        EditorUtility.DisplayDialog("L4D 2단계", body, "확인");
    }

    private static Zombie FindZombiePrefab()
    {
        ZombieSpawner spawner = Object.FindAnyObjectByType<ZombieSpawner>(FindObjectsInactive.Include);
        if (spawner != null)
        {
            SerializedProperty property = new SerializedObject(spawner).FindProperty("_zombiePrefab");
            if (property != null && property.objectReferenceValue is Zombie fromSpawner)
            {
                return fromSpawner;
            }
        }

        GameObject course = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/03.Prefabs/Zombie.prefab");
        if (course != null && course.GetComponent<Zombie>() != null)
        {
            return course.GetComponent<Zombie>();
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/Backup_"))
            {
                continue;
            }

            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null && go.GetComponent<Zombie>() != null)
            {
                return go.GetComponent<Zombie>();
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
                     .OrderBy(p => System.IO.Path.GetFileNameWithoutExtension(p) == "GameScene" ? 0 : 1))
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (Object.FindAnyObjectByType<PlayerMove>() != null)
            {
                return true;
            }
        }

        EditorUtility.DisplayDialog("L4D 2단계", "Player가 있는 씬을 찾지 못했습니다. GameScene을 연 뒤 다시 실행해 주세요.", "확인");
        return false;
    }
}
