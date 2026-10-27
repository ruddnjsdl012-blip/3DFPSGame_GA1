using System.IO;
using UnityEditor;
using UnityEngine;

// Unity의 패키지 Import 창(파일 목록 창)을 거치지 않고 .unitypackage를 바로 넣는다.
// Import 창이 NullReferenceException(PackageImportTreeView)으로 열리지 않을 때 쓴다.
// 메뉴: Tools > Zombie Game > Import Package (창 없이)
public static class QuickPackageImporter
{
    private const string LastFolderKey = "ZombieGame.QuickImport.LastFolder";

    [MenuItem("Tools/Zombie Game/Import Package (창 없이)")]
    private static void ImportWithoutDialog()
    {
        string folder = EditorPrefs.GetString(LastFolderKey, System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile));
        string path = EditorUtility.OpenFilePanel("넣을 .unitypackage 파일 선택", folder, "unitypackage");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        EditorPrefs.SetString(LastFolderKey, Path.GetDirectoryName(path));
        Debug.Log($"[QuickPackageImporter] 창 없이 import: {path}");

        // interactive = false: 파일 목록 창 없이 패키지의 모든 파일을 그대로 넣는다. (같은 파일은 덮어씀)
        AssetDatabase.ImportPackage(path, false);
    }
}
