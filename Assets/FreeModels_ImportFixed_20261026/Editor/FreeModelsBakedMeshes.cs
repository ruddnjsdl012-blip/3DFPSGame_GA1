#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeModelsImportFixed20261026.Editor
{
    // Asset-derived geometry is already Y-up in Unity coordinates. No FBX pose sampling,
    // root rotation conversion, or runtime animation is needed to keep characters upright.
    internal static class FreeModelsBakedMeshes
    {
        private const string Root = "Assets/FreeModels_ImportFixed_20261026";
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        [Serializable]
        private sealed class MeshData
        {
            public string name;
            public Vector3[] vertices;
            public Vector3[] normals;
            public Vector2[] uv;
            public int[] triangles;
        }

        internal static Mesh Load(string relativePath)
        {
            Mesh mesh;
            if (Cache.TryGetValue(relativePath, out mesh) && mesh != null) return mesh;
            string assetPath = Root + "/Meshes/BakedV5/" + Path.GetFileNameWithoutExtension(relativePath) + ".asset";
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh != null) { Cache[relativePath] = mesh; return mesh; }
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/" + relativePath);
            if (source == null) throw new FileNotFoundException("자세 보정 메시 데이터가 없습니다. 전체 Import하세요: " + relativePath);
            MeshData data = JsonUtility.FromJson<MeshData>(source.text);
            if (data == null || data.vertices == null || data.normals == null || data.uv == null || data.triangles == null ||
                data.vertices.Length < 3 || data.normals.Length != data.vertices.Length || data.uv.Length != data.vertices.Length ||
                data.triangles.Length == 0 || data.triangles.Length % 3 != 0)
                throw new InvalidDataException("메시 데이터가 올바르지 않습니다: " + relativePath);
            foreach (int index in data.triangles)
                if (index < 0 || index >= data.vertices.Length) throw new InvalidDataException("잘못된 메시 인덱스: " + relativePath);
            EnsureFolder(Root + "/Meshes/BakedV5");
            if (File.Exists(Path.GetFullPath(assetPath))) assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            mesh = new Mesh { name = data.name };
            if (data.vertices.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = data.vertices;
            mesh.normals = data.normals;
            mesh.uv = data.uv;
            mesh.triangles = data.triangles;
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, assetPath);
            Cache[relativePath] = mesh;
            return mesh;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            EnsureFolder(Path.GetDirectoryName(folder).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
        }
    }
}
#endif
