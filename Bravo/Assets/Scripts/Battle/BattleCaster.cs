using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using Noname.Asset;
using Noname.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Noname.Battle
{
    /// <summary>전투 자산을 보유하고 Ruler의 행동 계산과 연출 완료를 중개한다.</summary>
    public sealed class BattleCaster
    {
        private const int SAMPLE_SEED = 123;

        private readonly List<GameObject> _instances = new List<GameObject>();

        private LoadedAsset<UnitConfig> _unitConfig = null;
        private BattleField _field = null;
        private BattleHUD _hud = null;

        private BattleRuler _ruler = null;
        private IBattleUIHandle _uiHandle = null;
        private IBattleMotionHandle _motionHandle = null;

        private Action<BattleResult> _onCompleted = null;
        private Action<Exception> _onFailed = null;

        private bool _isRunning = false;

        /// <summary>Field를 보유하는 동안에는 결과 표시 중에도 true이다.</summary>
        public bool HasBattle => _field != null;

        /// <summary>씬, 유닛과 HUD를 준비하고 편성을 반환한다. 전투는 실행하지 않는다.</summary>
        public async UniTask<BattleStartData> PrepareAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LoadedAsset<GameObject> hudPrefab = null;
            try
            {
                // 서로의 결과가 필요 없는 씬, 설정과 HUD 프리팹 요청을 함께 시작한다.
                await WaitForLoadsAsync(LoadSceneAsync(), LoadConfigAsync(), LoadHUDPrefabAsync());
                cancellationToken.ThrowIfCancellationRequested();
                using (CancellationTokenSource lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _field.destroyCancellationToken))
                {
                    UnitConfig config = _unitConfig.Asset;
                    BattleStartData data = CreateSample(config);
                    UniTask[] requests = new UniTask[data.Units.Count];
                    // 완료 순서와 관계없이 편성 인덱스에 저장해 Field와 HUD의 유닛 연결을 맞춘다.
                    for (int i = 0; i < requests.Length; i++)
                    {
                        _instances.Add(null);
                        requests[i] = InstantiateUnitAsync(i);
                    }

                    // 모든 생성 결과를 기록한 뒤 취소를 확인해야 Release에서 늦게 생성된 인스턴스도 회수할 수 있다.
                    await WaitForLoadsAsync(requests);
                    lifetime.Token.ThrowIfCancellationRequested();
                    for (int i = 0; i < data.Units.Count; i++)
                    {
                        _field.RegisterUnit(data.Units[i], _instances[i]);
                    }

                    // 유닛 준비를 끝낸 뒤 HUD를 표시해 복귀 버튼이 준비 중인 전투를 조작하지 않게 한다.
                    _hud = await UIManager.Instance.PushAsync<BattleHUD>(AssetAddresses.BATTLE_HUD, lifetime.Token);
                    for (int i = 0; i < data.Units.Count; i++)
                    {
                        _hud.RegisterUnit(data.Units[i], _instances[i].transform);
                    }

                    return data;

                    async UniTask InstantiateUnitAsync(int index)
                    {
                        string address = config.Get(data.Units[index].UnitType).PrefabAddress;
                        _instances[index] = await AssetManager.InstantiateAsync(address, _field.transform);
                    }
                }
            }
            finally
            {
                // 사전 로드한 프리팹 참조만 반환한다. 표시 중인 HUD 인스턴스는 UIManager가 소유한다.
                AssetManager.ReleaseAsset(hudPrefab);
            }

            async UniTask LoadSceneAsync()
            {
                Scene scene = await AssetManager.LoadSceneAsync(AssetAddresses.BATTLE_SCENE);
                _field = FindField(scene);
            }

            async UniTask LoadConfigAsync()
            {
                _unitConfig = await AssetManager.LoadAssetAsync<UnitConfig>(AssetAddresses.UNIT_CONFIG);
            }

            async UniTask LoadHUDPrefabAsync()
            {
                hudPrefab = await AssetManager.LoadAssetAsync<GameObject>(AssetAddresses.BATTLE_HUD);
            }
        }

        /// <summary>준비된 전투를 실행하고 연출 완료마다 다음 행동을 진행한다.</summary>
        public void StartBattle(BattleStartData data, Action<BattleResult> onCompleted, Action<Exception> onFailed)
        {
            Stop();
            _uiHandle = _hud;
            _motionHandle = _field;
            _onCompleted = onCompleted;
            _onFailed = onFailed;
            try
            {
                if (data == null)
                {
                    throw new ArgumentNullException(nameof(data));
                }
                if (_motionHandle == null)
                {
                    throw new InvalidOperationException("전투가 준비되지 않았습니다.");
                }
                _ruler = new BattleRuler(data.Units, data.Seed);
            }
            catch (Exception exception)
            {
                HandleFailure(exception);
                return;
            }

            _isRunning = true;
            ExecuteNextAction();
        }

        /// <summary>별도의 Ruler로 끝까지 동기 계산한다. 준비된 자산과 진행 중인 전투 상태는 변경하지 않는다.</summary>
        public BattleResult Simulate(BattleStartData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            BattleRuler ruler = new BattleRuler(data.Units, data.Seed);
            while (!ruler.IsFinished)
            {
                ruler.ExecuteNextAction();
            }

            return new BattleResult(ruler);
        }

        /// <summary>정상 결과를 통지하지 않고 진행과 연출을 중단한다. 보유한 자산의 반환은 Release가 담당한다.</summary>
        public void Stop()
        {
            // 연출을 중단하기 전에 실행 상태와 콜백을 비워 중단 중 재진입이 다음 행동으로 이어지지 않게 한다.
            _isRunning = false;
            _ruler = null;
            _uiHandle = null;
            IBattleMotionHandle motionHandle = _motionHandle;
            _motionHandle = null;
            _onCompleted = null;
            _onFailed = null;
            motionHandle?.Stop();
        }

        /// <summary>전투 진행과 유닛 및 설정 참조를 정리한다. HUD 인스턴스는 UIManager가 반환한다.</summary>
        public void Release()
        {
            Stop();
            if (_field != null)
            {
                _field.Release();
            }
            _field = null;
            _hud = null;
            foreach (GameObject instance in _instances)
            {
                AssetManager.ReleaseInstance(instance);
            }
            _instances.Clear();
            AssetManager.ReleaseAsset(_unitConfig);
            _unitConfig = null;
        }

        /// <summary>한 행동을 계산해 연출을 요청하고 복귀 완료 콜백에서 다음 행동으로 이어간다.</summary>
        private void ExecuteNextAction()
        {
            if (!_isRunning)
            {
                return;
            }

            try
            {
                // 마지막 타격에서 승패가 정해져도 복귀 연출이 끝나 이 함수로 돌아온 뒤 결과를 통지한다.
                if (_ruler.IsFinished)
                {
                    _isRunning = false;
                    _onCompleted(new BattleResult(_ruler));
                    return;
                }

                DamageDesc damage = _ruler.ExecuteNextAction();
                // 체력과 승패는 이미 반영됐다. UI는 타격 시점에 갱신하고 다음 계산은 복귀 완료까지 기다린다.
                _motionHandle.PlayAction(damage, () => OnActionHit(damage), ExecuteNextAction, HandleFailure);
            }
            catch (Exception exception)
            {
                HandleFailure(exception);
            }
        }

        private void OnActionHit(DamageDesc damage)
        {
            if (!_isRunning)
            {
                return;
            }
            try
            {
                _uiHandle?.ShowAction(damage);
            }
            catch (Exception exception)
            {
                HandleFailure(exception);
            }
        }

        private void HandleFailure(Exception exception)
        {
            // Stop이 실패 콜백도 지우므로 먼저 보관하고, 진행을 끊은 뒤 소유자에게 알린다.
            Action<Exception> onFailed = _onFailed;
            Stop();
            onFailed?.Invoke(exception);
        }

        /// <summary>이미 시작한 요청의 완료를 모두 기다린 뒤 처음 확인한 오류를 원래 호출 스택으로 다시 전달한다.</summary>
        private static async UniTask WaitForLoadsAsync(params UniTask[] requests)
        {
            ExceptionDispatchInfo failure = null;
            foreach (UniTask request in requests)
            {
                try
                {
                    await request;
                }
                catch (Exception exception)
                {
                    failure ??= ExceptionDispatchInfo.Capture(exception);
                }
            }
            failure?.Throw();
        }

        private static BattleField FindField(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<BattleField>(out BattleField field))
                {
                    return field;
                }
            }
            throw new InvalidOperationException("전투 씬에 BattleField가 없습니다.");
        }

        private static BattleStartData CreateSample(UnitConfig config)
        {
            BattleUnitState[] units = new[]
            {
                config.Get(UnitType.Swordsman).CreateState(1, BattleSide.MySide, 1),
                config.Get(UnitType.Spearman).CreateState(2, BattleSide.MySide, 2),
                config.Get(UnitType.Cavalry).CreateState(3, BattleSide.MySide, 3),
                config.Get(UnitType.Archer).CreateState(4, BattleSide.MySide, 4),
                config.Get(UnitType.Swordsman).CreateState(5, BattleSide.Enemies, 1),
                config.Get(UnitType.Spearman).CreateState(6, BattleSide.Enemies, 2),
                config.Get(UnitType.Cavalry).CreateState(7, BattleSide.Enemies, 3),
                config.Get(UnitType.Archer).CreateState(8, BattleSide.Enemies, 4)
            };
            return new BattleStartData(units, SAMPLE_SEED);
        }
    }
}
