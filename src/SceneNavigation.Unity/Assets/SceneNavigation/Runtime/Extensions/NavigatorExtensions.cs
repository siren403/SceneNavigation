// Licensed to the.NET Foundation under one or more agreements.
// The.NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using VContainer;

namespace SceneNavigation.Extensions
{
    public class NavigatorBuilder
    {
        private readonly IContainerBuilder _builder;

        public bool StartupRoot = true;

        public NavigatorBuilder(IContainerBuilder builder)
        {
            _builder = builder;
        }
    }

    public record NavigatorOptions
    {
        public readonly string Root = "/";
        public bool StartupRoot { get; init; } = true;
    }


    public static class NavigatorExtensions
    {
#if !UNITY_WEBGL
        private static readonly AsyncLazy _readyCache = new(async () =>
        {
            await UniTask.WaitUntil(() => Caching.ready);
        });
#endif

        public static void StartupRootOnlyMainScene(this NavigatorBuilder builder)
        {
            builder.StartupRoot = SceneManager.GetSceneAt(0).buildIndex == 0;
        }

        public static async UniTask CheckForUpdates(this Navigator navigator)
        {
            var updates = await AddressableExtensions.CheckForCatalogUpdates();
            if (!updates.IsSuccess)
            {
                return;
            }

            if (!updates.Result.Any())
            {
                return;
            }

            // TODO: AddressableExtensions.UpdateCatalogs
            {
#if !UNITY_WEBGL
                await _readyCache;
#endif
                await Addressables.UpdateCatalogs(true, updates.Result);
            }

            await navigator.Clear();
            await navigator.Startup();
        }
    }
}
