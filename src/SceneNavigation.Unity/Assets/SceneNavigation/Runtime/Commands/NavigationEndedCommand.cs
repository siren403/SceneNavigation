using VitalRouter;

namespace SceneNavigation.Commands
{
    public readonly struct NavigationEndedCommand : ICommand
    {
        public readonly string Path { get; init; }
    }

}