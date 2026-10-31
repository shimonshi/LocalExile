using GameServer.Core.Protocol.Instance;

/// <summary>Build only at the native send gate under the combat lock; stale queued adds never survive a refund.</summary>
sealed class DeadeyeGalePresentation
{
    ushort tailwind;
    readonly Dictionary<EffectClock, (ushort Buff, double Deadline)> shown = [];

    public List<byte[]> Build(DeadeyeGaleState state, double now, uint player, Func<ushort> next)
    {
        state.Advance(now);
        var packets = new List<byte[]>();
        if (!state.Tailwind && tailwind != 0)
        { packets.Add(InstanceEntities.RemoveBuff(player, tailwind)); tailwind = 0; }
        if (state.Tailwind && tailwind == 0)
        {
            tailwind = next();
            packets.Add(InstanceEntities.AddBuff(player, tailwind, 1094, player));
        }
        var active = state.Tailwind ? state.Stacks.ToHashSet() : [];
        foreach (var clock in shown.Keys.Where(c => !active.Contains(c) || shown[c].Deadline != c.Deadline).ToArray())
        { packets.Add(InstanceEntities.RemoveBuff(player, shown[clock].Buff)); shown.Remove(clock); }
        foreach (var clock in active.Where(c => !shown.ContainsKey(c)))
        {
            ushort buff = next(); shown[clock] = (buff, clock.Deadline);
            double duration = clock.Rate > 0 ? clock.Remaining / clock.Rate : double.PositiveInfinity;
            packets.Add(InstanceEntities.AddBuff(player, buff, 1095, player,
                u68: double.IsFinite(duration) ? BitConverter.SingleToUInt32Bits((float)duration) : null, stacks: 1));
        }
        return packets;
    }

    public void Forget() { tailwind = 0; shown.Clear(); }
}
