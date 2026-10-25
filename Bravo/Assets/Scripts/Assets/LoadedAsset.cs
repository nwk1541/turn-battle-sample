using System;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Noname.Asset
{
    /// <summary>
    /// LoadAssetAsync 한 번으로 확보한 자산 참조를 보관하며 소유자가 ReleaseAsset으로 반환한다.
    /// </summary>
    /// <remarks>
    /// 같은 결과 객체를 공유해도 참조 횟수는 늘지 않는다. 반환 후에는 공유한 모든 변수에서 이 객체의 Asset 접근이 차단된다.
    /// </remarks>
    public sealed class LoadedAsset<T> where T : UnityEngine.Object
    {
        private AsyncOperationHandle<T> _handle;

        public bool IsReleased { get; private set; } = false;

        /// <summary>
        /// 참조 반환 후 접근하면 ObjectDisposedException이 발생한다.
        /// </summary>
        public T Asset
        {
            get
            {
                if (IsReleased)
                {
                    throw new ObjectDisposedException(nameof(LoadedAsset<T>));
                }

                return _handle.Result;
            }
        }

        internal LoadedAsset(AsyncOperationHandle<T> handle)
        {
            _handle = handle;
        }

        internal void Release()
        {
            if (IsReleased)
            {
                return;
            }

            IsReleased = true;
            AsyncOperationHandle<T> handle = _handle;
            _handle = default;
            Addressables.Release(handle);
        }
    }
}
