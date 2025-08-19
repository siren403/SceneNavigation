using System;
using Cysharp.Threading.Tasks;
using LitMotion;
using SceneNavigation.Commands;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using VitalRouter;
using VitalRouter.VContainer;

namespace GettingStarted
{
    public class TransitionSceneLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInHierarchy<Image>();
            builder.RegisterVitalRouter(routing =>
            {
                routing.MapEntryPoint<TransitionPresenter>();
            });
        }
    }

    [Routes(CommandOrdering.Drop)]
    public partial class TransitionPresenter : IInitializable
    {
        private readonly Image _image;
        private readonly RectTransform _transform;

        public TransitionPresenter(Image image)
        {
            _image = image;
            _transform = _image.GetComponent<RectTransform>();
        }

        public void Initialize()
        {
            var height = _transform.rect.height;
            _transform.pivot = new Vector2(0.5f, 0);
            _transform.anchoredPosition = new Vector2(0, height);
        }

        [Route]
        private async UniTask On(TransitionStartedCommand command, PublishContext context)
        {
            var height = _transform.rect.height;
            await LMotion.Create(height, 0, 0.3f)
                .WithEase(Ease.OutCirc)
                .Bind(_transform, static (value, state) =>
                {
                    state.anchoredPosition = new Vector2(0, value);
                })
                .ToUniTask(context.CancellationToken);
        }

        [Route]
        private async UniTask On(TransitionEndedCommand command, PublishContext context)
        {
            var height = _transform.rect.height;
            await LMotion.Create(0, height, 0.3f)
                .WithEase(Ease.OutCirc)
                .Bind(_transform, static (value, state) =>
                {
                    state.anchoredPosition = new Vector2(0, value);
                })
                .ToUniTask(context.CancellationToken);
        }
    }
}