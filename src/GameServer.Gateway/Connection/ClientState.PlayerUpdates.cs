sealed partial class ClientState
{
    internal ITransientCombatState? PlayerUpdatesState;
    internal ConnectionWorker PlayerUpdatesWorker { get; } = new("player-updates");

    internal void RetirePlayerUpdates()
    {
        lock (Monsters)
        {
            PlayerUpdatesWorker.Retire();
            PlayerUpdatesState?.Clear();
            PlayerUpdatesState = null;
        }
    }
}
