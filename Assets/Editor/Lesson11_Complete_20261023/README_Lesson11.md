# 11장 게임 매니저와 게임 상태 — 적용본

교안: https://fluoridated-libra-ce2.notion.site/11-402d025da087802c8ad9f617e66d3d55

## 적용

1. 기존 프로젝트에서 Unity Play를 끕니다.
2. Lesson11_Complete_Import.unitypackage를 열어 전체 Import합니다.
3. 컴파일과 Import가 끝날 때까지 기다립니다.
4. Assets/01.Scenes/GameScene.unity를 다시 엽니다. 외부 변경을 다시 불러오라는 창이 뜨면 Reload합니다.
5. Play합니다. Inspector를 추가로 연결할 필요 없이 씬에 GameManager, 글자, 버튼, 커서 연결이 저장되어 있습니다.

## 구현

- 준비 2초 → 시작! 0.5초 → 플레이. 준비 중에는 이동·회전·사격·재장전·투척·좀비 AI가 실행되지 않습니다.
- 플레이어 체력이 0이면 빨간 게임 오버가 나타나고 커서가 풀리며 게임이 멈춥니다.
- R키 또는 다시 하기 버튼으로 현재 씬을 처음부터 다시 불러옵니다. 체력·탄약·좀비도 원래 상태로 돌아옵니다.
- 좀비를 모두 처치하면 초록 승리!가 나타나며 멈춥니다. 승리 뒤에도 R/버튼으로 다시 시작할 수 있습니다.
- P키로 일시 정지하고 다시 P를 누르면 계속합니다. 일시 정지 중에는 입력을 차단하고 물리도 멈춥니다.
- 선택 과제 5도 구현했습니다. Hierarchy의 GameManager → Inspector의 Use Countdown을 켜면 준비 대신 3, 2, 1을 1초씩 보여 줍니다. 기본값은 본문의 준비 연출입니다.
- 피격 페이드는 게임 오버 때도 0.3초 동안 끝나며, 쓰러진 좀비도 정리됩니다.

## 한글 폰트

Assets/08.Fonts에 Pretendard Bold TTF, 한글 2,350자와 ASCII·자모·특수문자를 포함한 2,527자 목록, 2048×2048 Alpha8 정적 SDF 및 Dynamic 대체 폰트가 들어 있습니다. 폰트 테두리는 0.2입니다. 동적 대체 폰트는 드문 글자에 사용하며 Multi Atlas와 Clear Dynamic Data On Build를 켰습니다.

상태 글자와 버튼은 한글 폰트에 직접 연결되어 있습니다. 기본 TMP 설정이 없는 프로젝트에서는 동봉한 Editor 스크립트가 한글 기본 설정을 만듭니다. 이미 설정이 있으면 보존합니다. 씬 재시작을 위해 GameScene을 기존 Build Settings 목록에 추가하거나 활성화합니다. 다른 씬 목록은 보존합니다.

한글 아틀라스는 FreeType 기반으로 미리 만든 SDF입니다. Unity Editor의 Font Asset Creator를 직접 실행해 구운 결과라고 주장하지 않습니다. Dynamic 폰트의 빠진 글자는 실제 Unity FontEngine이 실행 중에 추가합니다.

## 호환과 범위

첨부한 test(20261023-055830).unitypackage가 기준입니다. GameManager.cs 안의 중복 GameState 정의를 분리하고 잘못된 싱글톤 조건을 수정했습니다. 여섯 스크립트의 if문은 중괄호를 사용합니다. 기존 IDamageable.TakeDamage와 DamageInfo 구조를 유지하며 Player에 TryTakeDamage를 추가했습니다.

맵·카메라 위치·좀비 프리팹·체력 UI·무기 수치는 그대로입니다. 입력을 막는 상태 검사 외에 폭탄 충전/풀링 등 다른 장의 무기 기능을 새로 추가하지 않았습니다.

## 검증과 한계

변경 C#과 관련 원본 스크립트를 Roslyn으로 컴파일하고 Unity API 모델로 게임 흐름·입력 차단·정지·재시작·승리·시간 복원을 검사했습니다. 씬/폰트 참조와 아틀라스 글자 영역도 검사했습니다. 상세 결과는 REQUIREMENTS_AND_VALIDATION.json에 있습니다.

이 환경에는 Unity Editor가 없어 실제 Import·Play·Profiler와 Unity의 동적 글자 생성은 직접 실행하지 못했습니다. 직접 플레이로 확인한 완전 무오류 결과라고 주장하지 않습니다.

## 원본 복구

이 폴더의 Lesson11_BeforeImplementation.unitypackage.bytes는 변경한 기존 10개 파일의 정확한 원본입니다.

복구하려면 Play를 끄고, 새로 추가된 Assets/02.Scripts/Game/GameState.cs를 먼저 삭제합니다. 원본 GameManager.cs에 같은 enum이 있어 남겨 두면 중복 정의가 됩니다. 이어서 백업을 프로젝트 밖으로 복사하여 마지막 .bytes를 지우고 Import합니다. 새 폰트와 문서는 남아 있어도 기존 게임 코드를 바꾸지 않습니다. 자동 설정도 되돌리려면 Lesson11BuildSetup.cs 및 새로 생성된 폰트 설정을 삭제하고 Build Settings의 GameScene 항목을 필요한 상태로 되돌립니다.

## 출처

- Pretendard: https://github.com/orioncactus/pretendard — 원본 OFL 라이선스 동봉
- TextMeshPro/uGUI SDF shader·기본 리소스: https://github.com/Unity-Technologies/uGUI — 원본 MIT 라이선스 동봉
