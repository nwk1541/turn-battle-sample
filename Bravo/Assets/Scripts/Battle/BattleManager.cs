using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Noname.Asset;
using Noname.UI;
using UnityEngine;

namespace Noname.Battle
{
    /// <summary>전투 진입과 화면 전환을 조정하며 로드 취소와 전투 진행 중단을 분리해 관리한다.</summary>
    public sealed class BattleManager : Singleton<BattleManager>
    {
        private readonly BattleCaster _battleCaster = new BattleCaster();

        private CancellationTokenSource _loadCancellation = null;

        private bool _isReturning = false;

        public BattleResult LastResult { get; private set; } = null;

        public bool IsBusy => _loadCancellation != null || _isReturning || _battleCaster.HasBattle;

        public event Action<BattleResult> BattleCompleted = null;
        public event Action<Exception> BattleFailed = null;

        public BattleManager()
        {
        }

        /// <summary>전투 준비를 시작한다. 정상 완료는 결과 UI를 표시한 뒤 BattleCompleted로 통지한다.</summary>
        public void StartBattle()
        {
            if (IsBusy)
            {
                return;
            }

            LastResult = null;
            _loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(Application.exitCancellationToken);
            PrepareBattleAsync(_loadCancellation).Forget();
        }

        /// <summary>진행은 즉시 중단한다. 로드 중이면 해당 비동기 경로가 완료 후 취소를 확인하고 자산을 정리한다.</summary>
        public void CancelBattle()
        {
            _loadCancellation?.Cancel();
            _battleCaster.Stop();
            if (_loadCancellation == null)
            {
                ReleaseBattle();
            }
        }

        public void ReturnToTitle()
        {
            if (_isReturning)
            {
                return;
            }

            _isReturning = true;
            CancelBattle();
            ReturnToTitleAsync().Forget();
        }

        private async UniTask PrepareBattleAsync(CancellationTokenSource cancellation)
        {
            BattleStartData data = null;
            try
            {
                UIManager.Instance.ClearHistory();
                data = await _battleCaster.PrepareAsync(cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                ReleaseBattle();
            }
            catch (Exception exception)
            {
                OnBattleFailed(exception);
            }
            finally
            {
                _loadCancellation = null;
                cancellation.Dispose();
            }

            // 준비용 토큰을 정리한 뒤 연출 진행을 시작해 로드와 전투 진행의 수명을 분리한다.
            if (data != null)
            {
                _battleCaster.StartBattle(data, OnBattleCompleted, OnBattleFailed);
            }
        }

        /// <summary>결과 화면 구성이 성공한 뒤 LastResult를 공개하고 완료 이벤트를 보낸다.</summary>
        private async UniTask ShowResultAsync(BattleResult result, CancellationTokenSource cancellation)
        {
            try
            {
                ResultUI resultUI = await UIManager.Instance.PushAsync<ResultUI>(AssetAddresses.RESULT_UI, cancellation.Token);
                resultUI.SetResult(result);
                LastResult = result;
                BattleCompleted?.Invoke(result);
            }
            catch (OperationCanceledException)
            {
                ReleaseBattle();
            }
            catch (Exception exception)
            {
                OnBattleFailed(exception);
            }
            finally
            {
                _loadCancellation = null;
                cancellation.Dispose();
            }
        }

        private async UniTask ReturnToTitleAsync()
        {
            try
            {
                // 진행은 이미 중단했다. 로드 취소 경로가 자산을 정리하고 토큰을 반환할 때까지 새 UI 로드를 보류한다.
                if (_loadCancellation != null)
                {
                    await UniTask.WaitUntil(() => _loadCancellation == null);
                }
                await UIManager.Instance.PushAsync<TitleUI>(AssetAddresses.TITLE_UI, Application.exitCancellationToken);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                BattleFailed?.Invoke(exception);
            }
            finally
            {
                _isReturning = false;
            }
        }

        private void ReleaseBattle()
        {
            // HUD가 유닛 Transform을 참조하므로 UI를 먼저 반환한다.
            UIManager.Instance.ClearHistory();
            _battleCaster.Release();
        }

        private void OnBattleCompleted(BattleResult result)
        {
            _loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(Application.exitCancellationToken);
            ShowResultAsync(result, _loadCancellation).Forget();
        }

        private void OnBattleFailed(Exception exception)
        {
            ReleaseBattle();
            Debug.LogException(exception);
            BattleFailed?.Invoke(exception);
        }
    }
}
