# 시점 회전 수정

## 적용

Lesson11_Complete_Import 적용 후 사용하는 추가 패치입니다.

1. Play 모드를 끕니다.
2. CameraLook_Fix_Import.unitypackage를 전체 Import합니다.
3. 컴파일이 끝나면 Assets/01.Scenes/GameScene.unity를 다시 엽니다. 외부 변경을 묻는 창은 Reload를 선택합니다.
4. Play → 준비/시작 연출 뒤 마우스를 좌우·상하로 움직입니다.

## 수정

- CameraRotate가 CameraPosition과 Main Camera에서 동시에 월드 회전을 덮어쓰던 중복을 제거했습니다.
- PlayerRotate는 플레이어 로컬 Y축으로 좌우 회전, CameraRotate는 CameraPosition 로컬 X축으로 상하 회전을 맡습니다.
- Main Camera는 부모로부터 회전/이동을 상속합니다. 별도 CameraFollow 컴포넌트를 제거했습니다.
- 회전은 이동·발사보다 먼저 처리합니다. 마우스 이동량에 deltaTime을 다시 곱하지 않습니다.
- CameraPosition에 기존 카메라 오프셋을 합치고 Main Camera의 로컬 위치를 0으로 옮겨, 초기 시점 위치는 유지하면서 상하 회전 중 카메라 위치가 궤도를 그리지 않게 했습니다.
- 좌우·상하 감도는 기존 Main Camera의 값 3을 기준으로 설정했습니다. 좌우는 Player → PlayerRotate의 Rotation Speed, 상하는 CameraPosition → CameraRotate의 Rotation Speed입니다.

변경은 GameScene, PlayerRotate.cs, CameraRotate.cs 세 파일과 회전 실행 순서 메타데이터입니다. 11장의 상태 제어, 일시 정지, 승리, 재시작은 유지합니다.

## 검증

원래 회전 코드가 실행 순서에 따라 서로 다른 최종 시점을 만드는 현상을 계층/쿼터니언 모델에서 재현했습니다. 수정 코드의 축 분리, 실행 순서 독립성, 30/60/144 Hz의 동일 총 입력 회전, 상하 제한, 정지 상태 입력 차단을 검사했습니다. 씬 참조와 시작 카메라 위치도 검사했습니다.

실제 Unity Editor Import/Play/Profiler는 실행할 수 없었습니다. 사용자 환경의 FPS나 마우스 폴링까지 해결되었다고 단정하지 않습니다. 상세 결과는 VALIDATION.json에 있습니다.

## 복구

동봉한 CameraLook_BeforeFix.unitypackage.bytes를 프로젝트 밖에 복사하고 마지막 .bytes를 제거한 뒤 Import하면 11장 적용 직후의 세 파일과 메타데이터로 되돌아갑니다.

## API 참고

- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Input.GetAxis.html
- https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/transform/localrotation
- https://docs.unity3d.com/kr/current/Manual/script-execution-order.html
