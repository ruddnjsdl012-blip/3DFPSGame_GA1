# 재생 및 맵 복구 패키지 — 입력 축 누락 수정

사용자가 올린 `test(20261027-035937).unitypackage`를 기준으로 수정했습니다.

## 적용

1. Unity에서 Play 모드를 종료합니다.
2. ZIP을 일반 폴더에 압축 해제하고, 안의 Assets 폴더를 기존 프로젝트 루트에 복사하여 병합·덮어씁니다. 기존 Assets 폴더를 삭제하지 않습니다.
3. Unity를 다시 열고 컴파일이 끝나면 `Tools > Zombie Game > Repair Play and Map`을 실행합니다.
4. `Assets/01.Scenes/GameScene.unity`가 열렸는지 확인한 뒤 Play를 누릅니다.
5. 시작 화면의 게임 시작 버튼을 선택합니다.

## Mouse X / Mouse Y / Fire1 is not setup 오류

이번 수정본에는 `Tools > Zombie Game > Repair Input Axes` 메뉴를 추가했습니다.
Play를 종료하고 이 메뉴를 실행한 뒤 Console을 Clear하고 다시 Play하세요.
맵 복구를 다시 실행할 필요 없이 입력 설정만 복구할 수 있습니다.

- `Mouse X`, `Mouse Y`, `Horizontal`, `Vertical`, `Fire1`, `Fire2`, `Jump`, `Submit`, `Cancel` 중 없는 이름을 등록합니다.
- 기존 입력 항목과 감도, 키 바인딩은 유지합니다. 다시 실행해도 같은 항목을 중복 추가하지 않습니다.
- 마우스 축은 Mouse Movement로 등록하고 X/Y축을 각각 지정합니다. 이동은 WASD/방향키, 발사는 마우스 왼쪽, 점프는 Space입니다.
- 변경 전 `ProjectSettings/InputManager.asset`을 `ZombieRepairBackups` 안에 복사합니다.
- 기존 `Repair Play and Map` 메뉴도 같은 입력 복구를 수행합니다.

## 변경

- 기존 게임의 이동, 마우스 회전, 사격, 정조준, 수류탄, 손전등, 일시 정지 입력을 하나의 입력 호환 코드로 연결했습니다.
- Input Manager만 사용하는 프로젝트와 Input System만 사용하는 프로젝트를 모두 처리합니다. Both에서는 원래 Input Manager의 축 설정을 사용합니다.
- Input System만 켜져 있을 때 시작 메뉴의 StandaloneInputModule을 InputSystemUIInputModule로 연결합니다.
- 새 FPS 샘플의 Character.cs와 TimeHandler.cs에서 Input System이 꺼져 있거나 설치되지 않은 경우 해당 입력 콜백을 컴파일하지 않습니다. 원본 샘플의 나머지 동작은 보존합니다.
- 복구 메뉴는 원래 GameScene을 실행 씬으로 지정하고, 정지 상태를 해제하며, 맵 재질의 셰이더가 현재 Built-in/URP 설정과 맞지 않을 때 교정합니다.
- 맵에서 메시가 누락된 Kenney 프리팹은 해당 모델을 다시 임포트하고 남은 누락을 Console에 표시합니다.
- 복구 메뉴가 수정하는 재질과 디스크상의 씬은 프로젝트의 `ZombieRepairBackups` 폴더에 먼저 복사됩니다.

## 확인 범위

- 원본 씬의 모든 로컬 참조와 오브젝트 ID, 원본 에셋 GUID를 검사했습니다.
- Old, New, Both 입력 조건 각각에서 C# 구문과 조건부 의존성을 검사했습니다.
- 맵의 지형, 건물 배치, 원래 씬 파일과 카메라 위치는 제공받은 상태를 보존했습니다.
- Unity 에디터와 프로젝트의 ProjectSettings/Packages/Editor.log가 제공되지 않아 실제 컴파일 및 Play 실행은 이 작업 환경에서 검증하지 못했습니다.
- 따라서 현재 Console의 정확한 오류나 화면이 다른 원인이라면 추가 확인이 필요합니다. 그 경우 Console 첫 번째 빨간 오류의 전체 내용을 보내 주세요.

외형 교체 작업보다 기존 게임 재생 복구를 먼저 처리한 수정본입니다.
