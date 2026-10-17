using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace Noname.Asset
{
    /// <summary>
    /// 프로젝트 공통 자산 및 씬 로드, 프리팹 생성과 해제를 담당한다.
    /// 모든 API는 Unity 메인 스레드에서 호출하며, 자산의 사용 기간은 호출자가 관리한다.
    /// </summary>
    public static class AssetManager
    {
        /// <summary>
        /// 주소로 자산을 로드하고 해당 호출이 소유할 로드 결과를 반환한다.
        /// 실패하면 핸들을 해제한 뒤 원래 오류를 포함한 InvalidOperationException을 전달한다.
        /// </summary>
        public static async UniTask<LoadedAsset<T>> LoadAssetAsync<T>(string address) where T : UnityEngine.Object
        {
            ValidateAddress(address);
            var handle = Addressables.LoadAssetAsync<T>(address);
            await EnsureSucceededAsync(handle, address);
            return new LoadedAsset<T>(handle);
        }

        /// <summary>
        /// 해당 로드 결과의 자산 참조를 반환한다. null 또는 이미 해제한 결과는 무시한다.
        /// </summary>
        public static void ReleaseAsset<T>(LoadedAsset<T> loadedAsset) where T : UnityEngine.Object
        {
            loadedAsset?.Release();
        }

        /// <summary>
        /// 프리팹을 생성하고 인스턴스를 반환한다. 위치와 회전은 프리팹의 로컬 값을 사용한다.
        /// 생성된 인스턴스는 직접 Destroy하지 않고, 사용이 끝나면 ReleaseInstance로 반환한다.
        /// </summary>
        public static async UniTask<GameObject> InstantiateAsync(string address, Transform parent = null)
        {
            ValidateAddress(address);
            var handle = Addressables.InstantiateAsync(address, parent);
            await EnsureSucceededAsync(handle, address);
            return handle.Result;
        }

        /// <summary>
        /// 프리팹을 생성하고 루트의 컴포넌트를 반환한다.
        /// 컴포넌트가 없으면 인스턴스를 반환하고 예외를 전달한다.
        /// 사용이 끝나면 반환된 컴포넌트의 gameObject를 ReleaseInstance에 전달한다.
        /// </summary>
        public static async UniTask<T> InstantiateAsync<T>(string address, Transform parent = null) where T : Component
        {
            var instance = await InstantiateAsync(address, parent);
            var component = instance.GetComponent<T>();

            if (component == null)
            {
                ReleaseInstance(instance);
                throw new InvalidOperationException($"프리팹 루트에 {typeof(T).Name} 컴포넌트가 없습니다. 주소: {address}");
            }

            return component;
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

        /// <summary>
        /// 기존 씬을 교체하고, 로드 및 활성화가 완료된 씬을 반환한다.
        /// 기존 씬에서 생성한 Addressables 인스턴스는 호출 전에 반환해야 한다.
        /// 성공한 씬 로드 핸들은 Addressables가 추적하며 씬 언로드 시 자동으로 해제한다.
        /// </summary>
        public static async UniTask<Scene> LoadSceneAsync(string address)
        {
            ValidateAddress(address);
            var handle = Addressables.LoadSceneAsync(address);
            await EnsureSucceededAsync(handle, address);
            return handle.Result.Scene;
        }

        private static async UniTask EnsureSucceededAsync<T>(AsyncOperationHandle<T> handle, string address)
        {
            // 이미 완료된 작업도 완료 이벤트를 기다려 Addressables의 내부 인스턴스 추적 등록을 보장한다.
            // 완료 소스는 await에서 결과를 소비한 뒤 풀로 반환된다.
            var completion = AutoResetUniTaskCompletionSource.Create();
            handle.Completed += _ => completion.TrySetResult();
            await completion.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                var exception = new InvalidOperationException($"Addressables 작업에 실패했습니다. 주소: {address}", handle.OperationException);
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
