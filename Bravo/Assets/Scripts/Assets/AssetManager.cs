using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Noname.Asset
{
    /// <summary>
    /// Unity 메인 스레드에서 사용하는 자산 진입점. 호출자가 로드 참조와 생성 인스턴스를 각 반환 API로 정리한다.
    /// </summary>
    public static class AssetManager
    {
        /// <summary>
        /// 호출별로 자산 참조를 확보하며 사용 후 ReleaseAsset으로 반환해야 한다.
        /// 로드 실패 시 핸들을 반환하고 원래 오류를 InnerException에 보관한다.
        /// </summary>
        public static async UniTask<LoadedAsset<T>> LoadAssetAsync<T>(string address) where T : UnityEngine.Object
        {
            ValidateAddress(address);
            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(address);
            await EnsureSucceededAsync(handle, address);
            return new LoadedAsset<T>(handle);
        }

        /// <summary>
        /// 기존 씬을 교체하고, 로드 및 활성화가 완료된 씬을 반환한다.
        /// 기존 씬에서 생성한 Addressables 인스턴스는 호출 전에 반환해야 한다.
        /// 성공한 씬 로드 핸들은 Addressables가 추적하며 씬 언로드 시 자동으로 해제한다.
        /// </summary>
        public static async UniTask<Scene> LoadSceneAsync(string address)
        {
            ValidateAddress(address);
            AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(address);
            await EnsureSucceededAsync(handle, address);
            return handle.Result.Scene;
        }

        /// <summary>
        /// 원본 자산 참조를 유지하는 인스턴스를 생성한다. 사용 후 Destroy 대신 ReleaseInstance로 반환한다.
        /// </summary>
        public static async UniTask<GameObject> InstantiateAsync(string address, Transform parent = null)
        {
            ValidateAddress(address);
            AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(address, parent);
            await EnsureSucceededAsync(handle, address);
            return handle.Result;
        }

        /// <summary>
        /// 루트 컴포넌트를 반환하며 컴포넌트가 없으면 생성 인스턴스를 즉시 반환하고 실패한다.
        /// 정상 반환된 컴포넌트는 gameObject를 ReleaseInstance에 전달해 정리한다.
        /// </summary>
        public static async UniTask<T> InstantiateAsync<T>(string address, Transform parent = null) where T : Component
        {
            GameObject instance = await InstantiateAsync(address, parent);
            T component = instance.GetComponent<T>();

            if (component == null)
            {
                ReleaseInstance(instance);
                throw new InvalidOperationException($"프리팹 루트에 {typeof(T).Name} 컴포넌트가 없습니다. 주소: {address}");
            }

            return component;
        }

        /// <summary>
        /// 로드 참조를 반환하며 null 또는 이미 반환한 결과는 무시한다.
        /// </summary>
        public static void ReleaseAsset<T>(LoadedAsset<T> loadedAsset) where T : UnityEngine.Object
        {
            loadedAsset?.Release();
        }

        /// <summary>
        /// Addressables가 추적하는 인스턴스를 파괴하고 연결된 자산 참조를 반환한다.
        /// null, 이미 파괴한 객체 또는 추적되지 않는 객체는 false를 반환한다.
        /// 부모 객체나 씬을 제거하기 전에 이 API로 인스턴스를 반환해야 한다.
        /// </summary>
        public static bool ReleaseInstance(GameObject instance)
        {
            return instance != null && Addressables.ReleaseInstance(instance);
        }

        private static async UniTask EnsureSucceededAsync<T>(AsyncOperationHandle<T> handle, string address)
        {
            // Addressables는 Completed에서 결과 추적을 등록한다. IsDone만 보고 진행하면 등록 전에 인스턴스를 반환할 수 있다.
            AutoResetUniTaskCompletionSource completion = AutoResetUniTaskCompletionSource.Create();
            handle.Completed += _ => completion.TrySetResult();
            await completion.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                InvalidOperationException exception = new InvalidOperationException($"Addressables 작업에 실패했습니다. 주소: {address}", handle.OperationException);
                Addressables.Release(handle);
                throw exception;
            }
        }

        private static void ValidateAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("자산 주소는 비어 있을 수 없습니다.", nameof(address));
            }
        }
    }
}
