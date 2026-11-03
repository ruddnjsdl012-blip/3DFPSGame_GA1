using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// 과제용 성능 측정기 (Before / After 를 같은 조건에서 재기 위한 도구)
// 에디터 메뉴 Tools > L4D 과제 > 성능 측정 이 PlayerPrefs에 측정 종류를 적고 Play를 시작하면,
// 씬이 열린 직후 이 컴포넌트가 만들어져 자동으로 측정하고 Play를 끈다.
//
// Route 모드: 진행 경로 위 고정된 8곳에서 같은 방향을 보며 3초씩 기록 (좀비/동료 끔, UI 끔) → 조명/그래픽/GPU 비교
// Combat 모드: 공장 마당에 좀비 40마리를 만들어 걷게 하고 8초 기록 → 스크립트(CPU)/GC 비교
// 결과: 프로젝트 폴더/Benchmarks/<라벨>_<모드>_<시각>/ 에 summary.md, summary.json, frames.csv, 스크린샷, 프로파일러 기록(.raw)
public class PerfBenchmark : MonoBehaviour
{
    public const string ModeKey = "L4D_Benchmark_Mode";
    public const string LabelKey = "L4D_Benchmark_Label";
    private const int RouteViews = 8;
    private const float Warmup = 1.5f;
    private const float RouteSample = 3f;
    private const float CombatSample = 8f;
    private const int CombatZombies = 40;

    [Serializable]
    private class ViewResult
    {
        public string name;
        public int frames;
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
        public float vertices;
        public float shadowCasters;
        public float gcAllocKbPerFrame;
        public float systemUsedMb;
        public float totalUsedMb;
        public float textureMb;
        public float meshMb;
    }

    [Serializable]
    private class Summary
    {
        public string label;
        public string mode;
        public string time;
        public string unity;
        public string device;
        public string gpu;
        public int width;
        public int height;
        public string quality;
        public List<ViewResult> views = new List<ViewResult>();
        public ViewResult total;
    }

    private string _mode;
    private string _label;
    private string _folder;
    private string _status = "측정 준비 중...";
    private readonly List<string> _csv = new List<string>();

    private ProfilerRecorder _mainThread;
    private ProfilerRecorder _renderThread;
    private ProfilerRecorder _batches;
    private ProfilerRecorder _setPass;
    private ProfilerRecorder _drawCalls;
    private ProfilerRecorder _triangles;
    private ProfilerRecorder _vertices;
    private ProfilerRecorder _shadowCasters;
    private ProfilerRecorder _gcAlloc;
    private ProfilerRecorder _systemMemory;
    private ProfilerRecorder _totalMemory;
    private ProfilerRecorder _textureMemory;
    private ProfilerRecorder _meshMemory;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!PlayerPrefs.HasKey(ModeKey)) return;
        string mode = PlayerPrefs.GetString(ModeKey);
        string label = PlayerPrefs.GetString(LabelKey, "Test");
        PlayerPrefs.DeleteKey(ModeKey);
        PlayerPrefs.Save();
        PerfBenchmark bench = new GameObject("@PerfBenchmark").AddComponent<PerfBenchmark>();
        bench._mode = mode;
        bench._label = label;

        // AfterSceneLoad는 Start 전에 불린다. 디렉터/스포너/동료를 지금 꺼야 좀비가 섞이지 않는다.
        bench.DisableGameplay();
    }

    private void DisableGameplay()
    {
        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            string type = behaviour.GetType().Name;
            if (type == "AIDirector" || type == "ZombieSpawner" || type == "SurvivorSquad")
            {
                behaviour.enabled = false;
            }
        }

        foreach (Zombie zombie in FindObjectsByType<Zombie>(FindObjectsSortMode.None))
        {
            Destroy(zombie.gameObject);
        }
    }

    private void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Benchmarks", Sanitize(_label) + "_" + _mode + "_" + stamp);
        Directory.CreateDirectory(_folder);
        _csv.Add("view,frame,frameMs,mainThreadMs,renderThreadMs,gpuMs,batches,setPass,drawCalls,triangles,gcAllocBytes");

        _mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
        _renderThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 15);
        _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        _vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
        _shadowCasters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
        _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        _systemMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        _totalMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        _textureMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory");
        _meshMemory = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Mesh Memory");

        // 프로파일러 기록을 파일로 남긴다. (Profiler 창 > Load 로 열어서 스크린샷)
        try
        {
            UnityEngine.Profiling.Profiler.logFile = Path.Combine(_folder, "profile");
            UnityEngine.Profiling.Profiler.enableBinaryLog = true;
            UnityEngine.Profiling.Profiler.enabled = true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[성능 측정] 프로파일러 기록을 켜지 못했습니다: " + exception.Message);
        }

        StartCoroutine(_mode == "Combat" ? Combat_Coroutine() : Route_Coroutine());
    }

    private void OnDestroy()
    {
        _mainThread.Dispose();
        _renderThread.Dispose();
        _batches.Dispose();
        _setPass.Dispose();
        _drawCalls.Dispose();
        _triangles.Dispose();
        _vertices.Dispose();
        _shadowCasters.Dispose();
        _gcAlloc.Dispose();
        _systemMemory.Dispose();
        _totalMemory.Dispose();
        _textureMemory.Dispose();
        _meshMemory.Dispose();
    }

    private void OnGUI()
    {
        GUI.color = Color.yellow;
        GUI.Label(new Rect(12f, 12f, 900f, 24f), "[성능 측정 " + _label + " / " + _mode + "] " + _status);
    }

    // ===================== 경로 8곳 (그래픽/GPU) =====================

    private IEnumerator Route_Coroutine()
    {
        yield return null;
        CityMapInfo map = FindAnyObjectByType<CityMapInfo>();
        if (map == null || map.RoutePoints == null || map.RoutePoints.Length < 4)
        {
            _status = "CityMapInfo(경로)를 찾지 못했습니다.";
            yield break;
        }

        Camera source = Camera.main;
        GameObject cameraObject = new GameObject("BenchmarkCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        if (source != null)
        {
            camera.CopyFrom(source);
            UniversalAdditionalCameraData from = source.GetComponent<UniversalAdditionalCameraData>();
            UniversalAdditionalCameraData to = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            if (from != null)
            {
                to.renderPostProcessing = from.renderPostProcessing;
                to.antialiasing = from.antialiasing;
                to.antialiasingQuality = from.antialiasingQuality;
                to.renderShadows = from.renderShadows;
            }
        }

        camera.depth = 100f;
        camera.targetTexture = null;
        foreach (Camera other in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (other != camera) other.enabled = false;
        }

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            canvas.enabled = false;
        }

        Transform[] route = map.RoutePoints;
        Summary summary = NewSummary();
        for (int v = 0; v < RouteViews; v++)
        {
            int index = Mathf.Clamp(Mathf.RoundToInt((route.Length - 4) * (v + 0.5f) / RouteViews), 0, route.Length - 4);
            Vector3 eye = route[index].position + Vector3.up * 1.7f;
            Vector3 look = route[index + 3].position + Vector3.up * 1.5f;
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
            string name = "View" + (v + 1) + "_Route" + index.ToString("000");
            _status = name + " 측정 중 (" + (v + 1) + "/" + RouteViews + ")";
            yield return Measure(name, RouteSample, summary);
            ScreenCapture.CaptureScreenshot(Path.Combine(_folder, name + ".png"));
            yield return null;
            yield return null;
        }

        Finish(summary);
    }

    // ===================== 좀비 40마리 (스크립트/CPU) =====================

    private IEnumerator Combat_Coroutine()
    {
        yield return null;
        CityMapInfo map = FindAnyObjectByType<CityMapInfo>();
        PlayerMove player = FindAnyObjectByType<PlayerMove>();
        AIDirector director = FindAnyObjectByType<AIDirector>(FindObjectsInactive.Include);
        FieldInfo field = typeof(AIDirector).GetField("_zombiePrefab", BindingFlags.NonPublic | BindingFlags.Instance);
        Zombie prefab = director != null && field != null ? field.GetValue(director) as Zombie : null;
        if (map == null || player == null || prefab == null || map.RoutePoints.Length < 120)
        {
            _status = "CityMapInfo / Player / 좀비 프리팹을 찾지 못했습니다.";
            yield break;
        }

        // 게임이 Playing이 될 때까지 기다린다. (좀비 Update는 Playing일 때만 돈다)
        float wait = 0f;
        while (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing && wait < 15f)
        {
            _status = "게임 시작 대기 중...";
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        // 공장 마당(넓은 전투 구역): 플레이어는 마당 입구에 서고, 좀비는 앞쪽에 만들어 100 m 앞 목표로 걷게 한다.
        Transform[] route = map.RoutePoints;
        int arena = Mathf.Min(97, route.Length - 15);
        Vector3 stand = route[arena - 3].position;
        Vector3 center = route[arena].position;
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.transform.SetPositionAndRotation(stand + Vector3.up * 1.1f, Quaternion.LookRotation(Flat(center - stand), Vector3.up));
        if (controller != null) controller.enabled = true;

        Transform goal = new GameObject("BenchmarkGoal").transform;
        goal.position = route[Mathf.Min(arena + 10, route.Length - 1)].position;
        int spawned = 0;
        System.Random random = new System.Random(7);
        for (int attempt = 0; attempt < CombatZombies * 6 && spawned < CombatZombies; attempt++)
        {
            float angle = (float)(random.NextDouble() * Math.PI * 2.0);
            float radius = 4f + (float)random.NextDouble() * 14f;
            Vector3 wanted = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 2f, NavMesh.AllAreas)) continue;
            Zombie zombie = Instantiate(prefab, hit.position + Vector3.up, Quaternion.LookRotation(Flat(goal.position - hit.position) + Vector3.forward * 0.001f));
            zombie.SetTarget(goal);
            zombie.BeginHunt();
            NavMeshAgent agent = zombie.GetComponent<NavMeshAgent>();
            if (agent != null) agent.Warp(hit.position);
            if (zombie.GetComponent<ZombieUnstuck>() == null) zombie.gameObject.AddComponent<ZombieUnstuck>();
            spawned++;
        }

        Summary summary = NewSummary();
        _status = "좀비 " + spawned + "마리 측정 중";
        yield return Measure("Combat_" + spawned + "Zombies", CombatSample, summary);
        ScreenCapture.CaptureScreenshot(Path.Combine(_folder, "Combat.png"));
        yield return null;
        yield return null;
        Finish(summary);
    }

    // ===================== 공통 =====================

    private IEnumerator Measure(string name, float seconds, Summary summary)
    {
        float t = 0f;
        while (t < Warmup)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        List<float> frameMs = new List<float>(1024);
        double main = 0, render = 0, gpu = 0, batches = 0, setPass = 0, draws = 0, tris = 0, verts = 0, shadows = 0, gc = 0;
        int gpuFrames = 0;
        FrameTiming[] timing = new FrameTiming[1];
        t = 0f;
        while (t < seconds)
        {
            yield return null;
            float dt = Time.unscaledDeltaTime;
            t += dt;
            frameMs.Add(dt * 1000f);
            double mainMs = _mainThread.LastValue * 1e-6;
            double renderMs = _renderThread.LastValue * 1e-6;
            main += mainMs;
            render += renderMs;
            batches += _batches.LastValue;
            setPass += _setPass.LastValue;
            draws += _drawCalls.LastValue;
            tris += _triangles.LastValue;
            verts += _vertices.LastValue;
            shadows += _shadowCasters.LastValue;
            gc += _gcAlloc.LastValue;
            FrameTimingManager.CaptureFrameTimings();
            double gpuMs = FrameTimingManager.GetLatestTimings(1, timing) > 0 ? timing[0].gpuFrameTime : 0.0;
            if (gpuMs > 0.0)
            {
                gpu += gpuMs;
                gpuFrames++;
            }

            _csv.Add(string.Join(",", name, frameMs.Count.ToString(), F(dt * 1000f), F(mainMs), F(renderMs), F(gpuMs),
                _batches.LastValue.ToString(), _setPass.LastValue.ToString(), _drawCalls.LastValue.ToString(), _triangles.LastValue.ToString(), _gcAlloc.LastValue.ToString()));
        }

        int n = Mathf.Max(1, frameMs.Count);
        List<float> sorted = new List<float>(frameMs);
        sorted.Sort();
        float avgMs = 0f;
        foreach (float ms in frameMs) avgMs += ms;
        avgMs /= n;
        int lowCount = Mathf.Max(1, n / 100);
        float lowMs = 0f;
        for (int i = sorted.Count - lowCount; i < sorted.Count; i++) lowMs += sorted[i];
        lowMs /= lowCount;

        summary.views.Add(new ViewResult
        {
            name = name,
            frames = n,
            avgFrameMs = avgMs,
            avgFps = 1000f / avgMs,
            low1Fps = 1000f / lowMs,
            avgMainThreadMs = (float)(main / n),
            avgRenderThreadMs = (float)(render / n),
            avgGpuMs = gpuFrames > 0 ? (float)(gpu / gpuFrames) : 0f,
            batches = (float)(batches / n),
            setPassCalls = (float)(setPass / n),
            drawCalls = (float)(draws / n),
            triangles = (float)(tris / n),
            vertices = (float)(verts / n),
            shadowCasters = (float)(shadows / n),
            gcAllocKbPerFrame = (float)(gc / n / 1024.0),
            systemUsedMb = _systemMemory.LastValue / 1048576f,
            totalUsedMb = _totalMemory.LastValue / 1048576f,
            textureMb = _textureMemory.LastValue / 1048576f,
            meshMb = _meshMemory.LastValue / 1048576f,
        });
    }

    private Summary NewSummary()
    {
        return new Summary
        {
            label = _label,
            mode = _mode,
            time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            unity = Application.unityVersion,
            device = SystemInfo.processorType + " / RAM " + SystemInfo.systemMemorySize + " MB",
            gpu = SystemInfo.graphicsDeviceName + " (" + SystemInfo.graphicsMemorySize + " MB, " + SystemInfo.graphicsDeviceType + ")",
            width = Screen.width,
            height = Screen.height,
            quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
        };
    }

    private void Finish(Summary summary)
    {
        ViewResult total = new ViewResult { name = "평균" };
        int count = Mathf.Max(1, summary.views.Count);
        foreach (ViewResult v in summary.views)
        {
            total.frames += v.frames;
            total.avgFps += v.avgFps / count;
            total.low1Fps += v.low1Fps / count;
            total.avgFrameMs += v.avgFrameMs / count;
            total.avgMainThreadMs += v.avgMainThreadMs / count;
            total.avgRenderThreadMs += v.avgRenderThreadMs / count;
            total.avgGpuMs += v.avgGpuMs / count;
            total.batches += v.batches / count;
            total.setPassCalls += v.setPassCalls / count;
            total.drawCalls += v.drawCalls / count;
            total.triangles += v.triangles / count;
            total.vertices += v.vertices / count;
            total.shadowCasters += v.shadowCasters / count;
            total.gcAllocKbPerFrame += v.gcAllocKbPerFrame / count;
            total.systemUsedMb = Mathf.Max(total.systemUsedMb, v.systemUsedMb);
            total.totalUsedMb = Mathf.Max(total.totalUsedMb, v.totalUsedMb);
            total.textureMb = Mathf.Max(total.textureMb, v.textureMb);
            total.meshMb = Mathf.Max(total.meshMb, v.meshMb);
        }

        summary.total = total;
        File.WriteAllText(Path.Combine(_folder, "summary.json"), JsonUtility.ToJson(summary, true), new UTF8Encoding(false));
        File.WriteAllLines(Path.Combine(_folder, "frames.csv"), _csv, new UTF8Encoding(false));

        StringBuilder md = new StringBuilder();
        md.AppendLine("# 성능 측정: " + summary.label + " / " + summary.mode);
        md.AppendLine();
        md.AppendLine("- 시각: " + summary.time + " / Unity " + summary.unity + " / 품질 " + summary.quality);
        md.AppendLine("- 해상도: " + summary.width + "x" + summary.height);
        md.AppendLine("- CPU: " + summary.device);
        md.AppendLine("- GPU: " + summary.gpu);
        md.AppendLine();
        md.AppendLine("| 지점 | 평균 FPS | 1% Low FPS | 프레임 ms | 메인 스레드 ms | 렌더 스레드 ms | GPU ms | Batches | SetPass | Draw Calls | Tris | GC KB/프레임 | 텍스처 MB | 메시 MB | 전체 사용 MB |");
        md.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (ViewResult v in summary.views) md.AppendLine(Row(v));
        md.AppendLine(Row(total));
        File.WriteAllText(Path.Combine(_folder, "summary.md"), md.ToString(), new UTF8Encoding(false));

        try
        {
            UnityEngine.Profiling.Profiler.enableBinaryLog = false;
            UnityEngine.Profiling.Profiler.logFile = string.Empty;
        }
        catch (Exception)
        {
            // 무시
        }

        _status = "완료 → " + _folder;
        Debug.Log("[성능 측정] 완료: " + _folder + "\n" + md);
        StartCoroutine(Quit_Coroutine());
    }

    private static IEnumerator Quit_Coroutine()
    {
        yield return new WaitForSecondsRealtime(1.5f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static string Row(ViewResult v)
    {
        return "| " + v.name + " | " + F(v.avgFps) + " | " + F(v.low1Fps) + " | " + F(v.avgFrameMs) + " | " + F(v.avgMainThreadMs) + " | " + F(v.avgRenderThreadMs)
               + " | " + (v.avgGpuMs > 0f ? F(v.avgGpuMs) : "-") + " | " + v.batches.ToString("0") + " | " + v.setPassCalls.ToString("0") + " | " + v.drawCalls.ToString("0")
               + " | " + (v.triangles / 1000f).ToString("0.0") + "K | " + F(v.gcAllocKbPerFrame) + " | " + v.textureMb.ToString("0") + " | " + v.meshMb.ToString("0") + " | " + v.totalUsedMb.ToString("0") + " |";
    }

    private static string F(double value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v : Vector3.forward;
    }

    private static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Replace(' ', '_');
    }
}
