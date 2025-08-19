using VitalRouter;

namespace SceneNavigation.Commands
{
    public readonly struct TransitionEndedCommand : ICommand
    {
        public readonly string Path { get; init; }
    }
}