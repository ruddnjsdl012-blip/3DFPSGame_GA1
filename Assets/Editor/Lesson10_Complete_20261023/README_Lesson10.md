# 10장 체력바와 피격 효과 — 적용본

교안: https://fluoridated-libra-ce2.notion.site/10-402d025da08780ddb006fa98ca8c590d

## 적용 방법

1. Unity의 Play 모드를 끕니다.
2. Lesson10_Complete_Import.unitypackage를 열고 전체 Import합니다.
3. 컴파일과 Import가 끝나면 Assets/01.Scenes/GameScene.unity를 다시 엽니다. 외부 변경을 다시 불러오라는 메시지는 Reload를 선택합니다.
4. Play합니다. Inspector를 추가로 연결할 필요가 없도록 씬과 좀비 프리팹에 연결을 저장했습니다.

## 동작

- 좀비 머리 위: 100×12, Scale 0.01의 World Space 체력바. 높이 1.4m. 슬라이더는 비활성 입력이며 손잡이/여백/Graphic Raycaster를 제거했습니다. 총에 맞으면 줄고, 체력이 그대로면 Slider를 다시 쓰지 않습니다.
- 화면 왼쪽 아래: 교안의 640×140 묶음 안에 체력 600×80, 스태미나 600×50 막대. 빨강 (229,72,77), 노랑 (255,197,61). 기준 해상도 1920×1080, Match 0.5입니다.
- 플레이어: 체력 100으로 시작하고 좀비 공격마다 10 감소합니다. 체력은 0 아래로 내려가지 않으며 쓰러진 뒤 추가 피격을 무시합니다.
- Shift 달리기와 점프: 기존 이동/소모/회복 수치를 사용합니다. 별도 Sprint 입력축 없이 좌우 Shift로 달릴 수 있습니다. 스태미나를 소비하는 동안 회복하지 않습니다.
- 피격: 화면 전체가 알파 0.4로 붉게 번쩍이고 0.3초에 걸쳐 0으로 사라집니다. 연속 피격 시 페이드를 재시작합니다. 피격 이미지는 조준선·막대보다 먼저 그리며 입력을 가로채지 않습니다.
- 좀비 빌보드는 LateUpdate에서 HealthBarCanvas만 카메라와 같은 방향으로 맞춥니다.

UI는 표시만 담당하고 Player/PlayerMove/Zombie가 실제 수치를 관리합니다. HUD와 좀비 체력바 모두 값이 바뀔 때만 Slider를 갱신합니다. 카메라는 캐시하며 매 프레임 오브젝트 검색·텍스처 생성은 없습니다.

## 범위

이번 교안 10.1~10.6의 구현입니다. 11장의 게임 오버와 이전 장의 선택 과제인 복귀 시 체력 회복은 추가하지 않았습니다. 기존 무기/폭탄/풀링/맵/카메라 제어 코드는 보존했습니다. PlayerMove는 기존 동작을 유지하며 Shift 입력만 명시적으로 읽습니다.

## 검증

동봉 REQUIREMENTS_AND_VALIDATION.json에 항목별 구현과 검사 결과가 있습니다. C# API 스텁 기반 검사와 씬/프리팹/패키지 정적 검사 결과이며 Unity Editor Import/Play/Profiler는 실행하지 않았습니다.

## 되돌리기

이 폴더의 Lesson10_BeforeImplementation.unitypackage.bytes를 프로젝트 밖으로 복사하고 마지막 .bytes를 제거한 뒤 Import하면 변경한 8개 파일을 원본으로 복원할 수 있습니다. 문서와 백업은 Editor 폴더에 있어 게임 빌드에 포함되지 않습니다.
