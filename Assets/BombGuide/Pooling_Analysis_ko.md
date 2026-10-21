# 오브젝트 풀링 구현 원리와 비교

## 이번 프로젝트의 직접 구현

매번 `Instantiate`로 만들고 `Destroy`로 삭제하는 대신, 시작 시 폭탄과 폭발 이펙트를 각각 10개 만든 뒤 비활성화해 보관한다. 필요할 때 꺼내 활성화하고, 사용이 끝나면 상태를 초기화하고 다시 보관한다.

핵심 자료구조는 `Stack<T>`와 `HashSet<T>`다.

| 자료구조/동작 | 이번 코드에서 하는 일 |
| --- | --- |
| Stack<Bomb> | 비활성 폭탄 보관. Pop으로 대여하고 Push로 반납 |
| Stack<PooledBombExplosion> | 비활성 폭발 이펙트 보관 |
| HashSet<Bomb> | 대여 중인 폭탄 추적. 이중 반납/다른 풀 반납 차단 |
| HashSet<PooledBombExplosion> | 재생 중인 이펙트 추적 |
| Initialize | 시작 시에만 생성해 준비 |
| TrySpawn | 남은 객체가 있으면 활성화. 없으면 실패를 반환 |
| Return | 속도/각속도/수명/소유자 충돌 상태를 정리하고 비활성화 |

`HashSet.Remove`가 true인 경우에만 대기 목록에 넣는다. 같은 폭탄을 두 번 반납해도 스택에 중복 등록되지 않는다. 여러 콜라이더의 충돌 메시지를 받더라도 이미 반납된 폭탄은 다시 폭발시키지 않는다.

폭탄의 대여 공간은 플레이어 Transform의 자식이 아닌 월드 루트에 둔다. 플레이어가 돌아도 이미 던진 폭탄이 같이 돌아가지 않는다. 폭발 이펙트는 루트뿐 아니라 자식 연기/파편의 `IsAlive(true)`까지 확인하고 반납한다.

풀 크기 10과 총 탄약 10은 별도 규칙이다. 풀은 재사용을 위한 객체 보관소이며 탄약을 보충하지 않는다. 이 구현의 초기화 이후에는 폭탄/폭발 이펙트의 새 인스턴스를 만들지 않는다. 실제 성능 향상 정도는 Unity Profiler 측정이 필요하며, 폭탄 10개만으로 눈에 띄는 FPS 개선을 보장할 수는 없다.

## Lean Pool — 질문의 LearnPool로 추정되는 에셋

정식 제품명은 **Lean Pool**이다. 여기서는 개발자 공식 문서에 공개된 구조와 API를 분석했다. 설치된 배포본 전체 소스를 줄 단위로 확인했다는 의미는 아니다.

공식 문서에 따르면 `LeanPool` 정적 API는 복제 객체와 소유 풀의 관계를 Dictionary에 보관한다. `Spawn`/`Despawn` 호출을 해당 `LeanGameObjectPool`로 전달하는 구조다. 풀 컴포넌트를 직접 참조하는 방식도 있으며 두 호출 방식을 섞지 않아야 한다. 준비 수와 최대 수는 풀 설정으로 조절한다.

활성화/비활성화 시점의 초기화는 `OnEnable`/`OnDisable`, `OnSpawn`/`OnDespawn`, 또는 `IPoolable` 알림으로 작성할 수 있다. Rigidbody 속도는 비활성화만으로 초기화되지 않아 별도 정리가 필요하다. 공식 문서에서 제공하는 Rigidbody용 초기화 컴포넌트도 이 문제를 해결한다. 우리 코드는 `Bomb.Deactivate`에서 직접 초기화한다.

에셋을 활용하는 경우의 기본 API는 `Lean.Pool.LeanPool.Spawn(...)`와 `Lean.Pool.LeanPool.Despawn(...)`다. 이 패키지의 직접 구현과 에셋에 같은 폭탄을 동시에 맡기면 소유권이 꼬이므로, 실제 전환 시 BombPool의 대여·반납을 하나의 에셋 백엔드로 통일해야 한다. 폭탄 수량 제한과 충전 계산은 PlayerBombFire 쪽 게임 규칙으로 유지한다.

출처: [Lean Pool 개발자 공식 문서](https://carloswilkes.com/Documentation/LeanPool)

## Pools — 정확한 제품 확인이 필요한 부분

질문에는 무료 풀링 에셋 검색 링크만 있고 `Pools` 제품의 제작자/개별 링크/소스가 없다. 첨부 패키지에도 해당 에셋은 포함되어 있지 않다. 따라서 특정 Asset Store 제품의 내부 자료구조나 Unity 6 호환성을 확인했다고 단정할 수 없다.

아래는 이름이 확인되는 **BBUnity Pools** 개발자 공개 저장소의 README에 근거한 비교 사례다. 수업에서 지칭한 Pools와 동일한 제품인지 확인된 것은 아니다.

BBUnity Pools는 풀 컨테이너 아래에 여러 `Pool Definition`을 둔다. 정의에는 이름, 프리팹, 시작 수, 최대 수와 비활성 인스턴스 재사용 설정이 들어간다. `StaticPool`은 요청에 따라 수동으로 스폰하고 `TimedPool`은 시간에 따라 스폰한다. 각 객체의 `PoolBehaviour`와 스폰 전후 콜백으로 개별 동작을 연결하는 형태다.

이번 폭탄 기능과 연결하면 폭탄 프리팹 정의 하나를 만들고, 버튼을 놓을 때 수동 대여하며, 충돌/시간 만료 시 돌려주는 방식으로 적용할 수 있다. 이 설명은 공개 README가 확인해 주는 기능 구조에 한정하며 내부가 Stack인지 Queue인지까지 확인한 설명은 아니다.

출처: [BBUnity Pools 개발자 저장소](https://github.com/bigbearstudios-unity/pools)

지정된 Pools 제품 링크나 해당 패키지의 스크립트가 있으면 초기 생성, 대여, 반납, 초기화, 최대 수 처리와 중복 반납 방어 부분을 실제 소스 기준으로 대조하면 된다.

## Unity 내장 UnityEngine.Pool과의 구분

`Pools`라는 외부 제품과 Unity 내장 `UnityEngine.Pool`은 구분해야 한다. Unity 공식 C# 참조 소스의 `ObjectPool<T>`는 대기 객체를 리스트 끝에서 꺼내 재사용하며, 최근 반납 객체를 따로 보관하는 경로도 있다. 객체 생성/대여/반납/삭제 콜백을 받는다.

중요한 차이는 `maxSize`가 대기 보관 한도라는 점이다. 풀이 비면 `Get`에서 생성 함수를 호출하므로 `maxSize = 10`만으로 총 투척 횟수 10번이나 동시 활성 객체 10개가 제한되지는 않는다. `defaultCapacity = 10`도 컨테이너 초기 용량이며 10개를 실제로 미리 생성하는 코드가 아니다. 이번 직접 구현은 시작 시 실제로 10개를 준비하고, 없으면 대여 실패를 반환한다.

출처: [Unity 공식 ObjectPool<T> 참조 소스](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/ObjectPool/ObjectPools.cs), [Unity 6.3 풀링 매뉴얼](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/performance-memory/managed-memory/optimizing-code-managed-memory/reusable-code)

## 비교

| 기준 | 이번 직접 구현 | Lean Pool | BBUnity Pools 공개 설명 | Unity 내장 ObjectPool<T> |
| --- | --- | --- | --- | --- |
| 외부 설치 | 필요 없음 | 에셋 설치 필요 | 라이브러리 설치 필요 | 엔진 제공 |
| 준비 방식 | 시작 시 10개 실제 생성 | Preload 설정 | Starting Size | 직접 사전 대여 등 구현 |
| 대여/반납 | TrySpawn / Return | Spawn / Despawn | Pool Definition 기반 | Get / Release |
| 총 투척 10회 제한 | PlayerBombFire에서 처리 | 별도 게임 규칙 필요 | 별도 게임 규칙 필요 | 별도 게임 규칙 필요 |
| 재사용 시 물리 초기화 | Bomb에서 직접 처리 | 알림/전용 컴포넌트 등 | 사용 객체에서 설계 필요 | 콜백에서 처리 |
| 이 문서의 근거 | 첨부한 실제 코드 | 개발자 문서 | 개발자 README | 공식 참조 소스 |

이번 패키지에는 첫 번째 직접 구현만 적용했다. Lean Pool·BBUnity Pools는 구조 비교 자료이며 외부 에셋 바이너리를 포함하거나 실제 에셋 전환을 완료한 버전은 아니다.
