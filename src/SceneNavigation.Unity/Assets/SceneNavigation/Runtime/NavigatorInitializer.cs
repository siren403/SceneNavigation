// Licensed to the.NET Foundation under one or more agreements.
// The.NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer.Unity;

namespace SceneNavigation
{
    public class NavigatorInitializer : IAsyncStartable
    {
        private readonly Navigator _navigator;

        public NavigatorInitializer(Navigator navigator)
        {
            _navigator = navigator;
        }

        public UniTask StartAsync(CancellationToken cancellation = default)
        {
            return _navigator.InitializeAsync();
        }
    }
}
