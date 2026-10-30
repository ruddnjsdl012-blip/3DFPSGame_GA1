using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

// 소스만 갱신하고 기존 .meta/GUID, 씬, 모델은 유지한다.
// 누적 소스를 TextAsset으로 함께 가져온 뒤 한 번에 적용한다. 파일/클래스명은 v6와 같아 적용기가 중복되지 않는다.
[InitializeOnLoad]
public static class L4DV6Installer
{
    [Serializable]
    private sealed class SourceEntry
    {
        public string target;
        public string payload;
        public string sha256;
    }

    [Serializable]
    private sealed class Manifest
    {
        public string revision;
        public SourceEntry[] files;
    }

    private sealed class Pending
    {
        public string Target;
        public byte[] Bytes;
        public byte[] Original;
        public bool Existed;
    }

    static L4DV6Installer()
    {
        EditorApplication.delayCall += AutoApply;
    }

    private static void AutoApply()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += AutoApply;
            return;
        }
        Apply(false);
    }

    [MenuItem("Tools/L4D 맵/v7 1인칭 무기 표시 패치 다시 적용", priority = 15)]
    public static void Reapply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogWarning("[L4D v7] Play를 끄고 컴파일이 끝난 뒤 실행하세요.");
            return;
        }
        Apply(true);
    }

    private static string Hash(byte[] bytes)
    {
        using (SHA256 sha = SHA256.Create())
        {
            StringBuilder result = new StringBuilder();
            foreach (byte value in sha.ComputeHash(bytes)) result.Append(value.ToString("x2"));
            return result.ToString();
        }
    }

    private static void Apply(bool force)
    {
        string editorFolder = Path.Combine(Application.dataPath, "Editor");
        string manifestPath = Path.Combine(editorFolder, "L4DV6_manifest.json");
        if (!File.Exists(manifestPath)) return;
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string revisionKey = "L4D.WeaponTableV6." + Hash(Encoding.UTF8.GetBytes(Application.dataPath));
        List<Pending> pending = new List<Pending>();
        try
        {
            byte[] manifestBytes = File.ReadAllBytes(manifestPath);
            string digest = Hash(manifestBytes);
            // 사용자 후속 수정을 매번 덮어쓰지 않는다. 같은 패치는 최초 한 번만 자동 적용한다.
            if (!force && EditorPrefs.GetString(revisionKey, string.Empty) == digest) return;
            Manifest manifest = JsonUtility.FromJson<Manifest>(Encoding.UTF8.GetString(manifestBytes));
            if (manifest == null || manifest.files == null || manifest.files.Length == 0) throw new InvalidDataException("패치 목록이 비어 있습니다.");
            HashSet<string> targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string sourceRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "02.Scripts")) + Path.DirectorySeparatorChar;
            foreach (SourceEntry entry in manifest.files)
            {
                if (entry == null || string.IsNullOrEmpty(entry.target) || !entry.target.StartsWith("Assets/02.Scripts/", StringComparison.Ordinal)
                    || !entry.target.EndsWith(".cs", StringComparison.Ordinal) || entry.target.Contains("..")) throw new InvalidDataException("허용되지 않은 대상 경로입니다.");
                if (string.IsNullOrEmpty(entry.payload) || Path.GetFileName(entry.payload) != entry.payload
                    || !entry.payload.StartsWith("L4DV6_", StringComparison.Ordinal) || !entry.payload.EndsWith(".cs.txt", StringComparison.Ordinal))
                    throw new InvalidDataException("허용되지 않은 소스 파일명입니다.");
                string target = Path.GetFullPath(Path.Combine(projectRoot, entry.target));
                if (!target.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase) || !targets.Add(target)) throw new InvalidDataException("중복되거나 잘못된 대상 경로입니다.");
                string payload = Path.Combine(editorFolder, entry.payload);
                if (!File.Exists(payload)) throw new FileNotFoundException("전체 패키지를 Import하세요.", payload);
                byte[] bytes = File.ReadAllBytes(payload);
                if (!string.Equals(Hash(bytes), entry.sha256, StringComparison.Ordinal)) throw new InvalidDataException("소스 해시가 맞지 않습니다: " + entry.payload);
                bool existed = File.Exists(target);
                byte[] original = existed ? File.ReadAllBytes(target) : null;
                if (existed && Hash(original) == entry.sha256) continue;
                pending.Add(new Pending { Target = target, Bytes = bytes, Original = original, Existed = existed });
            }

            string backup = Path.Combine(projectRoot, "Library", "L4D_V7_Backup", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"));
            foreach (Pending item in pending)
            {
                if (!item.Existed) continue;
                string relative = item.Target.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string backupPath = Path.Combine(backup, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                File.WriteAllBytes(backupPath, item.Original);
            }

            AssetDatabase.StartAssetEditing();
            try
            {
                try
                {
                    foreach (Pending item in pending)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(item.Target));
                        File.WriteAllBytes(item.Target, item.Bytes);
                    }
                }
                catch
                {
                    foreach (Pending item in pending)
                    {
                        if (item.Existed) File.WriteAllBytes(item.Target, item.Original);
                        else if (File.Exists(item.Target)) File.Delete(item.Target);
                    }
                    throw;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            EditorPrefs.SetString(revisionKey, digest);
            if (pending.Count > 0) AssetDatabase.Refresh();
            Debug.Log("[L4D v7] 1인칭 무기 표시 패치 적용 완료. 컴파일이 끝나면 기존 씬을 재생하세요. (갱신 " + pending.Count + "개)");
        }
        catch (Exception exception)
        {
            Debug.LogError("[L4D v7] 적용을 완료하지 못했습니다: " + exception.Message);
        }
    }
}
