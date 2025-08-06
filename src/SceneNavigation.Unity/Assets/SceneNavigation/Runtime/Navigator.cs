// Licensed to the.NET Foundation under one or more agreements.
// The.NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
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
using VitalRouter;
#if USE_ZLOGGER
using Microsoft.Extensions.Logging;
using ZLogger;
#endif

namespace SceneNavigation
{
    public class Navigator
    {
        private readonly NavigatorOptions _options;
        private readonly Router _router;
#if USE_ZLOGGER
        private readonly ILogger<Navigator> _logger;
#endif
        private readonly Stack<string> _history = new();
        private readonly Dictionary<string, IList<IResourceLocation>> _locationCache = new();
        private readonly HashSet<string> _loadedScenesCache = new();
        private readonly List<AsyncOperationHandle<SceneInstance>> _loadingSceneHandles = new();

        private bool _initialized;
        public bool IsInitialized => _initialized;

        public string LoadedLocation { get; private set; } = string.Empty;

        public bool HasHistory => _history.Count > 1;

#if USE_ZLOGGER
        public Navigator(NavigatorOptions options, Router router, ILogger<Navigator> logger)
        {
            _options = options;
            _router = router;
            _logger = logger;
        }
#else
        public Navigator(NavigatorOptions options, Router router)
        {
            _options = options;
            _router = router;
        }
#endif

        public async UniTask Initialize()
        {
            if (_initialized)
            {
                throw new InvalidOperationException("Already initialized.");
            }

            var initialized = await AddressableExtensions.Initialize();

            if (!initialized.IsSuccess)
            {
#if USE_ZLOGGER
                _logger.LogError("Failed to initialize");
#endif
                _ = _router.PublishAsync(new InitializeFailedCommand());
                return;
            }

            _initialized = true;
#if USE_ZLOGGER
            _logger.LogInformation("Initialized");
#endif
            _ = _router.PublishAsync(new InitializedCommand() { Keys = initialized.Result.Keys.ToArray() });

            if (_options.StartupRoot)
            {
                await Startup();

                if (!string.IsNullOrEmpty(_options.EntryPath))
                {
                    await To(_options.EntryPath);
                }
            }

            _ = _router.PublishAsync(new PostStartUpCommand());
        }

        public async UniTask Startup()
        {
            await To(_options.Root);
        }

        public async UniTask To(string path)
        {
            Assert.IsTrue(_initialized);
            IList<IResourceLocation> locations = await GetLocations(path);
            if (locations.Count == 0)
            {
#if USE_ZLOGGER
                _logger.ZLogWarning($"Empty resource locations: {path}");
#endif
                return;
            }

            if (!string.IsNullOrEmpty(LoadedLocation))
            {
                if (LoadedLocation != _options.Root)
                {
                    await UnloadRoute(LoadedLocation);
                    LoadedLocation = string.Empty;
                }
                else
                {
#if USE_ZLOGGER
                    _logger.LogWarning("Impossible unload root path");
#endif
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

            await LoadRoute(path);
            LoadedLocation = path;
        }

        public async UniTask Push(string path)
        {
            Assert.IsTrue(_initialized);
            await To(path);
            _history.Push(path);
        }

        public async UniTask Back()
        {
            if (!HasHistory)
            {
#if USE_ZLOGGER
                _logger.LogWarning("Back failed. empty history");
#endif
                return;
            }

            string top = _history.Pop()!;
            await UnloadRoute(top);

            string peeked = _history.Peek()!;
            if (peeked == _options.Root)
            {
#if USE_ZLOGGER
                _logger.LogWarning("Impossible reload root path");
#endif
                return;
            }

            await LoadRoute(peeked);
            LoadedLocation = peeked;
        }

        public async UniTask Clear()
        {
            if (_history.Count == 0)
            {
                return;
            }

            string top = _history.Pop()!;
            await UnloadRoute(top);

            _history.Clear();
            _locationCache.Clear();
            _loadedScenesCache.Clear();
            _loadingSceneHandles.Clear();
#if USE_ZLOGGER
            _logger.LogInformation("Clear");
#endif
        }

        private async UniTask<IList<IResourceLocation>> GetLocations(string path)
        {
            if (!_locationCache.TryGetValue(path, out var locations))
            {
                locations = await Addressables.LoadResourceLocationsAsync(path).Task;
                _locationCache.Add(path, locations);
            }

            return locations;
        }

        private async UniTask UnloadRoute(string path)
        {
            var locations = await GetLocations(path);

            if (locations.Count == 0)
            {
#if USE_ZLOGGER
                _logger.ZLogError($"Failed to unload resource locations: {path}");
#endif
                return;
            }

            _ = _router.PublishAsync(new PreUnloadRouteCommand() { Path = path });

            GetLoadedScenes(_loadedScenesCache);

            foreach (var location in locations)
            {
                string locationString = location.ToString();
                if (!_loadedScenesCache.Contains(locationString))
                {
#if USE_ZLOGGER
                    _logger.ZLogWarning($"Not loaded {locationString} from {path}");
#endif
                    continue;
                }

                var operation = SceneManager.UnloadSceneAsync(locationString);
                if (operation == null)
                {
#if USE_ZLOGGER
                    _logger.ZLogError($"Unload {locationString} operation is null.");
#endif
                    continue;
                }

                await operation;
            }

#if USE_ZLOGGER
            _logger.ZLogInformation($"Unloaded {path}");
#endif
        }

        private async UniTask LoadRoute(string path)
        {
            var locations = await GetLocations(path);

            if (locations.Count == 0)
            {
#if USE_ZLOGGER
                _logger.ZLogError($"Failed to load resource locations: {path}");
#endif
                return;
            }

            _ = _router.PublishAsync(new PreLoadRouteCommand() { Path = path });

            GetLoadedScenes(_loadedScenesCache);

            _loadingSceneHandles.Clear();

            foreach (IResourceLocation location in locations)
            {
                string locationString = location.ToString();
                if (_loadedScenesCache.Contains(locationString))
                {
#if USE_ZLOGGER
                    _logger.ZLogWarning($"Loaded {locationString} from {path}");
#endif
                    continue;
                }

                AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(location,
                    LoadSceneMode.Additive,
                    SceneReleaseMode.ReleaseSceneWhenSceneUnloaded, false);

                if (!handle.IsValid())
                {
#if USE_ZLOGGER
                    _logger.ZLogError($"Invalid handle {locationString} from {path}");
#endif
                    continue;
                }

                await handle.Task;

                _loadingSceneHandles.Add(handle);
            }

            if (_loadingSceneHandles.Count == 0)
            {
#if USE_ZLOGGER
                _logger.ZLogWarning($"Failed to load resource locations: {path}");
#endif
                return;
            }

            foreach (AsyncOperationHandle<SceneInstance> handle in _loadingSceneHandles)
            {
                await handle.Result.ActivateAsync();
            }

            _loadingSceneHandles.Clear();
#if USE_ZLOGGER
            _logger.ZLogInformation($"Loaded {path}");
#endif
            _ = _router.PublishAsync(new PostLoadRouteCommand() { Path = path });
            LoadedLocation = path;
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