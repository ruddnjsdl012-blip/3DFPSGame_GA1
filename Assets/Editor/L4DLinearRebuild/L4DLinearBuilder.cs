// Editor-only structural campaign builder. No gameplay source is replaced.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace L4D.StructuralRebuild
{
    public sealed class L4DLinearBuilder : EditorWindow
    {
        const string DataFolder = "Assets/Editor/L4DLinearRebuild/";
        static readonly string[] Fields = { "_startPoint", "_startSafeRoomArea", "_routePoints", "_spawnPoints", "_midSafeRooms", "_finaleArea", "_extractionPoint" };
        MonoBehaviour map;
        string assetFolder = "Assets/Flooded_Grounds";
        string status = "L4D 씬 하나만 열고 재생을 멈춘 뒤 실행하세요. 실행 전 현재 씬을 자동 백업합니다.";
        Vector2 scroll;

        [Serializable] public class BoxSpec { public Vector3 center, size; }
        [Serializable] public class DecorSpec { public Vector3 point, forward; public int side; public string kind; }
        [Serializable] public class MarkSpec { public Vector3 point, forward; }
        [Serializable] public class SectorSpec { public string title; public Vector3 position; public float yaw; }
        [Serializable] public class Plan
        {
            public string schema;
            public float nominalMainLength, nominalFullLength;
            public Vector3[] corners, route, spawn, lamps;
            public BoxSpec[] floor, road, boundaries;
            public DecorSpec[] decor;
            public MarkSpec[] marks;
            public SectorSpec[] sectors;
            public Vector3 start, mid, radio, finale, extraction;
        }
        sealed class Model
        {
            public GameObject asset;
            public Bounds bounds;
            public string path;
            public List<Transform> parts;
        }
        sealed class Part { public Transform transform; public Bounds bounds; public string family; }
        sealed class Catalog
        {
            public readonly List<Model> houses = new List<Model>(), industry = new List<Model>(), props = new List<Model>();
            public Material ground, road;
        }
        sealed class Context
        {
            public MonoBehaviour map;
            public Transform root, ground, buildings, props, details, route, spawns, start, extraction, radio;
            public Scene scene;
            public Plan plan;
            public Catalog catalog;
            public float scale, oldLength;
            public string generated;
            public TMP_FontAsset font;
            public Material gravel, asphalt, concrete, dark, metal, paint, fence;
            public BoxCollider startRoom, midRoom, finale;
            public readonly List<Transform> routePoints = new List<Transform>(), spawnPoints = new List<Transform>();
            public Vector3 Local(Vector3 p) { return new Vector3(p.x * scale, p.y, p.z * scale); }
            public Vector3 World(Vector3 p) { return root.TransformPoint(Local(p)); }
        }

        [MenuItem("Tools/L4D 맵/맵 구조 전체 교체 (v3)")]
        static void Open() { GetWindow<L4DLinearBuilder>("L4D 구조 교체").Show(); }
        void OnEnable() { map = FindMap(SceneManager.GetActiveScene()); }
        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("도로·건물 배치를 새 구조로 교체", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("주택가 → 공장·창고 → 중간 안전가옥 → 수로 다리 → 폐차장 → 구조 광장\n약 1.96km 진행 후 약 93m를 더 이동해 탈출합니다. 기존 경로가 더 길면 비례해서 늘립니다.", MessageType.Info);
            map = EditorGUILayout.ObjectField("현재 맵 정보", map, typeof(MonoBehaviour), true) as MonoBehaviour;
            assetFolder = EditorGUILayout.TextField("환경 에셋 폴더", assetFolder);
            Texture2D preview = AssetDatabase.LoadAssetAtPath<Texture2D>(DataFolder + "L4DLinearLayout.png");
            if (preview != null) GUI.DrawTexture(GUILayoutUtility.GetAspectRect((float)preview.width / preview.height), preview, ScaleMode.ScaleToFit);
            EditorGUILayout.HelpBox("가져오기만 하면 씬은 바뀌지 않습니다. 아래 버튼을 누르면 실제 도로·건물·충돌·경로·미션 위치를 교체하고 저장합니다. 기존 환경 모델을 사용하며 무기·동료·UI 스크립트는 덮어쓰지 않습니다.", MessageType.None);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                if (GUILayout.Button("이 구조로 실제 맵 교체", GUILayout.Height(42)))
                {
                    try { status = Build(map, assetFolder); }
                    catch (Exception e) { status = "교체 중단: " + e.Message; Debug.LogException(e); }
                }
                if (GUILayout.Button("직전 교체 전 씬으로 복원"))
                {
                    try { status = Restore(); map = FindMap(SceneManager.GetActiveScene()); }
                    catch (Exception e) { status = "복원 중단: " + e.Message; Debug.LogException(e); }
                }
            }
            EditorGUILayout.HelpBox(status, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        static string Build(MonoBehaviour map, string folder)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("재생 및 컴파일이 끝난 뒤 실행하세요.");
            Scene scene = SceneManager.GetActiveScene();
            if (SceneManager.sceneCount != 1 || string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("저장된 L4D 씬 하나만 열어주세요.");
            if (map == null) map = FindMap(scene);
            if (map == null || map.GetType().Name != "CityMapInfo" || map.gameObject.scene != scene) throw new InvalidOperationException("현재 씬의 CityMapInfo를 찾지 못했습니다.");
            if (!map.gameObject.activeInHierarchy) throw new InvalidOperationException("맵 루트를 활성화하세요.");
            if (Vector3.Distance(map.transform.lossyScale, Vector3.one) > .001f || Quaternion.Angle(map.transform.rotation, Quaternion.identity) > .01f) throw new InvalidOperationException("맵 루트의 회전은 0, 스케일은 1이어야 합니다.");
            if (!AssetDatabase.IsValidFolder(folder)) throw new InvalidOperationException("환경 에셋 폴더를 찾지 못했습니다: " + folder);
            TextAsset file = AssetDatabase.LoadAssetAtPath<TextAsset>(DataFolder + "L4DLinearPlan.json");
            if (file == null) throw new InvalidOperationException("설계 JSON이 없습니다. 패키지의 모든 파일을 가져오세요.");
            Plan plan = JsonUtility.FromJson<Plan>(file.text);
            if (plan == null || plan.schema != "L4D-structural-v3" || plan.route == null || plan.route.Length < 100) throw new InvalidOperationException("맵 설계 데이터가 올바르지 않습니다.");
            SerializedObject data = new SerializedObject(map);
            foreach (string field in Fields) if (data.FindProperty(field) == null) throw new InvalidOperationException("현재 CityMapInfo 인터페이스가 다릅니다: " + field);
            NavMeshSurface surface = map.GetComponent<NavMeshSurface>();
            if (surface == null || !surface.enabled) throw new InvalidOperationException("맵 루트의 NavMeshSurface를 확인하세요.");
            Transform player = FindActor(scene, "Player");
            if (player == null || player.IsChildOf(map.transform)) throw new InvalidOperationException("Player는 맵 외부에 있어야 합니다.");
            float oldLength = RouteLength(data.FindProperty("_routePoints"));
            if (oldLength < 10) throw new InvalidOperationException("기존 맵의 경로가 비어 있습니다.");
            Transform oldRadio = FindNamed(map.transform, "Radio");
            if (oldRadio == null) throw new InvalidOperationException("미션 라디오 Radio가 있는 게임 씬에서 실행하세요.");
            Transform radioGroup = oldRadio;
            while (radioGroup.parent != null && radioGroup.parent != map.transform) radioGroup = radioGroup.parent;
            if (radioGroup == map.transform || radioGroup.parent == null) throw new InvalidOperationException("Radio는 맵 루트 내부에 있어야 합니다.");
            Catalog catalog;
            try
            {
                EditorUtility.DisplayProgressBar("L4D 맵 구조", "기존 모델과 조립된 건물을 확인하는 중", .02f);
                catalog = ScanAssets(folder);
                FindSceneBuildings(map.transform, catalog);
            }
            finally { EditorUtility.ClearProgressBar(); }
            if (catalog.houses.Count + catalog.industry.Count < 3) throw new InvalidOperationException("재배치할 완성 건물 묶음을 3개 이상 찾지 못했습니다. 현재 Flooded Grounds 맵 씬과 환경 폴더를 확인하세요. 기존 씬은 변경하지 않았습니다.");
            if (catalog.houses.Count == 0) catalog.houses.AddRange(catalog.industry);
            if (catalog.industry.Count == 0) catalog.industry.AddRange(catalog.houses);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("현재 씬 저장에 실패했습니다.");
            string scenePath = scene.path;
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            string backupDir = Path.Combine("Library", "L4D_LinearRebuild", stamp);
            Directory.CreateDirectory(backupDir);
            string backup = Path.GetFullPath(Path.Combine(backupDir, Path.GetFileName(scenePath)));
            File.Copy(scenePath, backup);
            if (File.Exists(scenePath + ".meta")) File.Copy(scenePath + ".meta", backup + ".meta");
            Directory.CreateDirectory("Assets/L4D_LinearCampaignGenerated"); AssetDatabase.Refresh();
            string generated = "Assets/L4D_LinearCampaignGenerated/Build_" + stamp;
            AssetDatabase.CreateFolder("Assets/L4D_LinearCampaignGenerated", "Build_" + stamp);
            Context c = new Context { map = map, root = map.transform, scene = scene, plan = plan, catalog = catalog, oldLength = oldLength,
                scale = Mathf.Max(1.16f, oldLength / 1340f), generated = generated, font = FindFont(scene) };
            List<Transform> oldChildren = new List<Transform>();
            foreach (Transform child in map.transform) oldChildren.Add(child);
            bool committed = false;
            try
            {
                GameObject radio = Object.Instantiate(radioGroup.gameObject, c.root);
                radio.name = "RadioStation"; radio.SetActive(true);
                c.radio = FindNamed(radio.transform, "Radio");
                if (c.radio == null) throw new InvalidOperationException("라디오 복제에 실패했습니다.");
                radio.transform.position += c.World(plan.radio) - c.radio.position;
                Light oldSun = RenderSettings.sun;
                Light newSun = CloneSun(oldSun, c.root);
                if (oldSun != null && !oldSun.transform.IsChildOf(c.root)) oldSun.enabled = false;
                foreach (Transform child in oldChildren) { child.gameObject.SetActive(false); child.name = "_OLD_" + child.name; }
                EditorUtility.DisplayProgressBar("L4D 맵 구조", "도로·건물·안전가옥 배치", .15f);
                SaveBuildingTemplates(c);
                Materials(c);
                c.ground = Group(c.root, "Ground"); c.buildings = Group(c.root, "Buildings"); c.props = Group(c.root, "Props");
                c.details = Group(c.root, "StreetDetails"); c.route = Group(c.root, "Route"); c.spawns = Group(c.root, "SpawnPoints");
                Boxes(c, "ConnectedGround", plan.floor, c.ground, c.gravel, true, false);
                Boxes(c, "StreetAsphalt", plan.road, c.ground, c.asphalt, false, false);
                Boxes(c, "OuterChainFence", plan.boundaries, c.details, c.fence, true, true);
                PlaceModels(c); SafeRooms(c); Landmarks(c); Route(c); Details(c);
                c.finale = Group(c.root, "FinalePlaza").gameObject.AddComponent<BoxCollider>();
                c.finale.transform.position = c.World(plan.finale); c.finale.size = new Vector3(56 * c.scale, 4, 100 * c.scale); c.finale.isTrigger = true;
                c.extraction = Point(c.root, "ExtractionPoint", c.World(plan.extraction));
                MoveActors(c, player); Bind(c);
                if (newSun != null) RenderSettings.sun = newSun;
                Lightmapping.Clear();
                EditorUtility.DisplayProgressBar("L4D 맵 구조", "현재 충돌 지형으로 NavMesh 생성", .55f);
                surface.RemoveData(); surface.collectObjects = CollectObjects.Children; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                int excluded = 0;
                foreach (string layer in new[] { "Player", "Zombie" }) { int index = LayerMask.NameToLayer(layer); if (index >= 0) excluded |= 1 << index; }
                surface.layerMask = ~excluded; surface.overrideVoxelSize = true; surface.voxelSize = .12f;
                surface.overrideTileSize = true; surface.tileSize = 128;
                Physics.SyncTransforms();
                NavMeshData previous = surface.navMeshData;
                surface.BuildNavMesh();
                if (surface.navMeshData == null || surface.navMeshData == previous) throw new InvalidOperationException("새 NavMesh 생성 실패.");
                EditorUtility.DisplayProgressBar("L4D 맵 구조", "전체 경로·스폰·미션·물리 장애물 검사", .84f);
                string report = Validate(c, surface);
                Bind(c);
                if (!AssetDatabase.Contains(surface.navMeshData)) AssetDatabase.CreateAsset(surface.navMeshData, generated + "/NavMesh_Campaign.asset");
                foreach (Transform child in oldChildren) if (child != null) Object.DestroyImmediate(child.gameObject);
                foreach (GameObject root in scene.GetRootGameObjects()) if (root != map.gameObject && root.name == "_PreservedFromOldMap") Object.DestroyImmediate(root);
                File.WriteAllText(generated + "/BuildReport.txt", report + "\nScene: " + scenePath + "\nBackup: " + backup + "\n", System.Text.Encoding.UTF8);
                AssetDatabase.ImportAsset(generated + "/BuildReport.txt"); AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("완성 씬 저장 실패.");
                EditorPrefs.SetString(BackupKey("file"), backup); EditorPrefs.SetString(BackupKey("scene"), scenePath);
                committed = true;
                Selection.activeGameObject = map.gameObject;
                SceneView view = SceneView.lastActiveSceneView;
                if (view != null) view.LookAt(c.World(new Vector3(0,2,45)), Quaternion.Euler(45,0,0), 80);
                Debug.Log("[L4D 구조 교체 완료]\n" + report + "\n백업: " + backup, map);
                return "교체·저장 완료. 재생해서 출발→중간 안전가옥→라디오→탈출을 확인하세요.\n" + c.routePoints.Count + "개 경로 / " + c.spawnPoints.Count + "개 유효 스폰.\n백업: " + backup;
            }
            catch
            {
                if (!committed)
                {
                    File.Copy(backup, scenePath, true); AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceUpdate);
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    AssetDatabase.DeleteAsset(generated);
                    Debug.LogWarning("[L4D 구조 교체] 오류로 원래 씬을 복원했습니다. 백업: " + backup);
                }
                throw;
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static Catalog ScanAssets(string folder)
        {
            Catalog c = new Catalog(); HashSet<string> paths = new HashSet<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); if (!paths.Add(path)) continue;
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || !PrefabUtility.IsPartOfPrefabAsset(asset)) continue;
                string name = (asset.name + " " + Path.GetFileNameWithoutExtension(path)).ToLowerInvariant();
                if (Has(name, "terrain", "particle", "effect", "vfx", "sky", "water", "camera", "player", "zombie", "character", "road", "street", "sidewalk", "pavement", "fence", "bridge", "roof", "door", "window", "interior", "floor", "wall", "stair", "pole", "lamp", "light", "_mid_", "_cor_", "_base_", "_top_", "_deco_", "_column", "_beam")) continue;
                Bounds bounds; if (!MeshBounds(asset.transform, out bounds)) continue;
                Vector3 s = bounds.size; Model m = new Model { asset = asset, bounds = bounds, path = path };
                bool building = Has(name, "house", "building", "villa", "cabin", "church", "barn", "factory", "warehouse", "hangar", "garage", "apartment", "store", "home", "industrial", "barrack", "shed");
                if (building && Mathf.Min(s.x,s.z) > 2 && Mathf.Max(s.x,s.z) < 120 && s.y > 2 && s.y < 80)
                {
                    if (Has(name,"factory","warehouse","indbuilding","industrial","hangar","garage","barn","shed")) c.industry.Add(m); else c.houses.Add(m);
                }
                else if (Has(name,"car","truck","bus","van","container","barrel","crate","dumpster","rubble","wreck","pallet") && s.y > .2f && Mathf.Max(s.x,s.z) < 35) c.props.Add(m);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid), name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (c.road == null && Has(name,"asphalt","road","tarmac")) c.road = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (c.ground == null && Has(name,"gravel","ground","dirt","concrete")) c.ground = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            c.houses.Sort((a,b)=>string.CompareOrdinal(a.path,b.path)); c.industry.Sort((a,b)=>string.CompareOrdinal(a.path,b.path)); c.props.Sort((a,b)=>string.CompareOrdinal(a.path,b.path));
            return c;
        }

        static void FindSceneBuildings(Transform root, Catalog catalog)
        {
            Transform environment = root.Find("Environment"); if (environment == null) environment = root.Find("Buildings"); if (environment == null) return;
            Regex pattern = new Regex(@"^(Villa\d*|Cabin\d*|BrickHouse\d*|Church\d*|IndBuilding\d*|Outhouse\d*)[_\s]", RegexOptions.IgnoreCase);
            List<Part> parts = new List<Part>(); List<Model> houses = new List<Model>(), industry = new List<Model>();
            foreach (Transform child in environment)
            {
                if (!child.gameObject.activeInHierarchy) continue;
                Match match = pattern.Match(child.name);
                if (match.Success)
                {
                    Bounds b = WorldBounds(child); if (b.size.sqrMagnitude > .01f) parts.Add(new Part { transform = child, bounds = b, family = match.Groups[1].Value.ToLowerInvariant() });
                }
                else if (Has(child.name.ToLowerInvariant(),"building","villa","house","cabin") && child.GetComponentsInChildren<MeshFilter>().Length >= 10)
                {
                    Bounds b = WorldBounds(child);
                    if (AssemblySize(b))
                    {
                        Model m = new Model { parts = new List<Transform> { child }, bounds = b, path = "Scene_" + child.name };
                        if (Has(child.name.ToLowerInvariant(),"floodedbuilding","indbuilding","factory","warehouse")) industry.Add(m); else houses.Add(m);
                    }
                }
            }
            foreach (IGrouping<string,Part> family in parts.GroupBy(p=>p.family))
            {
                List<Part> items = family.ToList(); int[] parents = Enumerable.Range(0,items.Count).ToArray();
                Func<int,int> find = i => { while (parents[i]!=i) { parents[i]=parents[parents[i]]; i=parents[i]; } return i; };
                Dictionary<Vector3Int,List<int>> buckets = new Dictionary<Vector3Int,List<int>>();
                for (int i=0;i<items.Count;i++)
                {
                    Bounds expanded=items[i].bounds; expanded.Expand(1.8f);
                    Vector3Int low=Vector3Int.FloorToInt(expanded.min/8), high=Vector3Int.FloorToInt(expanded.max/8);
                    HashSet<int> neighbors=new HashSet<int>();
                    for(int x=low.x;x<=high.x;x++) for(int y=low.y;y<=high.y;y++) for(int z=low.z;z<=high.z;z++)
                    {
                        Vector3Int key=new Vector3Int(x,y,z); List<int> bucket;
                        if(!buckets.TryGetValue(key,out bucket)){bucket=new List<int>();buckets.Add(key,bucket);}
                        foreach(int other in bucket)neighbors.Add(other); bucket.Add(i);
                    }
                    foreach(int other in neighbors)
                    {
                        Bounds a=items[i].bounds,b=items[other].bounds; Vector3 d=a.center-b.center;
                        Vector3 gap=new Vector3(Mathf.Abs(d.x),Mathf.Abs(d.y),Mathf.Abs(d.z))-a.extents-b.extents;
                        if(gap.x<=.9f && gap.y<=.9f && gap.z<=.9f)parents[find(i)]=find(other);
                    }
                }
                int number=0;
                foreach(IGrouping<int,int> group in Enumerable.Range(0,items.Count).GroupBy(i=>find(i)))
                {
                    List<int> ids=group.ToList();if(ids.Count<10)continue;
                    Bounds b=items[ids[0]].bounds;foreach(int i in ids)b.Encapsulate(items[i].bounds);
                    if(!AssemblySize(b))continue;
                    Model m=new Model{bounds=b,parts=ids.Select(i=>items[i].transform).ToList(),path="Scene_"+family.Key+"_"+number++};
                    if(family.Key.StartsWith("indbuilding"))industry.Add(m);else houses.Add(m);
                }
            }
            if(houses.Count>0){catalog.houses.Clear();catalog.houses.AddRange(houses.OrderByDescending(m=>m.parts.Count).Take(6));}
            if(industry.Count>0){catalog.industry.Clear();catalog.industry.AddRange(industry.OrderByDescending(m=>m.parts.Count).Take(3));}
        }

        static bool AssemblySize(Bounds b){return Mathf.Min(b.size.x,b.size.z)>=4 && Mathf.Max(b.size.x,b.size.z)<=90 && b.size.y>=3.2f && b.size.y<=45;}

        static void SaveBuildingTemplates(Context c)
        {
            int index=0;
            foreach(Model m in c.catalog.houses.Concat(c.catalog.industry).Distinct())
            {
                if(m.parts==null)continue;
                GameObject temp=new GameObject("BuildingTemplate_"+index.ToString("00")); SceneManager.MoveGameObjectToScene(temp,c.scene);
                try
                {
                    temp.transform.position=new Vector3(m.bounds.center.x,m.bounds.min.y,m.bounds.center.z);
                    foreach(Transform original in m.parts)
                    {
                        GameObject part=Object.Instantiate(original.gameObject);part.transform.SetParent(temp.transform,true);part.SetActive(true);
                        if(PrefabUtility.IsPartOfPrefabInstance(part))PrefabUtility.UnpackPrefabInstance(part,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                    }
                    UnpackChildren(temp);Strip(temp);temp.transform.position=Vector3.zero;
                    m.asset=PrefabUtility.SaveAsPrefabAsset(temp,c.generated+"/BuildingTemplate_"+index.ToString("00")+".prefab");
                    if(m.asset==null)throw new InvalidOperationException("기존 건물 묶음 저장 실패: "+m.path);
                    Bounds bounds;if(!MeshBounds(m.asset.transform,out bounds))throw new InvalidOperationException("건물 메쉬를 읽지 못했습니다.");m.bounds=bounds;index++;
                }
                finally{Object.DestroyImmediate(temp);}
            }
        }

        static void PlaceModels(Context c)
        {
            int houses=0,industry=0,props=0;
            foreach(DecorSpec slot in c.plan.decor)
            {
                bool prop=slot.kind=="prop";List<Model> list=prop?c.catalog.props:slot.kind=="industrial"?c.catalog.industry:c.catalog.houses;if(list.Count==0)continue;
                int index=prop?props++:slot.kind=="industrial"?industry++:houses++;Model m=list[index%list.Count];
                GameObject go=PrefabUtility.InstantiatePrefab(m.asset,prop?c.props:c.buildings) as GameObject;
                if(go==null)throw new InvalidOperationException("모델 배치 실패: "+m.path);
                if(PrefabUtility.IsPartOfPrefabInstance(go))PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                go.name=(prop?"Prop_":"Building_")+index.ToString("000")+"_"+m.asset.name;go.SetActive(true);Strip(go);
                Vector3 forward=c.root.TransformDirection(slot.forward),right=Vector3.Cross(Vector3.up,forward),size=m.bounds.size;
                float fit=prop?Mathf.Min(8/Mathf.Max(.01f,size.x),3.1f/Mathf.Max(.01f,size.z),4/Mathf.Max(.01f,size.y)):
                    Mathf.Min(25*c.scale/Mathf.Max(.01f,size.x),16*c.scale/Mathf.Max(.01f,size.z),24/Mathf.Max(.01f,size.y));
                fit=Mathf.Min(1,fit);go.transform.localScale=Vector3.one*fit;go.transform.rotation=Quaternion.LookRotation(-right*slot.side,Vector3.up);go.transform.position=c.World(slot.point);
                Bounds bounds;if(!MeshBounds(go.transform,out bounds))throw new InvalidOperationException("빈 건물 메쉬: "+m.path);
                Vector3 target=c.World(slot.point)+right*slot.side*(prop?0:10*c.scale+bounds.extents.z*fit);
                Vector3 center=go.transform.TransformPoint(bounds.center);
                go.transform.position+=new Vector3(target.x-center.x,-center.y+bounds.extents.y*fit,target.z-center.z);
                BoxCollider collider=go.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;Blocked(go);
                foreach(Renderer r in go.GetComponentsInChildren<Renderer>(true))
                {
                    r.lightmapIndex=-1;r.shadowCastingMode=prop?ShadowCastingMode.Off:ShadowCastingMode.On;
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);
                }
                GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);
            }
        }

        static void Strip(GameObject go)
        {
            foreach(MonoBehaviour x in go.GetComponentsInChildren<MonoBehaviour>(true))if(x!=null)Object.DestroyImmediate(x);
            foreach(NavMeshAgent x in go.GetComponentsInChildren<NavMeshAgent>(true))Object.DestroyImmediate(x);
            foreach(NavMeshObstacle x in go.GetComponentsInChildren<NavMeshObstacle>(true))Object.DestroyImmediate(x);
            foreach(Joint x in go.GetComponentsInChildren<Joint>(true))Object.DestroyImmediate(x);
            foreach(Rigidbody x in go.GetComponentsInChildren<Rigidbody>(true))Object.DestroyImmediate(x);
            foreach(Collider x in go.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(x);
            foreach(Camera x in go.GetComponentsInChildren<Camera>(true))Object.DestroyImmediate(x);
            foreach(AudioSource x in go.GetComponentsInChildren<AudioSource>(true))Object.DestroyImmediate(x);
            foreach(AudioListener x in go.GetComponentsInChildren<AudioListener>(true))Object.DestroyImmediate(x);
            foreach(Light x in go.GetComponentsInChildren<Light>(true))Object.DestroyImmediate(x);
            foreach(Animator x in go.GetComponentsInChildren<Animator>(true))Object.DestroyImmediate(x);
            foreach(ParticleSystem x in go.GetComponentsInChildren<ParticleSystem>(true))x.gameObject.SetActive(false);
        }
        static void UnpackChildren(GameObject root)
        {
            foreach(Transform item in root.GetComponentsInChildren<Transform>(true))
                if(item!=null&&PrefabUtility.IsPartOfPrefabInstance(item.gameObject)&&PrefabUtility.GetOutermostPrefabInstanceRoot(item.gameObject)==item.gameObject)
                    PrefabUtility.UnpackPrefabInstance(item.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        }

        static Material Mat(Context c,string name,Color color,Material source=null)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("URP Lit 셰이더를 찾지 못했습니다.");
            Material m=source!=null&&source.shader==shader?new Material(source):new Material(shader);m.name=name;
            if(source==null||source.shader!=shader)m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.18f);
            AssetDatabase.CreateAsset(m,c.generated+"/"+name+".mat");return m;
        }
        static void Materials(Context c)
        {
            c.gravel=Mat(c,"Ground",new Color(.22f,.24f,.22f),c.catalog.ground);c.asphalt=Mat(c,"Asphalt",new Color(.10f,.12f,.13f),c.catalog.road);
            c.concrete=Mat(c,"Concrete",new Color(.31f,.32f,.28f));c.dark=Mat(c,"SafeRoomRoof",new Color(.10f,.13f,.13f));
            c.metal=Mat(c,"Steel",new Color(.17f,.20f,.19f));c.paint=Mat(c,"LanePaint",new Color(.63f,.65f,.53f));c.fence=Mat(c,"ChainFence",new Color(.26f,.30f,.27f));
            Texture2D tex=new Texture2D(64,64,TextureFormat.RGBA32,true);tex.name="ChainFencePattern";tex.wrapMode=TextureWrapMode.Repeat;tex.filterMode=FilterMode.Bilinear;
            Color[] pixels=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)pixels[y*64+x]=new Color(1,1,1,(x+y)%32<2||(x-y+128)%32<2?1:0);
            tex.SetPixels(pixels);tex.Apply(true,false);AssetDatabase.CreateAsset(tex,c.generated+"/ChainFencePattern.asset");
            c.fence.SetTexture("_BaseMap",tex);c.fence.SetFloat("_AlphaClip",1);c.fence.SetFloat("_Cutoff",.35f);c.fence.SetFloat("_Cull",0);
            c.fence.EnableKeyword("_ALPHATEST_ON");c.fence.SetOverrideTag("RenderType","TransparentCutout");c.fence.renderQueue=(int)RenderQueue.AlphaTest;
        }

        static void SafeRooms(Context c)
        {
            Transform start=Room(c,"StartSafeRoom",Vector3.zero,0,false,out c.startRoom);
            c.start=Point(c.root,"StartPoint",c.World(c.plan.start));
            Point(start,"AmmoCrate",start.TransformPoint(new Vector3(-3.5f,.06f,-3)));Point(start,"MedKitBox",start.TransformPoint(new Vector3(3.5f,.06f,-3)));
            Transform mid=Room(c,"MidSafeRoom",c.plan.mid,-90,true,out c.midRoom);
            Point(mid,"MidAmmoCrate",mid.TransformPoint(new Vector3(-3.5f,.06f,-3)));Point(mid,"MidMedKitBox",mid.TransformPoint(new Vector3(3.5f,.06f,-3)));
        }
        static Transform Room(Context c,string name,Vector3 p,float yaw,bool through,out BoxCollider trigger)
        {
            Transform room=Group(c.root,name);room.position=c.World(p);room.rotation=Quaternion.Euler(0,yaw,0);
            trigger=room.gameObject.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=new Vector3(0,2,0);trigger.size=new Vector3(11.4f,4,13.2f);
            LocalCube(room,"LeftWall",new Vector3(-6,2.15f,0),new Vector3(.35f,4.3f,14),c.concrete);
            LocalCube(room,"RightWall",new Vector3(6,2.15f,0),new Vector3(.35f,4.3f,14),c.concrete);
            LocalCube(room,"Roof",new Vector3(0,4.35f,0),new Vector3(12.4f,.3f,14.4f),c.dark);
            foreach(int end in new[]{-1,1})
            {
                if(end==-1&&!through)LocalCube(room,"BackWall",new Vector3(0,2.15f,-7),new Vector3(12,4.3f,.35f),c.concrete);
                else
                {
                    foreach(int side in new[]{-1,1})LocalCube(room,"DoorFrame",new Vector3(side*4.1f,2.15f,end*7),new Vector3(3.8f,4.3f,.35f),c.concrete);
                    LocalCube(room,"DoorLintel",new Vector3(0,3.95f,end*7),new Vector3(4.4f,.7f,.35f),c.concrete);
                }
            }
            Light light=Group(room,"SafeRoomLight").gameObject.AddComponent<Light>();light.transform.localPosition=new Vector3(0,3.5f,0);
            light.type=LightType.Point;light.color=new Color(1,.75f,.42f);light.intensity=3;light.range=12;light.shadows=LightShadows.None;
            Text(c,room,through?"중간 안전가옥":"출발 안전가옥",new Vector3(0,3,6.75f),0,new Vector2(4,.65f));return room;
        }
        static void Landmarks(Context c)
        {
            Cube(c,c.details,"WarehouseRoof",new Vector3(20,6.2f,600),new Vector3(190*c.scale,.4f,13*c.scale),c.metal,true);
            for(int i=0;i<8;i++)foreach(int side in new[]{-1,1})Cube(c,c.details,"WarehousePillar",new Vector3(110-i*26,3,600+side*6.8f),new Vector3(.45f,6,.45f),c.metal,true);
            for(int i=0;i<14;i++)foreach(int side in new[]{-1,1})Cube(c,c.details,"BridgePost",new Vector3(-80+side*8.6f,1.05f,665+i*12),new Vector3(.35f,2.1f,.35f),c.concrete,true);
            foreach(int side in new[]{-1,1})
            {
                Cube(c,c.details,"BridgeGuardrail",new Vector3(-80+side*8.6f,1.1f,744),new Vector3(.3f,1.15f,180*c.scale),c.metal,true);
                Cube(c,c.details,"BridgeSafetyFence",new Vector3(-80+side*8.7f,4.4f,744),new Vector3(.14f,8.8f,180*c.scale),c.fence,true);
            }
            Material water=Mat(c,"CanalWater",new Color(.035f,.095f,.10f));water.SetFloat("_Smoothness",.83f);
            foreach(int side in new[]{-1,1})Cube(c,c.details,"CanalSurface",new Vector3(-80+side*20,.065f,744),new Vector3(21*c.scale,.015f,180*c.scale),water,false);
            foreach(int side in new[]{-1,1})
            {
                Cube(c,c.details,"ExtractionBeacon",new Vector3(70+side*19,3.5f,1275),new Vector3(.7f,7,.7f),c.metal,true);
                PointLight(c,new Vector3(70+side*19,6.8f,1275),new Color(.28f,.65f,.95f),5,22);
            }
        }
        static void Route(Context c)
        {
            for(int i=0;i<c.plan.route.Length;i++)c.routePoints.Add(Point(c.route,"Route_"+i.ToString("000"),c.World(c.plan.route[i])));
            for(int i=0;i<c.plan.spawn.Length;i++)c.spawnPoints.Add(Point(c.spawns,"Spawn_"+i.ToString("000"),c.World(c.plan.spawn[i])));
        }
        static void Details(Context c)
        {
            List<BoxSpec> dashes=new List<BoxSpec>();
            foreach(MarkSpec mark in c.plan.marks)
            {
                bool north=Mathf.Abs(mark.forward.z)>.5f;
                dashes.Add(new BoxSpec{center=mark.point,size=new Vector3(north ? 0.13f : 2f,0.018f,north ? 2f : 0.13f)});
            }
            Boxes(c,"LaneDashes",dashes.ToArray(),c.details,c.paint,false,false);
            foreach(Vector3 p in c.plan.lamps)
            {
                Cube(c,c.details,"StreetLampPost",p+Vector3.up*3.2f,new Vector3(.14f,6.4f,.14f),c.metal,true);
                PointLight(c,p+Vector3.up*6,new Color(1,.70f,.34f),2.5f,13);
            }
            for(int i=1;i<c.plan.sectors.Length;i++)
            {
                SectorSpec s=c.plan.sectors[i];Transform sign=Group(c.details,"Sector_"+i.ToString("00"));
                Vector3 right=Quaternion.Euler(0,s.yaw,0)*Vector3.right;sign.position=c.World(s.position+right*7.8f);sign.rotation=Quaternion.Euler(0,s.yaw,0);
                LocalCube(sign,"Post",new Vector3(0,1.8f,0),new Vector3(.12f,3.6f,.12f),c.metal);
                LocalCube(sign,"Board",new Vector3(0,3.3f,0),new Vector3(4.5f,1.1f,.12f),c.dark);
                Text(c,sign,(i+1)+"  "+s.title+"  ↑",new Vector3(0,3.3f,-.08f),0,new Vector2(4.2f,.8f));
            }
        }
        static void MoveActors(Context c,Transform player)
        {
            Physics.SyncTransforms();Vector3 original=player.position;
            CharacterController controller=player.GetComponent<CharacterController>();if(controller==null)controller=player.GetComponentInChildren<CharacterController>();
            float bottom=controller!=null&&controller.enabled?controller.bounds.min.y-player.position.y:0;
            player.position=c.World(c.plan.start)-Vector3.up*bottom;player.rotation=Quaternion.identity;Vector3 delta=player.position-original;
            int zombie=0;
            foreach(GameObject root in c.scene.GetRootGameObjects())
            {
                if(root==c.root.gameObject||root.transform==player||root.name=="_PreservedFromOldMap")continue;
                Camera[] cameras=root.GetComponentsInChildren<Camera>(true);
                foreach(Camera camera in cameras)
                {
                    // Child overlay/viewmodel cameras already follow the moved camera parent.
                    if(cameras.Any(other=>other!=camera&&camera.transform.IsChildOf(other.transform)))continue;
                    camera.transform.position+=delta;if(camera.CompareTag("MainCamera"))camera.transform.rotation=Quaternion.identity;
                }
                foreach(MonoBehaviour actor in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if(actor==null||!actor.gameObject.activeInHierarchy)continue;string type=actor.GetType().Name;
                    if(type=="Zombie")actor.transform.position=c.routePoints[Mathf.Min(15+zombie++*3,c.routePoints.Count-1)].position+Vector3.right*4;
                    else if(type=="SurvivorCompanion"||type=="CompanionAI")actor.transform.position+=delta;
                }
            }
            Physics.SyncTransforms();
        }
        static void Bind(Context c)
        {
            SerializedObject so=new SerializedObject(c.map);
            so.FindProperty("_startPoint").objectReferenceValue=c.start;so.FindProperty("_startSafeRoomArea").objectReferenceValue=c.startRoom;
            Array(so.FindProperty("_routePoints"),c.routePoints.Cast<Object>().ToArray());Array(so.FindProperty("_spawnPoints"),c.spawnPoints.Cast<Object>().ToArray());
            Array(so.FindProperty("_midSafeRooms"),new Object[]{c.midRoom});so.FindProperty("_finaleArea").objectReferenceValue=c.finale;so.FindProperty("_extractionPoint").objectReferenceValue=c.extraction;
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach(GameObject root in c.scene.GetRootGameObjects())foreach(MonoBehaviour item in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(item==null||!item.gameObject.activeInHierarchy||item.GetType().Name!="AIDirector")continue;
                SerializedObject director=new SerializedObject(item);SerializedProperty mapField=director.FindProperty("_map");
                if(mapField!=null){mapField.objectReferenceValue=c.map;director.ApplyModifiedPropertiesWithoutUndo();}
            }
        }
        static void Array(SerializedProperty p,Object[] values){p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}

        static string Validate(Context c,NavMeshSurface surface)
        {
            NavMeshQueryFilter filter=new NavMeshQueryFilter{agentTypeID=surface.agentTypeID,areaMask=NavMesh.AllAreas};NavMeshPath path=new NavMeshPath();
            Vector3 previous=Vector3.zero;
            for(int i=0;i<c.routePoints.Count;i++)
            {
                Transform point=c.routePoints[i];NavMeshHit hit;
                if(!NavMesh.SamplePosition(point.position,out hit,1.1f,filter)||Mathf.Abs(hit.position.y-point.position.y)>.6f)throw new InvalidOperationException("NavMesh에서 벗어난 경로: "+point.name);
                point.position=hit.position+Vector3.up*.03f;
                if(i>0&&(!NavMesh.CalculatePath(previous,hit.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete))throw new InvalidOperationException("연결이 끊긴 경로: "+point.name);
                previous=hit.position;
            }
            Vector3 first=c.routePoints[0].position,last=c.routePoints[c.routePoints.Count-1].position;
            if(!NavMesh.CalculatePath(first,last,filter,path)||path.status!=NavMeshPathStatus.PathComplete)throw new InvalidOperationException("출발→피날레 전체 경로가 끊겼습니다.");
            float shortest=0;for(int i=1;i<path.corners.Length;i++)shortest+=Vector3.Distance(path.corners[i-1],path.corners[i]);
            if(shortest+1<c.oldLength)throw new InvalidOperationException("실제 최단 경로가 기존보다 짧습니다 ("+shortest.ToString("F0")+"m < "+c.oldLength.ToString("F0")+"m). 기존 씬으로 복원합니다.");
            foreach(Vector3 destination in new[]{c.extraction.position,c.midRoom.transform.position,c.radio.position+Vector3.back*2})
            {
                NavMeshHit hit;
                if(!NavMesh.SamplePosition(destination,out hit,3,filter)||!NavMesh.CalculatePath(first,hit.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete)throw new InvalidOperationException("중간 안전가옥·라디오·탈출 지점 중 연결되지 않은 지점이 있습니다.");
            }
            int rejected=0;
            for(int i=c.spawnPoints.Count-1;i>=0;i--)
            {
                Transform point=c.spawnPoints[i];NavMeshHit hit;
                if(!NavMesh.SamplePosition(point.position,out hit,.9f,filter)||!NavMesh.CalculatePath(first,hit.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete||Solid(c.root,hit.position))
                {c.spawnPoints.RemoveAt(i);Object.DestroyImmediate(point.gameObject);rejected++;continue;}
                point.position=hit.position+Vector3.up*.03f;
            }
            if(c.spawnPoints.Count<180)throw new InvalidOperationException("유효한 스폰 지점이 부족합니다.");
            for(int i=1;i<c.routePoints.Count;i++)
            {
                Vector3 a=c.routePoints[i-1].position,b=c.routePoints[i].position;int samples=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/.75f));
                for(int j=0;j<=samples;j++)if(Solid(c.root,Vector3.Lerp(a,b,(float)j/samples)))throw new InvalidOperationException("진행 경로에 물리 장애물이 있습니다: "+c.routePoints[i].name);
            }
            float length=0;for(int i=1;i<c.routePoints.Count;i++)length+=Vector3.Distance(c.routePoints[i-1].position,c.routePoints[i].position);
            return "Structural campaign v3\nMain route: "+length.ToString("F1")+" m\nNative shortest path: "+shortest.ToString("F1")+" m\nPrevious route: "+c.oldLength.ToString("F1")+" m\nRoute points: "+c.routePoints.Count+"\nConnected spawns: "+c.spawnPoints.Count+"\nRejected spawns: "+rejected+"\nSafe rooms, radio, extraction: connected\nCritical route capsule clearance: passed\nExisting gameplay component instance: preserved\nEnvironment texture/model assets: shared, not copied";
        }
        static bool Solid(Transform root,Vector3 floor)
        {
            foreach(Collider collider in Physics.OverlapCapsule(floor+Vector3.up*.65f,floor+Vector3.up*1.55f,.48f,~0,QueryTriggerInteraction.Ignore))
                if(collider!=null&&collider.enabled&&!collider.isTrigger&&collider.transform.IsChildOf(root))return true;
            return false;
        }

        static void Boxes(Context c,string name,BoxSpec[] specs,Transform parent,Material material,bool collision,bool blocked)
        {
            List<Vector3> vertices=new List<Vector3>();List<int> triangles=new List<int>();List<Vector2> uv=new List<Vector2>();
            foreach(BoxSpec box in specs)Box(vertices,triangles,uv,c.Local(box.center),c.Local(box.size));
            Mesh mesh=new Mesh{name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,c.generated+"/"+name+".asset");
            GameObject go=Group(parent,name).gameObject;go.AddComponent<MeshFilter>().sharedMesh=mesh;MeshRenderer renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            // Accurate collider also prevents CityMapNavigation's fallback from adding a map-wide AABB.
            if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;if(blocked)Blocked(go);
        }
        static void Box(List<Vector3> v,List<int> t,List<Vector2> uv,Vector3 center,Vector3 size)
        {
            Vector3 a=center-size*.5f,b=center+size*.5f;
            Quad(v,t,uv,new Vector3(a.x,b.y,a.z),new Vector3(a.x,b.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(b.x,b.y,a.z));
            Quad(v,t,uv,new Vector3(a.x,a.y,b.z),new Vector3(a.x,a.y,a.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,a.y,b.z));
            Quad(v,t,uv,new Vector3(a.x,a.y,a.z),new Vector3(a.x,a.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,b.y,a.z));
            Quad(v,t,uv,new Vector3(b.x,a.y,b.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,b.y,b.z));
            Quad(v,t,uv,new Vector3(b.x,a.y,a.z),new Vector3(a.x,a.y,a.z),new Vector3(a.x,b.y,a.z),new Vector3(b.x,b.y,a.z));
            Quad(v,t,uv,new Vector3(a.x,a.y,b.z),new Vector3(b.x,a.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(a.x,b.y,b.z));
        }
        static void Quad(List<Vector3> v,List<int> t,List<Vector2> uv,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int n=v.Count;v.Add(a);v.Add(b);v.Add(c);v.Add(d);t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            float width=Vector3.Distance(a,b),height=Vector3.Distance(b,c);uv.Add(Vector2.zero);uv.Add(new Vector2(width,0));uv.Add(new Vector2(width,height));uv.Add(new Vector2(0,height));
        }
        static GameObject Cube(Context c,Transform parent,string name,Vector3 p,Vector3 size,Material material,bool collision)
        {GameObject go=LocalCube(parent,name,Vector3.zero,size,material,collision);go.transform.position=c.World(p);return go;}
        static GameObject LocalCube(Transform parent,string name,Vector3 p,Vector3 size,Material material,bool collision=true)
        {
            GameObject go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;
            go.GetComponent<Renderer>().shadowCastingMode=Has(name.ToLowerInvariant(),"wall","roof","doorframe","doorlintel")?ShadowCastingMode.On:ShadowCastingMode.Off;
            if(collision)Blocked(go);else Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static void Blocked(GameObject go){NavMeshModifier m=go.GetComponent<NavMeshModifier>();if(m==null)m=go.AddComponent<NavMeshModifier>();m.overrideArea=true;m.area=1;}
        static void PointLight(Context c,Vector3 p,Color color,float intensity,float range)
        {
            Light light=Group(c.details,"StreetLight").gameObject.AddComponent<Light>();light.transform.position=c.World(p);light.type=LightType.Point;
            light.color=color;light.intensity=intensity;light.range=range;light.shadows=LightShadows.None;light.lightmapBakeType=LightmapBakeType.Realtime;
        }
        static Light CloneSun(Light old,Transform parent)
        {
            if(old==null)return null;GameObject go=Object.Instantiate(old.gameObject,parent);go.name="CampaignSun";go.SetActive(true);
            go.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation);
            Light light=go.GetComponent<Light>();if(light!=null){light.enabled=true;light.lightmapBakeType=LightmapBakeType.Realtime;}return light;
        }
        static void Text(Context c,Transform parent,string content,Vector3 p,float yaw,Vector2 size)
        {
            if(c.font==null)return;GameObject go=new GameObject("SignText");go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            TextMeshPro text=go.AddComponent<TextMeshPro>();text.font=c.font;text.text=content;text.fontSize=3.3f;text.alignment=TextAlignmentOptions.Center;
            text.enableAutoSizing=true;text.fontSizeMin=1.3f;text.fontSizeMax=3.3f;text.rectTransform.sizeDelta=size;text.color=new Color(.86f,.90f,.74f);
            BoxCollider collider=go.AddComponent<BoxCollider>();collider.size=new Vector3(size.x,size.y,.025f);Blocked(go);
        }
        static TMP_FontAsset FindFont(Scene scene)
        {
            foreach(GameObject root in scene.GetRootGameObjects())foreach(TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))if(text.font!=null)return text.font;
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("4a92330dfc849e649b0fc8967f0352ad"));
        }
        static bool MeshBounds(Transform root,out Bounds bounds)
        {
            bounds=new Bounds();bool found=false;
            foreach(MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter==null||filter.sharedMesh==null)continue;Bounds b=filter.sharedMesh.bounds;Matrix4x4 matrix=root.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    Vector3 p=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));
                    if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);
                }
            }
            return found&&bounds.size.sqrMagnitude>.001f;
        }
        static Bounds WorldBounds(Transform root)
        {
            Bounds local;if(!MeshBounds(root,out local))return new Bounds(root.position,Vector3.zero);Bounds world=new Bounds(root.TransformPoint(local.center),Vector3.zero);
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)world.Encapsulate(root.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3(x,y,z))));return world;
        }
        static MonoBehaviour FindMap(Scene scene)
        {
            if(!scene.IsValid()||!scene.isLoaded)return null;
            foreach(GameObject root in scene.GetRootGameObjects())foreach(MonoBehaviour item in root.GetComponentsInChildren<MonoBehaviour>(true))if(item!=null&&item.gameObject.activeInHierarchy&&item.GetType().Name=="CityMapInfo")return item;return null;
        }
        static Transform FindActor(Scene scene,string name)
        {
            foreach(GameObject root in scene.GetRootGameObjects())foreach(MonoBehaviour item in root.GetComponentsInChildren<MonoBehaviour>(true))if(item!=null&&item.gameObject.activeInHierarchy&&item.GetType().Name==name)return item.transform;return null;
        }
        static Transform FindNamed(Transform root,string name){foreach(Transform item in root.GetComponentsInChildren<Transform>(true))if(item.name==name)return item;return null;}
        static bool Has(string text,params string[] words){return words.Any(text.Contains);}
        static float RouteLength(SerializedProperty p)
        {
            float length=0;Transform previous=null;
            for(int i=0;i<p.arraySize;i++){Transform current=p.GetArrayElementAtIndex(i).objectReferenceValue as Transform;if(current==null)continue;if(previous!=null)length+=Vector3.Distance(previous.position,current.position);previous=current;}return length;
        }
        static Transform Group(Transform parent,string name){GameObject go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        static Transform Point(Transform parent,string name,Vector3 world){Transform point=Group(parent,name);point.position=world;return point;}
        static string BackupKey(string field){return "L4D.StructuralRebuild."+Application.dataPath+"."+field;}
        static string Restore()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new InvalidOperationException("재생과 컴파일을 멈춘 뒤 복원하세요.");
            string backup=EditorPrefs.GetString(BackupKey("file"),""),scene=EditorPrefs.GetString(BackupKey("scene"),"");
            if(!File.Exists(backup)||!scene.StartsWith("Assets/",StringComparison.Ordinal)||!File.Exists(scene))throw new InvalidOperationException("이 프로젝트의 직전 교체 백업이 없습니다.");
            if(SceneManager.sceneCount!=1||SceneManager.GetActiveScene().path!=scene)throw new InvalidOperationException("직전에 교체한 씬 하나만 열어주세요: "+scene);
            if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))throw new IOException("복원 전 현재 씬 저장 실패.");
            File.Copy(scene,Path.Combine(Path.GetDirectoryName(backup),"BeforeRestore_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+".unity"),false);
            File.Copy(backup,scene,true);AssetDatabase.ImportAsset(scene,ImportAssetOptions.ForceUpdate);EditorSceneManager.OpenScene(scene,OpenSceneMode.Single);
            return "교체 전 씬으로 복원했습니다. 복원 직전 씬도 같은 백업 폴더에 보관했습니다.";
        }
    }
}
