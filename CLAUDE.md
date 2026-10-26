# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 개요

Unity **6000.3.22f1** (URP 17.3)로 만드는 3D FPS 학습용 게임 프로젝트입니다. 씬은 `Assets/01.Scenes/GameScene.unity` 하나이고, 직접 작성하는 게임 코드는 모두 `Assets/02.Scripts/` 아래에 있습니다. `BloodDecalsAndEffects`, `BombGuide`, `FreeModels_*`, `Minimap_Resource`, `TextMesh Pro`, `TutorialInfo`, `Workshop` 등은 외부 에셋이나 수업 자료이므로 요청이 없으면 수정하지 않습니다.

## 명령어

빌드, 실행, 테스트는 Unity 에디터에서 합니다. 셸에서 실행할 수 있는 빌드나 테스트 스크립트는 없고, `Assets/02.Scripts/Test/`에 있는 파일도 단위 테스트가 아니라 C# 문법 연습용 MonoBehaviour입니다.

- **포맷/스타일 검사**: pre-commit 훅이 실행하는 명령과 같습니다.
  ```
  dotnet format 3DFPSGame_GA1.sln --verify-no-changes --severity warn --include Assets/02.Scripts/
  ```
  자동으로 고치려면 `--verify-no-changes`를 빼고 실행합니다. `.sln`과 `.csproj`는 Unity가 생성하므로, 파일이 없으면 에디터나 Rider에서 먼저 프로젝트를 열어야 합니다.
- **Git 훅** (Husky.Net, `.husky/`): 처음 클론했다면 `dotnet tool restore` 다음에 `dotnet husky install`을 실행합니다.
  - `commit-msg`: 커밋 메시지는 반드시 `type: 설명` 형식이어야 합니다. 허용 type은 `asset|build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test`이며, 기존 커밋은 한국어로 설명합니다 (예: `feat: 미니맵 구현`).
  - `pre-commit`: 위 `dotnet format` 검사를 실행합니다.
  - Git LFS를 사용합니다. `git-lfs`가 PATH에 있어야 합니다.
- **CI**: `main`에 `Assets/02.Scripts/**/*.cs` 변경이 푸시되면 `.github/workflows/gemini-commit-review.yml`이 Gemini로 커밋을 리뷰합니다. 리뷰 기준은 `Docs/CodeReviewGuide.md`에 있습니다.

## 코드 스타일 (`.editorconfig`)

- 4칸 공백 들여쓰기를 사용합니다.
- private 필드는 `_camelCase`, private `const`와 `static readonly`는 `PascalCase`, 타입은 `PascalCase`로 짓습니다. 위반하면 warning이 나고 pre-commit이 실패합니다.
- Inspector 값은 `[SerializeField] private` 필드와 `[Header("스탯")]` 같은 한국어 헤더로 노출합니다.
- 코루틴 이름에는 `_Coroutine` 접미사를 붙입니다 (예: `Reloading_Coroutine`).
- UI 스크립트에는 `UI_` 접두사를 붙입니다.
- 주석과 로그 메시지는 한국어로 씁니다.
- 필수 참조는 `Awake`에서 null을 검사하고, 없으면 `Debug.LogError(..., this)`를 남긴 뒤 `enabled = false`로 끕니다.
- 입력은 레거시 `Input` API를 사용합니다 (`Input.GetAxis`, `GetButton("Fire1")` 등). `InputSystem_Actions.inputactions`가 있지만 지금 코드에서는 쓰지 않습니다.

## 아키텍처

### 게임 상태와 GameManager
- `GameManager`는 싱글턴이고 `[DefaultExecutionOrder(-100)]`로 다른 스크립트보다 먼저 실행됩니다. 상태(`GameState`: Ready → Playing ↔ Pause, GameOver/Clear)는 **GameManager만 바꿉니다**.
- 게임플레이 스크립트(이동, 회전, 사격, 좀비 AI, 피격)는 모두 `Update`와 `TakeDamage` 맨 앞에서 `if (GameManager.IsPlaying == false) return;`으로 상태를 확인합니다. 새로 만드는 게임플레이 스크립트도 이 패턴을 따릅니다.
- 일시정지와 결과 화면은 `Time.timeScale = 0`으로 처리합니다. 그래서 그동안에도 움직여야 하는 연출은 `unscaledDeltaTime`이나 `WaitForSecondsRealtime`을 씁니다.
- 승리 판정: `Start`에서 씬의 `Zombie`를 한 번만 수집해 `HashSet`에 넣고, 좀비는 죽을 때 `NotifyZombieDefeated(this)`를 호출합니다. 런타임에 좀비를 스폰한다면 이 등록 경로도 함께 고쳐야 합니다.
- 재시작은 활성 씬을 다시 로드하는 방식입니다. `Assets/Editor/.../Lesson11BuildSetup.cs`가 GameScene을 Build Settings에 자동으로 등록합니다.
- 커서 잠금은 `CursorManager`가 맡습니다. Left Alt를 누르면 해제되고 Right Alt를 누르면 다시 잠깁니다. `PlayerGunFire`는 `Cursor.visible`이 true이면 발사하지 않습니다.

### 데미지 시스템
- `IDamageable.TakeDamage(DamageInfo)`가 공통 계약입니다. `DamageInfo`는 Value, HitPoint, HitNormal을 담는 struct입니다. 구현체는 `Player`, `Zombie`, `Drum`이 있고, `Box`는 아직 미구현이라 `IDamageable`을 구현하지 않았습니다.
- 공격하는 쪽은 대상 타입을 모르고 `GetComponent<IDamageable>()`로만 대미지를 전달합니다. 총은 카메라 정면으로 Raycast를 쏘고, `Bomb`과 `Drum`은 `OverlapSphere`로 범위 대미지를 줍니다. 범위 대미지는 `LayerMask.GetMask("Drum", "Zombie")` 레이어만 대상으로 하므로 새 피격 대상에는 레이어 설정이 필요합니다.
- 레이어는 `Bomb`, `Player`, `Drum`, `Zombie`, `Minimap`을 사용합니다 (`ProjectSettings/TagManager.asset`). 좀비는 `"Player"` 태그로 플레이어를 찾습니다.

### 플레이어와 카메라
- 좌우 회전은 `PlayerRotate`가 플레이어 루트에서 처리합니다 (실행 순서 -50).
- 상하 회전은 `CameraRotate`가 카메라 쪽 자식에서 로컬 X축만 처리합니다 (실행 순서 -40).
- `CameraFollow`는 `LateUpdate`에서 대상의 회전을 반영한 상대 오프셋을 유지합니다.
- 이동은 `CharacterController`로 하며 중력, 2단 점프, 스태미나 소모(달리기, 점프)를 포함합니다.
- `MinimapCamera`는 플레이어 위에서 내려다보는 직교 카메라이고, `Minimap` 레이어와 마스크를 이용해 둥근 미니맵을 만듭니다.

### 좀비 AI
- `Zombie`는 enum 기반 FSM입니다 (Idle/Trace/Comeback/Attack/Hit/Death).
- `Update`의 switch는 Idle/Trace/Comeback/Attack만 처리합니다. Hit와 Death는 코루틴으로 진행합니다.
- 피격 시 `StopAllCoroutines()`를 호출한 뒤 상태를 전이합니다.
- 체력바(`ZombieHealthBar`)와 플레이어 HUD(`UI_PlayerStats`)는 `[DefaultExecutionOrder(100)]`로 늦게 실행됩니다. 이전 값을 캐시해 두고 값이 바뀔 때만 Slider를 갱신합니다.

### 오브젝트 풀링 (진행 중)
- `BombPool`과 `PooledBombExplosion`은 Stack(대기)과 HashSet(대여 중)으로 구현된 풀입니다. 하지만 **아직 연결되지 않았습니다**. 지금 `PlayerBombFire`는 `Instantiate`를 쓰고 `Bomb`은 충돌하면 `Destroy`합니다.
- 풀링을 적용하려면 `TrySpawn`, `Explode`, `Return` 경로를 사용하도록 `PlayerBombFire`와 `Bomb`을 함께 수정해야 합니다.

## 코드 리뷰 기준

리뷰를 요청받으면 `Docs/CodeReviewGuide.md`의 형식(총평 / 발견된 문제(중요도·위치·설명·결과·수정 제안) / 설계 개선 제안 / 잘 작성된 점)과 중요도 기준(Critical/High/Medium/Low)을 따릅니다. 이 프로젝트는 학습 단계이므로 과도한 추상화나 디자인 패턴 적용은 제안하지 않습니다.
