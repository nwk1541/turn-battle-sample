using System;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Noname.Asset
{
    /// <summary>
    /// 한 번의 자산 로드로 확보한 참조와 해제에 필요한 핸들을 함께 보관한다.
    /// 자산의 복사본이 아니며, 같은 주소를 여러 번 로드하면 결과 객체마다 하나의 참조를 소유한다.
    /// 호출자는 Asset을 사용하는 동안 이 객체를 보관하고, 사용이 끝나면 AssetManager.ReleaseAsset으로 반환한다.
    /// </summary>
    /// <remarks>
    /// 이 객체를 다른 변수에 대입해도 참조 횟수가 늘어나지는 않는다.
    /// 프리팹을 직접 복제한 경우에는 모든 복제본의 사용이 끝날 때까지 원본의 로드 결과를 유지해야 한다.
    /// 해제 시점은 호출자가 결정하며, Addressables 핸들은 외부에 노출하지 않는다.
    /// </remarks>
    public sealed class LoadedAsset<T> where T : UnityEngine.Object
    {
        private AsyncOperationHandle<T> _handle;

        /// <summary>
        /// 이 로드 결과가 소유하던 참조를 반환했는지 나타낸다.
        /// </summary>
        public bool IsReleased { get; private set; }

        /// <summary>
        /// 로드한 실제 자산. 해제 후 접근하면 ObjectDisposedException이 발생한다.
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
            var handle = _handle;
            _handle = default;
            Addressables.Release(handle);
        }
    }
}
