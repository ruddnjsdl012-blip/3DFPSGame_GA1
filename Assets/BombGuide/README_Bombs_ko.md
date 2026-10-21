# 폭탄 10개 · 우클릭 충전 · 오브젝트 풀링

보내준 `test(20261021-033743).unitypackage`를 수정한 버전이다. 기존 GameScene에 빠져 있던 폭탄 발사 컴포넌트와 프리팹 연결까지 포함했다.

## 적용한 동작

| 항목 | 동작 |
| --- | --- |
| 보유량 | 플레이를 시작할 때 10개. 성공적으로 던질 때만 1개 소모 |
| 우클릭 누르기 | 충전 시작. 누르는 순간에는 발사하지 않음 |
| 우클릭 유지 | 1.5초까지 힘 증가. 최대 충전 이후에도 버튼을 놓을 때까지 대기 |
| 우클릭 놓기 | 한 번만 투척 |
| 투척 힘 | 최소 6 → 최대 22. 같은 조준 각도에서 오래 충전할수록 더 강하게 투척 |
| 충돌 | 플레이어 몸을 제외한 물리 충돌 시 폭발 이펙트 재생 후 반납 |
| 수량 제한 | 총 10번까지. 11번째 투척 불가. 풀에 돌아와도 보유량은 증가하지 않음 |
| 풀링 대상 | 폭탄과 폭발 이펙트 각각 10개를 시작할 때 준비해 재사용 |
| HUD | 오른쪽 아래 보유량과 충전 바 표시 |
| 충전 취소 | 창 포커스 상실, 일시정지, 컴포넌트 비활성화 시 취소 |
| 비행 시간 | 12초 동안 충돌하지 않으면 이펙트 없이 풀로 반납 |

기존의 충돌 즉시 폭발 방식을 유지했다. 충전 중에는 손에서 폭발하거나 자동 발사되지 않는다. 이번 구현은 투척·충돌·이펙트까지이며 적 체력/폭발 범위 대미지는 포함하지 않는다.

## Unity에서 가져오기

1. 기존 프로젝트를 열고 재생 중이면 상단 ▶ 버튼을 눌러 멈춘다.
2. 기존 씬을 따로 보관하려면 가져오기 전에 프로젝트를 복사해 둔다. 이 패키지는 원래 경로의 GameScene, PlayerBombFire.cs, Bomb.cs를 갱신한다.
3. 상단 `에셋(Assets) → 패키지 임포트(Import Package) → 사용자 지정 패키지(Custom Package...)`를 선택한다.
4. 받은 `.unitypackage`를 선택한다.
5. 가져오기 목록에서 모두 선택한 상태로 `Import`를 누른다. 새 스크립트·프리팹·문서를 함께 가져와야 한다.
6. 컴파일이 끝나면 Project 창에서 `Assets → 01.Scenes → GameScene`을 더블클릭한다.
7. 이미 열려 있던 씬을 다시 읽을지 묻는 창이 나오면 가져온 디스크 버전을 다시 읽는다. 이전 메모리의 씬으로 덮어 저장하지 않는다.
8. 상단 ▶ 재생 → Game 창을 한 번 클릭 → 오른쪽 아래 `BOMBS 10 / 10`을 확인한다.

프리팹이나 발사 위치를 다시 드래그할 필요 없이 연결해 두었다. 외부 풀링 에셋 설치 없이 실행하는 직접 구현 버전이다. 입력은 원본 프로젝트와 동일한 구 Input API를 사용한다. 입력 오류가 발생하는 프로젝트에서는 `프로젝트 설정 → 플레이어 → 기타 설정 → 활성 입력 처리`를 `둘 다(Both)` 또는 `Input Manager (Old)`로 둔다.

## 바로 확인할 순서

1. 우클릭을 아주 짧게 누르고 놓는다. 가까이 던져지고 보유량이 9로 줄어야 한다.
2. 같은 각도로 우클릭을 1.5초 이상 누른다. 바가 가득 차도 자동 투척되지 않아야 한다.
3. 버튼을 놓는다. 짧게 눌렀을 때보다 강하게 날아가야 한다.
4. 바닥이나 벽에 맞으면 폭발 이펙트가 나오고 폭탄은 비활성화되어야 한다.
5. 총 10개를 사용해 보유량이 0이 되면 다시 우클릭해도 던져지지 않아야 한다.
6. 충전하면서 다른 창으로 전환했다가 돌아온다. 놓기만 했을 때 뜻하지 않게 발사되지 않아야 한다.
7. 다시 플레이를 시작하면 보유량 10개로 시작한다.

## 풀링을 눈으로 확인하기

플레이 중 Hierarchy의 `BombPool_Objects`를 펼친다. 준비된 `Bomb_01`~`Bomb_10`, `BombExplosion_01`~`BombExplosion_10`이 있다. 폭탄을 던질 때 기존 자식이 활성화되고, 충돌하면 비활성화된다. 폭발 이펙트도 모든 자식 파티클 재생이 끝나면 비활성화된다.

대기 목록이 `Stack`이므로 바로 반납된 객체를 우선 재사용한다. 한 발을 던지고 터질 때까지 기다렸다가 다시 던지면 같은 폭탄이 다시 사용된다. 반납 시 위치 이외에 이동 속도·각속도·수명·플레이어 충돌 무시 상태도 정리한다. 플레이 중 폭탄/폭발 이펙트 수를 늘리거나 줄이는 `Instantiate`/`Destroy`는 실행하지 않는다. 실제 삭제는 소유자/씬을 정리할 때만 한다.

보유량과 대여 가능한 객체 수는 다르다. 보유량은 게임 규칙이고 풀의 대기 수는 재사용 가능한 객체 수다. 이 둘을 같은 숫자로 취급하면 폭탄이 반납될 때마다 보유량이 회복되는 오류가 생긴다.

## Inspector 설정

Hierarchy에서 `Player`를 선택하면 다음 컴포넌트를 확인할 수 있다.

| 컴포넌트/필드 | 연결/기본값 |
| --- | --- |
| PlayerBombFire / Fire Point | Main Camera 아래의 BombFirePoint |
| PlayerBombFire / Bomb Prefab | Assets/03.Prefabs/Bomb.prefab |
| PlayerBombFire / Aim Camera | Main Camera |
| Max Charge Time | 1.5 |
| Min Throw Power | 6 |
| Max Throw Power | 22 |
| Upward Bias | 0.12, 약간 위쪽으로 던지는 보정 |
| BombPool | 대기 중/대여 중 폭탄과 이펙트를 관리 |
| PlayerBombHud | 보유량과 충전량 표시. 필요하면 체크를 해제해 숨길 수 있음 |

10개 제한은 `PlayerBombFire.MaxBombCount` 상수로 고정했다. 충전 시간과 힘은 Inspector에서 조절할 수 있다. 실제 도달 거리는 조준 각도, 벽/바닥, 중력과 Rigidbody 설정의 영향도 받는다.

## 변경 파일

| 경로 | 역할 |
| --- | --- |
| Assets/01.Scenes/GameScene.unity | Player 컴포넌트 및 BombFirePoint 연결 |
| Assets/02.Scripts/Player/PlayerBombFire.cs | 입력, 충전, 총 10개 제한, 근거리 벽 검사 |
| Assets/02.Scripts/Weapon/Bomb.cs | 투척 물리, 충돌, 상태 초기화, 반납 |
| Assets/02.Scripts/Pooling/BombPool.cs | 직접 구현한 고정 크기 풀 |
| Assets/02.Scripts/Weapon/PooledBombExplosion.cs | 자식 파티클까지 종료 확인 후 반납 |
| Assets/02.Scripts/UI/PlayerBombHud.cs | 남은 개수와 충전 바 |
| Assets/03.Prefabs/Bomb.prefab | Rigidbody·SphereCollider·Bomb 연결 |
| Assets/03.Prefabs/BombExplosion.prefab | 원본 이펙트를 기반으로 한 폭발용 파티클 |
| Assets/09.Materials/BombMaterial.mat | 폭탄 재질 |
| Assets/BombGuide/Pooling_Analysis_ko.md | 직접 구현과 Lean Pool·Pools 비교 자료 |

원래 스크립트의 .meta GUID를 유지했으며 기존 파일 중에는 GameScene, PlayerBombFire.cs, Bomb.cs만 변경했다.

## 검증 범위

변경된 런타임 C# 5개를 Unity API 테스트 대역과 함께 컴파일하고, 입력/수량/대여/반납/상태 초기화/예외 상황 18개를 실행해 통과했다. 씬과 새 프리팹의 YAML 객체 112개를 파싱해 중복 fileID, 깨진 내부 참조, 새 프리팹의 누락된 패키지 의존성이 없는지 확인했다.

이 환경에는 Unity 에디터가 없어 실제 Unity 컴파일·물리 시뮬레이션·화면 렌더링·에셋 임포트는 실행하지 못했다. 위의 재생 확인 순서로 실제 프로젝트에서 마무리 확인해야 한다. 원래 프로젝트의 URP/UI/Input System 등 패키지 설정은 원본 프로젝트에서 제공되어야 한다.
