using VContainer;
using VContainer.Unity;

namespace GettingStarted
{
    public class IntroSceneLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<IntroSceneEntryPoint>();
        }
    }
}