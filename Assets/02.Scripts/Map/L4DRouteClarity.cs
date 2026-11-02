using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

// One continuous evacuation route, with clearly separated chapters and one-way signs.
// Uses only the uploaded scene's route, font and player references; does not replace gameplay scripts.
[DisallowMultipleComponent]
[DefaultExecutionOrder(500)]
public sealed class L4DRouteClarity : MonoBehaviour
{
    [Serializable]
    public sealed class Chapter
    {
        public int routeIndex;
        public string title;
        public string nextTitle;
        public Vector3 signPosition;
        public float yaw;
    }

    [Serializable]
    public sealed class Barrier
    {
        public string label;
        public Vector3 center;
        public Vector3 size;
        public float yaw;
    }

    [SerializeField] private Transform[] _routePoints;
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private Transform _player;
    [SerializeField] private Transform _radio;
    [SerializeField] private Transform _extraction;
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private Material _paintMaterial;
    [SerializeField] private Material _barrierMaterial;
    [SerializeField] private Material _signMaterial;
    [SerializeField] private Chapter[] _chapters;
    [SerializeField] private Barrier[] _barriers;
    [SerializeField] private bool _showFloorArrows = true;
    [SerializeField] private bool _showChapterSigns = true;
    [SerializeField] private bool _usePhysicalSeparation = true;

    private Vector3[] _points;
    private float[] _lengths;
    private readonly List<Mesh> _meshes = new List<Mesh>(4);
    private readonly RaycastHit[] _floorHits = new RaycastHit[12];
    private GameObject _barrierRoot;
    private TextMeshProUGUI _chapterLabel;
    private float _bannerUntil;
    private float _nextUpdate;
    private float _progress;
    private int _segment;
    private int _chapter = -1;
    private readonly List<Transform> _targetLabels = new List<Transform>(2);

    private void Start()
    {
        if (_routePoints == null || _routePoints.Length < 2) { enabled = false; return; }
        _points = new Vector3[_routePoints.Length];
        _lengths = new float[_routePoints.Length];
        for (int i = 0; i < _routePoints.Length; i++)
        {
            if (_routePoints[i] == null) { enabled = false; return; }
            _points[i] = _routePoints[i].position;
            if (i > 0) _lengths[i] = _lengths[i - 1] + Vector3.Distance(_points[i - 1], _points[i]);
        }

        if (_showFloorArrows && _paintMaterial != null)
        {
            Shape paint = new Shape();
            // Paint is one combined mesh, rather than a renderer/light per marker.
            for (float distance = 6f; distance < _lengths[_lengths.Length - 1] - 2f; distance += 14f)
            {
                Vector3 center = Sample(distance);
                Vector3 direction = Sample(Mathf.Min(distance + 11f, _lengths[_lengths.Length - 1])) - Sample(Mathf.Max(0f, distance - 2f));
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.1f) continue;
                GroundArrow(paint, center, direction.normalized);
            }
            MakeMesh("Evacuation_White_Arrows", paint, _paintMaterial, transform, false);
        }

        if (_showChapterSigns && _chapters != null && _signMaterial != null)
        {
            for (int i = 0; i < _chapters.Length; i++) BuildSign(_chapters[i], i);
            BuildChapterLabel();
            TargetLabel(_radio, "무전기 [E]");
            TargetLabel(_extraction, "탈출 지점");
        }
        if (_usePhysicalSeparation && _barriers != null && _barriers.Length > 0 && _barrierMaterial != null)
        {
            BuildBarriers();
            StartCoroutine(CheckSeparation());
        }
    }

    private Vector3 Sample(float distance)
    {
        for (int i = 0; i < _points.Length - 1; i++)
        {
            if (distance > _lengths[i + 1]) continue;
            float span = _lengths[i + 1] - _lengths[i];
            return Vector3.Lerp(_points[i], _points[i + 1], span > 0.001f ? (distance - _lengths[i]) / span : 0f);
        }
        return _points[_points.Length - 1];
    }

    private Vector3 OnFloor(Vector3 point)
    {
        int count = Physics.RaycastNonAlloc(point + Vector3.up * 1.25f, Vector3.down, _floorHits, 2.5f, ~0, QueryTriggerInteraction.Ignore);
        float best = 1.1f;
        float height = point.y;
        for (int i = 0; i < count; i++)
        {
            Collider collider = _floorHits[i].collider;
            if (collider == null || (_player != null && collider.transform.IsChildOf(_player))) continue;
            float difference = Mathf.Abs(_floorHits[i].point.y - point.y);
            if (difference >= best) continue;
            best = difference;
            height = _floorHits[i].point.y;
        }
        point.y = height + 0.07f;
        return point;
    }

    private void GroundArrow(Shape shape, Vector3 center, Vector3 forward)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 tail = center - forward * 1.4f;
        Vector3 neck = center + forward * 0.2f;
        shape.Quad(OnFloor(tail - right * 0.25f), OnFloor(neck - right * 0.25f), OnFloor(neck + right * 0.25f), OnFloor(tail + right * 0.25f));
        shape.Triangle(OnFloor(center + forward * 1.5f), OnFloor(neck + right * 0.85f), OnFloor(neck - right * 0.85f));
    }

    private void BuildSign(Chapter chapter, int index)
    {
        GameObject sign = new GameObject("Evac_Sector_" + (index + 1));
        sign.transform.SetParent(transform, false);
        Vector3 foot = OnFloor(chapter.signPosition) - Vector3.up * 0.07f;
        sign.transform.SetPositionAndRotation(foot, Quaternion.Euler(0f, chapter.yaw, 0f));
        Shape board = new Shape();
        board.Box(new Vector3(0f, 2.6f, 0f), new Vector3(3.8f, 1.8f, 0.16f), 0f);
        board.Box(new Vector3(0f, 1f, 0f), new Vector3(0.12f, 2f, 0.12f), 0f);
        MakeMesh("Sector_Sign_Board", board, _signMaterial, sign.transform, false, true);

        if (_font != null)
        {
            SignText(sign.transform, "SECTOR " + (index + 1).ToString("00") + " / " + _chapters.Length.ToString("00"), 3.07f, 3.8f);
            SignText(sign.transform, chapter.title, 2.57f, 4.7f);
            SignText(sign.transform, "다음: " + chapter.nextTitle + "  >", 2.05f, 3.1f);
        }
    }

    private void SignText(Transform parent, string value, float y, float size)
    {
        GameObject item = new GameObject("Route_Label", typeof(RectTransform));
        item.transform.SetParent(parent, false);
        item.transform.localPosition = new Vector3(0f, y, -0.1f);
        TextMeshPro text = item.AddComponent<TextMeshPro>();
        text.font = _font;
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.94f, 0.97f, 0.88f, 1f);
        text.rectTransform.sizeDelta = new Vector2(3.55f, 0.48f);
        text.raycastTarget = false;
        text.enableWordWrapping = false;
    }

    private void TargetLabel(Transform target, string caption)
    {
        if (target == null || _font == null) return;
        GameObject root = new GameObject("Evacuation_Target_Label");
        root.transform.SetParent(transform, false);
        root.transform.position = target.position + Vector3.up * 2.2f;
        SignText(root.transform, caption, 0f, 5.2f);
        _targetLabels.Add(root.transform);
    }

    private void BuildChapterLabel()
    {
        if (_font == null) return;
        GameObject root = new GameObject("Route_Chapter_Banner", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9;
        GameObject label = new GameObject("Chapter", typeof(RectTransform));
        label.transform.SetParent(root.transform, false);
        _chapterLabel = label.AddComponent<TextMeshProUGUI>();
        _chapterLabel.font = _font;
        _chapterLabel.fontSize = 25f;
        _chapterLabel.alignment = TextAlignmentOptions.Center;
        _chapterLabel.color = new Color(0.94f, 0.97f, 0.88f, 1f);
        _chapterLabel.raycastTarget = false;
        RectTransform rect = _chapterLabel.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -82f);
        rect.sizeDelta = new Vector2(800f, 42f);
        _chapterLabel.enabled = false;
    }

    private void BuildBarriers()
    {
        _barrierRoot = new GameObject("Separated_Lanes_And_Gate_Wings");
        _barrierRoot.SetActive(false);
        _barrierRoot.transform.SetParent(transform, false);
        Shape shapes = new Shape();
        Shape stripes = new Shape();
        foreach (Barrier barrier in _barriers)
        {
            Vector3 ground = OnFloor(barrier.center - Vector3.up * (barrier.size.y * 0.5f));
            Vector3 center = ground + Vector3.up * (barrier.size.y * 0.5f - 0.12f);
            shapes.Box(center, barrier.size, barrier.yaw);
            Quaternion rotation = Quaternion.Euler(0f, barrier.yaw, 0f);
            Vector3 band = new Vector3(barrier.size.x * 0.95f, 0.13f, 0.025f);
            stripes.Box(center + rotation * new Vector3(0f, 0.25f, -barrier.size.z * 0.5f - 0.025f), band, barrier.yaw);
            stripes.Box(center + rotation * new Vector3(0f, 0.25f, barrier.size.z * 0.5f + 0.025f), band, barrier.yaw);
            GameObject collision = new GameObject(barrier.label);
            collision.transform.SetParent(_barrierRoot.transform, false);
            collision.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, barrier.yaw, 0f));
            BoxCollider box = collision.AddComponent<BoxCollider>();
            box.size = barrier.size;
            NavMeshObstacle obstacle = collision.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = barrier.size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            obstacle.carvingTimeToStationary = 0.1f;
        }
        MakeMesh("Evacuation_Separation", shapes, _barrierMaterial, _barrierRoot.transform, true);
        if (_paintMaterial != null) MakeMesh("Separation_Reflective_Bands", stripes, _paintMaterial, _barrierRoot.transform, false);
    }

    private static bool CompletePath(Vector3 from, Vector3 to, NavMeshPath path)
    {
        NavMeshHit start;
        NavMeshHit end;
        if (!NavMesh.SamplePosition(from, out start, 1.2f, NavMesh.AllAreas)
            || !NavMesh.SamplePosition(to, out end, 1.2f, NavMesh.AllAreas))
        {
            return false;
        }

        if (Mathf.Abs(start.position.y - from.y) > 1.1f || Mathf.Abs(end.position.y - to.y) > 1.1f) return false;
        return NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    private IEnumerator CheckSeparation()
    {
        // Query the user's actual NavMesh. Keep barriers inactive if navigation is unavailable.
        NavMeshPath path = new NavMeshPath();
        float deadline = Time.realtimeSinceStartup + 12f;
        while (!CompletePath(_points[0], _points[1], path) && Time.realtimeSinceStartup < deadline)
            yield return new WaitForSecondsRealtime(0.25f);
        bool[] baseline = new bool[_points.Length - 1];
        int valid = 0;
        for (int i = 0; i < baseline.Length; i++)
        {
            baseline[i] = CompletePath(_points[i], _points[i + 1], path);
            if (baseline[i]) valid++;
            if (i % 12 == 0) yield return null;
        }
        if (valid != baseline.Length)
        {
            Debug.LogWarning("[L4D Route] 원본 경로 일부가 NavMesh에서 연결되지 않아 새 분리벽을 적용하지 않았습니다. 방향 표식은 표시됩니다.", this);
            yield break;
        }
        int spawnCount = _spawnPoints != null ? _spawnPoints.Length : 0;
        bool[] spawnBaseline = new bool[spawnCount];
        Vector3[] spawnTargets = new Vector3[spawnCount];
        for (int i = 0; i < spawnCount; i++)
        {
            if (_spawnPoints[i] == null) continue;
            Vector3 from = _spawnPoints[i].position;
            float best = float.MaxValue;
            for (int j = 0; j < _points.Length; j++)
            {
                float distance = (_points[j] - from).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                spawnTargets[i] = _points[j];
            }
            spawnBaseline[i] = CompletePath(from, spawnTargets[i], path);
            if (i % 12 == 0) yield return null;
        }
        _barrierRoot.SetActive(true);
        yield return new WaitForSecondsRealtime(0.6f);
        for (int i = 0; i < baseline.Length; i++)
        {
            if (!CompletePath(_points[i], _points[i + 1], path))
            {
                _barrierRoot.SetActive(false);
                Debug.LogWarning("[L4D Route] 이동 경로를 유지하기 위해 새 분리벽을 해제했습니다. 방향 표식은 표시됩니다.", this);
                yield break;
            }
            if (i % 12 == 0) yield return null;
        }
        for (int i = 0; i < spawnCount; i++)
        {
            if (spawnBaseline[i] && _spawnPoints[i] != null && !CompletePath(_spawnPoints[i].position, spawnTargets[i], path))
            {
                _barrierRoot.SetActive(false);
                Debug.LogWarning("[L4D Route] 좀비 추격 경로를 유지하기 위해 새 분리벽을 해제했습니다. 방향 표식은 표시됩니다.", this);
                yield break;
            }
            if (i % 12 == 0) yield return null;
        }
    }

    private void Update()
    {
        if (_player == null || _points == null || _chapterLabel == null || Time.time < _nextUpdate) return;
        _nextUpdate = Time.time + 0.16f;
        foreach (Transform label in _targetLabels)
        {
            Vector3 facing = label.position - _player.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.1f) label.rotation = Quaternion.LookRotation(facing);
        }
        // Bound the search so adjacent outbound/return lanes cannot jump the guide forward.
        int upper = Mathf.Min(_points.Length - 2, _segment + 3);
        float best = 36f;
        float candidate = _progress;
        int bestSegment = _segment;
        for (int i = Mathf.Max(0, _segment - 1); i <= upper; i++)
        {
            Vector3 delta = _points[i + 1] - _points[i];
            float t = Mathf.Clamp01(Vector3.Dot(_player.position - _points[i], delta) / Mathf.Max(0.001f, delta.sqrMagnitude));
            Vector3 projected = _points[i] + delta * t;
            float distance = (_player.position - projected).sqrMagnitude;
            float along = Mathf.Lerp(_lengths[i], _lengths[i + 1], t);
            if (distance >= best || along < _progress) continue;
            best = distance;
            candidate = along;
            bestSegment = i;
        }
        _progress = Mathf.Max(_progress, candidate);
        _segment = Mathf.Max(_segment, bestSegment);
        int chapter = 0;
        for (int i = 0; _chapters != null && i < _chapters.Length; i++)
            if (_segment >= _chapters[i].routeIndex) chapter = i;
        if (chapter != _chapter && _chapters != null && chapter < _chapters.Length)
        {
            _chapter = chapter;
            _chapterLabel.text = "구역 " + (chapter + 1) + " / " + _chapters.Length + "  ·  " + _chapters[chapter].title;
            _bannerUntil = Time.time + 4f;
        }
        _chapterLabel.enabled = Time.time < _bannerUntil;
    }

    private void MakeMesh(string name, Shape shape, Material material, Transform parent, bool shadows, bool local = false)
    {
        if (shape.vertices.Count == 0) return;
        GameObject item = new GameObject(name);
        item.transform.SetParent(parent, false);
        if (!local)
        {
            item.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            item.transform.localScale = Vector3.one;
        }
        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(shape.vertices);
        mesh.SetTriangles(shape.triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        item.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = item.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        renderer.receiveShadows = shadows;
        _meshes.Add(mesh);
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in _meshes) if (mesh != null) Destroy(mesh);
    }

    private sealed class Shape
    {
        public readonly List<Vector3> vertices = new List<Vector3>(2048);
        public readonly List<int> triangles = new List<int>(4096);
        public void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            int n = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
        }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Triangle(a, b, c); Triangle(a, c, d);
        }
        public void Box(Vector3 center, Vector3 size, float yaw)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 half = size * 0.5f;
            Vector3[] v = new Vector3[8];
            for (int i = 0; i < 8; i++)
                v[i] = center + rotation * new Vector3((i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y, (i & 4) == 0 ? -half.z : half.z);
            Quad(v[0], v[2], v[3], v[1]); Quad(v[4], v[5], v[7], v[6]);
            Quad(v[0], v[4], v[6], v[2]); Quad(v[1], v[3], v[7], v[5]);
            Quad(v[0], v[1], v[5], v[4]); Quad(v[2], v[6], v[7], v[3]);
        }
    }
}
