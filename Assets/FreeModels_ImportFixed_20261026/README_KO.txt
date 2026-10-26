무료 모델 V6: 총구화염 · 좀비 피 · 미니맵 +/- 줌
대상: Unity 6.3 / 기존 GameScene / V5 위에 덮어쓰기

적용 방법
1. Play를 종료합니다.
2. FreeModels_Combat_Minimap_V6.unitypackage를 전체 Import합니다.
   Assets/02.Scripts의 수정 파일 4개도 포함해서 Import해야 합니다.
3. 컴파일이 끝나면 기존 GameScene을 엽니다.
4. Tools > Free Models Fixed > Apply Combat FX and Minimap 을 실행합니다.
5. Ctrl+S로 씬을 저장하고 Game 탭에서 Play합니다.
   V5 총 모델이 아직 없는 씬에서는 이 메뉴가 모델 적용까지 진행합니다.

조작 확인
- 왼쪽 마우스 연사: 실제 발사 간격마다 총구화염 + 작은 불꽃/연기 + 짧은 따뜻한 조명.
- 좀비 피격: 실제 대미지를 받은 위치/표면 방향으로 붉은 피가 튑니다.
- 좀비를 죽이는 마지막 타격에도 피가 나오며, 좀비 이동/삭제와 독립적으로 사라집니다.
- 기존 CursorManager 조작은 왼쪽 Alt = 커서 표시, 오른쪽 Alt = 커서 잠금입니다.
- 왼쪽 Alt로 커서를 보이게 한 뒤 미니맵 + / - 버튼을 클릭합니다.
- +는 확대(orthographicSize 감소), -는 축소(orthographicSize 증가)입니다.
- 기본 줌 범위는 2~40, 클릭당 2이며 범위 끝에서는 해당 버튼이 비활성화됩니다.
- 기존 키보드 0 = 확대, 9 = 축소도 같은 함수를 사용합니다.
- 일시 정지 중에도 미니맵 버튼을 사용할 수 있습니다.

자동 연결
- 기존 1인칭 총의 메시 끝에서 총구 위치를 계산해 __FreeModels_MuzzleFX를 붙입니다.
- PlayerGunFire의 실제 Shoot 호출에만 효과를 연결합니다. 별도 입력 감지로 중복 발사하지 않습니다.
- 모든 씬 Zombie의 Blood Spray Prefab에 생성한 BloodHit.prefab을 연결합니다.
- 미니맵 Camera/RawImage/UI_Minimap과 기존 +/- 버튼을 찾아 연결합니다.
- TMP 또는 기존 Text의 +, ＋, -, − 표기와 ZoomIn/ZoomOut/Plus/Minus 이름을 인식합니다.
- 버튼이 없을 때만 미니맵 아래에 작은 +/- 버튼을 만듭니다.
- 필요한 Canvas GraphicRaycaster와 EventSystem이 없는 경우 추가합니다.
- 다른 버튼 이벤트를 지우지 않으며, 다시 활성화해도 자체 이벤트가 중복되지 않게 처리합니다.
- 같은 메뉴를 반복 실행하면 이 도구가 만든 총구 이펙트를 교체합니다.
- Inspector에서 직렬화된 연결이 실제로 저장됐는지도 적용 중 확인합니다.

이펙트
- 노란색/주황색 총구화염은 짧은 스타 모양 빌보드와 소량의 불꽃/연기로 구성됩니다.
- 좀비 피는 붉은 안개와 작은 피 방울 파티클입니다. 바닥에 영구 데칼을 남기지 않습니다.
- 피는 World 공간에서 움직이며 약 1.3초 후 자동 삭제됩니다.
- 파티클과 정리 시간에 unscaled time을 사용하여 승리 화면이 timeScale=0으로 멈춰도 끝납니다.
- 총구의 조명도 unscaled time으로 약 0.045초 후 꺼집니다.
- VFX에는 추가 충돌체나 데미지 처리가 없습니다.
- URP에서는 URP/Particles/Unlit, Built-in에서는 Particles/Standard Unlit 재질을 생성합니다.
- 효과용 알파 텍스처 2개는 수학적 마스크로 생성했으며 코드에도 내장했습니다.
- 기존 7종 모델 텍스처 복구와 서 있는 캐릭터/총/팔 모델은 유지합니다.

수정한 기존 파일 (원래 경로/GUID 유지)
Assets/02.Scripts/Player/PlayerGunFire.cs
  실제 발사 시 총구화염을 호출하고, 좀비 피와 일반 표면 피격 효과가 중첩되지 않게 분기합니다.
  기존 Hit Effect Prefab이 비어 있어도 대미지 처리가 예외로 중단되지 않게 했습니다.
Assets/02.Scripts/Monster/Zombie.cs
  기존 피 효과 호출을 생존/사망 분기 앞쪽으로 옮기고 World 좌표로 생성합니다.
Assets/02.Scripts/Camera/MinimapCamera.cs
  기존 Target/Height/추적 동작을 유지하고 제한된 ZoomIn/ZoomOut 함수를 추가했습니다.
Assets/02.Scripts/UI/UI_Minimap.cs
  기존에 비어 있던 미니맵 버튼 연결/클릭 처리를 구현했습니다.

유지한 내용
- Player 이동, 카메라 회전, 기존 조준 Raycast, 공격력/사거리/연사 간격/탄약/재장전 스탯.
- Zombie 추격/공격/Hit/Death 상태와 GameManager의 처치/승리 알림.
- CursorManager, Canvas의 기존 배치, 다른 HUD/버튼 기능.
- 기존 V5의 에셋 경로/GUID 52개와 원래 모델/메시/텍스처 데이터.
- 실제 탄환 오브젝트나 새 데미지 시스템은 추가하지 않습니다.

기존 모델 메뉴
Tools > Free Models Fixed > Apply Models to Current Scene
  서 있는 캐릭터/총/배경과 함께 V6의 이펙트/미니맵도 적용합니다.
Tools > Free Models Fixed > First Person Weapon > Rifle / SMG / Pistol
  총 외형을 바꾸고 새 총에 총구화염도 다시 연결합니다.
Tools > Free Models Fixed > Build Model Prefabs Only
  V5의 29개 모델 프리팹만 생성합니다.

되돌리기
- 적용 직후 Ctrl+Z로 씬 연결/추가 오브젝트를 되돌릴 수 있습니다.
- 업로드 원본은 스크립트 66개만 포함했고 씬/기존 프리팹은 포함하지 않았으므로,
  사용자가 열고 있는 씬에 메뉴로 연결하는 방식입니다.
- 스폰용 Zombie 프리팹 원본은 자동 수정하지 않습니다. 현재 프로젝트는 씬의 좀비를 사용합니다.

검증 범위
- 코드 구문, GUID/압축 형식, 기존 모델 보존, 텍스처 데이터와 이벤트 호출/연결 구조를 확인했습니다.
- 이 환경에는 Unity Editor가 없어 실제 Unity Import/Play 검증은 수행하지 못했습니다.

모델 에셋 출처: Kenney / CC0
https://kenney.nl/assets/city-kit-industrial
https://kenney.nl/assets/survival-kit
https://kenney.nl/assets/animated-characters-survivors
https://kenney.nl/assets/blaster-kit
원본 라이선스와 다운로드 해시는 Licenses 폴더에 있습니다.
