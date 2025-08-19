// Licensed to the.NET Foundation under one or more agreements.
// The.NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Assertions;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using SceneNavigation.Commands;
using SceneNavigation.Extensions;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.Exceptions;
using VitalRouter;

namespace SceneNavigation
{
    public readonly struct LoadingStatus
    {
        public readonly int TotalCount;
        public readonly int LoadedCount;
        public readonly float CurrentProgress;

        public LoadingStatus(int totalCount)
        {
            TotalCount = totalCount;
            LoadedCount = 0;
            CurrentProgress = 0f;
        }

        public LoadingStatus(int totalCount, int loadedCount, float currentProgress)
        {
            TotalCount = totalCount;
            LoadedCount = loadedCount;
            CurrentProgress = currentProgress;
        }

        public LoadingStatus WithLoadedCount(int loadedCount)
        {
            return new LoadingStatus(
                TotalCount,
                loadedCount,
                0
            );
        }

        public LoadingStatus WithCurrentProgress(float currentProgress)
        {
            return new LoadingStatus(
                TotalCount,
                LoadedCount,
                currentProgress
            );
        }

        public override string ToString()
        {
            return
                $"LoadingStatus: TotalCount={TotalCount}, LoadedCount={LoadedCount}, CurrentProgress={CurrentProgress}";
        }
    }

    public class Navigator
    {
        private readonly NavigatorOptions _options;
        private readonly Router _router;
        private readonly Stack<string> _history = new();
        private readonly Dictionary<string, IList<IResourceLocation>> _locationCache = new();
        private readonly HashSet<string> _loadedScenesCache = new();
        private readonly List<AsyncOperationHandle<SceneInstance>> _loadingSceneHandles = new();

        private bool _initialized;
        public bool IsInitialized => _initialized;

        public string LoadedLocation { get; private set; } = string.Empty;

        public bool HasHistory => _history.Count > 1;

        public Navigator(NavigatorOptions options, Router router)
        {
            _options = options;
            _router = router;
        }

        public async UniTask InitializeAsync()
        {
            if (_initialized)
            {
                throw new InvalidOperationException("Already initialized.");
            }

            var initialized = await AddressableExtensions.Initialize();

            if (!initialized.IsSuccess)
            {
                return;
            }

            var catalogs = await AddressableExtensions.CheckForCatalogUpdates();
            if (!catalogs.IsSuccess)
            {
                Debug.Log("Failed to check for catalog updates");
                return;
            }

            foreach (var location in catalogs.Result)
            {
                Debug.Log(location);
            }

            _initialized = true;

            if (_options.StartupRoot)
            {
                await StartupAsync();

                if (!string.IsNullOrEmpty(_options.EntryPath))
                {
                    await ToAsync(_options.EntryPath);
                }
            }
        }

        public async UniTask StartupAsync()
        {
            await ToAsync(_options.Root);
        }

        public readonly struct ToHandle
        {
            private readonly Navigator _navigator;
            private readonly string _path;

            public ToHandle(Navigator navigator, string path)
            {
                _navigator = navigator;
                _path = path;
            }

            public async UniTask<(int count, long bytes)> GetDownloadInfoAsync()
            {
                IList<IResourceLocation> locations = await _navigator.GetLocationsAsync(_path);
                if (locations.Count == 0)
                {
                    return default;
                }

                var size = await Addressables.GetDownloadSizeAsync(locations).Task.AsUniTask();
                return (locations.Count, size);
            }

            public async UniTask DownloadAsync(IProgress<DownloadStatus> progress = null)
            {
                IList<IResourceLocation> locations = await _navigator.GetLocationsAsync(_path);
                await _navigator.DownloadAsync(locations, progress);
            }

            public void Execute(IProgress<LoadingStatus> progress = null)
            {
                _navigator.ToAsync(_path, loadingProgress: progress).Forget();
            }

            public override string ToString()
            {
                return $"ToHandle: {_path}";
            }
        }

        public ToHandle To(string path)
        {
            return new ToHandle(this, path);
        }

        private static readonly Queue<RemoteProviderException> RemoteExceptions = new();
        private static readonly Queue<OperationException> OperationExceptions = new();

        private async UniTask DownloadAsync(
            IList<IResourceLocation> locations,
            IProgress<DownloadStatus> progress = null,
            CancellationToken ct = default
        )
        {
            if (locations is { Count: 0 })
            {
                return;
            }

            #region Error Handling

            ResourceManager.ExceptionHandler += OnExceptionHandler;

            void OnExceptionHandler(AsyncOperationHandle exceptionHandle, Exception exception)
            {
                switch (exception)
                {
                    case RemoteProviderException remote:
                        RemoteExceptions.Enqueue(remote);
                        break;
                    case OperationException operation:
                        OperationExceptions.Enqueue(operation);
                        break;
                }

                Debug.LogError(
                    $"{exceptionHandle.DebugName} | {exceptionHandle.OperationException} | {exception.Message}");
            }

            void ThrowIfResourceException()
            {
                try
                {
                    if (OperationExceptions.Any())
                    {
                        throw OperationExceptions.Dequeue();
                    }

                    // WebRequest 예외로 발생한 RemoteProviderException 리트라이 처리까지 포함해서
                    // 반복적으로 발생할 수 있음.
                    // if (RemoteExceptions.Any())
                    // {
                    //     throw LoadSceneException.CreateRemoteError(RemoteExceptions.Dequeue());
                    // }
                }
                finally
                {
                    OperationExceptions.Clear();
                    RemoteExceptions.Clear();
                }
            }

            #endregion


            var handle = Addressables.DownloadDependenciesAsync(locations);
            try
            {
                var initialStatus = handle.GetDownloadStatus();
                if (initialStatus is { TotalBytes: > 0 })
                {
                    progress?.Report(initialStatus);
                }

                Debug.Log($"Download status: {initialStatus}");
                while (!handle.IsDone && !ct.IsCancellationRequested)
                {
                    await UniTask.Yield();
                    ThrowIfResourceException();
                    var status = handle.GetDownloadStatus();
                    if (status is { TotalBytes: > 0 })
                    {
                        progress?.Report(status);
                    }
                }

                Debug.Log($"Download completed: {handle}");
            }
            finally
            {
                handle.Release();
                ResourceManager.ExceptionHandler -= OnExceptionHandler;
            }
        }

        public async UniTask ToAsync(
            string path,
            IProgress<DownloadStatus> downloadProgress = null,
            IProgress<LoadingStatus> loadingProgress = null
        )
        {
            Assert.IsTrue(_initialized);

            (string path, SceneInstance instance)? transitionScene = null;
            if (path != _options.Root)
            {
                if (path != _options.EntryPath)
                {
                    await _router.PublishAsync(new NavigationStartedCommand()
                    {
                        Path = path
                    });
                }

                var transitionPath = $"{path}:transition";
                var transitions = await GetLocationsAsync(transitionPath);
                if (transitions.Count == 0)
                {
                    transitions = await GetLocationsAsync($"{_options.Root}:transition");
                }

                if (transitions.Count > 0)
                {
                    var transition = transitions.First();
                    var handle = LoadSceneAsync(transition);
                    await handle.Task.AsUniTask();
                    await handle.Result.ActivateAsync();
                    transitionScene = (transitionPath, handle.Result);
                    // TODO: TransitionStartedCommand
                    await _router.PublishAsync(new TransitionStartedCommand()
                    {
                        Path = transitionPath
                    });
                }
            }


            IList<IResourceLocation> locations = await GetLocationsAsync(path);

            if (locations.Count == 0)
            {
                LoadedLocation = path;
                return;
            }

            ByteSize size = await Addressables.GetDownloadSizeAsync(locations).Task.AsUniTask();
            Debug.Log($"Size of locations for path '{path}': {size} bytes");
            if (size > 0)
            {
                await DownloadAsync(locations, downloadProgress);
            }

            if (!string.IsNullOrEmpty(LoadedLocation))
            {
                if (LoadedLocation != _options.Root)
                {
                    await UnloadRouteAsync(LoadedLocation);
                    LoadedLocation = string.Empty;
                }
            }
            else
            {
                for (var i = 0; i < SceneManager.loadedSceneCount; ++i)
                {
                    var unmanagedScene = SceneManager.GetSceneAt(i);
                    if (unmanagedScene.buildIndex == 0)
                    {
                        continue;
                    }

                    await SceneManager.UnloadSceneAsync(unmanagedScene);
                }
            }

            await LoadRouteAsync(path, loadingProgress);
            LoadedLocation = path;

            // TODO: TransitionEndedCommand
            if (transitionScene.HasValue)
            {
                var (transitionPath, instance) = transitionScene.Value;
                await _router.PublishAsync(new TransitionEndedCommand()
                {
                    Path = transitionPath
                });
                await SceneManager.UnloadSceneAsync(instance.Scene);
                transitionScene = null;
            }

            await _router.PublishAsync(new NavigationEndedCommand()
            {
                Path = path
            });
        }

        public async UniTask PushAsync(string path)
        {
            Assert.IsTrue(_initialized);
            await ToAsync(path);
            _history.Push(path);
        }

        public async UniTask BackAsync()
        {
            if (!HasHistory)
            {
                return;
            }

            string top = _history.Pop()!;
            await UnloadRouteAsync(top);

            string peeked = _history.Peek()!;
            if (peeked == _options.Root)
            {
                return;
            }

            await LoadRouteAsync(peeked);
            LoadedLocation = peeked;
        }

        public async UniTask ClearAsync()
        {
            if (_history.Count == 0)
            {
                return;
            }

            string top = _history.Pop()!;
            await UnloadRouteAsync(top);

            _history.Clear();
            _locationCache.Clear();
            _loadedScenesCache.Clear();
            _loadingSceneHandles.Clear();
        }

        private async UniTask<IList<IResourceLocation>> GetLocationsAsync(string path)
        {
            if (!_locationCache.TryGetValue(path, out var locations))
            {
                locations = await Addressables.LoadResourceLocationsAsync(path).Task;
                locations = locations
                    .Where(loc => loc.ResourceType == typeof(SceneInstance))
                    .ToArray();
                _locationCache.Add(path, locations);
            }

            return locations;
        }

        private async UniTask UnloadRouteAsync(string path)
        {
            var locations = await GetLocationsAsync(path);

            if (locations.Count == 0)
            {
                return;
            }

            GetLoadedScenes(_loadedScenesCache);

            foreach (var location in locations)
            {
                string locationString = location.ToString();
                if (!_loadedScenesCache.Contains(locationString))
                {
                    continue;
                }

                var operation = SceneManager.UnloadSceneAsync(locationString);
                if (operation == null)
                {
                    continue;
                }

                await operation;
            }
        }

        private async UniTask LoadRouteAsync(string path, IProgress<LoadingStatus> progress = null)
        {
            var locations = await GetLocationsAsync(path);

            if (locations.Count == 0)
            {
                return;
            }

            GetLoadedScenes(_loadedScenesCache);

            _loadingSceneHandles.Clear();

            var status = new LoadingStatus(
                locations.Count(location => !_loadedScenesCache.Contains(location.ToString()))
            );
            foreach (IResourceLocation location in locations)
            {
                string locationString = location.ToString();
                if (_loadedScenesCache.Contains(locationString))
                {
                    continue;
                }

                AsyncOperationHandle<SceneInstance> handle = LoadSceneAsync(location);

                if (!handle.IsValid())
                {
                    continue;
                }

                progress?.Report(status);

                while (!handle.IsDone)
                {
                    await UniTask.Yield();
                    var download = handle.GetDownloadStatus();
                    if (download is { TotalBytes: > 0 })
                    {
                        Debug.Log(
                            $"Download Status: {download.DownloadedBytes}, {download.TotalBytes}, {download.Percent:p2}");
                    }

                    status = status.WithCurrentProgress(handle.PercentComplete);
                    progress?.Report(status);
                }

                _loadingSceneHandles.Add(handle);
                status = status.WithLoadedCount(status.LoadedCount + 1);
                progress?.Report(status);
            }

            if (_loadingSceneHandles.Count == 0)
            {
                return;
            }

            foreach (AsyncOperationHandle<SceneInstance> handle in _loadingSceneHandles)
            {
                await handle.Result.ActivateAsync();
            }

            _loadingSceneHandles.Clear();
            LoadedLocation = path;
        }

        private AsyncOperationHandle<SceneInstance> LoadSceneAsync(IResourceLocation location)
        {
            return Addressables.LoadSceneAsync(location,
                LoadSceneMode.Additive,
                SceneReleaseMode.ReleaseSceneWhenSceneUnloaded,
                false
            );
        }

        private void GetLoadedScenes(in HashSet<string> cache)
        {
            cache.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                cache.Add(SceneManager.GetSceneAt(i).path);
            }
        }
    }
}