using GameServer.Core.Protocol.Instance;

/// <summary>Native Headsman instances only. Source stats are already projected once by WithSlayer.
/// The caller builds packets and allocates IDs at the send gate under the owner's combat lock.</summary>
sealed class SlayerBuffPresentation
{
    public const ushort MovementRow = 1341, AttackRow = 1342;
    sealed record Shown(ushort Buff, long Identity, int Value, double Deadline, double Rate);
    Shown? movement, attack;

    static bool Matches(Shown? shown, SlayerKillState.BuffGrant? grant) => grant is { } value
        ? shown is not null && shown.Identity == value.Identity && shown.Value == value.Value
            && shown.Deadline == value.Deadline && shown.Rate == value.Rate : shown is null;

    public bool NeedsPublication(SlayerKillState.BuffSnapshot state) =>
        !Matches(movement, state.Movement) || !Matches(attack, state.Attack);

    public byte[] Build(SlayerKillState.BuffSnapshot state, uint player, Func<ushort> next)
    {
        var packets = new List<byte>();
        void Update(ref Shown? shown, SlayerKillState.BuffGrant? grant, ushort row)
        {
            if (Matches(shown, grant)) return;
            if (shown is not null) packets.AddRange(InstanceEntities.RemoveBuff(player, shown.Buff));
            shown = null;
            if (grant is not { } value) return;
            ushort buff = next();
            double duration = value.Remaining / value.Rate;
            packets.AddRange(InstanceEntities.AddBuff(player, buff, row, player, values: [unchecked((uint)value.Value)],
                u68: BitConverter.SingleToUInt32Bits((float)duration)));
            shown = new(buff, value.Identity, value.Value, value.Deadline, value.Rate);
        }
        Update(ref movement, state.Movement, MovementRow);
        Update(ref attack, state.Attack, AttackRow);
        return packets.ToArray();
    }
}
