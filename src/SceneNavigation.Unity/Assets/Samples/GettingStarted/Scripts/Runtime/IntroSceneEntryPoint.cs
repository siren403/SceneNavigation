using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using SceneNavigation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer.Unity;

namespace GettingStarted
{
    public class IntroSceneEntryPoint : IAsyncStartable, IProgress<DownloadStatus>, IProgress<LoadingStatus>
    {
        private readonly Navigator _navigator;

        public IntroSceneEntryPoint(Navigator navigator)
        {
            _navigator = navigator;
        }

        private UniTask WaitForKeyDownAsync(KeyCode key, CancellationToken cancellation)
        {
            return UniTask.WaitUntil(
                key,
                static (state) => Input.GetKeyDown(state),
                cancellationToken: cancellation
            );
        }

        public async UniTask StartAsync(CancellationToken cancellation = new CancellationToken())
        {
            // https://docs.unity3d.com/Packages/com.unity.addressables@2.6/manual/remote-content-assetbundle-cache.html
            // 참조 되지 않는 캐시 항목 제거
            // var result = await Addressables.CleanBundleCache().Task.AsUniTask();
            // Debug.Log($"Addressables.CleanBundleCache: {result}");


            await UniTask.WaitUntil(
                () => Input.anyKeyDown &&
                      (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)),
                cancellationToken: cancellation);

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                Debug.Log($"Enter to handle");
                var toTitle = _navigator.To("/title");
                var downloadInfo = await toTitle.GetDownloadInfoAsync();
                Debug.Log($"ToAsync: {toTitle}, Download Info: {downloadInfo.count}");
                if (downloadInfo.bytes > 0)
                {
                    Debug.Log($"Downloading {downloadInfo.count} locations, {downloadInfo.bytes} bytes");
                    await WaitForKeyDownAsync(KeyCode.RightArrow, cancellation);
                    await toTitle.DownloadAsync(this);
                    downloadInfo = await toTitle.GetDownloadInfoAsync();
                    Debug.Log($"Completed. {downloadInfo.count}, {downloadInfo.bytes} bytes");
                }

                await WaitForKeyDownAsync(KeyCode.RightArrow, cancellation);

                toTitle.Execute(this);
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Debug.Log("Enter to async");
                _navigator.ToAsync("/title",
                    downloadProgress: this,
                    loadingProgress: this
                ).Forget();
            }
        }

        public void Report(DownloadStatus status)
        {
            Debug.Log($"Download status: {status.DownloadedBytes}/{status.TotalBytes}");
        }

        public void Report(LoadingStatus value)
        {
            Debug.Log($"Loading status: {value}");
        }
    }
}