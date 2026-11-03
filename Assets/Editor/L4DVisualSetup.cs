#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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

// 캠페인 맵의 실제 조명·후처리 설정 (과제용 도구와 별개)
// 메뉴: Tools > L4D 비주얼
//
//  - 시간 흐름: 진행할수록 늦은 오후 → 해질녘 → 노을 끝 → 블루 아워 → 밤 (AtmosphereDirector)
//  - 기본 색보정: ACES, Bloom, Shadows/Midtones/Highlights, Vignette, Film Grain, SSAO, SMAA
//  - 구역 볼륨: 안전가옥은 따뜻하고 밝게, 피날레 정원은 붉고 긴장감 있게
//  - 전투 반응 후처리: 체력 낮음(흑백+심장박동 비네트), 피격, 호드 긴장, 폭발 섬광 (CombatPostFX)
//  - 총구 화염·폭발 조명 (LightBurst), 손전등 빛줄기 (VolumetricBeam), 낮게 깔린 안개 파티클 (L4D/Soft Mist)
//  - 그림자: 해/달 실시간 그림자 4단계 90 m, 손전등 그림자
//  - 라이트맵: 건물·지형에 실내등 + 하늘빛 + AO를 굽고, 소품/좀비는 라이트 프로브
//    (해는 시간이 흐르며 움직이므로 Realtime. 라이트맵에는 굽지 않는다)
public static class L4DVisualSetup
{
    private const string Menu = "Tools/L4D 비주얼/";
    private const string RootName = "FloodedCampaign";
    private const string Folder = "Assets/L4DCampaign/Visual";
    private const string ProfileFolder = Folder + "/Profiles";
    private const string BackupFolder = Folder + "/Backup";
    private const string LightingPath = Folder + "/CampaignLighting_Visual.lighting";
    private const string MistMaterialPath = Folder + "/Resources/L4DVisual/SoftMist.mat";
    private const string MeshFolder = "Assets/L4DCampaign/Generated/Meshes";
    private const string UrpAssetPath = "Assets/Settings/PC_RPAsset.asset";
    private const string UrpRendererPath = "Assets/Settings/PC_Renderer.asset";
    private const float WaterLevel = 10.8f;
    private const int TriggerLayer = 2;

    // ======================================================================
    // 메뉴
    // ======================================================================

    [MenuItem(Menu + "1. 조명·그림자·후처리 전체 적용 + 라이트맵 굽기", priority = 0)]
    public static void ApplyAll()
    {
        CityMapInfo map = FindCampaign();
        if (map == null)
        {
            EditorUtility.DisplayDialog("L4D 비주얼", "FloodedCampaign이 있는 L4D 씬을 연 뒤 실행해 주세요.", "확인");
            return;
        }

        int choice = EditorUtility.DisplayDialogComplex("조명·후처리 전체 적용",
            "1. URP 그림자/HDR/깊이 텍스처/SSAO를 고사양 PC 기준으로 설정 (원래 설정은 백업)\n"
            + "2. 진행할수록 시간이 흐르는 분위기 (오후 → 해질녘 → 블루 아워 → 밤)\n"
            + "3. 기본 색보정 + 구역 볼륨(안전가옥, 피날레) + 전투 반응 후처리\n"
            + "4. 낮게 깔린 안개, 손전등 빛줄기, 총구/폭발 조명\n"
            + "5. 라이트맵 + 라이트 프로브 + 반사 프로브 굽기\n\n"
            + "굽기는 PC에 따라 10~40분 걸립니다. 굽는 동안 에디터는 쓸 수 있습니다.",
            "적용 + 굽기", "취소", "적용만 (굽기는 나중에)");
        if (choice == 1) return;

        StringBuilder log = new StringBuilder();
        bool ok = false;
        try
        {
            EnsureFolder(ProfileFolder);
            EnsureFolder(BackupFolder);
            Progress("URP 설정", 0.05f);
            SetupUrp(log);
            Progress("기본 색보정", 0.15f);
            SetupBaseVolume(log);
            Progress("시간 흐름", 0.25f);
            AtmosphereDirector director = SetupAtmosphere(map, log);
            Progress("구역 볼륨", 0.35f);
            SetupZoneVolumes(map, director.transform, log);
            Progress("안개", 0.4f);
            SetupMist(map, director.transform, log);
            Progress("카메라", 0.45f);
            SetupCameras(log);
            Progress("라이트맵 준비 (UV 생성)", 0.5f);
            PrepareBake(map, log);
            director.Apply(0f);
            SetBakeAmbient();
            EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            EditorSceneManager.SaveScene(map.gameObject.scene);
            ok = true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            log.AppendLine("❌ 오류: " + exception.Message + " (굽기는 시작하지 않았습니다)");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (choice == 0 && ok) StartBake(log);
        WriteLog(log.ToString());
        EditorUtility.DisplayDialog("L4D 비주얼", log + (choice == 0 ? "\n굽기를 시작했습니다. 오른쪽 아래 진행 막대가 끝나면 씬이 자동 저장됩니다." : string.Empty), "확인");
    }

    [MenuItem(Menu + "2. 라이트맵만 다시 굽기", priority = 1)]
    public static void BakeOnly()
    {
        CityMapInfo map = FindCampaign();
        if (map == null) return;
        StringBuilder log = new StringBuilder();
        AtmosphereDirector director = map.GetComponentInChildren<AtmosphereDirector>();
        if (director != null) director.Apply(0f);
        SetBakeAmbient();
        StartBake(log);
        EditorUtility.DisplayDialog("L4D 비주얼", log.ToString(), "확인");
    }

    [MenuItem(Menu + "시간대 미리보기/0% 늦은 오후 (출발)", priority = 20)]
    public static void Preview0() => Preview(0f);

    [MenuItem(Menu + "시간대 미리보기/35% 해질녘 (공장 지대)", priority = 21)]
    public static void Preview35() => Preview(0.35f);

    [MenuItem(Menu + "시간대 미리보기/60% 노을 끝 (철교)", priority = 22)]
    public static void Preview60() => Preview(0.6f);

    [MenuItem(Menu + "시간대 미리보기/78% 블루 아워 (읍내)", priority = 23)]
    public static void Preview78() => Preview(0.78f);

    [MenuItem(Menu + "시간대 미리보기/100% 밤 (피날레)", priority = 24)]
    public static void Preview100() => Preview(1f);

    private static void Preview(float t)
    {
        AtmosphereDirector director = Object.FindAnyObjectByType<AtmosphereDirector>();
        if (director == null)
        {
            EditorUtility.DisplayDialog("L4D 비주얼", "먼저 '1. 조명·그림자·후처리 전체 적용'을 실행해 주세요.", "확인");
            return;
        }

        director.Apply(t);
        SceneView.RepaintAll();
        Debug.Log("[L4D 비주얼] 시간대 미리보기 " + (t * 100f).ToString("0") + "% (하늘 밝기는 Play 중에만 바뀝니다. 굽기 전에는 0%로 돌려 두세요)");
    }

    [MenuItem(Menu + "URP 설정 되돌리기 (적용 전)", priority = 40)]
    public static void RestoreUrp()
    {
        int restored = 0;
        // 파일 내용을 그대로 되돌린다. (.meta의 guid는 그대로라 참조가 유지되고, 렌더러 기능이 백업 파일을 가리키지 않는다)
        AssetDatabase.SaveAssets();
        foreach (string path in new[] { UrpAssetPath, UrpRendererPath })
        {
            string backup = BackupFolder + "/" + Path.GetFileName(path);
            if (!File.Exists(backup)) continue;
            File.Copy(backup, path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            restored++;
        }

        EditorUtility.DisplayDialog("L4D 비주얼", restored > 0 ? "URP 설정을 적용 전으로 되돌렸습니다." : "백업이 없습니다.", "확인");
    }

    // ======================================================================
    // URP (고사양 PC)
    // ======================================================================

    private static void SetupUrp(StringBuilder log)
    {
        Backup(UrpAssetPath);
        Backup(UrpRendererPath);
        Object urp = AssetDatabase.LoadMainAssetAtPath(UrpAssetPath);
        if (urp != null)
        {
            SerializedObject so = new SerializedObject(urp);
            SetFloat(so, "m_ShadowDistance", 90f);
            SetInt(so, "m_ShadowCascadeCount", 4);
            SetInt(so, "m_MainLightShadowmapResolution", 4096);
            SetBool(so, "m_AdditionalLightShadowsSupported", true);     // 손전등 그림자
            SetInt(so, "m_AdditionalLightsShadowmapResolution", 4096);
            SetInt(so, "m_AdditionalLightsShadowResolutionTierHigh", 2048);   // 손전등 그림자 해상도 (High 단계)
            SetInt(so, "m_SoftShadowQuality", 3);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetBool(so, "m_SupportsHDR", true);
            SetBool(so, "m_RequireDepthTexture", true);                  // 안개/빛줄기 소프트 경계
            SetInt(so, "m_ColorGradingMode", 1);                         // HDR 색보정
            SetInt(so, "m_ColorGradingLutSize", 32);
            SetBool(so, "m_MixedLightingSupported", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
        }

        ScriptableObject ssao = AssetDatabase.LoadAllAssetsAtPath(UrpRendererPath).OfType<ScriptableObject>().FirstOrDefault(o => o != null && o.GetType().Name == "ScreenSpaceAmbientOcclusion");
        if (ssao != null)
        {
            SerializedObject so = new SerializedObject(ssao);
            SetBool(so, "m_Active", true);
            SetFloat(so, "m_Settings.Intensity", 1.1f);
            SetFloat(so, "m_Settings.Radius", 0.5f);
            SetFloat(so, "m_Settings.DirectLightingStrength", 0.3f);
            SetFloat(so, "m_Settings.Falloff", 60f);
            SetBool(so, "m_Settings.Downsample", false);
            SetInt(so, "m_Settings.BlurQuality", 0);
            SetBool(so, "m_Settings.AfterOpaque", false);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ssao);
        }

        AssetDatabase.SaveAssets();
        log.AppendLine("✔ URP: 그림자 90 m·4단계·4096, 손전등 그림자, 부드러운 그림자 High, HDR 색보정, 깊이 텍스처, SSAO(1.1, 0.5 m)");
    }

    // ======================================================================
    // 기본 색보정 (항상 적용)
    // ======================================================================

    private static void SetupBaseVolume(StringBuilder log)
    {
        Volume volume = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(v => v.isGlobal && v.GetComponentInParent<AtmosphereDirector>() == null && v.GetComponentInParent<CombatPostFX>() == null);
        if (volume == null)
        {
            GameObject go = new GameObject("Global Volume");
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
        }

        volume.priority = 0;
        VolumeProfile profile = NewProfile("Base_L4D");
        Tonemapping tonemapping = Add<Tonemapping>(profile);
        tonemapping.mode.Override(TonemappingMode.ACES);
        Bloom bloom = Add<Bloom>(profile);
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0.7f);
        bloom.scatter.Override(0.7f);
        bloom.tint.Override(new Color(1f, 0.88f, 0.76f));
        bloom.highQualityFiltering.Override(true);
        ColorAdjustments color = Add<ColorAdjustments>(profile);
        color.contrast.Override(16f);
        ShadowsMidtonesHighlights split = Add<ShadowsMidtonesHighlights>(profile);
        split.shadows.Override(new Vector4(0.9f, 0.98f, 1.06f, -0.02f));
        split.highlights.Override(new Vector4(1.06f, 1f, 0.93f, 0.02f));
        Vignette vignette = Add<Vignette>(profile);
        vignette.intensity.Override(0.26f);
        vignette.smoothness.Override(0.42f);
        FilmGrain grain = Add<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Medium1);
        grain.intensity.Override(0.16f);
        grain.response.Override(0.8f);
        ChromaticAberration aberration = Add<ChromaticAberration>(profile);
        aberration.intensity.Override(0.04f);
        SaveProfile(profile);
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);
        log.AppendLine("✔ 기본 색보정: ACES, Bloom(HQ), 대비, 청록 그림자/주황 하이라이트, Vignette, Film Grain, 색수차");
    }

    // ======================================================================
    // 시간 흐름
    // ======================================================================

    private static AtmosphereDirector SetupAtmosphere(CityMapInfo map, StringBuilder log)
    {
        Transform old = map.transform.Find("Atmosphere");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        GameObject root = new GameObject("Atmosphere");
        root.transform.SetParent(map.transform, false);
        AtmosphereDirector director = root.AddComponent<AtmosphereDirector>();
        root.AddComponent<CombatPostFX>();

        Light sun = map.GetComponentsInChildren<Light>(true).FirstOrDefault(l => l.type == LightType.Directional);
        if (sun == null)
        {
            GameObject go = new GameObject("Sun");
            go.transform.SetParent(map.transform, false);
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
        }

        sun.lightmapBakeType = LightmapBakeType.Realtime;   // 시간이 흐르며 움직인다.
        sun.shadows = LightShadows.Soft;
        sun.shadowBias = 0.04f;
        sun.shadowNormalBias = 0.35f;
        RenderSettings.sun = sun;

        _pendingProfiles.Clear();
        AtmosphereDirector.Key[] keys =
        {
            Key("늦은 오후", 0f, new Vector2(24f, 232f), new Color(1f, 0.86f, 0.7f), 1.25f, 0.85f,
                new Color(0.56f, 0.56f, 0.55f), 22f, 300f, 1f, new Color(0.5f, 0.49f, 0.47f),
                new Color(0.5f, 0.51f, 0.54f), new Color(0.34f, 0.35f, 0.35f), new Color(0.13f, 0.12f, 0.1f),
                0.1f, 6f, 0f, -10f, new Color(1f, 0.98f, 0.94f), 0f),
            Key("해질녘", 0.35f, new Vector2(9f, 244f), new Color(1f, 0.6f, 0.34f), 1.15f, 0.9f,
                new Color(0.62f, 0.48f, 0.38f), 18f, 260f, 0.85f, new Color(0.62f, 0.46f, 0.38f),
                new Color(0.55f, 0.43f, 0.38f), new Color(0.36f, 0.28f, 0.24f), new Color(0.12f, 0.09f, 0.07f),
                0.05f, 18f, 4f, -8f, new Color(1f, 0.93f, 0.84f), 0.1f),
            Key("노을 끝", 0.6f, new Vector2(2f, 250f), new Color(0.95f, 0.36f, 0.22f), 0.5f, 0.7f,
                new Color(0.36f, 0.29f, 0.31f), 15f, 220f, 0.5f, new Color(0.52f, 0.36f, 0.38f),
                new Color(0.36f, 0.3f, 0.34f), new Color(0.22f, 0.18f, 0.19f), new Color(0.07f, 0.06f, 0.05f),
                -0.15f, 6f, 6f, -18f, new Color(0.97f, 0.87f, 0.87f), 0.2f),
            Key("블루 아워", 0.78f, new Vector2(30f, 62f), new Color(0.56f, 0.67f, 0.95f), 0.24f, 0.6f,
                new Color(0.16f, 0.19f, 0.26f), 12f, 190f, 0.25f, new Color(0.32f, 0.4f, 0.58f),
                new Color(0.2f, 0.24f, 0.34f), new Color(0.11f, 0.13f, 0.17f), new Color(0.04f, 0.04f, 0.05f),
                -0.35f, -18f, 0f, -26f, new Color(0.83f, 0.9f, 1.04f), 0.3f),
            Key("밤", 1f, new Vector2(42f, 70f), new Color(0.5f, 0.6f, 0.9f), 0.17f, 0.55f,
                new Color(0.07f, 0.08f, 0.11f), 10f, 160f, 0.12f, new Color(0.28f, 0.34f, 0.5f),
                new Color(0.12f, 0.14f, 0.2f), new Color(0.07f, 0.08f, 0.1f), new Color(0.02f, 0.02f, 0.03f),
                -0.55f, -25f, 0f, -34f, new Color(0.78f, 0.86f, 1f), 0.4f),
        };

        for (int i = 0; i < keys.Length; i++)
        {
            GameObject go = new GameObject("Time_" + i + "_" + keys[i].name);
            go.transform.SetParent(root.transform, false);
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10 + i;
            volume.weight = i == 0 ? 1f : 0f;
            volume.sharedProfile = _pendingProfiles[i];
            keys[i].volume = volume;
        }

        _pendingProfiles.Clear();
        director.Configure(sun, keys);
        EditorUtility.SetDirty(director);
        log.AppendLine("✔ 시간 흐름: 늦은 오후(0%) → 해질녘(35%) → 노을 끝(60%) → 블루 아워(78%) → 밤(100%). 해/달·안개·하늘·환경광·색보정이 진행도에 따라 바뀜");
        log.AppendLine("  · 값은 FloodedCampaign/Atmosphere 의 AtmosphereDirector 와 Profiles 폴더의 Time_*.asset 에서 조절");
        return director;
    }

    private static readonly List<VolumeProfile> _pendingProfiles = new List<VolumeProfile>();

    private static AtmosphereDirector.Key Key(string name, float progress, Vector2 sunAngles, Color sunColor, float sunIntensity, float shadow,
        Color fog, float fogStart, float fogEnd, float skyExposure, Color skyTint, Color ambientSky, Color ambientEquator, Color ambientGround,
        float exposure, float temperature, float tint, float saturation, Color filter, float extraBloom)
    {
        VolumeProfile profile = NewProfile("Time_" + _pendingProfiles.Count + "_" + name);
        ColorAdjustments color = Add<ColorAdjustments>(profile);
        color.postExposure.Override(exposure);
        color.saturation.Override(saturation);
        color.colorFilter.Override(filter);
        WhiteBalance white = Add<WhiteBalance>(profile);
        white.temperature.Override(temperature);
        white.tint.Override(tint);
        Bloom bloom = Add<Bloom>(profile);
        bloom.intensity.Override(0.7f + extraBloom * 1.5f);   // 어두워질수록 불빛이 더 번진다.
        bloom.threshold.Override(0.95f - extraBloom * 0.5f);
        SaveProfile(profile);
        _pendingProfiles.Add(profile);
        return new AtmosphereDirector.Key
        {
            name = name,
            progress = progress,
            sunAngles = sunAngles,
            sunColor = sunColor,
            sunIntensity = sunIntensity,
            shadowStrength = shadow,
            fogColor = fog,
            fogStart = fogStart,
            fogEnd = fogEnd,
            skyExposure = skyExposure,
            skyTint = skyTint,
            ambientSky = ambientSky,
            ambientEquator = ambientEquator,
            ambientGround = ambientGround,
        };
    }

    // ======================================================================
    // 구역 볼륨
    // ======================================================================

    private static void SetupZoneVolumes(CityMapInfo map, Transform parent, StringBuilder log)
    {
        Transform zones = new GameObject("ZoneVolumes").transform;
        zones.SetParent(parent, false);

        VolumeProfile safe = NewProfile("Zone_SafeRoom");
        ColorAdjustments safeColor = Add<ColorAdjustments>(safe);
        safeColor.postExposure.Override(0.3f);
        safeColor.saturation.Override(-2f);
        WhiteBalance safeWhite = Add<WhiteBalance>(safe);
        safeWhite.temperature.Override(16f);
        Vignette safeVignette = Add<Vignette>(safe);
        safeVignette.intensity.Override(0.18f);
        Bloom safeBloom = Add<Bloom>(safe);
        safeBloom.intensity.Override(1f);
        SaveProfile(safe);

        VolumeProfile finale = NewProfile("Zone_Finale");
        ColorAdjustments finaleColor = Add<ColorAdjustments>(finale);
        finaleColor.contrast.Override(28f);
        finaleColor.saturation.Override(-12f);
        finaleColor.colorFilter.Override(new Color(1f, 0.86f, 0.84f));
        Vignette finaleVignette = Add<Vignette>(finale);
        finaleVignette.intensity.Override(0.38f);
        finaleVignette.color.Override(new Color(0.12f, 0f, 0f));
        ChromaticAberration finaleAberration = Add<ChromaticAberration>(finale);
        finaleAberration.intensity.Override(0.1f);
        SaveProfile(finale);

        int count = 0;
        if (map.StartSafeRoomArea != null) { LocalVolume(zones, "Zone_StartSafeRoom", map.StartSafeRoomArea.bounds, 1f, 2.5f, safe); count++; }
        foreach (BoxCollider room in map.MidSafeRooms ?? new BoxCollider[0])
        {
            if (room == null) continue;
            LocalVolume(zones, "Zone_MidSafeRoom", room.bounds, 1f, 2.5f, safe);
            count++;
        }

        if (map.FinaleArea != null) { LocalVolume(zones, "Zone_Finale", map.FinaleArea.bounds, 0f, 12f, finale); count++; }
        log.AppendLine("✔ 구역 볼륨 " + count + "개: 안전가옥(따뜻하고 밝게), 피날레 정원(붉고 대비 강하게) - 들어가면 부드럽게 섞임");
    }

    private static void LocalVolume(Transform parent, string name, Bounds bounds, float grow, float blend, VolumeProfile profile)
    {
        GameObject go = new GameObject(name);
        go.layer = TriggerLayer;
        go.transform.SetParent(parent, false);
        go.transform.position = bounds.center;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = bounds.size + Vector3.one * grow;
        go.AddComponent<Unity.AI.Navigation.NavMeshModifier>().ignoreFromBuild = true;
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = false;
        volume.blendDistance = blend;
        volume.priority = 50;
        volume.sharedProfile = profile;
    }

    // ======================================================================
    // 낮게 깔린 안개
    // ======================================================================

    private static void SetupMist(CityMapInfo map, Transform parent, StringBuilder log)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MistMaterialPath);
        if (material == null)
        {
            log.AppendLine("⚠ 안개 머티리얼(" + MistMaterialPath + ")이 없어 안개를 만들지 않았습니다.");
            return;
        }

        Terrain terrain = map.GetComponentInChildren<Terrain>();
        Transform mist = new GameObject("GroundMist").transform;
        mist.SetParent(parent, false);
        Transform[] route = map.RoutePoints;
        int count = 0;
        for (int i = 0; i < route.Length; i += 3)
        {
            Vector3 p = route[i].position;
            bool nearWater = false;
            if (terrain != null)
            {
                for (int k = 0; k < 8 && !nearWater; k++)
                {
                    float angle = k * Mathf.PI / 4f;
                    Vector3 probe = p + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 14f;
                    nearWater = terrain.SampleHeight(probe) + terrain.transform.position.y < WaterLevel + 0.3f;
                }
            }

            bool finale = map.FinaleArea != null && map.FinaleArea.bounds.Contains(new Vector3(p.x, map.FinaleArea.bounds.center.y, p.z));
            if (!nearWater && !finale) continue;
            CreateMist(mist, "Mist_" + i.ToString("000"), p, material, finale ? 30f : 22f);
            count++;
        }

        if (map.ExtractionPoint != null)
        {
            CreateMist(mist, "Mist_Dock", map.ExtractionPoint.position, material, 26f);
            count++;
        }

        log.AppendLine("✔ 낮게 깔린 안개 파티클 " + count + "곳 (늪지·강변·철교·정원·선착장, 소프트 파티클)");
    }

    private static void CreateMist(Transform parent, string name, Vector3 position, Material material, float size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position + Vector3.up * 0.6f;
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);   // 재생 중이면 duration을 바꿀 수 없다.
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 20f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 22f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        main.startSize = new ParticleSystem.MinMaxCurve(7f, 13f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.55f), new Color(0.92f, 0.94f, 0.96f, 0.9f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 18;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 1f;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(size, 0.6f, size);
        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        velocity.y = new ParticleSystem.MinMaxCurve(0f, 0.02f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.maxParticleSize = 3f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    // ======================================================================
    // 카메라
    // ======================================================================

    private static void SetupCameras(StringBuilder log)
    {
        int count = 0;
        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            UniversalAdditionalCameraData data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) continue;
            if (camera.orthographic || camera.targetTexture != null)
            {
                data.renderPostProcessing = false;
                data.renderShadows = false;
                EditorUtility.SetDirty(data);
                continue;
            }

            if (data.renderType != CameraRenderType.Base) continue;
            camera.allowHDR = true;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 330f);
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = true;
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(data);
            count++;
        }

        log.AppendLine("✔ 카메라 " + count + "개: HDR, 후처리, SMAA High / 미니맵 카메라는 그림자·후처리 끔");
    }

    // ======================================================================
    // 라이트맵 / 프로브
    // ======================================================================

    private static void PrepareBake(CityMapInfo map, StringBuilder log)
    {
        // 1. 합친 건물 메시는 조각들의 UV2가 겹쳐 있으므로 라이트맵 UV를 새로 만든다.
        int unwrapped = 0;
        string[] guids = AssetDatabase.FindAssets("t:Mesh", new[] { MeshFolder });
        for (int g = 0; g < guids.Length; g++)
        {
            foreach (Mesh mesh in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guids[g])).OfType<Mesh>())
            {
                if (mesh.name.EndsWith("_Collider")) continue;
                Progress("라이트맵 UV: " + mesh.name, 0.5f + 0.3f * g / Mathf.Max(1, guids.Length));
                UnwrapParam param;
                UnwrapParam.SetDefaults(out param);
                param.packMargin = 4f / 1024f;
                Unwrapping.GenerateSecondaryUVSet(mesh, param);
                EditorUtility.SetDirty(mesh);
                unwrapped++;
            }
        }

        AssetDatabase.SaveAssets();

        // 2. 건물(LOD 메시)·지형은 라이트맵, 소품은 라이트 프로브
        Transform buildings = map.transform.Find("Buildings");
        Transform props = map.transform.Find("Props");
        int lightmapped = 0;
        int probed = 0;
        foreach (MeshRenderer renderer in map.GetComponentsInChildren<MeshRenderer>(true))
        {
            bool isBuilding = buildings != null && renderer.transform.IsChildOf(buildings);
            bool isProp = props != null && renderer.transform.IsChildOf(props);
            if (!isBuilding && !isProp) continue;
            GameObject go = renderer.gameObject;
            StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
            if ((flags & StaticEditorFlags.BatchingStatic) == 0) continue;
            GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.ContributeGI);
            if (isBuilding && go.name.StartsWith("LOD"))
            {
                renderer.receiveGI = ReceiveGI.Lightmaps;
                SerializedObject so = new SerializedObject(renderer);   // receiveGI를 바꾼 뒤에 만들어야 되돌려지지 않는다.
                SetFloat(so, "m_ScaleInLightmap", go.name == "LOD0" ? 1f : 0.4f);
                so.ApplyModifiedPropertiesWithoutUndo();
                lightmapped++;
            }
            else
            {
                renderer.receiveGI = ReceiveGI.LightProbes;
                probed++;
            }

            EditorUtility.SetDirty(renderer);
        }

        Terrain terrain = map.GetComponentInChildren<Terrain>();
        if (terrain != null)
        {
            GameObjectUtility.SetStaticEditorFlags(terrain.gameObject, GameObjectUtility.GetStaticEditorFlags(terrain.gameObject) | StaticEditorFlags.ContributeGI);
            SerializedObject so = new SerializedObject(terrain);
            SetFloat(so, "m_ScaleInLightmap", 0.35f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 3. 조명: 깜빡이는 불(조명탄·드럼통·무전기) = Realtime, 나머지 실내등/선착장 등 = Baked, 해/달 = Realtime
        int baked = 0;
        int realtime = 0;
        foreach (Light light in map.GetComponentsInChildren<Light>(true))
        {
            if (light.type == LightType.Directional) continue;
            if (light.GetComponent<MapFlickerLight>() != null)
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
                SetFloat(so, "m_ShadowRadius", 0.12f);
                so.ApplyModifiedPropertiesWithoutUndo();
                baked++;
            }

            EditorUtility.SetDirty(light);
        }

        // 4. 라이트 프로브 (경로마다 4개 + 안전가옥·피날레 격자) / 반사 프로브
        SetupProbes(map);

        // 5. 라이트맵 설정
        EnsureFolder(Folder);
        LightingSettings settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
        if (settings == null)
        {
            settings = new LightingSettings { name = "CampaignLighting_Visual" };
            AssetDatabase.CreateAsset(settings, LightingPath);
        }

        settings.bakedGI = true;
        settings.realtimeGI = false;
        settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
        settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
        settings.lightmapResolution = 3f;
        settings.lightmapPadding = 4;
        settings.lightmapMaxSize = 2048;
        settings.directionalityMode = LightmapsMode.CombinedDirectional;   // 노멀맵이 라이트맵 빛에도 반응
        settings.lightmapCompression = LightmapCompression.HighQuality;
        settings.ao = true;
        settings.aoMaxDistance = 1.5f;
        settings.directSampleCount = 32;
        settings.indirectSampleCount = 256;
        settings.environmentSampleCount = 256;
        settings.maxBounces = 3;
        settings.filteringMode = LightingSettings.FilterMode.Auto;
        EditorUtility.SetDirty(settings);
        Lightmapping.lightingSettings = settings;
        AssetDatabase.SaveAssets();
        log.AppendLine("✔ 라이트맵 준비: 건물 메시 UV2 " + unwrapped + "개, 라이트맵 렌더러 " + lightmapped + "개, 프로브 소품 " + probed + "개, Baked 조명 " + baked + "개, Realtime 조명 " + realtime + "개");
        log.AppendLine("  · 3 texel/m, Directional, AO, 바운스 3, GPU 라이트매퍼 (해/달은 Realtime이라 굽지 않음)");
    }

    // 굽는 동안의 환경광: 낮과 밤 사이 정도. (라이트맵의 하늘빛은 고정되므로 너무 밝지 않게)
    private static void SetBakeAmbient()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.34f, 0.35f, 0.4f);
        RenderSettings.ambientEquatorColor = new Color(0.22f, 0.22f, 0.24f);
        RenderSettings.ambientGroundColor = new Color(0.07f, 0.065f, 0.06f);
        RenderSettings.ambientIntensity = 1f;
    }

    private static void SetupProbes(CityMapInfo map)
    {
        foreach (string name in new[] { "LightProbes", "ReflectionProbes" })
        {
            Transform old = map.transform.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        GameObject probesObject = new GameObject("LightProbes");
        probesObject.transform.SetParent(map.transform, false);
        LightProbeGroup group = probesObject.AddComponent<LightProbeGroup>();
        List<Vector3> positions = new List<Vector3>();
        Transform[] route = map.RoutePoints;
        for (int i = 0; i < route.Length; i++)
        {
            Vector3 p = route[i].position;
            Vector3 forward = route[Mathf.Min(i + 1, route.Length - 1)].position - route[Mathf.Max(i - 1, 0)].position;
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
                    Vector3 p = new Vector3(Mathf.Lerp(b.min.x, b.max.x, (x + 0.5f) / steps), b.min.y + 0.8f, Mathf.Lerp(b.min.z, b.max.z, (z + 0.5f) / steps));
                    positions.Add(p);
                    positions.Add(p + Vector3.up * 1.8f);
                }
            }
        }

        group.probePositions = positions.Select(p => probesObject.transform.InverseTransformPoint(p)).ToArray();

        GameObject reflections = new GameObject("ReflectionProbes");
        reflections.transform.SetParent(map.transform, false);
        List<Vector3> spots = new[] { 0, 40, 97, 124, 149, route.Length - 1 }.Where(i => i >= 0 && i < route.Length).Select(i => route[i].position).ToList();
        if (map.MidSafeRooms != null && map.MidSafeRooms.Length > 0 && map.MidSafeRooms[0] != null) spots.Add(map.MidSafeRooms[0].bounds.center);
        for (int i = 0; i < spots.Count; i++)
        {
            GameObject go = new GameObject("ReflectionProbe_" + i);
            go.transform.SetParent(reflections.transform, false);
            go.transform.position = spots[i] + Vector3.up * 2f;
            ReflectionProbe probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;
            probe.size = new Vector3(45f, 18f, 45f);
            probe.resolution = 256;
            probe.boxProjection = true;
            probe.blendDistance = 5f;
        }
    }

    private static void StartBake(StringBuilder log)
    {
        Lightmapping.bakeCompleted -= OnBakeCompleted;
        Lightmapping.bakeCompleted += OnBakeCompleted;
        if (Lightmapping.BakeAsync())
        {
            log.AppendLine("✔ 라이트맵 굽기 시작 (Window > Rendering > Lighting 에서 진행 상황 확인)");
        }
        else
        {
            log.AppendLine("⚠ 굽기를 시작하지 못했습니다. Window > Rendering > Lighting 에서 Generate Lighting을 눌러 주세요.");
        }
    }

    private static void OnBakeCompleted()
    {
        Lightmapping.bakeCompleted -= OnBakeCompleted;
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[L4D 비주얼] 라이트맵 굽기 완료, 씬 저장: " + scene.path + " / 라이트맵 " + LightmapSettings.lightmaps.Length + "장");
    }

    // ======================================================================
    // 도우미
    // ======================================================================

    private static CityMapInfo FindCampaign()
    {
        return Object.FindObjectsByType<CityMapInfo>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(info => info.name == RootName);
    }

    private static VolumeProfile NewProfile(string name)
    {
        string path = ProfileFolder + "/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = name;
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component = profile.Add<T>(false);   // false: Override()로 고른 값만 덮어쓴다.
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    private static void SaveProfile(VolumeProfile profile)
    {
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
    }

    private static void Backup(string path)
    {
        string target = BackupFolder + "/" + Path.GetFileName(path);
        if (!File.Exists(target)) AssetDatabase.CopyAsset(path, target);
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property == null) return;
        if (property.propertyType == SerializedPropertyType.Integer) property.intValue = Mathf.RoundToInt(value);
        else property.floatValue = value;
    }

    private static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property == null) return;
        if (property.propertyType == SerializedPropertyType.Boolean) property.boolValue = value != 0;
        else property.intValue = value;
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property == null) return;
        if (property.propertyType == SerializedPropertyType.Boolean) property.boolValue = value;
        else property.intValue = value ? 1 : 0;
    }

    private static void Progress(string message, float value)
    {
        EditorUtility.DisplayProgressBar("L4D 비주얼", message, value);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void WriteLog(string text)
    {
        Debug.Log("[L4D 비주얼]\n" + text);
    }
}
#endif
