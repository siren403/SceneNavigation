using VitalRouter;

namespace SceneNavigation.Commands
{
    public readonly struct TransitionStartedCommand : ICommand
    {
        public readonly string Path { get; init; }
    }

}