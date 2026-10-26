무료 모델 V5: 서 있는 캐릭터 + 1인칭 총/팔
대상: Unity 6.3 / 기존 GameScene

적용 방법
1. Play 모드를 종료합니다.
2. FreeModels_Upright_FPS_V5.unitypackage를 열고 전체 Import합니다.
   기존 V3/V4 폴더 위에 덮어쓰면 됩니다. 기존 폴더를 삭제할 필요가 없습니다.
3. 컴파일과 Import가 끝나면 기존 GameScene을 엽니다.
4. Tools > Free Models Fixed > Apply Models to Current Scene 을 실행합니다.
5. Game 탭에서 1인칭 총을 확인하고 Ctrl+S로 씬을 저장합니다.

캐릭터 수정
- 생존자는 팔을 내리고 서 있는 자세, 좀비는 팔을 앞으로 뻗고 서 있는 자세입니다.
- 원본 FBX의 골격/가중치/UV를 사용해 자세를 미리 계산한 실제 메시입니다.
- Unity 좌표계의 Y 위쪽, +Z 정면을 메시 데이터 자체에 반영했습니다.
- Unity에서 별도 FBX 애니메이션을 샘플링하지 않아 루트 회전이 다시 덮어써지지 않습니다.
- 이전에 생성된 survivor/zombie 프리팹과 씬의 __FreeModels_Visual도 갱신합니다.
- 원본 FBX와 idle/run/jump 파일도 그대로 포함되어 있습니다.
- 이번 버전의 캐릭터 외형은 정적인 모델입니다. 이동/공격 애니메이션 연동은 포함하지 않습니다.

1인칭 총
- Kenney Blaster Kit의 실제 총 모델과 기존 생존자 에셋에서 가져온 팔/손을 사용합니다.
- 기본은 초록색 라이플입니다.
- 게임용 MainCamera의 자식으로 __FreeModels_FirstPerson을 추가합니다.
- 기존 카메라의 회전과 이동을 따라 화면 오른쪽 아래에 총과 팔이 표시됩니다.
- 카메라 방향, 위치, 태그, FOV, 이동/발사 스크립트, 데미지, 탄약/재장전 스탯은 변경하지 않습니다.
- 총/팔에 충돌체를 추가하지 않아 기존 Camera.main 레이캐스트를 가리지 않습니다.
- 총이 근거리 클리핑으로 잘리지 않도록 Near Clip이 0.03보다 큰 경우에만 0.03으로 줄입니다.
- 로컬 Player의 새 몸 모델은 그림자만 표시하여 카메라에 얼굴이 겹쳐 보이지 않게 합니다.
- 전체 생존자 모델은 Prefabs/survivor.prefab에서 확인할 수 있습니다.
- Hierarchy의 MainCamera > __FreeModels_FirstPerson에서 총 전체 배치를 조정할 수 있습니다.
- 총/팔에는 발사, 반동, 재장전 애니메이션을 추가하지 않았습니다.

다른 총 외형 선택
Tools > Free Models Fixed > First Person Weapon > Rifle / SMG / Pistol
에디터에서 외형을 바꾸는 메뉴입니다. 게임 실행 중 무기 전환 기능은 추가하지 않습니다.

기존 맵과 기능
- Player / Zombie / Drum / Box 컴포넌트가 있는 씬 오브젝트에 모델 외형을 붙입니다.
- 원래 오브젝트의 스크립트, 충돌체, Rigidbody, CharacterController, 좌표, 태그를 유지합니다.
- 기존 Map 바깥의 산업 시설 배경은 중복 생성하지 않습니다.
- 기존 씬의 Canvas와 UI는 변경하지 않습니다.
- 원래 게임 스크립트 66개를 포함하거나 덮어쓰지 않습니다.
- 에디터 도구 C# 3개만 Editor 폴더에 추가합니다. 게임 빌드에는 포함되지 않습니다.

텍스처와 Import
- V4의 내장 RGBA PNG 복구 방식을 유지하고 무기 텍스처를 추가했습니다.
- 7종 텍스처 데이터를 사용해 지속되는 Texture2D .asset을 생성합니다.
- 현재 Built-in / URP / HDRP에 맞는 Lit 셰이더와 텍스처를 재질에 연결합니다.
- 기존 V3/V4 파일의 경로와 GUID를 유지합니다.
- 사용자가 올린 원본의 Unity TAR 헤더와 GZIP 형식을 사용합니다.
- 빈 폴더 전용 항목을 포함하지 않습니다.

직접 모델만 가져오기
Tools > Free Models Fixed > Build Model Prefabs Only
Assets/FreeModels_ImportFixed_20261026/Prefabs에서 사용할 수 있습니다.
서 있는 캐릭터, 총 3종, 1인칭 팔, 기존 산업 시설/소품을 포함해 프리팹 29개를 생성합니다.
Models에는 확인 가능한 OBJ/FBX, Data/BakedMeshesV5에는 자세/축을 고정한 메시 데이터가 있습니다.

되돌리기와 범위
- 적용 직후 Ctrl+Z로 씬 변경을 되돌릴 수 있습니다.
- 같은 메뉴를 다시 실행하면 이 도구가 만든 외형 자식을 교체합니다.
- 원래 스크립트가 참조하는 스폰용 프리팹 원본은 별도로 수정하지 않습니다.
  해당 프리팹이 외형 없는 원본이면 새로 생성된 캐릭터에도 외형 자식을 붙여야 합니다.
- 업로드된 원본은 스크립트만 포함했으며 GameScene과 스폰용 프리팹은 포함하지 않았습니다.
  따라서 사용자의 씬을 연 상태에서 메뉴를 실행하는 방식으로 적용합니다.

검증
- 캐릭터의 서 있는 자세와 총/팔 배치를 메시 렌더링으로 확인했습니다.
- 메시 좌표/UV/삼각형 인덱스/법선, 내장 PNG, C# 구문, 패키지 구조/GUID를 검사했습니다.
- 이 환경에는 Unity Editor가 없어 실제 Unity Import/Play 검증은 수행하지 못했습니다.

에셋 출처
제작자: Kenney / CC0 1.0
각 원본 라이선스와 다운로드 아카이브 해시는 Licenses에 있습니다.
https://kenney.nl/assets/city-kit-industrial
https://kenney.nl/assets/survival-kit
https://kenney.nl/assets/animated-characters-survivors
https://kenney.nl/assets/blaster-kit
