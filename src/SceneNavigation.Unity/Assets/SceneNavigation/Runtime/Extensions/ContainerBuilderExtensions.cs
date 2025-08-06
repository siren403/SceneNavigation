// Licensed to the.NET Foundation under one or more agreements.
// The.NET Foundation licenses this file to you under the MIT license.

using System;
using VContainer;
using VContainer.Unity;

namespace SceneNavigation.Extensions
{
    public static class ContainerBuilderExtensions
    {
        public static void RegisterNavigator(this IContainerBuilder builder,
            Action<NavigatorBuilder> configure)
        {
            var nav = new NavigatorBuilder(builder);
            configure(nav);

            builder.RegisterInstance(new NavigatorOptions() { StartupRoot = nav.StartupRoot, });
            builder.Register<Navigator>(Lifetime.Singleton).AsSelf();
            builder.RegisterEntryPoint<NavigatorInitializer>();
        }
    }
}
