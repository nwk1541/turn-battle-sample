using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Noname.Asset;

namespace Noname.UI
{
    /// <summary>최상단 UI만 활성화하고 이전 화면은 스택에 유지한다. ClearHistory는 진행 중 로드도 무효화한다.</summary>
    public sealed class UIManager : Singleton<UIManager>
    {
        private readonly Stack<UIBase> _history = new Stack<UIBase>();

        private int _version = 0;
        private bool _isLoading = false;

        public int Count => _history.Count;
        public UIBase Current => _history.Count > 0 ? _history.Peek() : null;

        public UIManager()
        {
        }

        /// <summary>새 UI의 초기화가 끝난 뒤 이전 UI를 숨긴다. 최상단이 요청한 타입이면 해당 인스턴스를 재사용한다.</summary>
        public async UniTask<T> PushAsync<T>(string address, CancellationToken cancellationToken = default) where T : UIBase
        {
            cancellationToken.ThrowIfCancellationRequested();
            int version = _version;
            if (_isLoading)
            {
                await UniTask.WaitUntil(() => !_isLoading || version != _version, cancellationToken: cancellationToken);
            }
            EnsureCurrentVersion(version);
            if (Current is T current)
            {
                return current;
            }

            _isLoading = true;
            T ui = null;
            try
            {
                ui = await AssetManager.InstantiateAsync<T>(address);
                // 생성 요청 자체는 취소되지 않으므로 완료 후 취소와 스택 버전을 확인하고 무효한 인스턴스는 catch에서 반환한다.
                cancellationToken.ThrowIfCancellationRequested();
                EnsureCurrentVersion(version);
                ui.gameObject.SetActive(false);
                ui.Init();
                if (Current != null)
                {
                    Current.gameObject.SetActive(false);
                }
                _history.Push(ui);
                ui.gameObject.SetActive(true);
                return ui;
            }
            catch
            {
                ReleaseUI(ui);
                throw;
            }
            finally
            {
                // ClearHistory 이후 시작한 로드의 상태를 이전 요청의 finally에서 덮어쓰지 않는다.
                if (version == _version)
                {
                    _isLoading = false;
                }
            }
        }

        /// <summary>최하단 화면은 제거하지 않으며 UI 로드 중에는 뒤로 가기를 허용하지 않는다.</summary>
        public bool Pop()
        {
            if (_isLoading || _history.Count <= 1)
            {
                return false;
            }
            ReleaseUI(_history.Pop());
            Current.gameObject.SetActive(true);
            return true;
        }

        /// <summary>스택을 비우고 이미 요청한 UI가 뒤늦게 새 스택에 들어오는 것을 차단한다.</summary>
        public void ClearHistory()
        {
            _version++;
            _isLoading = false;
            while (_history.Count > 0)
            {
                ReleaseUI(_history.Pop());
            }
        }

        private void EnsureCurrentVersion(int version)
        {
            if (version != _version)
            {
                throw new OperationCanceledException("UI 히스토리가 초기화되었습니다.");
            }
        }

        private static void ReleaseUI(UIBase ui)
        {
            if (ui == null)
            {
                return;
            }
            ui.gameObject.SetActive(false);
            ui.Release();
            AssetManager.ReleaseInstance(ui.gameObject);
        }
    }
}
