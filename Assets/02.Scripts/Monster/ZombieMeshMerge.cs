using System.Collections.Generic;
using UnityEngine;

// 좀비 모델은 몸 조각마다 SkinnedMeshRenderer가 따로 있다(14개).
// 좀비 한 마리가 스키닝 14번, 그리기 14번, 그림자 그리기 14번을 하므로 좀비가 많으면 크게 느려진다.
// 같은 머티리얼끼리 하나의 SkinnedMeshRenderer로 합친다. 합친 메시는 모델별로 한 번만 만들고 재사용한다.
public static class ZombieMeshMerge
{
    private class Cache
    {
        public Mesh Mesh;
        public string[] BoneNames;
        public string RootBoneName;
        public Material[] Materials;
    }

    private static readonly Dictionary<string, Cache> Caches = new Dictionary<string, Cache>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Caches.Clear();
    }

    // 합쳤으면 true를 돌려준다.
    public static bool Merge(Transform model)
    {
        if (model == null) return false;

        SkinnedMeshRenderer[] skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(false);
        if (skins.Length < 2) return false;

        string key = MakeKey(skins);
        if (key == null) return false;  // 읽을 수 없거나 블렌드셰이프가 있는 메시는 합치지 않는다.

        if (!Caches.TryGetValue(key, out Cache cache))
        {
            cache = Build(model, skins);
            if (cache == null) return false;
            Caches.Add(key, cache);
        }

        return Apply(model, skins, cache);
    }

    private static string MakeKey(SkinnedMeshRenderer[] skins)
    {
        System.Text.StringBuilder key = new System.Text.StringBuilder();
        foreach (SkinnedMeshRenderer skin in skins)
        {
            // 메시 Read/Write가 꺼져 있으면 정점을 읽을 수 없으므로 합치지 않는다. (플레이에서는 오류가 난다)
            if (skin.sharedMesh == null || !skin.sharedMesh.isReadable || skin.sharedMesh.blendShapeCount > 0) return null;
            key.Append(skin.sharedMesh.GetInstanceID()).Append('|');
            foreach (Material material in skin.sharedMaterials)
            {
                key.Append(material != null ? material.GetInstanceID() : 0).Append(',');
            }

            key.Append(';');
        }

        return key.ToString();
    }

    // 지금 자세 그대로 구운 정점(모델 기준 좌표)과, 지금 뼈 위치로 계산한 바인드포즈를 짝으로 저장한다.
    // 정점과 바인드포즈가 같은 자세에서 나왔으므로 어떤 동작을 해도 올바르게 스키닝된다.
    private static Cache Build(Transform model, SkinnedMeshRenderer[] skins)
    {
        List<Transform> bones = new List<Transform>();
        Dictionary<Transform, int> boneIndex = new Dictionary<Transform, int>();
        List<Material> materials = new List<Material>();
        List<List<int>> triangles = new List<List<int>>();
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector4> tangents = new List<Vector4>();
        List<Vector2> uvs = new List<Vector2>();
        List<BoneWeight> weights = new List<BoneWeight>();
        Matrix4x4 toModel = model.worldToLocalMatrix;
        Mesh baked = new Mesh();

        foreach (SkinnedMeshRenderer skin in skins)
        {
            Mesh source = skin.sharedMesh;
            Transform[] skinBones = skin.bones;
            BoneWeight[] sourceWeights = source.boneWeights;
            if (sourceWeights.Length != source.vertexCount || skinBones.Length == 0) return null;

            int[] remap = new int[skinBones.Length];
            for (int b = 0; b < skinBones.Length; b++)
            {
                Transform bone = skinBones[b];
                if (bone == null) return null;
                if (!boneIndex.TryGetValue(bone, out int index))
                {
                    index = bones.Count;
                    bones.Add(bone);
                    boneIndex.Add(bone, index);
                }

                remap[b] = index;
            }

            skin.BakeMesh(baked, true);
            Matrix4x4 matrix = toModel * skin.transform.localToWorldMatrix;
            int offset = vertices.Count;
            Vector3[] bakedVertices = baked.vertices;
            Vector3[] bakedNormals = baked.normals;
            Vector4[] bakedTangents = baked.tangents;
            Vector2[] sourceUvs = source.uv;
            for (int v = 0; v < bakedVertices.Length; v++)
            {
                vertices.Add(matrix.MultiplyPoint3x4(bakedVertices[v]));
                normals.Add(v < bakedNormals.Length ? matrix.MultiplyVector(bakedNormals[v]).normalized : Vector3.up);
                Vector4 t = v < bakedTangents.Length ? bakedTangents[v] : new Vector4(1f, 0f, 0f, 1f);
                Vector3 dir = matrix.MultiplyVector(t).normalized;
                tangents.Add(new Vector4(dir.x, dir.y, dir.z, t.w));
                uvs.Add(v < sourceUvs.Length ? sourceUvs[v] : Vector2.zero);
                BoneWeight w = sourceWeights[v];
                w.boneIndex0 = remap[w.boneIndex0];
                w.boneIndex1 = remap[w.boneIndex1];
                w.boneIndex2 = remap[w.boneIndex2];
                w.boneIndex3 = remap[w.boneIndex3];
                weights.Add(w);
            }

            Material[] skinMaterials = skin.sharedMaterials;
            for (int s = 0; s < source.subMeshCount; s++)
            {
                Material material = skinMaterials[Mathf.Min(s, skinMaterials.Length - 1)];
                int slot = materials.IndexOf(material);
                if (slot < 0)
                {
                    slot = materials.Count;
                    materials.Add(material);
                    triangles.Add(new List<int>());
                }

                int[] sourceTriangles = source.GetTriangles(s);
                for (int i = 0; i < sourceTriangles.Length; i++)
                {
                    triangles[slot].Add(sourceTriangles[i] + offset);
                }
            }
        }

        Object.Destroy(baked);

        Mesh mesh = new Mesh { name = "ZombieMerged" };
        if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTangents(tangents);
        mesh.SetUVs(0, uvs);
        mesh.boneWeights = weights.ToArray();
        mesh.subMeshCount = materials.Count;
        for (int s = 0; s < materials.Count; s++)
        {
            mesh.SetTriangles(triangles[s], s);
        }

        Matrix4x4 modelToWorld = model.localToWorldMatrix;
        Matrix4x4[] bindposes = new Matrix4x4[bones.Count];
        string[] boneNames = new string[bones.Count];
        for (int b = 0; b < bones.Count; b++)
        {
            bindposes[b] = bones[b].worldToLocalMatrix * modelToWorld;
            boneNames[b] = bones[b].name;
        }

        mesh.bindposes = bindposes;
        mesh.RecalculateBounds();

        Transform root = skins[0].rootBone != null ? skins[0].rootBone : bones[0];
        return new Cache { Mesh = mesh, BoneNames = boneNames, RootBoneName = root.name, Materials = materials.ToArray() };
    }

    private static bool Apply(Transform model, SkinnedMeshRenderer[] skins, Cache cache)
    {
        Dictionary<string, Transform> byName = new Dictionary<string, Transform>();
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
        {
            if (!byName.ContainsKey(t.name)) byName.Add(t.name, t);
        }

        Transform[] bones = new Transform[cache.BoneNames.Length];
        for (int b = 0; b < bones.Length; b++)
        {
            if (!byName.TryGetValue(cache.BoneNames[b], out bones[b])) return false;  // 뼈 구조가 다르면 합치지 않는다.
        }

        GameObject merged = new GameObject("Z_Merged");
        merged.layer = skins[0].gameObject.layer;
        merged.transform.SetParent(model, false);

        SkinnedMeshRenderer renderer = merged.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = cache.Mesh;
        renderer.sharedMaterials = cache.Materials;
        renderer.bones = bones;
        renderer.rootBone = byName.TryGetValue(cache.RootBoneName, out Transform root) ? root : bones[0];
        renderer.updateWhenOffscreen = false;
        renderer.shadowCastingMode = skins[0].shadowCastingMode;
        renderer.receiveShadows = skins[0].receiveShadows;
        renderer.quality = skins[0].quality;
        renderer.lightProbeUsage = skins[0].lightProbeUsage;
        renderer.localBounds = ToRootBounds(cache.Mesh.bounds, model, renderer.rootBone);

        foreach (SkinnedMeshRenderer skin in skins)
        {
            Object.Destroy(skin);
        }

        return true;
    }

    // 메시 경계(모델 기준)를 루트 뼈 기준으로 옮기고 동작으로 팔다리가 나가도 잘리지 않게 넉넉히 키운다.
    private static Bounds ToRootBounds(Bounds bounds, Transform model, Transform rootBone)
    {
        Matrix4x4 matrix = rootBone.worldToLocalMatrix * model.localToWorldMatrix;
        Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
        float size = bounds.size.magnitude / Mathf.Max(0.0001f, rootBone.lossyScale.x) * model.lossyScale.x * 1.2f;
        return new Bounds(center, Vector3.one * size);
    }
}
