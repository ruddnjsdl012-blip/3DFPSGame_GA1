Flooded Grounds 작은 맵 정보 추출 도구 v3 (CS0104 수정)

큰 맵 폴더 전체를 업로드할 필요가 없습니다.
모델/텍스처는 PC의 기존 프로젝트에 둔 채, 아래 도구로 만든 ZIP만 채팅에 첨부하세요.

사용 방법
1. Unity의 Play를 끄고 열려 있는 씬을 저장(Ctrl+S)합니다.
2. 이 unitypackage를 전체 Import하고 컴파일을 기다립니다.
3. Tools > L4D 맵 > 새 맵 정보 추출을 누릅니다.
4. 창에 선택된 씬이 실제 Flooded Grounds 맵 데모 씬인지 확인합니다.
   폴더에서 가장 큰 씬을 자동 선택합니다. 다른 씬이면 SceneAsset을 해당 칸에 드래그합니다.
5. 기본값인 '이동 경로 분석 포함'을 유지하고 '작은 정보 ZIP 만들기'를 누릅니다.
6. 바탕화면 등 Assets 폴더 밖에 저장합니다. 분석이 끝나면 저장 폴더가 열립니다.
7. 만들어진 FloodedGrounds_MapInfo.zip만 채팅에 첨부합니다.

경로 분석은 네 Unity에서 실제 충돌체를 기준으로 수행하므로 큰 맵에서는 잠시 걸립니다.
경로 분석이 실패해도 배치/지형/재질 정보는 ZIP에 포함되며 경고가 기록됩니다.
분석이 너무 오래 걸릴 때는 경로 분석 체크를 끄고 정보만 먼저 추출할 수 있습니다.

씬을 못 찾는 경우
Project의 Assets/Flooded_Grounds 안에 맵의 .unity 씬이 있어야 합니다.
Scripts와 PostProcessing만 있다면 Package Manager > My Assets > Flooded Grounds에서
Scenes, Models, Materials, Textures를 포함해 로컬 프로젝트에 Import하세요.
이 대용량 파일들은 PC에만 있으면 되며, 채팅에 올릴 필요는 없습니다.

포함되는 데이터
- 맵 씬 원본과 .meta, 에셋 파일 경로/GUID 목록
  YAML 씬은 environment.unity.txt, Binary 씬은 environment.unity.bytes로 저장합니다.
  배치/지형/경로는 Unity가 직접 읽으므로 Binary 씬도 별도 형식 변환 없이 추출합니다.
- 모델·충돌체의 월드 위치/범위, 재질과 셰이더 이름
- 지형의 129x129 높이 샘플
- 실제 이동 경로 분석 결과(체크한 경우)
FBX/PNG/TGA/텍스처 원본 바이트는 ZIP에 넣지 않습니다.

작업 범위
이 도구는 정보 추출 단계입니다. 맵 교체와 구형 맵 삭제는 실행하지 않습니다.
원본 맵 씬/에셋/프로젝트 설정은 저장하지 않으며, 분석 후 원래 씬 구성을 다시 엽니다.
저장되지 않은 씬이 열려 있으면 추출을 시작하지 않아 작업 유실을 막습니다.

함께 포함된 수정
L4DMapInfoExporter.cs의 CompressionLevel에 System.IO.Compression 타입 별칭을 지정해
UnityEngine.CompressionLevel과의 CS0104 이름 충돌을 해결합니다.
기존 파일을 같은 GUID로 갱신하므로 이전 추출기 파일을 직접 지울 필요가 없습니다.
업로드된 MinDrawer.cs의 잘못 입력된 문자와 MinAttribute 이름 충돌을 수정합니다.
해당 파일의 기존 GUID/.meta는 그대로 사용합니다.

검증 범위
패키지 구성과 C# 구문/정보 추출 구조를 검사합니다.
이 제작 환경에는 Unity Editor가 없어 네 프로젝트에서의 실제 실행/경로 분석은 수행하지 못했습니다.
