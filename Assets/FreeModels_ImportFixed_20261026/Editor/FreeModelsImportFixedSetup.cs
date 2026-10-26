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
    // Editor setup for model visuals and the V6 combat/minimap connections.
    public static class FreeModelsImportFixedSetup
    {
        private const string Root = "Assets/FreeModels_ImportFixed_20261026";
        private const string VisualName = "__FreeModels_Visual";
        private const string BackgroundName = "FreeModels_Background";
        private const string FirstPersonName = "__FreeModels_FirstPerson";

        [Serializable] private sealed class Catalog { public Entry[] models; }
        [Serializable] private sealed class Entry
        {
            public string id;
            public string path;
            public string texture;
            public float height = 1f;
            public float rotationY;
            public string meshData;
            public bool preserveOrigin;
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
            Camera viewCamera = FindGameplayCamera(scene, originalObjects);
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
                    if (target == null || IsGenerated(target)) continue;
                    string kind = GetTargetKind(target.gameObject);
                    string id = null;
                    if (kind == "Zombie") id = zombies % 2 == 0 ? "zombie_a" : "zombie_b";
                    else if (kind == "Player") id = "survivor";
                    else if (kind == "Drum") id = "barrel";
                    else if (kind == "Box") id = "crate";
                    if (id == null) continue;

                    // Refresh our existing child as well: V3/V4 instances may already have
                    // a cached horizontal FBX pose. Leave the gameplay object untouched.
                    Transform previous = target.Find(VisualName);
                    if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);

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
                    if (kind == "Player" && viewCamera != null)
                    {
                        // Avoid drawing the local player's head around the FPS camera.
                        // The full standing survivor remains available as a model prefab.
                        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                            renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    }

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
                    if (target == null || IsGenerated(target)) continue;
                    if (target.name == "Ground") RecolorPrimitive(target, groundMaterial);
                    else if (HasAncestorNamed(target, "Map") && GetTargetKind(target.gameObject) == null)
                        RecolorPrimitive(target, wallMaterial);
                }

                bool addedBackground = !originalObjects.Exists(t => t != null && t.name == BackgroundName);
                if (addedBackground) AddBackground(scene, prefabs, arena, floorY);
                if (viewCamera != null) ApplyFirstPersonModel(viewCamera, prefabs, "fps_rifle");
                else Debug.LogWarning("[Free Models] 게임용 Camera를 찾지 못했습니다. MainCamera 태그를 확인하고 메뉴를 다시 실행하세요.");
                if (viewCamera != null) FreeModelsCombatSetup.ApplyToScene(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                Debug.Log(string.Format(
                    "[Free Models] 외형 적용 완료: Player {0}, Zombie {1}, Drum {2}, Box {3}. " +
                    "서 있는 캐릭터/1인칭 총/총구화염/좀비 피/미니맵 줌/재장전 모션을 적용했습니다. Ctrl+S로 저장하세요. Ctrl+Z로 되돌릴 수 있습니다.",
                    players, zombies, drums, boxes));
                Transform map = originalObjects.Find(t => t != null && t.name == "Map");
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

        [MenuItem("Tools/Free Models Fixed/First Person Weapon/Rifle")]
        public static void UseRifle() { ApplyWeaponOnly("fps_rifle"); }

        [MenuItem("Tools/Free Models Fixed/First Person Weapon/SMG")]
        public static void UseSmg() { ApplyWeaponOnly("fps_smg"); }

        [MenuItem("Tools/Free Models Fixed/First Person Weapon/Pistol")]
        public static void UsePistol() { ApplyWeaponOnly("fps_pistol"); }

        private static void ApplyWeaponOnly(string id)
        {
            if (!CanEdit()) return;
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;
            Camera camera = FindGameplayCamera(scene, GetSceneTransforms(scene));
            if (camera == null) { Debug.LogWarning("[Free Models] MainCamera를 찾지 못했습니다."); return; }
            Dictionary<string, GameObject> prefabs = BuildLibrary();
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Set first person weapon model");
            try
            {
                ApplyFirstPersonModel(camera, prefabs, id);
                FreeModelsCombatSetup.ApplyToScene(scene, false);
                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(group);
                Debug.Log("[Free Models] 1인칭 외형 변경: " + id + ". 발사 기능과 스탯은 기존 설정을 사용합니다. Ctrl+S로 저장하세요.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
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

        private static Camera FindGameplayCamera(Scene scene, List<Transform> objects)
        {
            Camera main = Camera.main;
            if (main != null && main.gameObject.scene == scene && !main.orthographic) return main;
            Camera childCamera = null;
            Camera fallback = null;
            foreach (Transform target in objects)
            {
                if (target == null || IsGenerated(target)) continue;
                Camera camera = target.GetComponent<Camera>();
                if (camera == null || camera.orthographic || camera.cameraType != CameraType.Game) continue;
                if (camera.CompareTag("MainCamera")) return camera;
                if (camera.isActiveAndEnabled && fallback == null) fallback = camera;
                for (Transform parent = camera.transform.parent; parent != null; parent = parent.parent)
                    if (GetTargetKind(parent.gameObject) == "Player") { childCamera = camera; break; }
            }
            return childCamera != null ? childCamera : fallback;
        }

        private static void ApplyFirstPersonModel(Camera camera, Dictionary<string, GameObject> prefabs, string id)
        {
            Transform old = camera.transform.Find(FirstPersonName);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            GameObject root = new GameObject(FirstPersonName);
            SceneManager.MoveGameObjectToScene(root, camera.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(root, "Attach first person gun and arms");
            root.transform.SetParent(camera.transform, false);
            root.transform.localPosition = new Vector3(0f, -0.01f, 0.12f);
            Vector3 cameraScale = camera.transform.lossyScale;
            root.transform.localScale = new Vector3(
                1f / Mathf.Max(Mathf.Abs(cameraScale.x), 0.0001f),
                1f / Mathf.Max(Mathf.Abs(cameraScale.y), 0.0001f),
                1f / Mathf.Max(Mathf.Abs(cameraScale.z), 0.0001f));

            // Both meshes have authored Unity coordinates; their +Z barrel direction is
            // the existing camera's forward. Parenting handles the existing look controls.
            GameObject gun = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[id], root.transform);
            Undo.RegisterCreatedObjectUndo(gun, "Add gun model");
            gun.name = id;
            gun.transform.localPosition = new Vector3(0.225f, -0.18f, 0.59f);
            gun.transform.localRotation = Quaternion.identity;
            gun.transform.localScale = Vector3.one;

            GameObject arms = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["fps_arms"], root.transform);
            Undo.RegisterCreatedObjectUndo(arms, "Add first person arms");
            arms.name = "FPS_Arms";
            arms.transform.localPosition = Vector3.zero;
            arms.transform.localRotation = Quaternion.identity;
            arms.transform.localScale = Vector3.one;
            if (id == "fps_smg") arms.transform.localPosition = new Vector3(0f, 0f, -0.025f);
            else if (id == "fps_pistol")
            {
                arms.transform.localScale = new Vector3(1f, 1f, 0.78f);
                arms.transform.localPosition = new Vector3(0f, 0f, 0.09f);
            }

            int layer = 2; // Ignore Raycast; no colliders are added in any case.
            if ((camera.cullingMask & (1 << layer)) == 0)
            {
                layer = -1;
                for (int candidate = 0; candidate < 32; candidate++)
                    if ((camera.cullingMask & (1 << candidate)) != 0) { layer = candidate; break; }
                if (layer < 0) throw new InvalidOperationException("MainCamera의 Culling Mask가 Nothing입니다. 렌더링할 레이어를 활성화하세요.");
            }
            SetLayerRecursively(root.transform, layer);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }
            if (camera.nearClipPlane > 0.03f)
            {
                Undo.RecordObject(camera, "Set first person near clipping plane");
                camera.nearClipPlane = 0.03f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
            }
            foreach (Transform target in GetSceneTransforms(camera.gameObject.scene))
            {
                if (target == null || IsGenerated(target) || GetTargetKind(target.gameObject) != "Player") continue;
                Transform body = target.Find(VisualName);
                if (body == null) continue;
                foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
                {
                    Undo.RecordObject(renderer, "Hide local player body in first person");
                    renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
            }
            Debug.Log("[Free Models] 1인칭 총/팔을 " + camera.name + " 아래에 배치했습니다. Game 탭에서 확인하세요.");
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
                if (!string.IsNullOrEmpty(entry.meshData))
                {
                    FreeModelsBakedMeshes.Load(entry.meshData);
                    FreeModelsTextureRecovery.Load(entry.texture);
                    continue;
                }
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
                if (existing != null && string.IsNullOrEmpty(entry.meshData)) { result.Add(entry.id, existing); continue; }
                GameObject wrapper = new GameObject(entry.id);
                try
                {
                    GameObject model;
                    if (!string.IsNullOrEmpty(entry.meshData))
                    {
                        model = new GameObject(entry.id + "_Mesh");
                        model.transform.SetParent(wrapper.transform, false);
                        model.AddComponent<MeshFilter>().sharedMesh = FreeModelsBakedMeshes.Load(entry.meshData);
                        model.AddComponent<MeshRenderer>();
                    }
                    else
                    {
                        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + entry.path);
                        model = (GameObject)PrefabUtility.InstantiatePrefab(source, wrapper.transform);
                        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    }
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
                    model.transform.localPosition = entry.preserveOrigin ? Vector3.zero :
                        new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
                    GameObject saved = PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
                    if (saved == null) throw new InvalidOperationException("프리팹 저장 실패: " + entry.id);
                    result.Add(entry.id, saved);
                }
                finally { UnityEngine.Object.DestroyImmediate(wrapper); }
            }
            foreach (string required in new[] { "survivor", "zombie_a", "zombie_b", "barrel", "crate", "warehouse_a", "warehouse_b", "warehouse_c", "water_tower", "storage_tank", "container_a", "container_b", "planks", "fence", "fps_rifle", "fps_smg", "fps_pistol", "fps_arms" })
                if (!result.ContainsKey(required)) throw new InvalidOperationException("필수 모델 누락: " + required);
            return result;
        }

        private static void PrepareNativeImportSettings(Catalog catalog)
        {
            HashSet<string> paths = new HashSet<string>();
            foreach (Entry entry in catalog.models)
            {
                if (!string.IsNullOrEmpty(entry.meshData)) continue;
                paths.Add(Root + "/" + entry.path);
            }
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

        private static Material GetMaterial(string name, string texturePath, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Texture2D texture = string.IsNullOrEmpty(texturePath) ? null : FreeModelsTextureRecovery.Load(texturePath);
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
            else shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("현재 렌더 파이프라인의 Lit 셰이더를 찾지 못했습니다.");
            bool created = material == null;
            if (created) material = new Material(shader) { name = name };
            else if (material.shader != shader) material.shader = shader;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColorMap")) material.SetTexture("_BaseColorMap", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.18f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (created) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
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
            return HasAncestorNamed(target, VisualName) || HasAncestorNamed(target, BackgroundName) || HasAncestorNamed(target, FirstPersonName);
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
