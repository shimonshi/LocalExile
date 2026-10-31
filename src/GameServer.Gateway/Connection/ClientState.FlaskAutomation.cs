using System.Collections.Concurrent;

// Reload-persistent data only: no Game delegate, task, module type or lifetime is retained here.
readonly record struct FlaskAutomationEvent(FlaskEnchantments.Trigger Trigger, uint Item, byte Slot, long Activation = 0);
sealed class FlaskAutomationState
{
    internal readonly ConcurrentQueue<FlaskAutomationEvent> Events = new();
    internal readonly ConcurrentDictionary<FlaskAutomationEvent, byte> Pending = new();
    internal readonly SemaphoreSlim UseGate = new(1, 1);
    // Presentation receipts and revisions are accessed under Monsters, including at the send gate.
    internal readonly List<byte[]> Packets = [];
    internal long StatRevision, PresentedStatRevision;
    internal void Enqueue(FlaskAutomationEvent signal)
    {
        if (Pending.TryAdd(signal, 0)) Events.Enqueue(signal);
    }
}
sealed partial class ClientState
{
    internal FlaskAutomationState FlaskAutomation { get; } = new();
}
