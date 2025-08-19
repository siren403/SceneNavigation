using Cysharp.Threading.Tasks;
using SceneNavigation.Commands;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using VitalRouter;
using VitalRouter.VContainer;

namespace GettingStarted
{
    public class FadeSceneLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<FadeCanvas>();
            builder.RegisterVitalRouter(routing =>
            {
                routing.Map<FadePresenter>();
            });
        }
    }

    [Routes(CommandOrdering.Drop)]
    public partial class FadePresenter
    {
        private readonly FadeCanvas _fade;

        public FadePresenter(FadeCanvas fade)
        {
            _fade = fade;
        }

        [Route]
        private async UniTask On(TransitionStartedCommand command, PublishContext context)
        {
            Debug.Log($"In: {command.Path}");
            await _fade.InAsync(ct: context.CancellationToken);
        }

        [Route]
        private async UniTask On(TransitionEndedCommand command, PublishContext context)
        {
            Debug.Log($"Out: {command.Path}");
            await _fade.OutAsync(ct: context.CancellationToken);
        }
    }
}