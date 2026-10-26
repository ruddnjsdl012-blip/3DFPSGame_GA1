무료 3D 모델 외형 적용 패키지
대상: Unity 6.3 / 사용자가 보여준 GameScene

적용 방법
1. 오류가 나던 Import 창을 닫고 Play 모드를 종료합니다.
2. FreeModels_TextureFix_V4.unitypackage를 열어 전체 Import합니다.
3. 기존 GameScene을 엽니다.
4. Tools > Free Models Fixed > Apply Models to Current Scene 을 누릅니다.
5. 적용 후 Ctrl+S로 씬을 저장합니다.

이번 수정본
- 텍스처 6종의 RGBA PNG 데이터를 에디터 코드에도 내장했습니다.
- PNG 에셋 조회 성공 여부에 의존하지 않고 Unity Texture2D .asset을 생성합니다.
- 기존에 생성된 재질에도 정상 텍스처를 다시 연결합니다.
- 기존 V3와 동일한 경로/GUID를 유지하여 덮어쓰기 업데이트합니다.
- 올린 Unity 원본의 TAR 헤더 형식과 GZIP 설정을 사용해 다시 구성했습니다.
- 내부 압축 파일명은 .tar로 지정했습니다.
- 빈 폴더나 폴더 전용 항목 없이 실제 에셋 파일만 포함합니다.
- 메타데이터는 원본과 동일한 fileFormatVersion/guid 기본 형식입니다.
- 모델/텍스처 설정은 에디터의 정식 Importer API로 준비합니다.
- 새 경로와 새 GUID, 별도 에디터 클래스/메뉴를 사용합니다.

포함 모델
- 생존자 외형 2종, 좀비 외형 2종
- 드럼통, 상자, 큰 상자, 보급 상자, 판자 더미, 보강 울타리, 작업대, 잔해
- 창고/공장 건물, 물탱크 타워, 저장 탱크, 굴뚝, 컨테이너
- 25개 모델 프리팹을 에디터에서 생성합니다.

현재 씬에 적용하는 내용
- Player / Zombie / Drum / Box 컴포넌트가 붙은 오브젝트를 찾습니다.
- 원래 오브젝트 아래에 __FreeModels_Visual 이라는 외형 자식을 추가합니다.
- 원래 충돌체 크기와 중심을 기준으로 외형의 크기와 발 위치를 맞춥니다.
- 원래 오브젝트의 MeshRenderer만 숨깁니다.
- 기존 오브젝트의 스크립트, 태그, 레이어, 위치, 충돌체, Rigidbody,
  CharacterController, 카메라, Canvas와 UI는 유지합니다.
- Ground와 Map 아래의 기본 MeshRenderer 재질을 회색 계열로 맞춥니다.
- 기존 Map의 바깥에 FreeModels_Background라는 산업 시설 배경을 배치합니다.
- 배경은 장식 모델이며 새 충돌체나 AI, 조명, 카메라를 추가하지 않습니다.

애니메이션과 기능 범위
- 기존 이동/공격/체력/FSM/드럼통 폭발 코드는 교체하지 않습니다.
- 캐릭터 외형은 에디터에서 한 번 포즈를 샘플링한 정적 메시입니다.
- Animator Controller나 이동/공격 애니메이션 연동은 추가하지 않습니다.
- 에디터 적용 도구는 Editor 폴더에만 있으며 게임 빌드에는 포함되지 않습니다.

직접 배치할 경우
Tools > Free Models Fixed > Build Model Prefabs Only 를 누릅니다.
Assets/FreeModels_ImportFixed_20261026/Prefabs 폴더의 모델을 Scene에 드래그합니다.
Models 폴더에는 실제 OBJ/FBX 원본 모델이, Textures에는 해당 텍스처가 있습니다.

다시 적용 / 되돌리기
- 같은 오브젝트에 메뉴를 여러 번 실행해도 외형 자식을 중복으로 붙이지 않습니다.
- 적용 직후 Edit > Undo 또는 Ctrl+Z 한 번으로 씬 변경을 되돌릴 수 있습니다.
- 기존 프리팹 원본 에셋은 변경하지 않으므로 새로 생성되는 원본 프리팹은 별도 적용이 필요합니다.
- 필요하면 생성된 프리팹의 외형 자식을 직접 기존 프리팹에 넣을 수 있습니다.

프로젝트 파일 확인 결과
업로드된 test(20261026-021403).unitypackage에는 C# 스크립트 66개만 포함되어
GameScene과 기존 프리팹의 좌표/참조를 직접 수정할 수 없었습니다.
이 추가 패키지는 실행 중인 사용자의 원래 씬에서 외형을 적용하는 방식입니다.
원래 66개 스크립트는 이 패키지에 포함하거나 덮어쓰지 않습니다.

검증 범위
- 원본 ZIP 무결성, OBJ 메시/UV 인덱스, PNG 파일, FBX 구조를 검사했습니다.
- 에디터 코드의 C# 구문과 패키지의 GUID/상위 폴더/메타데이터 구성을 검사했습니다.
- 이 작업 환경에는 Unity Editor가 없어 실제 Unity Import/Play 실행은 검증하지 못했습니다.
- 수정본은 생성용 빈 폴더 항목을 포함하지 않고, 실제 파일만 Unity 기본 순서로 묶었습니다.

에셋 출처 / 라이선스
제작자: Kenney
모두 CC0 1.0. 개인/교육/상업 프로젝트에서 사용 가능한 무료 에셋입니다.
각 원본 라이선스와 출처/아카이브 해시는 Licenses 폴더에 보관했습니다.

City Kit Industrial 2.0
https://kenney.nl/assets/city-kit-industrial

Survival Kit 2.0
https://kenney.nl/assets/survival-kit

Animated Characters Survivors 1.1
https://kenney.nl/assets/animated-characters-survivors
