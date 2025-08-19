using VitalRouter;

namespace SceneNavigation.Commands
{
    public readonly struct NavigationStartedCommand : ICommand
    {
        public readonly string Path { get; init; }
    }

}