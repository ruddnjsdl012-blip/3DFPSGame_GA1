#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// 과제 도구: 조명 / 포스트 프로세싱 / 최적화 + Before·After 측정
// 메뉴: Tools > L4D 과제
//
// 권장 순서 (같은 PC, 같은 Game 창 해상도(예: 1920x1080 고정), 같은 품질 단계에서 측정)
//   0. 측정(Before)       : "성능 측정 - 경로 8곳"과 "성능 측정 - 좀비 40마리"를 라벨 Before로 실행
//   1. 조명 적용           : Mixed(해) + Baked(실내등) + Realtime(깜빡이는 불) / 라이트맵 / 라이트 프로브 / 반사 프로브 → 굽기
//   2. 포스트 프로세싱 적용 : Bloom, Color Adjustments, Vignette, Tonemapping, SSAO 등
//      → 측정(Lighting_PostFX)
//   3. 최적화 적용         : 스크립트(CPU) / 렌더링(GPU) / 에셋 임포트(RAM)
//      → 측정(After) → "측정 결과 비교표 만들기"
public static class L4DAssignmentTools
{
    private const string Menu = "Tools/L4D 과제/";
    private const string Folder = "Assets/L4DCampaign";
    private const string AssignmentFolder = Folder + "/Assignment";
    private const string BackupFolder = AssignmentFolder + "/Backup";
    private const string LightingSettingsPath = AssignmentFolder + "/CampaignLighting.lighting";
    private const string PostFxProfilePath = AssignmentFolder + "/L4D_Campaign_PostFX.asset";
    private const string MeshFolder = Folder + "/Generated/Meshes";
    private const string RootName = "FloodedCampaign";
    private const string UrpAssetPath = "Assets/Settings/PC_RPAsset.asset";
    private const string UrpRendererPath = "Assets/Settings/PC_Renderer.asset";

    // ======================================================================
    // 0. 측정
    // ======================================================================

    [MenuItem(Menu + "0-1. 성능 측정 - 경로 8곳 (그래픽·GPU)", priority = 0)]
    public static void BenchmarkRoute()
    {
        StartBenchmark("Route");
    }

    [MenuItem(Menu + "0-2. 성능 측정 - 좀비 40마리 (스크립트·CPU)", priority = 1)]
    public static void BenchmarkCombat()
    {
        StartBenchmark("Combat");
    }

    [MenuItem(Menu + "0-3. 측정 결과 비교표 만들기 (Before vs After)", priority = 2)]
    public static void Compare()
    {
        string root = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Benchmarks");
        Directory.CreateDirectory(root);
        string before = EditorUtility.OpenFolderPanel("Before 측정 폴더 선택", root, string.Empty);
        if (string.IsNullOrEmpty(before)) return;
        string after = EditorUtility.OpenFolderPanel("After 측정 폴더 선택", root, string.Empty);
        if (string.IsNullOrEmpty(after)) return;
        BenchSummary a = ReadSummary(before);
        BenchSummary b = ReadSummary(after);
        if (a == null || b == null || a.total == null || b.total == null)
        {
            EditorUtility.DisplayDialog("비교", "선택한 폴더에 summary.json이 없습니다.", "확인");
            return;
        }

        StringBuilder md = new StringBuilder();
        md.AppendLine("# Before / After 비교: " + a.label + " → " + b.label + " (" + a.mode + ")");
        md.AppendLine();
        md.AppendLine("- Before: " + a.time + ", " + a.width + "x" + a.height + ", " + a.gpu);
        md.AppendLine("- After : " + b.time + ", " + b.width + "x" + b.height + ", " + b.gpu);
        if (a.width != b.width || a.height != b.height) md.AppendLine("- ⚠ 해상도가 달라 비교가 정확하지 않습니다.");
        md.AppendLine();
        md.AppendLine("| 항목 | Before | After | 변화 |");
        md.AppendLine("|---|---|---|---|");
        AddRow(md, "평균 FPS", a.total.avgFps, b.total.avgFps, true);
        AddRow(md, "1% Low FPS", a.total.low1Fps, b.total.low1Fps, true);
        AddRow(md, "프레임 시간 (ms)", a.total.avgFrameMs, b.total.avgFrameMs, false);
        AddRow(md, "메인 스레드 (ms)", a.total.avgMainThreadMs, b.total.avgMainThreadMs, false);
        AddRow(md, "렌더 스레드 (ms)", a.total.avgRenderThreadMs, b.total.avgRenderThreadMs, false);
        AddRow(md, "GPU (ms)", a.total.avgGpuMs, b.total.avgGpuMs, false);
        AddRow(md, "Batches", a.total.batches, b.total.batches, false);
        AddRow(md, "SetPass Calls", a.total.setPassCalls, b.total.setPassCalls, false);
        AddRow(md, "Draw Calls", a.total.drawCalls, b.total.drawCalls, false);
        AddRow(md, "Triangles", a.total.triangles, b.total.triangles, false);
        AddRow(md, "그림자 캐스터", a.total.shadowCasters, b.total.shadowCasters, false);
        AddRow(md, "GC 할당 (KB/프레임)", a.total.gcAllocKbPerFrame, b.total.gcAllocKbPerFrame, false);
        AddRow(md, "텍스처 메모리 (MB)", a.total.textureMb, b.total.textureMb, false);
        AddRow(md, "메시 메모리 (MB)", a.total.meshMb, b.total.meshMb, false);
        AddRow(md, "전체 사용 메모리 (MB)", a.total.totalUsedMb, b.total.totalUsedMb, false);
        md.AppendLine();
        md.AppendLine("## 지점별 평균 FPS / Batches");
        md.AppendLine();
        md.AppendLine("| 지점 | FPS Before | FPS After | Batches Before | Batches After |");
        md.AppendLine("|---|---|---|---|---|");
        for (int i = 0; i < Mathf.Min(a.views.Count, b.views.Count); i++)
        {
            md.AppendLine("| " + a.views[i].name + " | " + a.views[i].avgFps.ToString("0.0") + " | " + b.views[i].avgFps.ToString("0.0") + " | "
                          + a.views[i].batches.ToString("0") + " | " + b.views[i].batches.ToString("0") + " |");
        }

        string path = Path.Combine(root, "Compare_" + a.label + "_vs_" + b.label + "_" + a.mode + "_" + DateTime.Now.ToString("MMdd_HHmm") + ".md");
        File.WriteAllText(path, md.ToString(), new UTF8Encoding(false));
        EditorUtility.RevealInFinder(path);
        EditorUtility.DisplayDialog("비교표 완료", md.ToString().Substring(0, Mathf.Min(1400, md.Length)), "확인");
    }

    [Serializable]
    private class BenchView
    {
        public string name;
        public float avgFps;
        public float low1Fps;
        public float avgFrameMs;
        public float avgMainThreadMs;
        public float avgRenderThreadMs;
        public float avgGpuMs;
        public float batches;
        public float setPassCalls;
        public float drawCalls;
        public float triangles;
        public float shadowCasters;
        public float gcAllocKbPerFrame;
        public float totalUsedMb;
        public float textureMb;
        public float meshMb;
    }

    [Serializable]
    private class BenchSummary
    {
        public string label;
        public string mode;
        public string time;
        public string gpu;
        public int width;
        public int height;
        public List<BenchView> views = new List<BenchView>();
        public BenchView total;
    }

    private static BenchSummary ReadSummary(string folder)
    {
        string path = Path.Combine(folder, "summary.json");
        return File.Exists(path) ? JsonUtility.FromJson<BenchSummary>(File.ReadAllText(path)) : null;
    }

    private static void AddRow(StringBuilder md, string name, float before, float after, bool higherIsBetter)
    {
        float change = before != 0f ? (after - before) / Mathf.Abs(before) * 100f : 0f;
        bool better = higherIsBetter ? after > before : after < before;
        string mark = Mathf.Abs(change) < 1f ? "≈" : better ? "✔" : "✖";
        md.AppendLine("| " + name + " | " + before.ToString("0.##", CultureInfo.InvariantCulture) + " | " + after.ToString("0.##", CultureInfo.InvariantCulture)
                      + " | " + (change >= 0f ? "+" : string.Empty) + change.ToString("0.0") + "% " + mark + " |");
    }

    private static void StartBenchmark(string mode)
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("성능 측정", "Play를 끈 상태에서 실행해 주세요.", "확인");
            return;
        }

        if (FindCampaign() == null)
        {
            EditorUtility.DisplayDialog("성능 측정", "FloodedCampaign이 있는 L4D 씬을 연 뒤 실행해 주세요.", "확인");
            return;
        }

        int choice = EditorUtility.DisplayDialogComplex("성능 측정 (" + mode + ")",
            "측정 라벨을 고르세요.\n\nGame 창 해상도를 고정(예: 1920x1080)하고, 다른 프로그램을 닫은 뒤 Before와 After를 같은 조건에서 재세요.\n"
            + "Play가 자동으로 시작되고, 끝나면 자동으로 꺼집니다. 결과는 프로젝트 폴더/Benchmarks 에 저장됩니다.",
            "Before", "취소", "After");
        if (choice == 1) return;
        string label = choice == 0 ? "Before" : "After";
        if (choice == 2 && EditorUtility.DisplayDialog("성능 측정", "중간 단계(조명+포스트 프로세싱만 적용) 라벨로 잴까요?", "Lighting_PostFX로 측정", "After로 측정"))
        {
            label = "Lighting_PostFX";
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        PlayerPrefs.SetString(PerfBenchmark.ModeKey, mode);
        PlayerPrefs.SetString(PerfBenchmark.LabelKey, label);
        PlayerPrefs.Save();
        EditorApplication.isPlaying = true;
    }

    // ======================================================================
    // 1. 조명
    // ======================================================================

    [MenuItem(Menu + "1. 조명 적용 (Mixed·Baked·Realtime + 라이트맵 + 라이트 프로브)", priority = 20)]
    public static void ApplyLighting()
    {
        CityMapInfo map = FindCampaign();
        if (map == null)
        {
            EditorUtility.DisplayDialog("조명", "FloodedCampaign이 있는 L4D 씬을 연 뒤 실행해 주세요.", "확인");
            return;
        }

        int choice = EditorUtility.DisplayDialogComplex("조명 적용",
            "1. 해(Sun) = Mixed (가까운 그림자는 실시간, 먼 곳은 Shadowmask로 구운 그림자)\n"
            + "2. 안전가옥·저택·선착장 실내등 = Baked (간접광까지 라이트맵에 굽기)\n"
            + "3. 조명탄·드럼통 불·무전기 = Realtime (깜빡여야 해서)\n"
            + "4. 건물·지형 = 라이트맵, 작은 소품·좀비·플레이어 = 라이트 프로브\n"
            + "5. 경로를 따라 라이트 프로브 / 주요 지점에 반사 프로브 배치\n"
            + "6. 라이트맵 UV 생성 후 굽기 (GPU 라이트매퍼)\n\n"
            + "굽기는 PC에 따라 5~30분 걸립니다. 굽는 중에도 에디터는 쓸 수 있습니다.",
            "적용 + 굽기 (제출용 품질)", "취소", "적용 + 빠른 굽기 (확인용)");
        if (choice == 1) return;
        bool quality = choice == 0;
        StringBuilder log = new StringBuilder();
        try
        {
            EnsureFolder(AssignmentFolder);
            EditorUtility.DisplayProgressBar("조명 적용", "라이트맵 UV 만드는 중", 0.1f);
            int unwrapped = GenerateLightmapUvs(log);

            EditorUtility.DisplayProgressBar("조명 적용", "정적 플래그 / GI 설정", 0.5f);
            SetupGiFlags(map, log);

            EditorUtility.DisplayProgressBar("조명 적용", "조명 모드", 0.6f);
            SetupLights(map, log);

            EditorUtility.DisplayProgressBar("조명 적용", "라이트 프로브 / 반사 프로브", 0.7f);
            SetupProbes(map, log);

            EditorUtility.DisplayProgressBar("조명 적용", "라이트맵 설정", 0.8f);
            SetupLightingSettings(quality, log);

            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            EditorSceneManager.SaveScene(map.gameObject.scene);
            log.Insert(0, "라이트맵 UV를 만든 건물 메시 " + unwrapped + "개\n");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Lightmapping.bakeCompleted -= OnBakeCompleted;
        Lightmapping.bakeCompleted += OnBakeCompleted;
        if (!Lightmapping.BakeAsync())
        {
            log.AppendLine("⚠ 굽기를 시작하지 못했습니다. Window > Rendering > Lighting 에서 Generate Lighting을 눌러 주세요.");
        }

        WriteLog("Lighting", log.ToString());
        EditorUtility.DisplayDialog("조명 적용", log + "\n굽기를 시작했습니다. 오른쪽 아래 진행 막대가 끝나면 씬이 자동 저장됩니다.", "확인");
    }

    private static void OnBakeCompleted()
    {
        Lightmapping.bakeCompleted -= OnBakeCompleted;
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[조명] 굽기 완료, 씬 저장: " + scene.path + " / 라이트맵 " + LightmapSettings.lightmaps.Length + "장");
    }

    // 합친 건물 메시는 원본 조각의 UV2가 겹쳐 있으므로 라이트맵용 UV를 새로 만든다.
    private static int GenerateLightmapUvs(StringBuilder log)
    {
        int count = 0;
        string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { MeshFolder });
        for (int g = 0; g < guids.Length; g++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[g]);
            foreach (Mesh mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
            {
                if (mesh.name.EndsWith("_Collider")) continue;
                EditorUtility.DisplayProgressBar("조명 적용", "라이트맵 UV: " + mesh.name, 0.1f + 0.4f * g / Mathf.Max(1, guids.Length));
                UnwrapParam param;
                UnwrapParam.SetDefaults(out param);
                param.packMargin = 4f / 1024f;
                Unwrapping.GenerateSecondaryUVSet(mesh, param);
                EditorUtility.SetDirty(mesh);
                count++;
            }
        }

        AssetDatabase.SaveAssets();
        log.AppendLine("✔ 건물 메시 라이트맵 UV(UV2) 생성");
        return count;
    }

    private static void SetupGiFlags(CityMapInfo map, StringBuilder log)
    {
        int lightmapped = 0;
        int probed = 0;
        Transform buildings = map.transform.Find("Buildings");
        Transform props = map.transform.Find("Props");
        foreach (MeshRenderer renderer in map.GetComponentsInChildren<MeshRenderer>(true))
        {
            GameObject go = renderer.gameObject;
            bool isBuilding = buildings != null && renderer.transform.IsChildOf(buildings);
            bool isProp = props != null && renderer.transform.IsChildOf(props);
            if (!isBuilding && !isProp) continue;
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
            if ((flags & StaticEditorFlags.BatchingStatic) == 0) continue; // 움직이는 보트 등
            GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.ContributeGI);
            SerializedObject so = new SerializedObject(renderer);
            if (isBuilding && go.name.StartsWith("LOD"))
            {
                renderer.receiveGI = ReceiveGI.Lightmaps;
                SetFloat(so, "m_ScaleInLightmap", go.name == "LOD0" ? 1f : 0.5f);
                lightmapped++;
            }
            else
            {
                // 작은 소품은 라이트맵 대신 라이트 프로브로 받는다. (라이트맵 용량 절약, 다른 면에 그림자/반사광은 준다)
                renderer.receiveGI = ReceiveGI.LightProbes;
                probed++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Terrain terrain = map.GetComponentInChildren<Terrain>();
        if (terrain != null)
        {
            GameObjectUtility.SetStaticEditorFlags(terrain.gameObject, GameObjectUtility.GetStaticEditorFlags(terrain.gameObject) | StaticEditorFlags.ContributeGI);
            SerializedObject so = new SerializedObject(terrain);
            SetFloat(so, "m_ScaleInLightmap", 0.35f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        log.AppendLine("✔ 라이트맵 받는 건물 렌더러 " + lightmapped + "개, 라이트 프로브 받는 소품 " + probed + "개, 지형 라이트맵(스케일 0.35)");
    }

    private static void SetupLights(CityMapInfo map, StringBuilder log)
    {
        int mixed = 0;
        int baked = 0;
        int realtime = 0;
        foreach (Light light in map.GetComponentsInChildren<Light>(true))
        {
            if (light.type == LightType.Directional)
            {
                light.lightmapBakeType = LightmapBakeType.Mixed;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.9f;
                mixed++;
            }
            else if (light.GetComponent<MapFlickerLight>() != null)
            {
                light.lightmapBakeType = LightmapBakeType.Realtime;
                light.shadows = LightShadows.None;
                realtime++;
            }
            else
            {
                light.lightmapBakeType = LightmapBakeType.Baked;
                light.shadows = LightShadows.Soft;
                SerializedObject so = new SerializedObject(light);
                SetFloat(so, "m_ShadowRadius", 0.15f);
                so.ApplyModifiedPropertiesWithoutUndo();
                baked++;
            }

            EditorUtility.SetDirty(light);
        }

        // Distance Shadowmask: 그림자 거리 안은 실시간 그림자, 밖은 구운 그림자
        QualitySettings.shadowmaskMode = ShadowmaskMode.DistanceShadowmask;
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = 1.1f;
        log.AppendLine("✔ 조명: Mixed " + mixed + "개(해), Baked " + baked + "개(실내등), Realtime " + realtime + "개(조명탄·불·무전기)");
        log.AppendLine("✔ Distance Shadowmask, 환경광 = 하늘(Skybox) 기반으로 굽기");
    }

    private static void SetupProbes(CityMapInfo map, StringBuilder log)
    {
        Transform old = map.transform.Find("LightProbes");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        Transform oldReflections = map.transform.Find("ReflectionProbes");
        if (oldReflections != null) Object.DestroyImmediate(oldReflections.gameObject);

        GameObject probesObject = new GameObject("LightProbes");
        probesObject.transform.SetParent(map.transform, false);
        LightProbeGroup group = probesObject.AddComponent<LightProbeGroup>();
        List<Vector3> positions = new List<Vector3>();
        Transform[] route = map.RoutePoints;
        for (int i = 0; i < route.Length; i++)
        {
            Vector3 p = route[i].position;
            Vector3 next = route[Mathf.Min(i + 1, route.Length - 1)].position;
            Vector3 prev = route[Mathf.Max(i - 1, 0)].position;
            Vector3 forward = next - prev;
            forward.y = 0f;
            Vector3 side = forward.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, forward.normalized) : Vector3.right;
            positions.Add(p + Vector3.up * 0.7f);
            positions.Add(p + Vector3.up * 2.8f);
            positions.Add(p + side * 3.5f + Vector3.up * 1.2f);
            positions.Add(p - side * 3.5f + Vector3.up * 1.2f);
        }

        foreach (BoxCollider room in new[] { map.StartSafeRoomArea }.Concat(map.MidSafeRooms ?? new BoxCollider[0]).Concat(new[] { map.FinaleArea }))
        {
            if (room == null) continue;
            Bounds b = room.bounds;
            int steps = room == map.FinaleArea ? 5 : 3;
            for (int x = 0; x < steps; x++)
            {
                for (int z = 0; z < steps; z++)
                {
                    float fx = (x + 0.5f) / steps;
                    float fz = (z + 0.5f) / steps;
                    Vector3 p = new Vector3(Mathf.Lerp(b.min.x, b.max.x, fx), b.min.y + 0.8f, Mathf.Lerp(b.min.z, b.max.z, fz));
                    positions.Add(p);
                    positions.Add(p + Vector3.up * 1.8f);
                }
            }
        }

        group.probePositions = positions.Select(p => probesObject.transform.InverseTransformPoint(p)).ToArray();

        // 반사 프로브: 출발 / 중간 안전가옥 / 공장 마당 / 철교 / 교회 광장 / 피날레 정원
        GameObject reflections = new GameObject("ReflectionProbes");
        reflections.transform.SetParent(map.transform, false);
        int[] indices = { 0, 97, 124, 149, route.Length - 1 };
        List<Vector3> spots = indices.Where(i => i >= 0 && i < route.Length).Select(i => route[i].position).ToList();
        if (map.MidSafeRooms != null && map.MidSafeRooms.Length > 0 && map.MidSafeRooms[0] != null) spots.Add(map.MidSafeRooms[0].bounds.center);
        int reflectionCount = 0;
        foreach (Vector3 spot in spots)
        {
            GameObject go = new GameObject("ReflectionProbe_" + reflectionCount);
            go.transform.SetParent(reflections.transform, false);
            go.transform.position = spot + Vector3.up * 2f;
            ReflectionProbe probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;
            probe.size = new Vector3(40f, 16f, 40f);
            probe.resolution = 128;
            probe.boxProjection = false;
            probe.blendDistance = 4f;
            reflectionCount++;
        }

        log.AppendLine("✔ 라이트 프로브 " + positions.Count + "개 (경로 따라 4개씩 + 안전가옥·피날레 격자), 반사 프로브 " + reflectionCount + "개 (Baked, 128px)");
    }

    private static void SetupLightingSettings(bool quality, StringBuilder log)
    {
        LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
        if (settings == null)
        {
            settings = new LightingSettings { name = "CampaignLighting" };
            AssetDatabase.CreateAsset(settings, LightingSettingsPath);
        }

        settings.bakedGI = true;
        settings.realtimeGI = false;
        settings.mixedBakeMode = MixedLightingMode.Shadowmask;
        settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
        settings.lightmapResolution = quality ? 3f : 1f;
        settings.lightmapPadding = 4;
        settings.lightmapMaxSize = 2048;
        settings.directionalityMode = LightmapsMode.NonDirectional;
        settings.lightmapCompression = LightmapCompression.NormalQuality;
        settings.ao = true;
        settings.aoMaxDistance = 1.2f;
        settings.directSampleCount = quality ? 32 : 16;
        settings.indirectSampleCount = quality ? 256 : 64;
        settings.environmentSampleCount = quality ? 256 : 64;
        settings.maxBounces = 2;
        settings.filteringMode = LightingSettings.FilterMode.Auto;
        EditorUtility.SetDirty(settings);
        Lightmapping.lightingSettings = settings;
        AssetDatabase.SaveAssets();
        log.AppendLine("✔ 라이트맵 설정: GPU 라이트매퍼, Shadowmask, " + settings.lightmapResolution + " texel/m, 최대 2048, 비방향성, AO, 바운스 2 (" + (quality ? "제출용" : "빠른 확인용") + ")");
    }

    // ======================================================================
    // 2. 포스트 프로세싱
    // ======================================================================

    [MenuItem(Menu + "2. 포스트 프로세싱 적용 (Bloom·Color·Vignette·AO 등)", priority = 21)]
    public static void ApplyPostProcessing()
    {
        Volume volume = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(v => v.isGlobal);
        if (volume == null)
        {
            EditorUtility.DisplayDialog("포스트 프로세싱", "씬에 Global Volume이 없습니다. L4D 씬을 연 뒤 실행해 주세요.", "확인");
            return;
        }

        EnsureFolder(AssignmentFolder);
        StringBuilder log = new StringBuilder();
        if (volume.sharedProfile != null && AssetDatabase.GetAssetPath(volume.sharedProfile) != PostFxProfilePath)
        {
            log.AppendLine("· 이전 프로필: " + AssetDatabase.GetAssetPath(volume.sharedProfile) + " (지우지 않음, 되돌릴 때 다시 연결)");
        }

        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostFxProfilePath) != null) AssetDatabase.DeleteAsset(PostFxProfilePath);
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, PostFxProfilePath);

        // 흐린 늪지 + 좀비물: 채도 낮추고 대비 올림, 어두운 곳은 청록, 밝은 곳은 주황 (L4D2/WWZ 느낌)
        Tonemapping tonemapping = Add<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.ACES);

        Bloom bloom = Add<Bloom>(profile);
        bloom.threshold.Override(1.05f);
        bloom.intensity.Override(0.55f);
        bloom.scatter.Override(0.65f);
        bloom.tint.Override(new Color(1f, 0.86f, 0.72f));
        bloom.highQualityFiltering.Override(false);

        ColorAdjustments color = Add<ColorAdjustments>(profile);
        color.postExposure.Override(0.15f);
        color.contrast.Override(18f);
        color.saturation.Override(-22f);
        color.colorFilter.Override(new Color(0.95f, 0.98f, 1f));

        ShadowsMidtonesHighlights split = Add<ShadowsMidtonesHighlights>(profile);
        split.shadows.Override(new Vector4(0.92f, 1f, 1.05f, -0.03f));
        split.midtones.Override(new Vector4(1f, 1f, 1f, 0f));
        split.highlights.Override(new Vector4(1.05f, 1f, 0.94f, 0.02f));

        WhiteBalance white = Add<WhiteBalance>(profile);
        white.temperature.Override(-6f);
        white.tint.Override(3f);

        Vignette vignette = Add<Vignette>(profile);
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(new Color(0.03f, 0.03f, 0.03f));

        FilmGrain grain = Add<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Medium2);
        grain.intensity.Override(0.18f);
        grain.response.Override(0.8f);

        ChromaticAberration aberration = Add<ChromaticAberration>(profile);
        aberration.intensity.Override(0.06f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);
        log.AppendLine("✔ Volume 프로필 " + PostFxProfilePath + ": Tonemapping(ACES), Bloom, Color Adjustments, Shadows/Midtones/Highlights, White Balance, Vignette, Film Grain, Chromatic Aberration");

        // SSAO (URP 렌더러 기능): 이미 있으므로 세기와 반경을 맵 크기에 맞춘다.
        ScriptableObject ssao = FindRendererFeature("ScreenSpaceAmbientOcclusion");
        if (ssao != null)
        {
            SerializedObject so = new SerializedObject(ssao);
            SetBool(so, "m_Active", true);
            SetFloat(so, "m_Settings.Intensity", 0.9f);
            SetFloat(so, "m_Settings.Radius", 0.45f);
            SetFloat(so, "m_Settings.DirectLightingStrength", 0.3f);
            SetFloat(so, "m_Settings.Falloff", 60f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ssao);
            log.AppendLine("✔ SSAO(Ambient Occlusion): 세기 0.4 → 0.9, 반경 0.3 → 0.45 m");
        }
        else
        {
            log.AppendLine("⚠ PC_Renderer에서 SSAO를 찾지 못했습니다. PC_Renderer > Add Renderer Feature > Screen Space Ambient Occlusion 으로 추가해 주세요.");
        }

        int cameras = 0;
        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera.orthographic || camera.targetTexture != null) continue;
            UniversalAdditionalCameraData data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null || data.renderType != CameraRenderType.Base) continue;
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.Medium;
            EditorUtility.SetDirty(data);
            cameras++;
        }

        log.AppendLine("✔ 카메라 " + cameras + "개: Post Processing 켬, SMAA(Medium) 안티에일리어싱");
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(volume.gameObject.scene);
        EditorSceneManager.SaveScene(volume.gameObject.scene);
        WriteLog("PostProcessing", log.ToString());
        EditorUtility.DisplayDialog("포스트 프로세싱", log.ToString(), "확인");
    }

    private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component = profile.Add<T>(true);
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    private static ScriptableObject FindRendererFeature(string typeName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(UrpRendererPath).OfType<ScriptableObject>().FirstOrDefault(o => o != null && o.GetType().Name == typeName);
    }

    // ======================================================================
    // 3. 최적화
    // ======================================================================

    [MenuItem(Menu + "3-1. GPU 최적화 적용 (그림자·SSAO·소품 그림자·지형)", priority = 40)]
    public static void ApplyGpuOptimization()
    {
        CityMapInfo map = FindCampaign();
        if (map == null)
        {
            EditorUtility.DisplayDialog("GPU 최적화", "FloodedCampaign이 있는 L4D 씬을 연 뒤 실행해 주세요.", "확인");
            return;
        }

        EnsureFolder(BackupFolder);
        StringBuilder log = new StringBuilder();
        BackupAsset(UrpAssetPath, log);
        BackupAsset(UrpRendererPath, log);

        // URP 설정: 그림자 단계 4 → 2, 소프트 그림자 High → Medium, 추가 조명 그림자 끔 (점광원은 그림자를 안 씀)
        Object urp = AssetDatabase.LoadMainAssetAtPath(UrpAssetPath);
        if (urp != null)
        {
            SerializedObject so = new SerializedObject(urp);
            Change(so, "m_ShadowCascadeCount", 2, log, "그림자 캐스케이드");
            ChangeFloat(so, "m_Cascade2Split", 0.25f, log, "캐스케이드 분할");
            Change(so, "m_SoftShadowQuality", 2, log, "소프트 그림자 품질(3 High → 2 Medium)");
            Change(so, "m_AdditionalLightShadowsSupported", 0, log, "추가 조명 그림자");
            ChangeFloat(so, "m_ShadowDistance", 45f, log, "실시간 그림자 거리(m) - 먼 곳은 Shadowmask");
            Change(so, "m_AdditionalLightsPerObjectLimit", 4, log, "오브젝트당 추가 조명 수");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
        }

        // SSAO: 절반 해상도로 계산 (품질 차이 적고 GPU 시간 큰 폭 감소)
        ScriptableObject ssao = FindRendererFeature("ScreenSpaceAmbientOcclusion");
        if (ssao != null)
        {
            SerializedObject so = new SerializedObject(ssao);
            Change(so, "m_Settings.Downsample", 1, log, "SSAO 절반 해상도");
            Change(so, "m_Settings.BlurQuality", 1, log, "SSAO 블러 품질(0 High → 1 Medium)");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ssao);
        }

        // 작은 소품(담장 조각, 비석, 풀, 드럼통 등)은 그림자를 그리지 않는다. (그림자 패스 Draw Call/Tris 감소)
        int shadowOff = 0;
        Transform buildings = map.transform.Find("Buildings");
        foreach (Renderer renderer in map.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer.shadowCastingMode == ShadowCastingMode.Off) continue;
            if (buildings != null && renderer.transform.IsChildOf(buildings)) continue;
            Vector3 size = renderer.bounds.size;
            if (Mathf.Max(size.x, size.z) > 3f || size.y > 2.2f) continue;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            EditorUtility.SetDirty(renderer);
            shadowOff++;
        }

        log.AppendLine("· 작은 소품 그림자 끔: " + shadowOff + "개 (높이 2.2 m, 폭 3 m 이하)");

        // 지형: 화면 오차 4 → 6 px, 나무 거리 240 → 200 m, 기본 맵 거리 220 → 150 m
        Terrain terrain = map.GetComponentInChildren<Terrain>();
        if (terrain != null)
        {
            log.AppendLine("· 지형 Pixel Error " + terrain.heightmapPixelError + " → 6, 나무 거리 " + terrain.treeDistance + " → 200 m, Basemap 거리 " + terrain.basemapDistance + " → 150 m");
            terrain.heightmapPixelError = 6f;
            terrain.treeDistance = 200f;
            terrain.basemapDistance = 150f;
            EditorUtility.SetDirty(terrain);
        }

        // 카메라: 깊이/불투명 텍스처를 쓰는 기능이 없으면 끈다. (SSAO는 자체적으로 깊이를 요청함)
        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            UniversalAdditionalCameraData data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) continue;
            if (camera.orthographic)
            {
                // 미니맵 카메라는 그림자·후처리가 필요 없다.
                data.renderShadows = false;
                data.renderPostProcessing = false;
                EditorUtility.SetDirty(data);
                log.AppendLine("· 미니맵 카메라 '" + camera.name + "': 그림자·후처리 끔");
            }
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
        EditorSceneManager.SaveScene(map.gameObject.scene);
        WriteLog("GPU", log.ToString());
        EditorUtility.DisplayDialog("GPU 최적화", log + "\n되돌리기: Tools > L4D 과제 > 3-3. GPU 설정 되돌리기", "확인");
    }

    [MenuItem(Menu + "3-2. RAM 최적화 적용 (텍스처·오디오 임포트 설정)", priority = 41)]
    public static void ApplyRamOptimization()
    {
        if (!EditorUtility.DisplayDialog("RAM 최적화",
                "게임 씬이 쓰는 텍스처와 프로젝트 오디오의 임포트 설정을 바꿉니다. (다시 임포트하느라 몇 분 걸릴 수 있음)\n\n"
                + "텍스처: 4096 → 2048 제한, 압축 안 된 것은 압축, Flooded Grounds 텍스처 Read/Write 끔\n"
                + "오디오: 3초 미만 = Decompress On Load(ADPCM), 3~15초 = Compressed In Memory(Vorbis), 15초 이상 = Streaming, 15초 미만은 Mono\n\n"
                + "바뀐 목록과 전후 메모리는 Benchmarks/RAM_Report 에 저장됩니다.", "적용", "취소"))
        {
            return;
        }

        StringBuilder log = new StringBuilder();
        string scenePath = SceneManager.GetActiveScene().path;
        string[] dependencies = AssetDatabase.GetDependencies(scenePath, true);
        List<string> texturePaths = dependencies.Where(p => AssetImporter.GetAtPath(p) is TextureImporter).ToList();
        long textureBefore = 0;
        long textureAfter = 0;
        List<string> changedTextures = new List<string>();
        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < texturePaths.Count; i++)
            {
                string path = texturePaths[i];
                if (i % 20 == 0) EditorUtility.DisplayProgressBar("RAM 최적화", "텍스처 " + i + "/" + texturePaths.Count, (float)i / texturePaths.Count);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                if (texture != null) textureBefore += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
                if (importer.textureType == TextureImporterType.Sprite || importer.textureType == TextureImporterType.GUI || importer.textureType == TextureImporterType.Lightmap) continue;
                bool changed = false;
                List<string> what = new List<string>();
                if (importer.maxTextureSize > 2048)
                {
                    importer.maxTextureSize = 2048;
                    what.Add("최대 2048");
                    changed = true;
                }

                if (importer.textureCompression == TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = importer.textureType == TextureImporterType.NormalMap ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Compressed;
                    what.Add("압축");
                    changed = true;
                }

                if (importer.isReadable && path.StartsWith("Assets/Flooded_Grounds/"))
                {
                    importer.isReadable = false;
                    what.Add("Read/Write 끔");
                    changed = true;
                }

                if (changed)
                {
                    changedTextures.Add(path + " (" + string.Join(", ", what) + ")");
                    importer.SaveAndReimport();
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }

        foreach (string path in texturePaths)
        {
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
            if (texture != null) textureAfter += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
        }

        // 오디오
        string[] audioGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/06.Sounds", "Assets/Flooded_Grounds" });
        long audioBefore = 0;
        long audioAfter = 0;
        List<string> changedAudio = new List<string>();
        foreach (string guid in audioGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (importer == null || clip == null) continue;
            audioBefore += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(clip);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            float length = clip.length;
            AudioClipLoadType load = length < 3f ? AudioClipLoadType.DecompressOnLoad : length < 15f ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.Streaming;
            AudioCompressionFormat format = length < 3f ? AudioCompressionFormat.ADPCM : AudioCompressionFormat.Vorbis;
            bool mono = length < 15f;
            if (settings.loadType == load && settings.compressionFormat == format && importer.forceToMono == mono) continue;
            changedAudio.Add(path + " (" + length.ToString("0.0") + "초: " + settings.loadType + "/" + settings.compressionFormat + " → " + load + "/" + format + (mono ? ", Mono" : string.Empty) + ")");
            settings.loadType = load;
            settings.compressionFormat = format;
            settings.quality = length < 15f ? 0.6f : 0.5f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = mono;
            importer.SaveAndReimport();
        }

        foreach (string guid in audioGuids)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip != null) audioAfter += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(clip);
        }

        log.AppendLine("✔ 텍스처 " + texturePaths.Count + "개 중 " + changedTextures.Count + "개 변경: " + Mb(textureBefore) + " MB → " + Mb(textureAfter) + " MB (에디터 기준 추정)");
        log.AppendLine("✔ 오디오 " + audioGuids.Length + "개 중 " + changedAudio.Count + "개 변경: " + Mb(audioBefore) + " MB → " + Mb(audioAfter) + " MB (에디터 기준 추정)");
        log.AppendLine("· 모델(FBX)의 Read/Write는 스크립트(SoldierMeshReadable 등)가 쓰므로 바꾸지 않았습니다.");
        StringBuilder report = new StringBuilder(log.ToString());
        report.AppendLine();
        report.AppendLine("## 바뀐 텍스처");
        foreach (string line in changedTextures) report.AppendLine("- " + line);
        report.AppendLine();
        report.AppendLine("## 바뀐 오디오");
        foreach (string line in changedAudio) report.AppendLine("- " + line);
        report.AppendLine();
        report.AppendLine("## 메모리 큰 텍스처 20개 (적용 후)");
        foreach (var item in texturePaths.Select(p => new { Path = p, Texture = AssetDatabase.LoadAssetAtPath<Texture>(p) }).Where(x => x.Texture != null)
                     .Select(x => new { x.Path, Size = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(x.Texture), x.Texture.width, x.Texture.height })
                     .OrderByDescending(x => x.Size).Take(20))
        {
            report.AppendLine("- " + item.Path + " " + item.width + "x" + item.height + " " + Mb(item.Size) + " MB");
        }

        WriteLog("RAM_Report", report.ToString());
        EditorUtility.DisplayDialog("RAM 최적화", log.ToString(), "확인");
    }

    [MenuItem(Menu + "3-3. GPU 설정 되돌리기 (Before 상태)", priority = 42)]
    public static void RestoreGpu()
    {
        int restored = 0;
        // 파일 내용을 그대로 되돌린다. (.meta guid 유지 → 참조 유지, 렌더러 기능이 백업 파일을 가리키지 않음)
        AssetDatabase.SaveAssets();
        foreach (string path in new[] { UrpAssetPath, UrpRendererPath })
        {
            string backup = BackupFolder + "/" + Path.GetFileName(path);
            if (!File.Exists(backup)) continue;
            File.Copy(backup, path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            restored++;
        }

        EditorUtility.DisplayDialog("되돌리기", restored > 0 ? "URP 설정 " + restored + "개를 Before 상태로 되돌렸습니다.\n(소품 그림자/지형 설정은 백업 씬으로 되돌리세요)" : "백업이 없습니다.", "확인");
    }

    // ======================================================================
    // 도우미
    // ======================================================================

    private static CityMapInfo FindCampaign()
    {
        return Object.FindObjectsByType<CityMapInfo>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(info => info.name == RootName);
    }

    private static void BackupAsset(string path, StringBuilder log)
    {
        string target = BackupFolder + "/" + Path.GetFileName(path);
        if (File.Exists(target))
        {
            log.AppendLine("· 백업 유지: " + target + " (처음 적용 전 상태)");
            return;
        }

        if (AssetDatabase.CopyAsset(path, target)) log.AppendLine("· 백업: " + target);
    }

    private static void Change(SerializedObject so, string name, int value, StringBuilder log, string label)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property == null)
        {
            log.AppendLine("⚠ 설정 없음: " + name);
            return;
        }

        int before = property.propertyType == SerializedPropertyType.Boolean ? (property.boolValue ? 1 : 0) : property.intValue;
        if (property.propertyType == SerializedPropertyType.Boolean) property.boolValue = value != 0;
        else if (property.propertyType == SerializedPropertyType.Enum) property.enumValueIndex = value;
        else property.intValue = value;
        log.AppendLine("· " + label + ": " + before + " → " + value);
    }

    private static void ChangeFloat(SerializedObject so, string name, float value, StringBuilder log, string label)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property == null)
        {
            log.AppendLine("⚠ 설정 없음: " + name);
            return;
        }

        log.AppendLine("· " + label + ": " + property.floatValue.ToString("0.##") + " → " + value.ToString("0.##"));
        property.floatValue = value;
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property == null) return;
        if (property.propertyType == SerializedPropertyType.Integer) property.intValue = Mathf.RoundToInt(value);
        else property.floatValue = value;
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.boolValue = value;
    }

    private static string Mb(long bytes)
    {
        return (bytes / 1048576.0).ToString("0.0");
    }

    private static void WriteLog(string name, string text)
    {
        string root = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Benchmarks");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, name + "_" + DateTime.Now.ToString("MMdd_HHmmss") + ".md");
        File.WriteAllText(path, "# " + name + " (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")\n\n" + text, new UTF8Encoding(false));
        Debug.Log("[과제] " + name + "\n" + text + "\n→ " + path);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
#endif
