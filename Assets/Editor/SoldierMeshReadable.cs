using UnityEditor;

// 1인칭 화면에 손만 남기려면 군인 메시를 플레이 중에 읽어야 한다.
// Soldier_demo 모델의 Read/Write를 켜 둔다. (한 번만, 자동)
[InitializeOnLoad]
public static class SoldierMeshReadable
{
    static SoldierMeshReadable()
    {
        EditorApplication.delayCall += Ensure;
    }

    [MenuItem("Tools/L4D 맵/군인 메시 Read/Write 켜기", priority = 30)]
    public static void Ensure()
    {
        foreach (string guid in AssetDatabase.FindAssets("Soldier_demo t:Model"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith("Soldier_demo.fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                UnityEngine.Debug.Log("[L4D] Soldier_demo의 Read/Write를 켰습니다. (1인칭 손 화면용)");
            }
        }
    }
}
