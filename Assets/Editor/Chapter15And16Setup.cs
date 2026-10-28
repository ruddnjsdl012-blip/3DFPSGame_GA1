using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// 노션 강의 "15. 좀비 모델과 애니메이션"과 "16. 플레이어 모델과 애니메이션"의
// Unity 에디터 작업(임포트 설정, 애니메이터 컨트롤러, 프리팹, 씬)을 노션 순서 그대로 해 주는 도구.
// 메뉴: Tools > 노션 15~16장 > ...
// 스크립트 코드(Zombie, ZombieHitEvent, PlayerMove, PlayerGunFire, GameManager)는 함께 드린 파일에 들어 있다.
// 바꾸기 전의 씬, 프리팹, 애니메이터는 Assets/Backup_15_16 폴더에 복사해 둔다.
[InitializeOnLoad]
public static class Chapter15And16Setup
{
    private const string Menu = "Tools/노션 15~16장/";
    private const string AnimationsFolder = "Assets/07.Animations";
    private const string PlayerAnimationsFolder = "Assets/07.Animations/Player";
    private const string ZombieControllerPath = "Assets/07.Animations/ZombieAnimator.controller";
    private const string PlayerControllerPath = "Assets/07.Animations/Player/PlayerAC.controller";
    private const string BackupRoot = "Assets/Backup_15_16";

    private static readonly List<string> Log = new List<string>();
    private static int _warnings;
    private static string _backupFolder;

    static Chapter15And16Setup()
    {
        EditorApplication.delayCall += AskOnce;
    }

    // ===================== 메뉴 =====================

    [MenuItem(Menu + "15장 + 16장 한 번에 적용", priority = 0)]
    public static void ApplyBoth()
    {
        Run(true, true);
    }

    [MenuItem(Menu + "15장만 적용 (좀비)", priority = 20)]
    public static void ApplyChapter15()
    {
        Run(true, false);
    }

    [MenuItem(Menu + "16장만 적용 (플레이어)", priority = 21)]
    public static void ApplyChapter16()
    {
        Run(false, true);
    }

    [MenuItem(Menu + "필요한 에셋이 있는지 확인", priority = 40)]
    public static void CheckAssets()
    {
        Log.Clear();
        _warnings = 0;
        Check("Zombie (Pxltiger) - Zombie1 모델", FindModelPath("Zombie1") != null);
        Check("Z_Idle 동작", FindClip("Z_Idle") != null);
        Check("Z_Run_InPlace 동작", FindClip("Z_Run_InPlace") != null);
        Check("Z_Attack 동작", FindClip("Z_Attack") != null);
        Check("Z_FallingForward 동작", FindClip("Z_FallingForward") != null);
        Check("Zombie_Reaction_Hit (Mixamo)", FindModelPath("Zombie_Reaction_Hit") != null);
        Check("Low Poly Soldiers Demo - Soldier_demo 모델", FindModelPath("Soldier_demo") != null);
        Check("demo_combat_idle 동작", FindModelPath("demo_combat_idle") != null);
        Check("demo_combat_run 동작", FindModelPath("demo_combat_run") != null);
        Check("demo_combat_shoot 동작", FindModelPath("demo_combat_shoot") != null);
        Check("Zombie 프리팹", FindZombiePrefabPath() != null);
        ShowReport("필요한 에셋 확인 결과");
    }

    private static void Check(string name, bool ok)
    {
        if (ok)
        {
            Ok(name);
        }
        else
        {
            Warn(name + " 를 찾지 못했습니다.");
        }
    }

    // 스크립트를 처음 넣었을 때 한 번만 물어본다.
    private static void AskOnce()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        string key = "Chapter15And16Setup.Asked." + Application.dataPath;
        if (EditorPrefs.GetBool(key, false))
        {
            return;
        }

        EditorPrefs.SetBool(key, true);
        if (EditorUtility.DisplayDialog("노션 15~16장",
                "노션 15장(좀비 모델과 애니메이션)과 16장(플레이어 모델과 애니메이션)의\nUnity 작업을 지금 적용할까요?\n\n" +
                "바꾸기 전의 씬/프리팹/애니메이터는 Assets/Backup_15_16에 복사해 둡니다.\n" +
                "나중에 Tools > 노션 15~16장 메뉴로도 할 수 있습니다.",
                "지금 적용", "나중에"))
        {
            ApplyBoth();
        }
    }

    // ===================== 실행 =====================

    private static void Run(bool chapter15, bool chapter16)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("노션 15~16장", "플레이를 멈춘 뒤에 실행해 주세요.", "확인");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Log.Clear();
        _warnings = 0;
        _backupFolder = BackupRoot + "/" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

        try
        {
            if (chapter15)
            {
                RunChapter15();
            }

            if (chapter16)
            {
                RunChapter16();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Warn("도중에 오류가 났습니다: " + exception.Message + " (Console의 빨간 줄을 확인해 주세요)");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
        }

        ShowReport(_warnings == 0 ? "적용을 마쳤습니다. ▶ Play로 확인해 보세요." : "적용을 마쳤지만 확인할 것이 " + _warnings + "개 있습니다.");
    }

    private static void Progress(string text, float value)
    {
        EditorUtility.DisplayProgressBar("노션 15~16장 적용", text, value);
    }

    private static void Section(string title)
    {
        Log.Add(string.Empty);
        Log.Add("■ " + title);
    }

    private static void Ok(string text)
    {
        Log.Add("  ✔ " + text);
    }

    private static void Warn(string text)
    {
        _warnings++;
        Log.Add("  ⚠ " + text);
    }

    private static void ShowReport(string title)
    {
        string body = title + "\n" + string.Join("\n", Log);
        Debug.Log("[노션 15~16장] " + body);

        // 대화 상자는 길면 잘리므로 핵심만 보여 주고, 전체는 Console에 남긴다.
        string shortBody = body.Length > 1800 ? body.Substring(0, 1800) + "\n... (전체 내용은 Console 창)" : body;
        EditorUtility.DisplayDialog("노션 15~16장", shortBody, "확인");
    }

    // ===================== 15장 =====================

    private static void RunChapter15()
    {
        // ---------- 15.1 좀비 모델 가져오기 ----------
        Section("15.1 좀비 모델 가져오기");
        Progress("15.1 좀비 모델", 0.05f);
        string zombieModelPath = FindModelPath("Zombie1");
        if (zombieModelPath == null)
        {
            Warn("Zombie1 모델이 없습니다. Package Manager > My Assets에서 Pxltiger의 Zombie를 Import한 뒤 다시 실행해 주세요.");
            return;
        }

        ModelImporter zombieImporter = AssetImporter.GetAtPath(zombieModelPath) as ModelImporter;
        if (zombieImporter != null)
        {
            Ok("Zombie1 Scale Factor " + zombieImporter.globalScale + ", Convert Units " + (zombieImporter.useFileUnits ? "켜짐" : "꺼짐") + " (바꾸지 않고 그대로)");
        }

        ConvertMaterialsToUrp(RootFolderOf(zombieModelPath), "좀비");

        // ---------- 15.4 / 15.5 Z_Attack: Loop Time 끄기 + 11프레임 HitEvent ----------
        Section("15.4~15.5 Z_Attack 클립 (Loop Time 끔, 11프레임 HitEvent)");
        Progress("15.4 Z_Attack", 0.12f);
        AnimationClip attackClip = FindClip("Z_Attack");
        if (attackClip == null)
        {
            Warn("Z_Attack 동작을 찾지 못했습니다.");
        }
        else
        {
            bool edited = EditClip(AssetDatabase.GetAssetPath(attackClip), attackClip.name, clip =>
            {
                clip.loopTime = false;

                // 같은 이벤트가 두 번 생기지 않게 HitEvent는 지우고 하나만 넣는다.
                float frames = clip.lastFrame - clip.firstFrame;
                float time = frames > 0f ? 11f / frames : 11f / 30f;
                List<AnimationEvent> events = (clip.events ?? new AnimationEvent[0]).Where(e => e.functionName != "HitEvent").ToList();
                events.Add(new AnimationEvent { functionName = "HitEvent", time = Mathf.Clamp01(time) });
                clip.events = events.OrderBy(e => e.time).ToArray();
            });

            if (edited)
            {
                Ok("Z_Attack: Loop Time 끔, 11프레임(0:11)에 HitEvent 이벤트");
            }
        }

        // ---------- 15.6.3 Mixamo 피격 동작을 Humanoid로 ----------
        Section("15.6.3 Zombie_Reaction_Hit을 Humanoid로");
        Progress("15.6 Zombie_Reaction_Hit", 0.2f);
        string hitModelPath = FindModelPath("Zombie_Reaction_Hit");
        AnimationClip hitClip = null;
        if (hitModelPath == null)
        {
            Warn("07.Animations에 Zombie_Reaction_Hit이 없습니다. (Mixamo 'Zombie Reaction Hit', Without Skin) 피격(Hit) 상태가 비어 있게 됩니다.");
        }
        else
        {
            ModelImporter hitImporter = AssetImporter.GetAtPath(hitModelPath) as ModelImporter;
            if (hitImporter != null && (hitImporter.animationType != ModelImporterAnimationType.Human
                                        || hitImporter.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel))
            {
                hitImporter.animationType = ModelImporterAnimationType.Human;
                hitImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                hitImporter.SaveAndReimport();
            }

            Avatar hitAvatar = LoadAvatar(hitModelPath);
            if (hitAvatar != null && hitAvatar.isValid && hitAvatar.isHuman)
            {
                Ok("Animation Type: Humanoid, Avatar Definition: Create From This Model → " + hitAvatar.name);
            }
            else
            {
                Warn("Zombie_Reaction_Hit의 아바타가 올바르지 않습니다. Rig 탭의 Configure...에서 뼈 연결을 확인해 주세요.");
            }

            hitClip = FirstClipIn(hitModelPath);
            if (hitClip != null)
            {
                Ok("피격 클립: " + hitClip.name);
            }
        }

        // ---------- 15.3 ~ 15.6.4 ZombieAnimator ----------
        Section("15.3~15.6.4 ZombieAnimator 만들기");
        Progress("15.3 ZombieAnimator", 0.3f);
        AnimationClip idle = FindClip("Z_Idle");
        AnimationClip run = FindClip("Z_Run_InPlace");
        AnimationClip attack = FindClip("Z_Attack");
        AnimationClip death = FindClip("Z_FallingForward");
        if (idle == null || run == null || attack == null || death == null)
        {
            Warn("Z_Idle / Z_Run_InPlace / Z_Attack / Z_FallingForward 중 없는 동작이 있어 ZombieAnimator를 만들지 못했습니다.");
            return;
        }

        if (death.isLooping)
        {
            EditClip(AssetDatabase.GetAssetPath(death), death.name, clip => clip.loopTime = false);
            death = FindClip("Z_FallingForward");
        }

        EnsureFolder(AnimationsFolder);
        Backup(ZombieControllerPath);
        AnimatorController zombieController = CreateZombieController(idle, run, attack, hitClip, death);
        Ok("07.Animations/ZombieAnimator: 트리거 IdleToMove, MoveToIdle, MoveAttackDelay, StartAttack, AttackMove, Hit, Death");
        Ok("상태 Idle(Z_Idle, 처음), Move(Z_Run_InPlace), Attack Process[AttackDelay(Z_Idle), Attack(Z_Attack)], Hit(속도 4), Death(Z_FallingForward)");

        // ---------- 15.2 + 15.3 + 15.5 Zombie 프리팹 ----------
        Section("15.2 Zombie 프리팹의 몸을 모델로 바꾸기");
        Progress("15.2 Zombie 프리팹", 0.4f);
        string prefabPath = FindZombiePrefabPath();
        if (prefabPath == null)
        {
            Warn("Zombie 스크립트가 붙은 프리팹을 찾지 못했습니다. (03.Prefabs/Zombie)");
            return;
        }

        Backup(prefabPath);
        GameObject zombieModelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(zombieModelPath);
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            GameObject model = PlaceModel(root.transform, zombieModelAsset, zombieModelPath);
            Ok("Zombie1을 자식으로 넣고 이름을 Model로, Position (0, -1, 0), Rotation (0, 0, 0)");

            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
            {
                animator = model.AddComponent<Animator>();
            }

            Avatar avatar = LoadAvatar(zombieModelPath);
            if (avatar != null)
            {
                animator.avatar = avatar;
            }

            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = zombieController;
            Ok("Model의 Animator: Avatar " + (animator.avatar != null ? animator.avatar.name : "없음") + ", Apply Root Motion 끔, Controller ZombieAnimator");

            if (model.GetComponent<ZombieHitEvent>() == null)
            {
                model.AddComponent<ZombieHitEvent>();
            }

            Ok("Model에 ZombieHitEvent 붙이기 (15.5)");

            MeshRenderer meshRenderer = root.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.enabled = false;
                Ok("Zombie의 Mesh Renderer 끄기");
            }

            CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                capsule = root.AddComponent<CapsuleCollider>();
            }

            capsule.center = new Vector3(0f, -0.1f, 0f);
            capsule.radius = 0.25f;
            capsule.height = 1.8f;
            Ok("Capsule Collider: Center (0, -0.1, 0), Radius 0.25, Height 1.8 (Nav Mesh Agent는 그대로)");

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Ok(prefabPath + " 저장");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 15.1 7번처럼 씬에 남은 Zombie1이 있으면 알려 준다.
        Scene scene = SceneManager.GetActiveScene();
        int looseModels = scene.GetRootGameObjects().Count(go => IsInstanceOf(go, zombieModelPath));
        if (looseModels > 0)
        {
            Warn("씬에 확인용으로 끌어다 놓은 Zombie1이 " + looseModels + "개 남아 있습니다. (15.1 7번처럼 지워 주세요)");
        }
    }

    private static AnimatorController CreateZombieController(AnimationClip idle, AnimationClip run, AnimationClip attack,
        AnimationClip hit, AnimationClip death)
    {
        AssetDatabase.DeleteAsset(ZombieControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ZombieControllerPath);

        // 파라미터 (15.3, 15.4, 15.6.4)
        foreach (string trigger in new[] { "IdleToMove", "MoveToIdle", "MoveAttackDelay", "StartAttack", "AttackMove", "Hit", "Death" })
        {
            controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
        }

        AnimatorStateMachine baseLayer = controller.layers[0].stateMachine;

        // 15.3 Idle, Move
        AnimatorState idleState = baseLayer.AddState("Idle", new Vector3(300f, 0f, 0f));
        idleState.motion = idle;
        baseLayer.defaultState = idleState;

        AnimatorState moveState = baseLayer.AddState("Move", new Vector3(300f, 140f, 0f));
        moveState.motion = run;

        AddTransition(idleState, moveState, false, "IdleToMove");
        AddTransition(moveState, idleState, false, "MoveToIdle");

        // 15.4 서브 스테이트 머신 Attack Process
        AnimatorStateMachine attackProcess = baseLayer.AddStateMachine("Attack Process", new Vector3(600f, 140f, 0f));
        AnimatorState attackDelay = attackProcess.AddState("AttackDelay", new Vector3(300f, 0f, 0f));
        attackDelay.motion = idle;
        attackProcess.defaultState = attackDelay;

        AnimatorState attackState = attackProcess.AddState("Attack", new Vector3(300f, 140f, 0f));
        attackState.motion = attack;

        AddTransition(attackDelay, attackState, false, "StartAttack");
        AddTransition(attackState, attackDelay, true, null);
        AddTransition(attackDelay, moveState, false, "AttackMove");
        AddTransition(attackState, moveState, false, "AttackMove");
        AddTransition(moveState, attackDelay, false, "MoveAttackDelay");

        // 15.6.4 Hit, Death
        AnimatorState hitState = baseLayer.AddState("Hit", new Vector3(0f, 280f, 0f));
        hitState.motion = hit;
        hitState.speed = 4f;

        AnimatorState deathState = baseLayer.AddState("Death", new Vector3(300f, 320f, 0f));
        deathState.motion = death;

        AnimatorStateTransition anyToHit = baseLayer.AddAnyStateTransition(hitState);
        SetupTransition(anyToHit, false, "Hit");
        anyToHit.canTransitionToSelf = true;

        AddTransition(hitState, moveState, true, null);

        AnimatorStateTransition anyToDeath = baseLayer.AddAnyStateTransition(deathState);
        SetupTransition(anyToDeath, false, "Death");
        anyToDeath.canTransitionToSelf = false;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    // ===================== 16장 =====================

    private static void RunChapter16()
    {
        // ---------- 16.1 군인 모델 가져오기 ----------
        Section("16.1 군인 모델 가져오기 (Scale Factor 0.7)");
        Progress("16.1 군인 모델", 0.5f);
        string soldierPath = FindModelPath("Soldier_demo");
        string idlePath = FindModelPath("demo_combat_idle");
        string runPath = FindModelPath("demo_combat_run");
        string shootPath = FindModelPath("demo_combat_shoot");
        if (soldierPath == null || idlePath == null || runPath == null || shootPath == null)
        {
            Warn("Low Poly Soldiers Demo의 Soldier_demo 또는 demo_combat_idle/run/shoot을 찾지 못했습니다. Import한 뒤 다시 실행해 주세요.");
            return;
        }

        ConvertMaterialsToUrp(RootFolderOf(soldierPath), "군인");

        // ---------- 16.6 Humanoid로 바꾸기 (16.1의 0.7과 함께) ----------
        ModelImporter soldierImporter = (ModelImporter)AssetImporter.GetAtPath(soldierPath);
        soldierImporter.globalScale = 0.7f;
        soldierImporter.animationType = ModelImporterAnimationType.Human;
        soldierImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        soldierImporter.SaveAndReimport();
        Ok("Soldier_demo: Scale Factor 0.7");

        Avatar soldierAvatar = LoadAvatar(soldierPath);

        Section("16.6 군인을 Humanoid로 바꾸기");
        if (soldierAvatar != null && soldierAvatar.isValid && soldierAvatar.isHuman)
        {
            Ok("Soldier_demo: Animation Type Humanoid, Create From This Model → " + soldierAvatar.name);
        }
        else
        {
            Warn("Soldier_demo의 Humanoid 아바타가 올바르지 않습니다. Rig 탭의 Configure...에서 확인해 주세요.");
        }

        Section("16.1 + 16.6 + 16.6.1 동작 파일 (Scale 0.7, Humanoid, Copy From Other Avatar, Based Upon Original)");
        foreach (string clipPath in new[] { idlePath, runPath, shootPath })
        {
            bool loop = clipPath != shootPath;
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(clipPath);
            importer.globalScale = 0.7f;
            importer.animationType = ModelImporterAnimationType.Human;
            if (soldierAvatar != null && soldierAvatar.isHuman)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = soldierAvatar;
            }

            importer.SaveAndReimport();

            EditClip(clipPath, null, clip =>
            {
                clip.loopTime = loop;
                clip.lockRootRotation = true;        // Root Transform Rotation: Bake Into Pose
                clip.keepOriginalOrientation = true; //   Based Upon: Original
                clip.lockRootHeightY = true;         // Root Transform Position (Y): Bake Into Pose
                clip.keepOriginalPositionY = true;   //   Based Upon (at Start): Original
                clip.lockRootPositionXZ = true;      // Root Transform Position (XZ): Bake Into Pose
                clip.keepOriginalPositionXZ = true;  //   Based Upon (at Start): Original
            });

            Ok(Path.GetFileNameWithoutExtension(clipPath) + ": Scale 0.7, Humanoid, Source " + (soldierAvatar != null ? soldierAvatar.name : "-")
               + ", Loop Time " + (loop ? "켬" : "끔") + ", Bake Into Pose 3곳 + Original");
        }

        AnimationClip idleClip = FirstClipIn(idlePath);
        AnimationClip runClip = FirstClipIn(runPath);
        AnimationClip shootClip = FirstClipIn(shootPath);

        // ---------- 16.3 ~ 16.5 PlayerAC ----------
        Section("16.3~16.5 PlayerAC 만들기");
        Progress("16.3 PlayerAC", 0.65f);
        EnsureFolder(PlayerAnimationsFolder);
        Backup(PlayerControllerPath);
        AnimatorController playerController = CreatePlayerController(idleClip, runClip, shootClip);
        Ok("07.Animations/Player/PlayerAC: MoveBlend(블렌드 트리, MoveMotion: demo_combat_idle 0, demo_combat_run 1, Automate Thresholds 끔, 처음 상태)");
        Ok("demo_combat_shoot 상태, Attack 트리거, MoveBlend → shoot(Has Exit Time 끔, Attack), shoot → MoveBlend(Has Exit Time 켬)");

        // ---------- 16.2 ~ 16.5 씬 ----------
        Section("16.2 플레이어의 몸을 군인으로 바꾸기");
        Progress("16.2 Player", 0.8f);
        if (!OpenSceneWithPlayer())
        {
            Warn("PlayerMove가 붙은 Player가 있는 씬을 찾지 못했습니다. GameScene을 연 뒤 '16장만 적용'을 다시 눌러 주세요.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        Backup(scene.path);
        PlayerMove playerMove = Object.FindAnyObjectByType<PlayerMove>();
        GameObject player = playerMove.gameObject;

        GameObject soldierAsset = AssetDatabase.LoadAssetAtPath<GameObject>(soldierPath);
        GameObject model = PlaceModel(player.transform, soldierAsset, soldierPath);
        Ok("Soldier_demo를 Player의 자식으로 넣고 이름을 Model로, Position (0, -1, 0), Rotation (0, 0, 0)");

        Animator animator = model.GetComponent<Animator>();
        if (animator == null)
        {
            animator = model.AddComponent<Animator>();
        }

        if (soldierAvatar != null)
        {
            animator.avatar = soldierAvatar;
        }

        animator.applyRootMotion = false;
        animator.runtimeAnimatorController = playerController;
        Ok("Model의 Animator: Avatar " + (animator.avatar != null ? animator.avatar.name : "없음") + ", Apply Root Motion 끔, Controller PlayerAC (16.3)");

        MeshRenderer meshRenderer = player.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
            Ok("Player의 Mesh Renderer 끄기");
        }

        Transform nose = player.transform.Find("Nose");
        if (nose != null)
        {
            try
            {
                Object.DestroyImmediate(nose.gameObject);
                Ok("Nose 지우기");
            }
            catch (Exception)
            {
                nose.gameObject.SetActive(false);
                Warn("Nose를 지울 수 없어서 꺼 두었습니다. (Player가 프리팹이면 프리팹에서 지워 주세요)");
            }
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.center = new Vector3(0f, -0.06f, 0f);
            controller.radius = 0.4f;
            controller.height = 1.8f;
            Ok("Character Controller: Center (0, -0.06, 0), Radius 0.4, Height 1.8 (Skin Width " + controller.skinWidth + " 그대로)");
        }
        else
        {
            Warn("Player에 Character Controller가 없습니다.");
        }

        Section("16.3~16.4 눈 위치와 카메라 Near");
        Transform cameraPosition = FindCameraPosition(player.transform);
        if (cameraPosition != null)
        {
            cameraPosition.localPosition = new Vector3(0.117f, 0.553f, 0.373f);
            Ok("CameraPosition: Position (0.117, 0.553, 0.373) (16.4 9번의 마지막 값)");
        }
        else
        {
            Warn("Player의 자식 CameraPosition을 찾지 못했습니다.");
        }

        Camera mainCamera = FindMainCamera();
        if (mainCamera != null)
        {
            mainCamera.nearClipPlane = 0.01f;
            Ok("Main Camera: Clipping Planes Near 0.01 (16.3에서 0.22 → 16.4에서 0.01)");
        }
        else
        {
            Warn("Main Camera를 찾지 못했습니다.");
        }

        Section("16.5 GameManager의 Player Animator");
        GameManager gameManager = Object.FindAnyObjectByType<GameManager>();
        if (gameManager != null)
        {
            SerializedObject serialized = new SerializedObject(gameManager);
            SerializedProperty property = serialized.FindProperty("_playerAnimator");
            if (property != null)
            {
                property.objectReferenceValue = animator;
                serialized.ApplyModifiedProperties();
                Ok("GameManager의 Player Animator 칸에 Player/Model");
            }
            else
            {
                Warn("GameManager에 _playerAnimator 칸이 없습니다. 함께 드린 GameManager.cs로 바꿨는지 확인해 주세요.");
            }
        }
        else
        {
            Warn("씬에 GameManager가 없습니다.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Ok(scene.path + " 저장");
    }

    private static AnimatorController CreatePlayerController(AnimationClip idle, AnimationClip run, AnimationClip shoot)
    {
        AssetDatabase.DeleteAsset(PlayerControllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(PlayerControllerPath);

        // 16.4: 블렌드 트리의 Float 파라미터 Blend → 이름을 MoveMotion으로
        controller.AddParameter("MoveMotion", AnimatorControllerParameterType.Float);
        AnimatorState moveBlend = controller.CreateBlendTreeInController("MoveBlend", out BlendTree tree, 0);
        tree.name = "Blend Tree";
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "MoveMotion";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(run, 1f);

        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = parameters.Length - 1; i >= 0; i--)
        {
            if (parameters[i].name == "Blend")
            {
                controller.RemoveParameter(i);
            }
        }

        AnimatorStateMachine baseLayer = controller.layers[0].stateMachine;
        baseLayer.defaultState = moveBlend;  // Set as Layer Default State

        // 16.5 쏘는 동작
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        AnimatorState shootState = baseLayer.AddState("demo_combat_shoot", new Vector3(300f, 140f, 0f));
        shootState.motion = shoot;

        AddTransition(moveBlend, shootState, false, "Attack");
        AddTransition(shootState, moveBlend, true, null);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    // ===================== 애니메이터 도우미 =====================

    // 노션의 Make Transition과 같은 기본값(섞는 시간 0.25초)으로 전환을 만든다.
    private static void AddTransition(AnimatorState from, AnimatorState to, bool hasExitTime, string trigger)
    {
        AnimatorStateTransition transition = from.AddTransition(to, true);
        SetupTransition(transition, hasExitTime, trigger);
    }

    private static void SetupTransition(AnimatorStateTransition transition, bool hasExitTime, string trigger)
    {
        transition.hasExitTime = hasExitTime;
        transition.hasFixedDuration = true;
        transition.duration = 0.25f;
        if (!string.IsNullOrEmpty(trigger))
        {
            transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }
    }

    // ===================== 모델 / 프리팹 / 씬 도우미 =====================

    // parent 아래에 모델을 Model이라는 이름으로 둔다. 이미 같은 모델이 있으면 그것을 쓰고, 다른 모델의 Model은 바꾼다.
    private static GameObject PlaceModel(Transform parent, GameObject modelAsset, string modelPath)
    {
        GameObject model = null;
        foreach (Transform child in parent)
        {
            if (IsInstanceOf(child.gameObject, modelPath))
            {
                model = child.gameObject;
                break;
            }
        }

        if (model == null)
        {
            // 예전에 넣어 둔 다른 모델(애니메이터가 있는 자식)은 노션의 모델로 바꾼다.
            Transform old = null;
            foreach (Transform child in parent)
            {
                if (child.name != "Model_Old" && child.GetComponentInChildren<Animator>(true) != null)
                {
                    old = child;
                    break;
                }
            }

            if (old != null)
            {
                try
                {
                    Object.DestroyImmediate(old.gameObject);
                    Log.Add("  - 원래 있던 Model은 노션의 모델로 바꿨습니다.");
                }
                catch (Exception)
                {
                    // 프리팹 인스턴스의 자식이라 지울 수 없으면 끄고 이름을 바꿔 둔다.
                    old.gameObject.SetActive(false);
                    old.name = "Model_Old";
                    Warn("원래 있던 Model을 지울 수 없어서 꺼 두고 이름을 Model_Old로 바꿨습니다.");
                }
            }

            model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, parent);
        }

        model.name = "Model";
        model.transform.localPosition = new Vector3(0f, -1f, 0f);
        model.transform.localRotation = Quaternion.identity;
        return model;
    }

    private static bool IsInstanceOf(GameObject go, string modelPath)
    {
        if (!PrefabUtility.IsAnyPrefabInstanceRoot(go))
        {
            return false;
        }

        Object source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
        return source != null && AssetDatabase.GetAssetPath(source) == modelPath;
    }

    private static string FindZombiePrefabPath()
    {
        const string coursePath = "Assets/03.Prefabs/Zombie.prefab";
        GameObject course = AssetDatabase.LoadAssetAtPath<GameObject>(coursePath);
        if (course != null && course.GetComponent<Zombie>() != null)
        {
            return coursePath;
        }

        string fallback = null;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(BackupRoot))
            {
                continue;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.GetComponent<Zombie>() != null)
            {
                if (Path.GetFileNameWithoutExtension(path) == "Zombie")
                {
                    return path;
                }

                fallback ??= path;
            }
        }

        return fallback;
    }

    // 지금 열린 씬에 Player(PlayerMove)가 없으면 GameScene을 연다.
    private static bool OpenSceneWithPlayer()
    {
        if (Object.FindAnyObjectByType<PlayerMove>() != null)
        {
            return true;
        }

        List<string> candidates = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !p.StartsWith(BackupRoot))
            .OrderBy(p => Path.GetFileNameWithoutExtension(p) == "GameScene" ? 0 : 1)
            .ToList();

        foreach (string path in candidates)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (Object.FindAnyObjectByType<PlayerMove>() != null)
            {
                Log.Add("  - " + path + " 씬을 열었습니다.");
                return true;
            }
        }

        return false;
    }

    private static Transform FindCameraPosition(Transform player)
    {
        Transform found = player.Find("CameraPosition");
        if (found != null)
        {
            return found;
        }

        CameraFollow follow = Object.FindAnyObjectByType<CameraFollow>();
        if (follow != null)
        {
            SerializedProperty target = new SerializedObject(follow).FindProperty("_target");
            if (target != null && target.objectReferenceValue is Transform transform)
            {
                return transform;
            }
        }

        return null;
    }

    private static Camera FindMainCamera()
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            return camera;
        }

        CameraFollow follow = Object.FindAnyObjectByType<CameraFollow>();
        return follow != null ? follow.GetComponent<Camera>() : null;
    }

    private static void Backup(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || AssetDatabase.LoadAssetAtPath<Object>(assetPath) == null)
        {
            return;
        }

        EnsureFolder(_backupFolder);
        string target = _backupFolder + "/" + Path.GetFileName(assetPath);
        if (AssetDatabase.CopyAsset(assetPath, target))
        {
            Log.Add("  - 백업: " + target);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // 15.1 / 16.1: Render Pipeline Converter의 Material Upgrade처럼 Standard 머티리얼을 URP Lit으로 바꾼다.
    private static void ConvertMaterialsToUrp(string folder, string label)
    {
        Shader urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null || string.IsNullOrEmpty(folder))
        {
            return;
        }

        int converted = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == null)
            {
                continue;
            }

            string shaderName = material.shader.name;
            bool builtIn = shaderName.StartsWith("Standard") || shaderName.StartsWith("Legacy Shaders") || shaderName.StartsWith("Mobile/")
                           || shaderName == "Hidden/InternalErrorShader";
            if (!builtIn)
            {
                continue;
            }

            Texture mainTexture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            Texture normal = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;

            material.shader = urp;
            if (mainTexture == null)
            {
                // 컨버터가 실패해 분홍색인 경우: 같은 폴더 묶음의 이름이 같은 텍스처를 Base Map에 (15.1 경고 상자)
                mainTexture = FindTextureNear(folder, material.name);
            }

            material.SetTexture("_BaseMap", mainTexture);
            material.SetColor("_BaseColor", color);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(material);
            converted++;
        }

        Ok(label + " 머티리얼 URP 변환 (Material Upgrade): " + (converted > 0 ? converted + "개 바꿈" : "이미 URP라 바꿀 것 없음"));
    }

    private static Texture FindTextureNear(string folder, string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Texture2D", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }

        return null;
    }

    // ===================== 임포트 설정 도우미 =====================

    // 모델 파일 안 동작의 설정을 고친다. clipName이 null이면 첫 동작
    private static bool EditClip(string modelPath, string clipName, Action<ModelImporterClipAnimation> edit)
    {
        ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null)
        {
            Warn(modelPath + " 의 임포트 설정을 열 수 없습니다.");
            return false;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        if (clips == null || clips.Length == 0)
        {
            Warn(modelPath + " 안에 동작이 없습니다.");
            return false;
        }

        int index = 0;
        if (clipName != null)
        {
            index = Array.FindIndex(clips, c => c.name == clipName || c.takeName == clipName);
            if (index < 0)
            {
                index = 0;
            }
        }

        edit(clips[index]);
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
        return true;
    }

    private static string FindModelPath(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Model"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }

    private static string RootFolderOf(string assetPath)
    {
        // Assets/Zombie/FBXs/Zombie1.fbx → Assets/Zombie
        string[] parts = assetPath.Split('/');
        return parts.Length >= 3 ? parts[0] + "/" + parts[1] : Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
    }

    private static AnimationClip FindClip(string clipName)
    {
        // 1) 동작 이름이 같은 클립
        foreach (string guid in AssetDatabase.FindAssets(clipName))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(BackupRoot) || AssetDatabase.IsValidFolder(path))
            {
                continue;
            }

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && clip.name == clipName)
                {
                    return clip;
                }
            }
        }

        // 2) 파일 이름이 'Zombie@Z_Attack'처럼 끝나는 모델의 첫 동작
        foreach (string guid in AssetDatabase.FindAssets("t:Model"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (string.Equals(fileName, clipName, StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith("@" + clipName, StringComparison.OrdinalIgnoreCase))
            {
                AnimationClip clip = FirstClipIn(path);
                if (clip != null)
                {
                    return clip;
                }
            }
        }

        return null;
    }

    private static AnimationClip FirstClipIn(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    private static Avatar LoadAvatar(string modelPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
    }
}
