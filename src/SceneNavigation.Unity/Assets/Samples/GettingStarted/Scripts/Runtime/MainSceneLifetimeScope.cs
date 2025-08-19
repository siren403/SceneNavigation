using Cysharp.Threading.Tasks;
using SceneNavigation.Commands;
using SceneNavigation.Extensions;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using VitalRouter;
using VitalRouter.VContainer;

namespace GettingStarted
{
    public class MainSceneLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterNavigator(navigation => { navigation.StartupRootOnlyMainScene("/intro"); });
            builder.RegisterVitalRouter(routing => { });
            // builder.RegisterComponentInHierarchy<FadeCanvas>();
            // builder.RegisterBuildCallback(container =>
            // {
            //     var router = container.Resolve<Router>();
            //     var fade = container.Resolve<FadeCanvas>();
            //     var ct = Application.exitCancellationToken;
            //     router.SubscribeAwait<NavigationStartedCommand>(async (command, ctx) =>
            //     {
            //         await fade.InAsync(ct: ctx.CancellationToken);
            //     }).AddTo(ct);
            //     router.SubscribeAwait<NavigationEndedCommand>(async (command, ctx) =>
            //     {
            //         await fade.OutAsync(ct: ctx.CancellationToken);
            //     }).AddTo(ct);
            // });
            // 전체 번들 캐시 제거
            var result = Caching.ClearCache();
            Debug.Log($"Caching.ClearCache: {result}");
        }
    }
}