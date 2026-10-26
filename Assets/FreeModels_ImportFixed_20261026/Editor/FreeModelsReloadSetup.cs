#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using FreeModelsImportFixed20261026;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FreeModelsImportFixed20261026.Editor
{
    internal static class FreeModelsReloadSetup
    {
        private const string Root = "Assets/FreeModels_ImportFixed_20261026";
        private const string ViewName = "__FreeModels_FirstPerson";
        private const string LeftName = "__FreeModels_ReloadLeft";
        private const string RightName = "__FreeModels_ReloadRight";
        private const string MagazineName = "__FreeModels_Magazine";

        [Serializable] private sealed class ReloadCatalog
        {
            public string leftArmData, rightArmData;
            public Vector3 leftWrist, rightWrist;
            public Weapon[] weapons;
        }
        [Serializable] private sealed class Weapon
        {
            public string weaponId, bodyData, magazineData;
            public Vector3 magazinePivot, gripOffset;
        }

        [MenuItem("Tools/Free Models Fixed/Apply Reload Motion")]
        public static void ApplyMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            { Debug.LogWarning("[Free Models V7] Play를 종료하고 Import/컴파일이 끝난 뒤 실행하세요."); return; }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            { Debug.LogWarning("[Free Models V7] 프리팹 편집을 닫고 GameScene에서 실행하세요."); return; }
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;
            Transform view = FindView(scene);
            if (view == null) { FreeModelsImportFixedSetup.ApplyToCurrentScene(); return; }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add first person magazine reload motion");
            try
            {
                ApplyToScene(scene);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log("[Free Models V7] 탄창/양손 재장전 모션 연결 완료. Ctrl+S로 저장하고 Game에서 R키로 확인하세요.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        internal static void ApplyToScene(Scene scene)
        {
            Transform view = FindView(scene);
            if (view == null) throw new InvalidOperationException("1인칭 총 모델을 찾지 못했습니다. Apply Models to Current Scene을 먼저 실행하세요.");
            Transform gun = null;
            foreach (Transform child in view)
                if (child.name == "fps_rifle" || child.name == "fps_smg" || child.name == "fps_pistol") { gun = child; break; }
            Transform originalArms = view.Find("FPS_Arms");
            if (gun == null || originalArms == null) throw new InvalidOperationException("기본 1인칭 총/팔 모델을 찾지 못했습니다. Apply Models to Current Scene을 실행하세요.");
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/ReloadCatalogV7.json");
            if (source == null) throw new InvalidOperationException("재장전 메시 카탈로그가 없습니다. V7 전체를 Import하세요.");
            ReloadCatalog catalog = JsonUtility.FromJson<ReloadCatalog>(source.text);
            if (catalog == null || catalog.weapons == null) throw new InvalidOperationException("재장전 메시 카탈로그가 올바르지 않습니다.");
            Weapon weapon = Array.Find(catalog.weapons, item => item.weaponId == gun.name);
            if (weapon == null) throw new InvalidOperationException("이 총의 재장전 메시가 없습니다: " + gun.name);

            // Load all persistent meshes before changing the scene hierarchy.
            Mesh body = FreeModelsBakedMeshes.Load(weapon.bodyData);
            Mesh magazine = FreeModelsBakedMeshes.Load(weapon.magazineData);
            Mesh left = FreeModelsBakedMeshes.Load(catalog.leftArmData);
            Mesh right = FreeModelsBakedMeshes.Load(catalog.rightArmData);
            MeshFilter bodyFilter = null, armsFilter = null;
            foreach (MeshFilter filter in gun.GetComponentsInChildren<MeshFilter>(true))
                if (filter.name == weapon.weaponId + "_Mesh") { bodyFilter = filter; break; }
            foreach (MeshFilter filter in originalArms.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && filter.sharedMesh.name == "fps_arms") { armsFilter = filter; break; }
            if (bodyFilter == null || armsFilter == null) throw new InvalidOperationException("원래 총/팔 메시를 찾지 못했습니다. 기본 모델을 다시 적용하세요.");
            MeshRenderer bodyRenderer = bodyFilter.GetComponent<MeshRenderer>();
            MeshRenderer armsRenderer = armsFilter.GetComponent<MeshRenderer>();
            if (bodyRenderer == null || armsRenderer == null) throw new InvalidOperationException("총/팔 MeshRenderer가 없습니다.");

            FreeModelsReloadMotion oldMotion = view.GetComponent<FreeModelsReloadMotion>();
            if (oldMotion != null) Undo.DestroyObjectImmediate(oldMotion);
            foreach (Transform child in new List<Transform>(view.GetComponentsInChildren<Transform>(true)))
                if (child != null && (child.name == LeftName || child.name == RightName || child.name == MagazineName))
                    Undo.DestroyObjectImmediate(child.gameObject);

            Undo.RecordObject(bodyFilter, "Separate the removable magazine from the gun");
            bodyFilter.sharedMesh = body;
            RecordPrefab(bodyFilter);
            Undo.RecordObject(armsRenderer, "Replace combined arms with independent arm models");
            armsRenderer.enabled = false;
            RecordPrefab(armsRenderer);
            Transform leftArm = CreatePart(LeftName, armsFilter.transform, catalog.leftWrist, left, armsRenderer.sharedMaterials);
            Transform rightArm = CreatePart(RightName, armsFilter.transform, catalog.rightWrist, right, armsRenderer.sharedMaterials);
            // The right hand keeps its starting pose and follows the gun while it tilts.
            Undo.SetTransformParent(rightArm, gun, "Attach the trigger hand to the gun");
            Transform magazinePart = CreatePart(MagazineName, bodyFilter.transform, weapon.magazinePivot, magazine, bodyRenderer.sharedMaterials);
            FreeModelsReloadMotion motion = Undo.AddComponent<FreeModelsReloadMotion>(view.gameObject);
            Assign(motion, "_gun", gun);
            Assign(motion, "_leftArm", leftArm);
            Assign(motion, "_magazine", magazinePart);
            SerializedObject motionData = new SerializedObject(motion);
            motionData.FindProperty("_gripOffset").vector3Value = weapon.gripOffset;
            motionData.ApplyModifiedProperties();

            int players = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (PlayerGunFire fire in root.GetComponentsInChildren<PlayerGunFire>(true))
                { Assign(fire, "_reloadMotion", motion); players++; }
            if (players == 0) Debug.LogWarning("[Free Models V7] PlayerGunFire를 찾지 못했습니다. 기존 Player 발사 컴포넌트를 확인하세요.");
            Debug.Log("[Free Models V7] " + gun.name + "의 분리 탄창/양손 → PlayerGunFire " + players + "개 재장전 타이머에 연결했습니다.");
        }

        private static Transform CreatePart(string name, Transform parent, Vector3 position, Mesh mesh, Material[] materials)
        {
            GameObject owner = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            Undo.RegisterCreatedObjectUndo(owner, "Add reload model part");
            owner.transform.SetParent(parent, false);
            owner.transform.localPosition = position;
            owner.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = owner.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            owner.layer = parent.gameObject.layer;
            return owner.transform;
        }

        private static void Assign(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            Undo.RecordObject(target, "Connect reload animation");
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException("V7 스크립트 필드가 없습니다: " + field + ". 전체 Import하세요.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            RecordPrefab(target);
        }

        private static void RecordPrefab(UnityEngine.Object target)
        { if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target); }

        private static Transform FindView(Scene scene)
        {
            if (Camera.main != null && Camera.main.gameObject.scene == scene)
            {
                Transform found = Camera.main.transform.Find(ViewName);
                if (found != null) return found;
            }
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    Transform found = camera.transform.Find(ViewName);
                    if (found != null) return found;
                }
            return null;
        }
    }
}
#endif
