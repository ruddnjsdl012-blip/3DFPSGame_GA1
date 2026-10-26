#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using FreeModelsImportFixed20261026;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FreeModelsImportFixed20261026.Editor
{
    internal static class FreeModelsCombatSetup
    {
        private const string Root = "Assets/FreeModels_ImportFixed_20261026";
        private const string EffectFolder = Root + "/EffectsV6";
        private const string ViewModelName = "__FreeModels_FirstPerson";
        private const string MuzzleName = "__FreeModels_MuzzleFX";

        [MenuItem("Tools/Free Models Fixed/Apply Combat FX and Minimap")]
        public static void ApplyMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            { Debug.LogWarning("[Free Models] Play를 종료하고 Import/컴파일이 끝난 뒤 실행하세요."); return; }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            { Debug.LogWarning("[Free Models] 프리팹 편집을 닫고 GameScene에서 실행하세요."); return; }
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;
            Camera camera = FindViewCamera(scene);
            if (camera == null || camera.transform.Find(ViewModelName) == null)
            {
                // A fresh import can use this same menu to prepare the V5 models as well.
                FreeModelsImportFixedSetup.ApplyToCurrentScene();
                return;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Connect muzzle flash, blood, and minimap zoom");
            try
            {
                ApplyToScene(scene);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                Undo.CollapseUndoOperations(group);
                Debug.Log("[Free Models V6] 총구화염/피격 피/미니맵 줌 연결 완료. Ctrl+S로 저장하세요. 왼쪽 Alt: 커서 표시, 오른쪽 Alt: FPS 복귀.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }

        internal static void ApplyToScene(Scene scene, bool connectMinimap = true)
        {
            GameObject blood = BuildAssets();
            Camera camera = FindViewCamera(scene);
            if (camera == null) throw new InvalidOperationException("게임용 MainCamera를 찾지 못했습니다.");
            Transform viewModel = camera.transform.Find(ViewModelName);
            if (viewModel == null) throw new InvalidOperationException("먼저 Apply Models to Current Scene으로 1인칭 총을 준비하세요.");
            Transform gun = null;
            foreach (Transform child in viewModel)
                if (child.name.StartsWith("fps_", StringComparison.Ordinal) && child.name != "fps_arms") { gun = child; break; }
            if (gun == null) throw new InvalidOperationException("1인칭 총 모델을 찾지 못했습니다.");
            FreeModelsMuzzleFlash muzzle = AttachMuzzle(gun);
            int players = 0, zombies = 0;
            foreach (Transform target in SceneTransforms(scene))
            {
                PlayerGunFire fire = target.GetComponent<PlayerGunFire>();
                if (fire != null) { AssignReference(fire, "_muzzleFlash", muzzle); players++; }
                Zombie zombie = target.GetComponent<Zombie>();
                if (zombie != null) { AssignReference(zombie, "_bloodSprayPrefab", blood); zombies++; }
            }
            if (players == 0) Debug.LogWarning("[Free Models V6] 씬에 PlayerGunFire가 없습니다. 기존 발사 컴포넌트를 확인하세요.");
            if (connectMinimap) ConnectMinimap(scene);
            Debug.Log("[Free Models V6] 실제 Shoot 호출에 연결한 총 " + players + "개, 피 이펙트를 연결한 좀비 " + zombies + "개.");
        }

        private static GameObject BuildAssets()
        {
            EnsureFolder(EffectFolder);
            Material bloodMaterial = GetParticleMaterial("Blood_Alpha", "Textures/FX_SoftParticle.png", false);
            string path = EffectFolder + "/BloodHit.prefab";
            GameObject root = new GameObject("FX_BloodHit_V6");
            root.SetActive(false);
            try
            {
                root.AddComponent<FreeModelsEffectLifetime>();
                ParticleSystem mist = AddParticles(root.transform, "Blood_Mist", bloodMaterial, true, false);
                ConfigureBurst(mist, 14, 0.16f, 0.30f, 1.1f, 2.3f, 0.12f, 0.23f,
                    new Color(0.42f, 0.012f, 0.018f, 0.85f), new Color(0.72f, 0.025f, 0.035f, 0.95f));
                SetCone(mist, 33f, 0.035f);
                SetFade(mist, 0.8f);
                SetSizeCurve(mist, 0.65f, 1.7f);
                ParticleSystem droplets = AddParticles(root.transform, "Blood_Droplets", bloodMaterial, true, false);
                ConfigureBurst(droplets, 12, 0.34f, 0.65f, 2.4f, 4.0f, 0.025f, 0.052f,
                    new Color(0.32f, 0.005f, 0.01f, 1f), new Color(0.7f, 0.02f, 0.025f, 1f));
                SetCone(droplets, 42f, 0.025f);
                var main = droplets.main; main.gravityModifier = 1.2f;
                SetFade(droplets, 1f);
                ParticleSystemRenderer renderer = droplets.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 2.3f;
                renderer.velocityScale = 0.015f;
                SetLayer(root.transform, 0);
                root.SetActive(true);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (saved == null) throw new InvalidOperationException("피 이펙트 프리팹 저장 실패");
                return saved;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static FreeModelsMuzzleFlash AttachMuzzle(Transform gun)
        {
            Transform previous = gun.Find(MuzzleName);
            if (previous != null) Undo.DestroyObjectImmediate(previous.gameObject);
            GameObject root = new GameObject(MuzzleName);
            Undo.RegisterCreatedObjectUndo(root, "Attach muzzle flash to barrel");
            root.transform.SetParent(gun, false);
            root.transform.localPosition = FindMuzzle(gun) + Vector3.forward * 0.012f;
            Material flash = GetParticleMaterial("Muzzle_Additive", "Textures/FX_MuzzleStar.png", true);
            Material dot = GetParticleMaterial("Muzzle_Sparks", "Textures/FX_SoftParticle.png", true);
            Material smokeMaterial = GetParticleMaterial("Smoke_Alpha", "Textures/FX_SoftParticle.png", false);
            ParticleSystem flame = AddParticles(root.transform, "Muzzle_Flame", flash, false, true);
            ConfigureBurst(flame, 0, 0.045f, 0.068f, 0f, 0f, 0.15f, 0.22f,
                new Color(1f, 0.62f, 0.1f, 1f), new Color(1f, 0.92f, 0.57f, 1f));
            var shape = flame.shape; shape.enabled = false;
            var main = flame.main; main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            SetFade(flame, 1f);
            ParticleSystem sparks = AddParticles(root.transform, "Muzzle_Sparks", dot, false, true);
            ConfigureBurst(sparks, 0, 0.055f, 0.10f, 0.6f, 1.8f, 0.012f, 0.028f,
                new Color(1f, 0.45f, 0.05f, 1f), new Color(1f, 0.9f, 0.45f, 1f));
            SetCone(sparks, 16f, 0.01f); SetFade(sparks, 1f);
            ParticleSystem smoke = AddParticles(root.transform, "Muzzle_Smoke", smokeMaterial, false, true);
            ConfigureBurst(smoke, 0, 0.17f, 0.3f, 0.18f, 0.4f, 0.045f, 0.075f,
                new Color(0.35f, 0.35f, 0.33f, 0.14f), new Color(0.5f, 0.5f, 0.46f, 0.2f));
            SetCone(smoke, 12f, 0.01f); SetFade(smoke, 1f); SetSizeCurve(smoke, 0.7f, 3.2f);
            GameObject lightObject = new GameObject("Muzzle_Light");
            Undo.RegisterCreatedObjectUndo(lightObject, "Add muzzle light");
            lightObject.transform.SetParent(root.transform, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.56f, 0.18f);
            light.range = 2.2f; light.intensity = 0.9f; light.shadows = LightShadows.None; light.enabled = false;
            FreeModelsMuzzleFlash controller = Undo.AddComponent<FreeModelsMuzzleFlash>(root);
            AssignReference(controller, "_flame", flame);
            AssignReference(controller, "_sparks", sparks);
            AssignReference(controller, "_smoke", smoke);
            AssignReference(controller, "_flashLight", light);
            SetLayer(root.transform, gun.gameObject.layer);
            return controller;
        }

        private static Vector3 FindMuzzle(Transform gun)
        {
            List<Vector3> points = new List<Vector3>();
            float front = float.NegativeInfinity;
            foreach (MeshFilter filter in gun.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Matrix4x4 matrix = gun.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(vertex);
                    points.Add(point); front = Mathf.Max(front, point.z);
                }
            }
            if (points.Count == 0) throw new InvalidOperationException("총구 위치를 계산할 메시가 없습니다.");
            Vector3 total = Vector3.zero; int count = 0;
            foreach (Vector3 point in points)
                if (point.z >= front - 0.012f) { total += point; count++; }
            Vector3 center = total / Mathf.Max(count, 1);
            center.z = front;
            return center;
        }

        private static ParticleSystem AddParticles(Transform parent, string name, Material material, bool autoPlay, bool local)
        {
            GameObject owner = new GameObject(name);
            if (!autoPlay && parent.gameObject.scene.IsValid()) Undo.RegisterCreatedObjectUndo(owner, "Create effect particles");
            owner.transform.SetParent(parent, false);
            ParticleSystem particles = owner.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false; main.duration = 0.12f; main.playOnAwake = autoPlay;
            main.maxParticles = 96; main.useUnscaledTime = true;
            main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.stopAction = ParticleSystemStopAction.None;
            var emission = particles.emission; emission.enabled = true; emission.rateOverTime = 0f;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return particles;
        }

        private static void ConfigureBurst(ParticleSystem particles, short count, float minLife, float maxLife,
            float minSpeed, float maxSpeed, float minSize, float maxSize, Color minColor, Color maxColor)
        {
            var main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = new ParticleSystem.MinMaxGradient(minColor, maxColor);
            var emission = particles.emission;
            if (count > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
            else emission.SetBursts(new ParticleSystem.Burst[0]);
        }

        private static void SetCone(ParticleSystem particles, float angle, float radius)
        {
            var shape = particles.shape; shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle; shape.radius = radius;
        }

        private static void SetFade(ParticleSystem particles, float alpha)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha * 0.8f, 0.25f), new GradientAlphaKey(0f, 1f) });
            var color = particles.colorOverLifetime; color.enabled = true; color.color = gradient;
        }

        private static void SetSizeCurve(ParticleSystem particles, float start, float end)
        {
            var size = particles.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, start, 1f, end));
        }

        private static Material GetParticleMaterial(string name, string texture, bool additive)
        {
            Shader shader = Shader.Find("Particles/Standard Unlit");
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null && pipeline.GetType().Name.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0)
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            else if (pipeline != null && pipeline.GetType().Name.IndexOf("HDRender", StringComparison.OrdinalIgnoreCase) >= 0)
                shader = Shader.Find("HDRP/Unlit");
            if (shader == null) throw new InvalidOperationException("파티클 Unlit 셰이더를 찾지 못했습니다. 렌더 파이프라인 설치를 확인하세요.");
            if (!shader.isSupported) throw new InvalidOperationException("현재 그래픽 설정에서 파티클 셰이더를 사용할 수 없습니다: " + shader.name);
            EnsureFolder(EffectFolder + "/Materials");
            string path = EffectFolder + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created) material = new Material(shader) { name = name };
            else material.shader = shader;
            Texture2D image = FreeModelsTextureRecovery.Load(texture);
            image.filterMode = FilterMode.Bilinear;
            EditorUtility.SetDirty(image);
            foreach (string key in new[] { "_BaseMap", "_MainTex", "_UnlitColorMap" })
                if (material.HasProperty(key)) material.SetTexture(key, image);
            foreach (string key in new[] { "_BaseColor", "_Color", "_TintColor", "_UnlitColor" })
                if (material.HasProperty(key)) material.SetColor(key, Color.white);
            SetFloat(material, "_Surface", 1f); SetFloat(material, "_SurfaceType", 1f);
            SetFloat(material, "_Blend", additive ? 2f : 0f);
            SetFloat(material, "_Mode", additive ? 4f : 2f);
            SetFloat(material, "_ColorMode", 0f); SetFloat(material, "_VertexColorMode", 1f);
            SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloat(material, "_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloat(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_ZWrite", 0f); SetFloat(material, "_Cull", (float)CullMode.Off);
            SetFloat(material, "_AlphaClip", 0f); SetFloat(material, "_SoftParticlesEnabled", 0f);
            SetFloat(material, "_CameraFadingEnabled", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_COLORADDSUBDIFF_ON"); material.DisableKeyword("_COLORCOLOR_ON");
            material.DisableKeyword("_SOFTPARTICLES_ON"); material.DisableKeyword("_FADING_ON");
            material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = 3000;
            if (created) AssetDatabase.CreateAsset(material, path); else EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetFloat(Material material, string property, float value)
        { if (material.HasProperty(property)) material.SetFloat(property, value); }

        private static void AssignReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            Undo.RecordObject(target, "Connect combat or minimap reference");
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException(target.name + "에 " + field + " 필드가 없습니다. V6 전체를 Import하세요.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static void ConnectMinimap(Scene scene)
        {
            List<Transform> objects = SceneTransforms(scene);
            MinimapCamera minimap = null;
            UI_Minimap ui = null;
            RawImage mapImage = null;
            foreach (Transform t in objects)
            {
                if (minimap == null) minimap = t.GetComponent<MinimapCamera>();
                if (ui == null) ui = t.GetComponent<UI_Minimap>();
            }
            if (minimap == null)
            {
                foreach (Transform t in objects)
                {
                    Camera candidate = t.GetComponent<Camera>();
                    if (candidate != null && candidate.orthographic &&
                        (t.name.IndexOf("mini", StringComparison.OrdinalIgnoreCase) >= 0 || candidate.targetTexture != null))
                    { minimap = Undo.AddComponent<MinimapCamera>(t.gameObject); break; }
                }
            }
            if (minimap == null) throw new InvalidOperationException("미니맵 Camera를 찾지 못했습니다. MinimapCamera가 붙은 기존 카메라가 있는 씬에서 실행하세요.");
            Camera mapCamera = minimap.GetComponent<Camera>();
            Undo.RecordObject(mapCamera, "Enable minimap orthographic zoom");
            mapCamera.orthographic = true;
            if (PrefabUtility.IsPartOfPrefabInstance(mapCamera)) PrefabUtility.RecordPrefabInstancePropertyModifications(mapCamera);
            foreach (Transform t in objects)
            {
                RawImage image = t.GetComponent<RawImage>();
                if (image == null) continue;
                if (mapCamera.targetTexture != null && image.texture == mapCamera.targetTexture) { mapImage = image; break; }
                if (mapImage == null && (t.name.IndexOf("mini", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.name.IndexOf("미니맵", StringComparison.Ordinal) >= 0)) mapImage = image;
            }
            if (mapImage != null)
            {
                UI_Minimap nearby = mapImage.GetComponentInParent<UI_Minimap>();
                if (nearby != null) ui = nearby;
                if (ui == null) ui = Undo.AddComponent<UI_Minimap>(mapImage.gameObject);
            }
            if (ui == null)
            {
                foreach (Transform t in objects)
                    if (t is RectTransform && t.name.IndexOf("mini", StringComparison.OrdinalIgnoreCase) >= 0)
                    { ui = Undo.AddComponent<UI_Minimap>(t.gameObject); break; }
            }
            if (ui == null) throw new InvalidOperationException("미니맵 UI/RawImage를 찾지 못했습니다. 미니맵이 포함된 GameScene에서 실행하세요.");

            SerializedObject cameraData = new SerializedObject(minimap);
            SerializedProperty target = cameraData.FindProperty("_target");
            if (target != null && target.objectReferenceValue == null)
            {
                foreach (Transform t in objects)
                    if (t.GetComponent<Player>() != null) { AssignReference(minimap, "_target", t); break; }
            }

            SerializedObject uiData = new SerializedObject(ui);
            Button plus = uiData.FindProperty("_zoomInButton").objectReferenceValue as Button;
            Button minus = uiData.FindProperty("_zoomOutButton").objectReferenceValue as Button;
            for (Transform scope = ui.transform; scope != null; scope = scope.parent)
            {
                if (plus == null) plus = FindZoomButton(scope, 1, ui.transform.position);
                if (minus == null) minus = FindZoomButton(scope, -1, ui.transform.position);
                if (plus != null && minus != null) break;
                if (scope.GetComponent<Canvas>() != null) break;
            }
            if (plus == null || minus == null)
            {
                Transform buttonParent = mapImage != null ? mapImage.transform : ui.transform;
                if (!(buttonParent is RectTransform)) throw new InvalidOperationException("미니맵 UI에 RectTransform이 없습니다.");
                Transform panel = buttonParent.Find("__FreeModels_ZoomButtons");
                if (panel == null)
                {
                    GameObject owner = new GameObject("__FreeModels_ZoomButtons", typeof(RectTransform));
                    Undo.RegisterCreatedObjectUndo(owner, "Create minimap zoom controls");
                    owner.transform.SetParent(buttonParent, false);
                    RectTransform rect = owner.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f); rect.pivot = new Vector2(1f, 0f);
                    rect.anchoredPosition = new Vector2(-6f, 6f); rect.sizeDelta = new Vector2(82f, 34f);
                    panel = rect;
                }
                if (plus == null) plus = CreateZoomButton(panel, "ZoomIn_Plus", "+", -42f, ui);
                if (minus == null) minus = CreateZoomButton(panel, "ZoomOut_Minus", "−", 0f, ui);
            }
            if (plus == minus) throw new InvalidOperationException("미니맵 +와 -에 같은 버튼이 연결되어 있습니다.");
            AssignReference(ui, "_minimapCamera", minimap);
            AssignReference(ui, "_zoomInButton", plus);
            AssignReference(ui, "_zoomOutButton", minus);
            VerifyReference(ui, "_minimapCamera", minimap);
            VerifyReference(ui, "_zoomInButton", plus);
            VerifyReference(ui, "_zoomOutButton", minus);
            MakeButtonClickable(plus); MakeButtonClickable(minus);
            Canvas canvas = ui.GetComponentInParent<Canvas>(true);
            if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null) Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);
            EnsureEventSystem(scene);
            Debug.Log("[Free Models V6] 미니맵 +: " + plus.name + ", -: " + minus.name + " → " + minimap.name + ". +는 확대, -는 축소, 범위 2~40.");
        }

        private static void VerifyReference(UnityEngine.Object target, string field, UnityEngine.Object expected)
        {
            SerializedProperty property = new SerializedObject(target).FindProperty(field);
            if (property == null || property.objectReferenceValue != expected)
                throw new InvalidOperationException("연결 검증에 실패했습니다: " + target.name + " / " + field);
        }

        private static Button FindZoomButton(Transform scope, int direction, Vector3 origin)
        {
            Button closest = null; float distance = float.PositiveInfinity;
            foreach (Button button in scope.GetComponentsInChildren<Button>(true))
            {
                if (UI_Minimap.GetZoomButtonDirection(button) != direction) continue;
                float next = (button.transform.position - origin).sqrMagnitude;
                if (next < distance) { closest = button; distance = next; }
            }
            return closest;
        }

        private static Button CreateZoomButton(Transform parent, string name, string symbol, float x, UI_Minimap ui)
        {
            Transform previous = parent.Find(name);
            if (previous != null && previous.GetComponent<Button>() != null) return previous.GetComponent<Button>();
            GameObject owner = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(owner, "Add minimap zoom button");
            owner.transform.SetParent(parent, false);
            RectTransform rect = owner.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f); rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(x, 0f); rect.sizeDelta = new Vector2(38f, 34f);
            owner.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.11f, 0.92f);
            Button button = owner.GetComponent<Button>(); button.targetGraphic = owner.GetComponent<Image>();
            GameObject labelObject = new GameObject("Label", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(labelObject, "Add zoom button label");
            labelObject.transform.SetParent(owner.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            Canvas parentCanvas = ui.GetComponentInParent<Canvas>(true);
            if (font == null && parentCanvas != null)
                foreach (TMP_Text existing in parentCanvas.GetComponentsInChildren<TMP_Text>(true))
                    if (existing.font != null) { font = existing.font; break; }
            if (font != null) label.font = font;
            label.text = symbol; label.fontSize = 24f; label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white; label.raycastTarget = false;
            return button;
        }

        private static void MakeButtonClickable(Button button)
        {
            Undo.RecordObject(button, "Enable minimap zoom button");
            button.interactable = true;
            Navigation navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            if (button.targetGraphic != null)
            {
                Undo.RecordObject(button.targetGraphic, "Enable zoom button raycast");
                button.targetGraphic.raycastTarget = true;
            }
            if (PrefabUtility.IsPartOfPrefabInstance(button)) PrefabUtility.RecordPrefabInstancePropertyModifications(button);
        }

        private static void EnsureEventSystem(Scene scene)
        {
            if (EventSystem.current != null) return;
            foreach (Transform t in SceneTransforms(scene))
            {
                EventSystem existing = t.GetComponent<EventSystem>();
                if (existing == null) continue;
                Undo.RecordObject(existing, "Enable UI events"); existing.enabled = true;
                if (existing.GetComponent<BaseInputModule>() == null) Undo.AddComponent<StandaloneInputModule>(existing.gameObject);
                return;
            }
            GameObject root = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Enable minimap UI clicks");
        }

        private static Camera FindViewCamera(Scene scene)
        {
            if (Camera.main != null && Camera.main.gameObject.scene == scene && !Camera.main.orthographic) return Camera.main;
            foreach (Transform t in SceneTransforms(scene))
            {
                Camera camera = t.GetComponent<Camera>();
                if (camera != null && !camera.orthographic && t.Find(ViewModelName) != null) return camera;
            }
            return null;
        }

        private static List<Transform> SceneTransforms(Scene scene)
        {
            List<Transform> transforms = new List<Transform>();
            foreach (GameObject root in scene.GetRootGameObjects()) transforms.AddRange(root.GetComponentsInChildren<Transform>(true));
            return transforms;
        }

        private static void SetLayer(Transform root, int layer)
        { root.gameObject.layer = layer; foreach (Transform child in root) SetLayer(child, layer); }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
#endif
