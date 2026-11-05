using System.Runtime.CompilerServices;

// Packet identity is preserved by AddRange and reload-retained queues. No Game delegates or types are retained.
sealed class TargetBuffOutputLedger
{
    sealed class Receipt(TargetBuffOutputLedger ledger, ClientState owner, uint target, ushort instance, int generation)
    {
        internal readonly TargetBuffOutputLedger Ledger = ledger;
        internal readonly WeakReference<ClientState> Owner = new(owner);
        internal readonly (uint Target, ushort Instance) Key = (target,instance);
        internal readonly int Generation = generation;
        internal bool Pending = true;
    }
    static readonly ConditionalWeakTable<byte[],Receipt> receipts = new();
    readonly Lock gate = new();
    readonly Dictionary<(uint Target,ushort Instance),int> pending = [];
    int generation;
    bool retired;

    internal byte[] Retain(ClientState owner,uint target,ushort instance,byte[] packet)
    {
        lock (gate)
        {
            var receipt = new Receipt(this,owner,target,instance,generation);
            receipts.Add(packet,receipt);
            if (retired || owner.Closed || owner.LeavingArea) receipt.Pending = false;
            else pending[receipt.Key] = pending.GetValueOrDefault(receipt.Key) + 1;
            return packet;
        }
    }
    internal bool Holds(uint target,ushort instance)
    { lock (gate) return pending.ContainsKey((target,instance)); }
    internal static bool IsPending(byte[] packet)
    {
        if (!receipts.TryGetValue(packet,out var receipt)) return false;
        lock (receipt.Ledger.gate) return receipt.Pending && receipt.Generation == receipt.Ledger.generation;
    }
    // Called while the common cipher send gate is held, before writing. Later sends cannot overtake consumption.
    internal static bool Consume(byte[] packet)
    {
        if (!receipts.TryGetValue(packet,out var receipt)) return true;
        lock (receipt.Ledger.gate)
        {
            bool send = receipt.Pending && receipt.Generation == receipt.Ledger.generation
                && receipt.Owner.TryGetTarget(out var owner) && !owner.Closed && !owner.LeavingArea;
            receipt.Ledger.Release(receipt);
            return send;
        }
    }
    internal static void Discard(byte[] packet)
    {
        if (receipts.TryGetValue(packet,out var receipt))
            lock (receipt.Ledger.gate) receipt.Ledger.Release(receipt);
    }
    void Release(Receipt receipt)
    {
        if (!receipt.Pending) return;
        receipt.Pending = false;
        if (receipt.Generation != generation) return;
        int count = pending.GetValueOrDefault(receipt.Key);
        if (count <= 1) pending.Remove(receipt.Key); else pending[receipt.Key] = count - 1;
    }
    internal void Retire()
    { lock (gate) { retired = true; generation++; pending.Clear(); } }
}

sealed partial class ClientState
{
    internal readonly TargetBuffOutputLedger TargetBuffOutputs = new();
}
