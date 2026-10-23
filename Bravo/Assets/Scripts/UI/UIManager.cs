using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Noname.Asset;

namespace Noname.UI
{
    /// <summary>UI 인스턴스와 스택을 소유한다. 페이즈가 바뀌면 ClearHistory 후 새 UI를 Push한다.</summary>
    public sealed class UIManager : Singleton<UIManager>
    {
        private readonly Stack<UIBase> _history = new Stack<UIBase>();
        private int _version;
        private bool _isLoading;

        public int Count => _history.Count;
        public UIBase Current => _history.Count > 0 ? _history.Peek() : null;

        [UnityEngine.Scripting.Preserve]
        private UIManager()
        {
        }

        /// <summary>이전 UI는 생성이 성공한 뒤 숨긴다. 중복 요청은 같은 최상단 인스턴스를 반환한다.</summary>
        public async UniTask<T> PushAsync<T>(string address, CancellationToken cancellationToken = default) where T : UIBase
        {
            cancellationToken.ThrowIfCancellationRequested();
            var version = _version;
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
                // 이전 페이즈의 로드 완료가 새 페이즈의 로드 상태를 덮어쓰지 않는다.
                if (version == _version)
                {
                    _isLoading = false;
                }
            }
        }

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
