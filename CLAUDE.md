# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 개요

Unity 6 (`6000.3.22f1`, URP) 기반 3D FPS 학습용 프로젝트. 게임 코드는 전부 `Assets/02.Scripts/` 아래에 있고, 메인 씬은 `Assets/01.Scenes/GameScene.unity` 하나다. 코드 주석과 로그 메시지는 한국어로 작성한다.

## 명령어

빌드와 실행은 Unity 에디터에서 한다. CLI 빌드 스크립트나 테스트 코드(asmdef, Test Runner 테스트)는 현재 없다.

```bash
# 최초 1회: Husky.Net 로컬 도구 복원 및 git hook 설치
dotnet tool restore
dotnet husky install

# 포맷/네이밍 검사 (pre-commit hook과 동일)
dotnet format 3DFPSGame_GA1.sln --verify-no-changes --severity warn --include Assets/02.Scripts/

# 자동 수정
dotnet format 3DFPSGame_GA1.sln --severity warn --include Assets/02.Scripts/
```

`.sln`/`.csproj`는 Unity가 생성하는 파일이다. 스크립트를 새로 추가했다면 `dotnet format`이 인식하도록 Unity 에디터에서 프로젝트 파일을 다시 생성해야 할 수 있다.

## Git 규칙 (Husky hook으로 강제)

- **커밋 메시지**: `type(scope)?: 설명` 형식. 허용 type은 `asset|build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test`. 형식이 틀리면 `commit-msg` hook이 커밋을 거부한다.
- **pre-commit**: 위의 `dotnet format --verify-no-changes`가 실행되어 경고가 하나라도 있으면 커밋이 실패한다.
- `main`에 `Assets/02.Scripts/**/*.cs` 변경이 push되면 GitHub Actions(`.github/workflows/gemini-commit-review.yml`)가 Gemini로 해당 커밋을 리뷰해 커밋 코멘트를 단다. 리뷰 기준은 `Docs/CodeReviewGuide.md`.

## 코드 스타일 (`.editorconfig`, `Assets/02.Scripts/**.cs`에만 적용)

- 4칸 스페이스, 여는 중괄호는 항상 새 줄(Allman).
- 중괄호는 여러 줄일 때만 강제. 한 줄 조기 리턴(`if (...) return;`)은 중괄호 없이 허용.
- private 필드는 `_camelCase`, private `const`와 `static readonly`는 `PascalCase`, 타입명은 `_` 없는 PascalCase, 지역변수는 camelCase.
- Inspector 노출은 public 필드 대신 `[SerializeField] private` + 읽기 전용 프로퍼티(`public float Health => _health;`) 패턴을 쓴다.
- 코루틴 메서드는 `Xxx_Coroutine` 접미사를 붙인다.

## 아키텍처

- **게임 상태 게이트**: `GameManager`는 싱글톤(`GameManager.Instance`)이며 `GameState`(Ready → Playing → GameOver)를 가진다. 플레이어 입력, 사격, 좀비 AI 같은 게임플레이 `Update`는 맨 앞에서 `GameManager.Instance.State != GameState.Playing`이면 바로 리턴한다. 새로운 게임플레이 로직도 같은 가드를 따라야 한다.
- **대미지 시스템**: 대미지를 받는 모든 객체(`Player`, `Zombie`, `Drum`, `Box`)는 `Core/IDamageable`을 구현하고, 공격 쪽은 `Core/DamageInfo` 구조체(대미지 양, 맞은 위치, 법선)를 만들어 `GetComponent<IDamageable>()?.TakeDamage(info)`로 전달한다. 구체 타입별로 분기하지 말고 이 인터페이스를 통해야 한다.
- **좀비 AI**: `Monster/Zombie`는 `ZombieState` enum과 `switch` 기반의 FSM이다. `Update`에서 상태별 메서드를 호출하고, 시간이 걸리는 상태(Hit, Death)는 코루틴으로 처리한 뒤 다음 상태로 전이한다. 이동에는 `CharacterController`를 쓰고, 플레이어는 `"Player"` 태그로 찾는다.
- **플레이어**: 기능별로 컴포넌트가 나뉘어 있다(`Player` 체력/피격, `PlayerMove`, `PlayerRotate`, `PlayerGunFire`, `PlayerBombFire`). 사격은 카메라 기준 `Physics.Raycast`로 판정한다. 커서가 보이는 동안(`CursorManager`)에는 사격하지 않는다.
- **입력**: Active Input Handling이 Both로 설정되어 있지만, 현재 코드는 레거시 `Input` API(`Input.GetButton`, `Input.GetKeyDown`)를 쓴다.
- **UI**: `UI_` 접두사 클래스(`UI_PlayerStats`, `UI_HitEffect`, `UI_Minimap`)는 TextMeshPro와 uGUI를 사용하며, 대개 Inspector 참조로 연결된다.
- `Assets/02.Scripts/Example/`은 C# 문법 학습용 예제이고 게임 로직과는 무관하다.

## 코드 리뷰 기준

리뷰를 요청받으면 `Docs/CodeReviewGuide.md`를 따른다. 핵심은 다음과 같다.

- 변경된 코드에서 실제로 일어날 수 있는 문제를 우선한다.
- 취향 차이나 과도한 추상화/디자인 패턴 적용을 필수 수정처럼 표현하지 않는다.
- 중요도는 Critical/High/Medium/Low로 표시한다.
- 결과는 총평 → 발견된 문제 → 설계 개선 제안 → 잘 작성된 점 순서로 작성한다.
