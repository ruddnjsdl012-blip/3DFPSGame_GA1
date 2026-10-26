무료 모델 V7: 1인칭 탄창 교체 재장전 모션
Unity 6.3 / 기존 V6 위에 전체 Import

적용 방법
1. Play를 종료합니다.
2. FreeModels_Reload_V7.unitypackage를 전체 Import합니다.
   Assets/02.Scripts/Player/PlayerGunFire.cs도 포함해야 합니다.
3. 컴파일이 끝나면 기존 GameScene을 엽니다.
4. Tools > Free Models Fixed > Apply Reload Motion 을 누릅니다.
5. Ctrl+S로 씬을 저장하고 Game에서 Play한 뒤 R키를 누릅니다.
   1인칭 모델이 없는 씬에서는 기존 모델/총구화염/피/미니맵 적용도 진행합니다.

추가한 모션
- 총을 안쪽으로 기울입니다.
- 왼손으로 탄창을 잡아 아래로 빼냅니다.
- 화면 아래에서 새 탄창을 가져와 다시 끼웁니다.
- 왼손을 리시버 쪽으로 옮기는 짧은 동작 후 조준 자세로 돌아옵니다.
- 오른손은 총과 함께 움직여 방아쇠 쪽을 계속 잡고 있습니다.
- 라이플, SMG, 권총 외형을 모두 지원합니다.
- R키 재장전이 실제로 시작될 때만 모션을 재생합니다.

탄약/시간 동기화
- 기존 PlayerGunFire의 Reloading Time 값을 사용합니다. 기본값은 3초입니다.
- 재장전 시간 전체를 마친 뒤에만 기존 Max Bullet Count로 탄약을 채웁니다.
- 재장전 중 발사하면 기존 동작대로 재장전을 취소합니다.
- 발사 직전에 총/팔/탄창을 조준 자세로 돌려놓아 총구화염 위치가 어긋나지 않게 했습니다.
- 취소된 재장전으로 탄약이 보충되지는 않습니다.
- 기존 연사 간격/사거리/공격력/탄약 수/재장전 시간 설정은 유지합니다.
- 이미 재장전 중이면 R을 다시 눌러도 중복 시작하지 않습니다.
- Time.deltaTime을 사용하므로 일시 정지하면 모션과 재장전 시간이 함께 멈춥니다.
- 비활성화/취소/완료 시 원래 로컬 위치/회전/크기와 탄창 표시 상태를 복원합니다.
- 재장전 완료/취소를 반복해도 위치 오프셋이 누적되지 않게 기준 자세에서 계산합니다.

모델 구성
- 기존 Kenney 무기와 팔의 텍스처/메시를 사용합니다.
- 라이플/SMG의 실제 탄창 메시를 본체에서 분리해 독립적으로 움직입니다.
- 권총은 기존 그립 안에 들어가는 내부 탄창 메시를 기존 그립 형상에서 파생했습니다.
- 팔은 원본 생존자 에셋의 좌/우 팔로 나눴고, 시작 자세는 기존 V6와 같습니다.
- 새 모션은 정적 모델 부품을 움직이는 절차적 애니메이션입니다.
- 별도 Animator Controller, 유료 애니메이션, 외부 플러그인은 필요하지 않습니다.
- 손가락 뼈 애니메이션이나 재장전 효과음은 포함하지 않습니다.
- 실제 탄창/왼팔/오른팔은 Hierarchy의 __FreeModels_Magazine / __FreeModels_ReloadLeft / __FreeModels_ReloadRight에서 확인할 수 있습니다.
- 기존 합쳐진 팔 모델은 보관하되 해당 MeshRenderer를 숨깁니다.
- MainCamera > __FreeModels_FirstPerson의 FreeModelsReloadMotion에서 총 기울기/이동량을 조정할 수 있습니다.

기존 V6 기능
- 실제 발사 시점의 총구화염, 소량의 불꽃/연기, 짧은 조명.
- 좀비 피격 지점의 피 분사와 치명타 피 효과.
- 기존 미니맵 +/− 버튼 확대/축소, 범위 제한 2~40과 버튼 비활성화.
- 왼쪽 Alt로 커서 표시 후 버튼 클릭, 오른쪽 Alt로 FPS 조작 복귀.
- 기존 좀비 상태 전이/승리 알림, 이동과 카메라 조작은 유지합니다.

기존 메뉴와 재적용
Tools > Free Models Fixed > Apply Combat FX and Minimap
  V6 기능과 함께 재장전 모션도 연결합니다.
Tools > Free Models Fixed > Apply Models to Current Scene
  서 있는 캐릭터/총/배경 및 V6/V7 기능을 적용합니다.
Tools > Free Models Fixed > First Person Weapon > Rifle / SMG / Pistol
  외형을 바꾼 새 총에 총구화염과 재장전 모션을 다시 연결합니다.
Tools > Free Models Fixed > Apply Reload Motion
  기존 배치를 유지하며 재장전 부품/연결만 갱신합니다.

원본/호환성
- 기존 V6 파일 61개의 경로와 GUID를 모두 유지합니다.
- V6의 원래 게임 스크립트 4개 중 PlayerGunFire에만 재장전 모션 호출/동기화를 추가했습니다.
- Zombie, MinimapCamera, UI_Minimap의 V6 동작과 소스는 그대로입니다.
- 기존 V5/V6 원본 OBJ/FBX/PNG와 모델 카탈로그/기본 메시 데이터도 유지합니다.
- 추가 충돌체, 새 대미지 처리, 카메라 이동/회전 코드는 넣지 않습니다.
- 현재 씬에서 적용하며, 원래 스폰용 프리팹 에셋은 자동 수정하지 않습니다.
- 적용 직후 Ctrl+Z로 씬 변경을 되돌릴 수 있습니다.

검증
- 탄창/총 본체 분리, 양손 분리, 메시/법선/UV/삼각형 인덱스를 확인했습니다.
- 조준 자세 → 탄창 제거 → 재삽입 → 복귀를 메시 렌더링으로 확인했습니다.
- 코드 구문, 재장전 완료/취소/일시 정지 동기화 구조, GUID와 압축 형식을 검사했습니다.
- Unity Editor가 없는 환경이므로 실제 Unity Import/Play 검증은 수행하지 못했습니다.

에셋 출처 / CC0
Kenney City Kit Industrial / Survival Kit / Animated Characters Survivors / Blaster Kit
원본 라이선스와 다운로드 해시는 Licenses 폴더에 있습니다.
V6 이펙트용 마스크는 절차적으로 생성했습니다.
