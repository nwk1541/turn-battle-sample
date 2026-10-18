# turn-battle-sample

Unity 턴제 전투 샘플. 프로젝트는 `Bravo` 폴더에 있다.

## 현재 구현 상태와 진입 흐름

최초 씬은 `Bravo/Assets/Scenes/Bootstrap.unity`이며, `Bootstrap.Start()`가 진입점이다. 현재 화면 흐름은 타이틀 표시와 전투 씬 교체까지 구현되어 있다. 별도로 전투용 데이터, 피해 계산과 라운드 진행을 구현했으며, Caster의 규칙 실행 연결, 유닛 생성과 배치, UI 및 모션 연동은 후속 작업이다.

```text
Bootstrap.Start()
  → AssetManager로 TitleUI 생성
  → TitleUI.Init()으로 Play 버튼 연결
  → Play 클릭
  → TitleUI.Release()로 버튼 콜백 정리 및 인스턴스 반환
  → BattleManager.StartBattle()
  → Manager 내부 StartBattleAsync()
  → BattleCaster.StartBattleAsync()
  → AssetManager.LoadSceneAsync()
  → Battle.unity 로드 및 활성화, 기존 씬 교체
```

- `Bootstrap`은 초기 UI 생성과 초기화만 담당하며, 버튼 동작이나 타이틀 종료를 관리하지 않는다.
- `TitleUI`가 `OnPlayClicked()`를 구현한다. 외부 제어 요청은 `BattleManager`를 통해 전달한다.
- UI는 특별한 이유가 없으면 Unity 이벤트 함수 대신 `Init()`과 `Release()`를 명시적으로 호출한다. `UIBase.Release()`는 자신이 등록한 콜백을 제거하고 Addressables 인스턴스를 반환한다.
- `BattleManager`는 외부에 `void StartBattle()`을 제공하고, 내부의 비공개 `UniTask` 함수에서 비동기 흐름을 처리한다.
- 현재 `BattleCaster.StartBattleAsync()`의 완료는 씬 로드 완료를 뜻하며, 전투 종료를 의미하지 않는다.

## 전투 구조

다음 표는 각 구성 요소의 책임과 현재 구현 범위를 구분한다.

| 구성 요소 | 책임 | 현재 상태 |
| --- | --- | --- |
| `BattleManager` | 아웃게임과 인게임의 경계인 싱글톤. 외부 요청 접수, 중복 시작 방지, 완료 결과와 실패 통지를 담당한다. | `BattleCaster`를 `readonly` 멤버로 소유하고 시작 요청을 전달한다. 중복 방지와 결과 및 실패 통지는 미구현이다. |
| `BattleCaster` | 전투 한 판의 준비, 진행 순서 조율, 자산과 표현 객체 정리를 담당한다. | 전투 씬 로드만 수행한다. |
| `BattleRuler` | 전투 상태, 행동 순서, 피해 적용, 턴 진행과 승패 판정을 담당한다. | 속도순 교대, 라운드별 1회 행동, 라인 순환 탐색, 공격 유형별 피격 대상 결정과 승패 및 20라운드 무승부 판정을 구현했다. 피해 계산은 `DamageFormula`에 위임한다. |
| `BattleField` | 진영별 배치 위치를 제공하고, 생성된 유닛의 표현 참조와 모션을 관리한다. | 슬롯 배열과 에디터의 `Reset()` 자동 연결만 구현되어 있다. |
| `IBattleUIHandle` | 전투 상태와 행동 결과를 UI에 표현하는 연결 규약이다. | 표현 메서드는 아직 정의하지 않았다. |
| `IBattleMotionHandle` | 접근, 공격, 피격과 복귀 모션을 요청하고 완료를 기다리는 연결 규약이다. | 메서드는 아직 정의하지 않았으며, `BattleField`가 구현할 예정이다. |

전투 내부 클래스는 `BattleManager` 싱글톤을 역으로 참조하지 않는다. 시작 같은 제어 요청과 UI 및 모션 표현 요청을 구분한다. 인터페이스는 전투를 시작하거나 규칙을 계산하는 입력 경로로 사용하지 않는다.

### 전투 한 판의 수명과 소유권

- Manager와 멤버 Caster는 유지하되, 매 판 사용할 Ruler와 전투 상태는 새로 구성하는 방향으로 설계한다.
- Caster는 자신이 준비한 유닛, 로드 자산, 맵과 표현 핸들의 참조를 관리하고 종료 또는 실패 시 정리한다. 씬을 교체하기 전에 생성한 Addressables 인스턴스를 반환한다.
- 전투 중단 시에는 진행 중인 모션과 대기를 함께 종료한 뒤 자산과 참조를 정리해야 한다. 구체적인 중단 API와 처리 순서는 후속 구현에서 정한다.
- 로드, 규칙 계산, 표현 실행은 각 담당 클래스가 수행하고, Manager는 외부 요청과 전투 상태 및 결과 전달에 집중한다.

### 로직과 표현의 연결

전투 시작 후에는 매 턴 UI 입력 없이 배치 데이터와 규칙에 따라 자동 진행하는 것을 목표로 한다.

```text
전투 데이터로 Ruler 초기화
  → Ruler가 다음 행동을 결정하고 상태를 갱신하여 행동 결과 생성
  → Caster가 결과를 UI 및 모션 핸들에 전달
  → 필요한 연출이 끝날 때까지 대기
  → 전투가 끝날 때까지 반복
  → 최종 결과를 Manager를 통해 외부에 전달
```

행동 결과는 `DamageDesc`로 전달한다. 공격자와 대상 ID, 실제 피해량, 치명타 여부, 적용 후 체력과 사망 여부를 포함한다. 피해 적용, 턴 진행과 승패 판정은 Ruler가 담당하며, UI나 모션 콜백에 의존하지 않는다. 모션 완료는 다음 행동의 표현으로 진행하기 위한 신호이다.

데이터만으로 실행할 때는 씬과 프리팹 준비부터 생략하고, 같은 Ruler의 규칙 실행을 반복한다. UI와 모션 핸들이 없으면 표현 호출과 연출 대기도 생략한다. 현재 Caster는 항상 씬을 로드하므로 이 경로는 아직 미구현이다. Ruler에는 씬, `Transform`, DOTween 의존성을 넣지 않으며, 난수를 도입할 경우 같은 초기 데이터와 시드로 같은 결과를 재현할 수 있도록 한다.

## 전투 데이터와 규칙

- `UnitConfig`는 병종, 공격 유형과 기본 능력치를 설정하는 ScriptableObject이다. `CreateStats()`로 능력치를 복사하고, `CreateState(id, side, slot)`로 공격 유형과 최대 체력을 포함하는 초기 전투 상태를 생성한다.
- `BattleUnitState`는 전투 내 고유 ID, 진영, 1~12의 슬롯 번호, 공격 유형, 능력치와 현재 체력을 보관하는 불변 값이다. Ruler는 입력을 복사하여 소유하며, `UnitConfig`나 전달받은 입력을 변경하지 않는다.
- `new BattleRuler(units, seed)`로 한 판을 생성하고 `NextUnit`으로 다음 행동자를 확인한다. 인자 없는 `ExecuteNextAction()`을 호출하면 Ruler가 공격자의 슬롯을 기준으로 라인과 피격자를 자동 선택한다. `DamageDesc.TargetId`와 체력 및 피해량은 실제 피격자 기준이다.
- 자기 라인부터 아래로 한 라인씩 확인하고, 맨 아래 다음에는 맨 위로 돌아온다. 탐색 순서는 위→가운데→아래, 가운데→아래→위, 아래→위→가운데이며, 생존한 적이 있는 첫 라인을 선택한다. 매 행동마다 현재 생존 상태로 다시 탐색한다.
- 앞뒤 배치는 두 진영 모두 `1→4→7→10`, `2→5→8→11`, `3→6→9→12`의 세 줄이다. 정면은 선택한 라인의 맨 앞 생존 유닛을 타격한다. 건너 뜀은 맨 앞 생존 유닛을 건너뛰고 두 번째 생존 유닛을 타격하며, 한 명만 살아 있으면 그 유닛을 타격하며 다른 라인으로 바꾸지 않는다. 사망 유닛과 빈 슬롯은 건너뛸 대상으로 세지 않는다.
- 양 진영에서 가장 빠른 생존 유닛이 최초 선공한다. 이후 진영을 교대하며, 각 진영에서 이번 라운드에 행동하지 않은 유닛을 속도 내림차순으로 선택한다.
- 한 진영이 먼저 행동을 마치면 다른 진영의 남은 유닛이 행동한다. 모든 생존 유닛이 행동하면 새 라운드를 시작한다. 라운드 경계에서도 직전 행동자의 반대 진영부터 진행한다.
- 속도가 같을 때 최초 선공은 아군, 같은 진영 안에서는 슬롯 번호가 작은 유닛을 우선한다. 이 동률 처리와 아래 소수점 및 피해 하한 처리는 현재 기본 정책이다.
- 체력이 0 이하이면 사망이다. 사망 유닛은 행동자와 공격 대상에서 제외한다. 한 진영의 생존 유닛이 없어지면 `IsFinished`가 참이 되고 `Winner`로 승리 진영을 제공한다. `Units`로 최종 체력을 조회하며, 처음부터 양측이 모두 사망했다면 승리 진영은 없다.
- `DamageFormula.Calculate(attacker, target, criticalRoll)`은 상태를 변경하지 않고 `DamageDesc`를 반환한다. Ruler가 판별용 난수와 피해 적용을 담당한다. 같은 실행 환경의 동일 입력과 시드는 같은 행동 순서와 피해 결과를 재현한다.

```text
치명타 = 0 이상 1 미만의 난수 < 공격자의 치명타율
산정 피해 = max(0, floor(공격력 × (치명타이면 1.5, 아니면 1) - 대상 방어력))
실제 피해 = min(대상의 현재 체력, 산정 피해)
최종 체력 = 대상의 현재 체력 - 실제 피해
```

피해 0을 허용하며, 총 20라운드까지 진행한다. 20라운드의 모든 생존 유닛이 행동한 뒤에도 양측이 살아 있으면 `IsFinished`와 `IsDraw`가 참이고 `Winner`는 null인 무승부로 끝난다. `Round`는 20을 유지하며 `NextUnit`은 null이다. 마지막 행동으로 승패가 나면 승패가 무승부보다 우선한다. Ruler는 호출마다 한 번의 행동만 처리하며, 전투 전체를 자동 반복하는 루프는 후속 작업이다.

전투 규칙 테스트는 `Bravo/Assets/Tests/Editor/BattleRulesTests.cs`에 NUnit의 `[Test]`로 작성한다. Unity의 `Window > General > Test Runner`에서 `EditMode` 탭의 `BattleRulesTests`를 실행한다. 현재는 기존 `Assembly-CSharp-Editor`의 NUnit 참조를 사용하며, 별도 .NET 테스트 프로젝트는 두지 않는다.

테스트는 피해 계산, 행동 순서와 라운드, 라인 및 피격 대상 선택, 종료 판정과 상태 독립성의 대표 사례를 검증한다. 테스트용 유닛은 밸런스 자산과 독립적으로 만들고, 체력과 피해의 관계는 입력 상태를 기준으로 확인한다. 피해 공식의 수치 예시와 20라운드 제한은 요구사항을 검증하는 고정 기대값으로 유지한다.

Unity/NUnit 참조 컴파일을 확인했으며, Test Runner에서의 탐색과 실행은 아직 검증하지 않았다. Unity Editor와 Test Runner 실행은 명시적으로 요청된 경우에 진행한다.

## 인게임 화면과 프리팹

- 전투 씬 파일은 `Bravo/Assets/Scenes/Battle.unity`이며 Addressables 주소는 `battle/scene`이다.
- 원근 카메라와 Quad 바닥을 사용하며, 왼쪽 `MySide`와 오른쪽 `Enemies`에 각각 4×3, 최대 12개의 배치 위치를 둔다. 구상도의 글자는 설명용이며 게임 화면 텍스트가 아니다.
- 각 진영의 슬롯은 `Pos - 1`부터 `Pos - 12`까지다. `BattleField`의 Inspector에서 Reset을 실행하면 같은 씬의 진영 루트를 찾아 번호순으로 배열에 연결한다. 이 연결 코드는 `UNITY_EDITOR` 범위에 있다.
- 논리적인 배치는 진영과 슬롯 번호로 표현하고, 실제 월드 좌표는 `BattleField`가 제공한다. 칸 이동과 점유 규칙이 없는 현재 단계에서는 별도의 그리드 시스템을 추가하지 않는다.
- 공통 캐릭터 프리팹은 `Bravo/Assets/Prefabs/Battle/BattleUnitCube.prefab`이다. 크기는 `(1, 1, 1)`이며, 피벗이 중심이므로 바닥 높이가 0일 때 슬롯 Y 위치는 0.5이다.
- 검병 머티리얼은 `Bravo/Assets/Materials/Battle/SwordsmanBlue.mat`이며, URP Lit 기반 파란색(`#3478DB`)이다. 병종은 공통 큐브에 적용하는 머티리얼 배색으로 구분하며 현재 검병용 파란색만 준비했다.

## 모션 연출

- 공격 연출은 상대 앞까지 접근 → 공격과 피격 표현 → 원래 위치로 복귀 순서이며, 복귀까지 끝나면 완료를 전달한다.
- `IBattleMotionHandle` 구현체 내부에서 DOTween의 Sequence로 연출을 구성할 예정이다. 인터페이스와 Caster, Ruler에는 DOTween 타입을 노출하지 않는다.
- DOTween은 현재 `Bravo/Assets/Plugins/Demigiant/DOTween`에 설치되어 있다. 실제 전투 모션은 아직 구현하지 않았다.

## 자산 로드와 비동기 처리

- `Noname.Asset.AssetManager`는 정적 클래스이며, 자산 로드와 해제, 프리팹 생성과 반환, 씬 교체를 담당한다.
- `LoadAssetAsync<T>()`는 자산과 해제에 필요한 정보를 가진 `LoadedAsset<T>`를 반환한다. 소유자는 사용이 끝나면 `ReleaseAsset()`을 호출한다.
- `InstantiateAsync()`는 GameObject를, `InstantiateAsync<T>()`는 생성한 프리팹 루트의 컴포넌트를 반환한다. 생성한 인스턴스는 직접 파괴하지 않고 `ReleaseInstance()`로 반환한다.
- `LoadSceneAsync()`는 기존 씬을 교체하고 로드 및 활성화된 Unity `Scene`을 반환한다. 성공한 씬 로드 핸들은 Addressables가 추적하며 씬 언로드 시 해제한다.
- UniTask가 도입되어 있다. AssetManager는 완료 이벤트를 `AutoResetUniTaskCompletionSource`로 기다리고 `EnsureSucceededAsync()`에서 성공 여부 확인과 실패한 핸들 해제를 처리한다.
- 주소는 `Noname.Asset.AssetAddresses`에 모은다. Addressables 그룹의 등록 주소를 변경하면 상수도 함께 변경한다.

| 상수 | Addressables 주소 |
| --- | --- |
| `AssetAddresses.TITLE_UI` | `title/ui` |
| `AssetAddresses.BATTLE_SCENE` | `battle/scene` |
| `AssetAddresses.BATTLE_UNIT_CUBE` | `battle/unit/cube` |

## 다음 구현과 결정 사항

1. 외부 전달용 최종 결과 타입을 정의한다. 라인 순환 탐색, 공격 유형별 실제 피격 대상 결정과 20라운드 무승부 처리는 구현했다.
2. 구현된 Ruler와 DamageFormula를 Caster에 연결한다. 핵심 규칙 검증은 EditMode NUnit 테스트로 구성했으며, Test Runner 실행 검증은 아직 수행하지 않았다.
3. Caster가 로드한 씬에서 BattleField를 찾고 유닛 생성, 배치와 표현 핸들을 연결한다.
4. 행동 결과에 맞춰 UI 및 모션 인터페이스를 정의하고, 연출 완료 후 다음 행동을 진행하도록 연결한다.
5. 준비 및 진행 중 중복 요청, 전투 종료와 중단, 실패 시 자산 정리 및 외부 통지를 구현한다. 내부 비동기 함수의 완료가 준비 완료인지 전투 종료인지 명확히 구분한다.

현재는 타이틀을 해제한 뒤 씬 로드를 요청하므로, 로드가 실패하면 돌아갈 UI가 없다. 실패 화면 또는 타이틀 복귀 경로는 아직 정하지 않았으며 Bootstrap으로 종료 책임을 옮기지 않는다. 자동 전투 설정이나 전투 배속 옵션은 현재 구현 범위에 포함하지 않는다.
