# turn-battle-sample

Unity 턴제 전투 샘플. 프로젝트는 `Bravo` 폴더에 있다.

## 현재 구현 상태와 진입 흐름

최초 씬은 `Bravo/Assets/Scenes/Bootstrap.unity`이며, `Bootstrap.Start()`가 진입점이다. 현재는 타이틀 표시와 전투 씬 교체까지 구현되어 있다. 전투 데이터, 유닛 생성과 배치, 규칙 실행, UI 및 모션 연동은 후속 작업이다.

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
| `BattleRuler` | 전투 상태, 행동 선택, 피해 계산, 턴 진행과 승패 판정을 담당한다. | 클래스 골격만 있다. |
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

행동 결과에는 공격자와 대상 식별자, 피해량, 변경된 체력과 사망 여부 등 표현에 필요한 정보를 담는다. 구체적인 데이터 타입은 아직 정의하지 않았다. 피해 적용, 턴 진행과 승패 판정은 Ruler가 담당하며, UI나 모션 콜백에 의존하지 않는다. 모션 완료는 다음 행동의 표현으로 진행하기 위한 신호이다.

데이터만으로 실행할 때는 씬과 프리팹 준비부터 생략하고, 같은 Ruler의 규칙 실행을 반복한다. UI와 모션 핸들이 없으면 표현 호출과 연출 대기도 생략한다. 현재 Caster는 항상 씬을 로드하므로 이 경로는 아직 미구현이다. Ruler에는 씬, `Transform`, DOTween 의존성을 넣지 않으며, 난수를 도입할 경우 같은 초기 데이터와 시드로 같은 결과를 재현할 수 있도록 한다.

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

1. 전투 입력 데이터, 유닛 및 슬롯 식별 방식, 행동 결과와 최종 결과 타입을 정의한다.
2. Ruler의 최소 1대1 전투를 구현하고, 표현 없이 규칙만으로 승패를 검증한다.
3. Caster가 로드한 씬에서 BattleField를 찾고 유닛 생성, 배치와 표현 핸들을 연결한다.
4. 행동 결과에 맞춰 UI 및 모션 인터페이스를 정의하고, 연출 완료 후 다음 행동을 진행하도록 연결한다.
5. 준비 및 진행 중 중복 요청, 전투 종료와 중단, 실패 시 자산 정리 및 외부 통지를 구현한다. 내부 비동기 함수의 완료가 준비 완료인지 전투 종료인지 명확히 구분한다.

현재는 타이틀을 해제한 뒤 씬 로드를 요청하므로, 로드가 실패하면 돌아갈 UI가 없다. 실패 화면 또는 타이틀 복귀 경로는 아직 정하지 않았으며 Bootstrap으로 종료 책임을 옮기지 않는다. 자동 전투 설정이나 전투 배속 옵션은 현재 구현 범위에 포함하지 않는다.
