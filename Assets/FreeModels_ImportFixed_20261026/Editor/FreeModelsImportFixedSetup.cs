#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FreeModelsImportFixed20261026.Editor
{
    // Editor-only visual setup. Does not depend on, copy, or replace gameplay scripts.
    public static class FreeModelsImportFixedSetup
    {
        private const string Root = "Assets/FreeModels_ImportFixed_20261026";
        private const string VisualName = "__FreeModels_Visual";
        private const string BackgroundName = "FreeModels_Background";

        [Serializable] private sealed class Catalog { public Entry[] models; }
        [Serializable] private sealed class Entry
        {
            public string id;
            public string path;
            public string texture;
            public float height = 1f;
            public float rotationY;
        }

        [MenuItem("Tools/Free Models Fixed/Apply Models to Current Scene")]
        public static void ApplyToCurrentScene()
        {
            if (!CanEdit()) return;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogWarning("[Free Models] 프리팹 편집 모드를 닫고 GameScene을 연 뒤 실행하세요.");
                return;
            }

            // Load and validate everything before touching the user's scene.
            Dictionary<string, GameObject> prefabs = BuildLibrary();
            List<Transform> originalObjects = GetSceneTransforms(scene);
            Bounds arena = GetArenaBounds(originalObjects);
            float floorY = GetFloorY(originalObjects, arena);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply free models (visuals only)");
            int players = 0, zombies = 0, drums = 0, boxes = 0;
            bool completed = false;

            try
            {
                foreach (Transform target in originalObjects)
                {
                    if (IsGenerated(target) || target.Find(VisualName) != null) continue;
                    string kind = GetTargetKind(target.gameObject);
                    string id = null;
                    if (kind == "Zombie") id = zombies % 2 == 0 ? "zombie_a" : "zombie_b";
                    else if (kind == "Player") id = "survivor";
                    else if (kind == "Drum") id = "barrel";
                    else if (kind == "Box") id = "crate";
                    if (id == null) continue;

                    Bounds slot = GetLocalSlot(target.gameObject, kind);
                    GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[id], target);
                    Undo.RegisterCreatedObjectUndo(visual, "Attach model visual");
                    visual.name = VisualName;
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localRotation = Quaternion.identity;
                    visual.transform.localScale = Vector3.one;
                    Bounds modelBounds = GetLocalMeshBounds(visual.transform);
                    Vector3 size = modelBounds.size;
                    bool character = kind == "Player" || kind == "Zombie";
                    float scale = slot.size.y / Mathf.Max(size.y, 0.001f);
                    Vector3 fit = character ? Vector3.one * scale : new Vector3(
                        slot.size.x / Mathf.Max(size.x, 0.001f), scale,
                        slot.size.z / Mathf.Max(size.z, 0.001f));
                    visual.transform.localScale = fit;
                    visual.transform.localPosition = slot.center - Vector3.Scale(modelBounds.center, fit);
                    SetLayerRecursively(visual.transform, target.gameObject.layer);

                    // Only replace the root primitive body. Never disable particles, UI, or other children.
                    MeshRenderer oldBody = target.GetComponent<MeshRenderer>();
                    if (oldBody != null)
                    {
                        Undo.RecordObject(oldBody, "Hide primitive visual");
                        oldBody.enabled = false;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(oldBody);
                    }
                    if (kind == "Player") players++;
                    else if (kind == "Zombie") zombies++;
                    else if (kind == "Drum") drums++;
                    else if (kind == "Box") boxes++;
                }

                Material groundMaterial = GetMaterial("Ground_Concrete", null, new Color(0.30f, 0.33f, 0.34f));
                Material wallMaterial = GetMaterial("Wall_Concrete", null, new Color(0.43f, 0.46f, 0.46f));
                foreach (Transform target in originalObjects)
                {
                    if (IsGenerated(target)) continue;
                    if (target.name == "Ground") RecolorPrimitive(target, groundMaterial);
                    else if (HasAncestorNamed(target, "Map") && GetTargetKind(target.gameObject) == null)
                        RecolorPrimitive(target, wallMaterial);
                }

                bool addedBackground = !originalObjects.Exists(t => t.name == BackgroundName);
                if (addedBackground) AddBackground(scene, prefabs, arena, floorY);
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                Debug.Log(string.Format(
                    "[Free Models] 외형 적용 완료: Player {0}, Zombie {1}, Drum {2}, Box {3}. " +
                    "기존 스크립트/충돌체/카메라/UI 설정은 유지했습니다. Ctrl+S로 씬을 저장하세요. Ctrl+Z로 되돌릴 수 있습니다.",
                    players, zombies, drums, boxes));
                Transform map = originalObjects.Find(t => t.name == "Map");
                if (map != null && SceneView.lastActiveSceneView != null)
                {
                    Selection.activeGameObject = map.gameObject;
                    SceneView.lastActiveSceneView.FrameSelected();
                }
                completed = true;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
            finally { if (completed) Undo.CollapseUndoOperations(undoGroup); }
        }

        [MenuItem("Tools/Free Models Fixed/Build Model Prefabs Only")]
        public static void BuildPrefabsOnly()
        {
            if (!CanEdit()) return;
            Dictionary<string, GameObject> prefabs = BuildLibrary();
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<DefaultAsset>(Root + "/Prefabs");
            EditorGUIUtility.PingObject(Selection.activeObject);
            Debug.Log("[Free Models] 모델 프리팹 " + prefabs.Count + "개를 준비했습니다. Prefabs 폴더에서 씬으로 드래그할 수 있습니다.");
        }

        private static bool CanEdit()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating) return true;
            Debug.LogWarning("[Free Models] Play 모드를 종료하고 컴파일과 파일 Import가 끝난 뒤 실행하세요.");
            return false;
        }

        private static Dictionary<string, GameObject> BuildLibrary()
        {
            TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/ModelCatalog.json");
            if (text == null) throw new InvalidOperationException("모델 카탈로그가 없습니다. 패키지 전체를 Import하세요.");
            Catalog catalog = JsonUtility.FromJson<Catalog>(text.text);
            if (catalog == null || catalog.models == null || catalog.models.Length == 0)
                throw new InvalidOperationException("모델 카탈로그가 비어 있습니다.");

            PrepareNativeImportSettings(catalog);

            foreach (Entry entry in catalog.models)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + entry.path) == null)
                {
                    AssetDatabase.ImportAsset(Root + "/" + entry.path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + entry.path) == null)
                        throw new FileNotFoundException("모델을 찾을 수 없습니다: " + entry.path);
                }
                FreeModelsTextureRecovery.Load(entry.texture);
            }
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Meshes");
            Dictionary<string, GameObject> result = new Dictionary<string, GameObject>();
            foreach (Entry entry in catalog.models)
            {
                string prefabPath = Root + "/Prefabs/" + entry.id + ".prefab";
                Material material = GetMaterial(Path.GetFileNameWithoutExtension(entry.texture), entry.texture, Color.white);
                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (existing != null) { result.Add(entry.id, existing); continue; }
                GameObject wrapper = new GameObject(entry.id);
                try
                {
                    GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + entry.path);
                    GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source, wrapper.transform);
                    PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    FreezeCharacterGeometry(model, entry.id);
                    model.transform.localRotation = Quaternion.Euler(0f, entry.rotationY, 0f) * model.transform.localRotation;
                    model.transform.localPosition = Vector3.zero;
                    Vector3 importedScale = model.transform.localScale;
                    foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                    {
                        Material[] slots = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                        for (int i = 0; i < slots.Length; i++) slots[i] = material;
                        renderer.sharedMaterials = slots;
                        renderer.shadowCastingMode = ShadowCastingMode.On;
                        renderer.receiveShadows = true;
                    }
                    Bounds bounds = GetLocalMeshBounds(wrapper.transform);
                    float scale = entry.height / Mathf.Max(bounds.size.y, 0.001f);
                    model.transform.localScale = importedScale * scale;
                    model.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
                    GameObject saved = PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
                    if (saved == null) throw new InvalidOperationException("프리팹 저장 실패: " + entry.id);
                    result.Add(entry.id, saved);
                }
                finally { UnityEngine.Object.DestroyImmediate(wrapper); }
            }
            foreach (string required in new[] { "survivor", "zombie_a", "zombie_b", "barrel", "crate", "warehouse_a", "warehouse_b", "warehouse_c", "water_tower", "storage_tank", "container_a", "container_b", "planks", "fence" })
                if (!result.ContainsKey(required)) throw new InvalidOperationException("필수 모델 누락: " + required);
            return result;
        }

        private static void PrepareNativeImportSettings(Catalog catalog)
        {
            HashSet<string> paths = new HashSet<string>();
            foreach (Entry entry in catalog.models)
            {
                paths.Add(Root + "/" + entry.path);
            }
            paths.Add(Root + "/Source/idle.fbx");
            foreach (string path in paths)
            {
                ModelImporter model = AssetImporter.GetAtPath(path) as ModelImporter;
                if (model != null)
                {
                    bool animate = path.EndsWith("/Source/idle.fbx", StringComparison.Ordinal);
                    bool fbx = path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
                    bool changed = model.addCollider || model.materialImportMode != ModelImporterMaterialImportMode.None || model.importAnimation != animate;
                    if (fbx) changed |= model.animationType != ModelImporterAnimationType.Generic || model.optimizeGameObjects;
                    if (changed)
                    {
                        model.addCollider = false;
                        model.materialImportMode = ModelImporterMaterialImportMode.None;
                        model.importAnimation = animate;
                        if (fbx)
                        {
                            model.animationType = ModelImporterAnimationType.Generic;
                            model.optimizeGameObjects = false;
                        }
                        model.SaveAndReimport();
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    }
                    continue;
                }
            }
        }

        private static void FreezeCharacterGeometry(GameObject model, string id)
        {
            SkinnedMeshRenderer[] skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length == 0) return;

            // Sample one idle pose in the editor, then bake static geometry. No animation controller
            // or runtime animation behaviour is added to the user's existing character objects.
            AnimationClip idle = null;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(Root + "/Source/idle.fbx"))
            {
                AnimationClip candidate = asset as AnimationClip;
                if (candidate == null || candidate.name.StartsWith("__preview__", StringComparison.Ordinal)) continue;
                if (idle == null || candidate.length > idle.length) idle = candidate;
            }
            if (idle != null) idle.SampleAnimation(model, 0f);
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;

            for (int index = 0; index < skins.Length; index++)
            {
                SkinnedMeshRenderer skin = skins[index];
                string meshPath = Root + "/Meshes/" + id + "_" + index + ".asset";
                Mesh mesh = new Mesh { name = id + "_StaticPose_" + index };
                skin.BakeMesh(mesh);
                if (mesh.vertexCount == 0) throw new InvalidOperationException("캐릭터 메시 생성 실패: " + id);
                mesh.RecalculateBounds();
                Mesh stored = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (stored == null) AssetDatabase.CreateAsset(mesh, meshPath);
                else
                {
                    EditorUtility.CopySerialized(mesh, stored);
                    UnityEngine.Object.DestroyImmediate(mesh);
                    mesh = stored;
                }
                Material[] materials = skin.sharedMaterials;
                GameObject owner = skin.gameObject;
                UnityEngine.Object.DestroyImmediate(skin);
                MeshFilter filter = owner.GetComponent<MeshFilter>();
                if (filter == null) filter = owner.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                MeshRenderer renderer = owner.GetComponent<MeshRenderer>();
                if (renderer == null) renderer = owner.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
            }
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator);
            foreach (Animation animation in model.GetComponentsInChildren<Animation>(true)) UnityEngine.Object.DestroyImmediate(animation);
        }

        private static Material GetMaterial(string name, string texturePath, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Texture2D texture = string.IsNullOrEmpty(texturePath) ? null : FreeModelsTextureRecovery.Load(texturePath);
            if (material != null)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                if (material.HasProperty("_BaseColorMap")) material.SetTexture("_BaseColorMap", texture);
                EditorUtility.SetDirty(material);
                return material;
            }
            Shader shader = null;
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null)
            {
                string pipelineType = pipeline.GetType().Name;
                if (pipelineType.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0)
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                else if (pipelineType.IndexOf("HDRender", StringComparison.OrdinalIgnoreCase) >= 0)
                    shader = Shader.Find("HDRP/Lit");
            }
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("사용 가능한 Lit 셰이더를 찾지 못했습니다.");
            material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColorMap")) material.SetTexture("_BaseColorMap", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.18f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static List<Transform> GetSceneTransforms(Scene scene)
        {
            List<Transform> all = new List<Transform>();
            foreach (GameObject root in scene.GetRootGameObjects()) all.AddRange(root.GetComponentsInChildren<Transform>(true));
            return all;
        }

        private static string GetTargetKind(GameObject target)
        {
            foreach (MonoBehaviour component in target.GetComponents<MonoBehaviour>())
            {
                if (component == null) continue;
                string type = component.GetType().Name;
                if (type == "Player" || type == "Zombie" || type == "Drum" || type == "Box") return type;
            }
            return null;
        }

        private static Bounds GetLocalSlot(GameObject target, string kind)
        {
            CharacterController character = target.GetComponent<CharacterController>();
            if (character != null) return new Bounds(character.center, new Vector3(character.radius * 2f, character.height, character.radius * 2f));
            CapsuleCollider capsule = target.GetComponent<CapsuleCollider>();
            if (capsule != null && capsule.direction == 1) return new Bounds(capsule.center, new Vector3(capsule.radius * 2f, capsule.height, capsule.radius * 2f));
            BoxCollider box = target.GetComponent<BoxCollider>();
            if (box != null) return new Bounds(box.center, box.size);
            MeshFilter mesh = target.GetComponent<MeshFilter>();
            if (mesh != null && mesh.sharedMesh != null) return mesh.sharedMesh.bounds;
            return new Bounds(Vector3.zero, kind == "Player" || kind == "Zombie" ? new Vector3(0.8f, 2f, 0.8f) : Vector3.one);
        }

        private static Bounds GetLocalMeshBounds(Transform root)
        {
            Bounds result = new Bounds();
            bool found = false;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds b = filter.sharedMesh.bounds;
                Matrix4x4 matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = b.center + Vector3.Scale(b.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    p = matrix.MultiplyPoint3x4(p);
                    if (!found) { result = new Bounds(p, Vector3.zero); found = true; }
                    else result.Encapsulate(p);
                }
            }
            if (!found) throw new InvalidOperationException("모델에 MeshFilter가 없습니다: " + root.name);
            return result;
        }

        private static Bounds GetArenaBounds(List<Transform> objects)
        {
            Bounds bounds = new Bounds(Vector3.zero, new Vector3(24f, 2f, 24f));
            bool found = false;
            foreach (Transform t in objects)
            {
                if (!HasAncestorNamed(t, "Map") || IsGenerated(t)) continue;
                Collider collider = t.GetComponent<Collider>();
                Renderer renderer = t.GetComponent<MeshRenderer>();
                if (collider == null && renderer == null) continue;
                Bounds next = collider != null ? collider.bounds : renderer.bounds;
                if (!found) { bounds = next; found = true; } else bounds.Encapsulate(next);
            }
            if (!found)
            {
                foreach (Transform t in objects)
                    if (GetTargetKind(t.gameObject) == "Player") { bounds.center = t.position; break; }
            }
            // Huge ground planes are not treated as playable-map extents.
            Vector3 size = bounds.size;
            size.x = Mathf.Clamp(size.x, 16f, 160f);
            size.z = Mathf.Clamp(size.z, 16f, 160f);
            bounds.size = size;
            return bounds;
        }

        private static float GetFloorY(List<Transform> objects, Bounds arena)
        {
            foreach (Transform t in objects)
            {
                if (t.name != "Ground") continue;
                Collider collider = t.GetComponent<Collider>();
                if (collider != null) return collider.bounds.max.y;
                Renderer renderer = t.GetComponent<Renderer>();
                if (renderer != null) return renderer.bounds.max.y;
                return t.position.y;
            }
            return arena.min.y;
        }

        private static void AddBackground(Scene scene, Dictionary<string, GameObject> prefabs, Bounds arena, float y)
        {
            GameObject parent = new GameObject(BackgroundName);
            SceneManager.MoveGameObjectToScene(parent, scene);
            Undo.RegisterCreatedObjectUndo(parent, "Add industrial background");
            Vector3 c = arena.center;
            // Place decorative geometry beyond the existing map. No new collision or gameplay objects.
            Spawn(prefabs, "warehouse_a", parent.transform, new Vector3(arena.min.x - 11f, y, c.z - 4f), 90f);
            Spawn(prefabs, "warehouse_b", parent.transform, new Vector3(arena.max.x + 12f, y, c.z + 3f), -90f);
            Spawn(prefabs, "warehouse_c", parent.transform, new Vector3(c.x - 3f, y, arena.max.z + 12f), 180f);
            Spawn(prefabs, "water_tower", parent.transform, new Vector3(arena.min.x - 9f, y, arena.max.z + 9f), 0f);
            Spawn(prefabs, "storage_tank", parent.transform, new Vector3(arena.max.x + 7f, y, arena.max.z + 7f), 0f);
            Spawn(prefabs, "container_a", parent.transform, new Vector3(c.x + 8f, y, arena.max.z + 5f), 90f);
            Spawn(prefabs, "container_b", parent.transform, new Vector3(arena.min.x - 5f, y, c.z + 8f), 0f);
            Spawn(prefabs, "container_a", parent.transform, new Vector3(arena.max.x + 5f, y, c.z - 8f), 0f);
            Spawn(prefabs, "planks", parent.transform, new Vector3(c.x + 5f, y, arena.max.z + 3f), 15f);
            Spawn(prefabs, "planks", parent.transform, new Vector3(c.x + 5f, y + 0.2f, arena.max.z + 3f), -7f);
            Spawn(prefabs, "fence", parent.transform, new Vector3(c.x - 8f, y, arena.max.z + 3f), 0f);
            Spawn(prefabs, "fence", parent.transform, new Vector3(c.x - 6f, y, arena.max.z + 3f), 0f);
        }

        private static void Spawn(Dictionary<string, GameObject> prefabs, string id, Transform parent, Vector3 position, float yaw)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[id], parent);
            Undo.RegisterCreatedObjectUndo(instance, "Place background model");
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }

        private static void RecolorPrimitive(Transform target, Material material)
        {
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled) return;
            Undo.RecordObject(renderer, "Set environment visual material");
            Material[] slots = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }

        private static bool HasAncestorNamed(Transform target, string name)
        {
            for (Transform current = target; current != null; current = current.parent)
                if (current.name == name) return true;
            return false;
        }

        private static bool IsGenerated(Transform target)
        {
            return HasAncestorNamed(target, VisualName) || HasAncestorNamed(target, BackgroundName);
        }

        private static void SetLayerRecursively(Transform target, int layer)
        {
            target.gameObject.layer = layer;
            foreach (Transform child in target) SetLayerRecursively(child, layer);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
#endif
