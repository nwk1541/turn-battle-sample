# turn-battle-sample

검병, 창병, 기병과 궁병으로 구성된 Unity 턴제 자동 전투 샘플이다. 타이틀에서 전투를 시작하고 결과를 확인한 뒤 다시 타이틀로 돌아갈 수 있다. 전투 규칙과 화면 연출을 분리하여 같은 편성과 시드로 결과를 재현하거나 연출 없이 시뮬레이션할 수 있다.

## 개발 환경

- Unity **6000.6.2f1**
- 프로젝트 경로: [Bravo](Bravo)
- Git: UniTask 패키지 복원에 필요하다.

| 외부 라이브러리 | 버전 | 용도 |
| --- | --- | --- |
| DOTween | 1.3.045 | 유닛의 이동, 공격과 피격 연출 |
| UniTask | 2.5.11 | 자산, 씬과 UI의 비동기 로드 |

DOTween은 `Assets/Plugins/Demigiant/DOTween`에 포함되어 있으며, UniTask는 Unity Package Manager의 Git 의존성으로 복원된다.

## 실행 방법

1. Unity Hub에서 저장소의 `Bravo` 폴더를 위 Unity 버전으로 연다.
2. 패키지 복원과 프로젝트 임포트가 끝날 때까지 기다린다.
3. Addressables Groups 창의 `Play Mode Script`를 `Use Asset Database (fastest)`로 선택한다.
4. [Bootstrap 씬](Bravo/Assets/Scenes/Bootstrap.unity)을 열고 Play Mode에 진입한다.
5. 타이틀의 Play 버튼으로 전투를 시작한다. 결과 화면의 Back은 전투 HUD로, Return은 타이틀로 돌아간다.

## 핵심 구조

```text
Bootstrap → TitleUI → BattleManager → BattleCaster → BattleRuler
```

| 구성 요소 | 역할 |
| --- | --- |
| [BattleManager](Bravo/Assets/Scripts/Battle/BattleManager.cs) | 전투 진입, 중단과 타이틀 복귀를 제어하고 결과와 실패를 알린다. |
| [BattleCaster](Bravo/Assets/Scripts/Battle/BattleCaster.cs) | 전투 자산을 준비하고 행동 계산, 연출 요청과 다음 행동 진행을 중개한다. |
| [BattleRuler](Bravo/Assets/Scripts/Battle/BattleRuler.cs), [DamageFormula](Bravo/Assets/Scripts/Battle/DamageFormula.cs) | 행동 순서, 대상 선택, 피해량과 승패를 계산한다. |
| [BattleField](Bravo/Assets/Scripts/Battle/BattleField.cs), [BattleHUD](Bravo/Assets/Scripts/UI/BattleHUD.cs) | 유닛 모션과 체력 표시를 담당한다. |
| [UIManager](Bravo/Assets/Scripts/UI/UIManager.cs) | UI 인스턴스의 생성, 표시 스택과 반환을 관리한다. |
| [AssetManager](Bravo/Assets/Scripts/Assets/AssetManager.cs) | Addressables 기반 자산 및 씬 로드와 인스턴스 반환을 담당한다. |

연출이 있는 전투는 `행동 계산 → 모션 재생과 타격 시 HUD 갱신 → 연출 완료 → 다음 행동 계산`을 반복한다. 자산 로드는 UniTask로 대기하고, 전투 진행은 연출 완료 콜백으로 이어진다.

모든 병종의 능력치, 공격 유형과 프리팹 주소는 [UnitConfig.asset](Bravo/Assets/Data/UnitConfig.asset)에서 설정한다. [UnitConfig](Bravo/Assets/Scripts/Battle/UnitConfig.cs)의 `Get(UnitType)`으로 병종별 설정을 조회한다.

## 주요 API

전투 API의 네임스페이스는 `Noname.Battle`이다.

| API | 설명 |
| --- | --- |
| `BattleManager.Instance.StartBattle()` | 샘플 편성의 전투 준비와 연출 포함 실행을 시작한다. |
| `BattleManager.Instance.CancelBattle()` | 준비 중인 로드에 취소를 요청하고 전투 진행과 연출을 중단한다. |
| `BattleManager.Instance.ReturnToTitle()` | 전투 자산을 정리하고 타이틀을 표시한다. |
| `BattleManager.LastResult`, `BattleCompleted`, `BattleFailed` | 마지막 결과 조회와 완료 및 실패 알림을 제공한다. |
| `BattleCaster.PrepareAsync(cancellationToken)` | 전투 씬, 설정, 유닛과 HUD를 준비하고 `BattleStartData`를 반환한다. |
| `BattleCaster.StartBattle(data, onCompleted, onFailed)` | 준비된 전투를 연출과 함께 실행한다. |
| `BattleCaster.Simulate(data)` | 준비나 연출 없이 전투를 끝까지 동기 계산하여 `BattleResult`를 반환한다. |
| `BattleRuler.ExecuteNextAction()` | 한 행동의 상태 변경을 적용하고 `DamageDesc`를 반환한다. |

입력인 `BattleStartData`는 편성과 난수 시드를 보관한다. 결과인 `BattleResult`에는 승리 진영, 무승부 여부, 종료 라운드와 최종 유닛 상태가 담긴다.

## 테스트

[Editor 테스트](Bravo/Assets/Tests/Editor)는 코드로 생성한 데이터로 피해량, 행동 순서, 대상 선택, 전투 종료 조건과 시뮬레이션 결과의 재현성을 검증한다. Unity Test Runner의 EditMode에서 실행할 수 있다.
