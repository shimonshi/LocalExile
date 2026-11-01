using GameServer.Core.Protocol.Instance;

static partial class Game
{
    // Resolve the batch once, before any output await. Only current-state presentation remains queued, so a delayed
    // hook/send cannot replay historical events through the live ticker or resurrect their expired deadlines.
    static void CommitAcceptedChargeBatchLocked(ClientState conn, IReadOnlyList<PlayerMinions.AcceptedCharges> accepted,
        IReadOnlyDictionary<uint, int> player)
    {
        if (conn.PlayerLife == 0) return;
        var session = ChargesOf(conn);
        int Get(string name) => Stats.Key(name) is uint key ? player.GetValueOrDefault(key) : 0;
        session.Observe(Get);
        var events = accepted.Select(e => e.KillEvent ? (ChargeEvent)new ChargeEvent.Kill(e.Rarity)
            : new ChargeEvent.Hit(e.Critical, true, null, e.Rarity, e.Effect,
                name => Stats.Key(name) is uint key ? e.Skill!.GetValueOrDefault(key) : 0, e.Killed, e.Primary));
        var step = session.Admit(accepted.Max(e => e.At), events, Get, () => WeaponProfileFor(conn), conn.PlayerLife > 0, Random.Shared);
        foreach (string line in step.Log) Console.WriteLine($"[gateway] {conn.Wire.Remote} {line} (accepted contact)");
        if (step.CountsChanged)
        {
            conn.Pools = PoolsFor(conn, conn.Pools.Level); conn.ApplyMovementSpeed(); conn.Minions.StatRevision++;
        }
        conn.Minions.OutputQueue.Add(new([], null, Kind: PlayerMinions.OutputKind.Charges, Charges: accepted));
    }

    static byte[]? AcceptedChargePresentationLocked(ClientState conn)
    {
        if (conn.PlayerLife == 0) return null;
        var session = ChargesOf(conn); _ = CharacterPlayerStats(conn);
        double now = ProjectileWorldTime(conn);
        var step = session.Refresh(now);
        if (step.CountsChanged)
        {
            conn.Pools = PoolsFor(conn, conn.Pools.Level); conn.ApplyMovementSpeed(); conn.Minions.StatRevision++;
        }
        var packets = session.Presentation(now, () => conn.NextBuff++)
            .SelectMany(b => ChargeBuffs.Packets(b, PlayerId).SelectMany(p => p.Packet)).ToArray();
        return packets.Length > 0 ? packets : null;
    }

    static void CommitAcceptedCritFlasksLocked(ClientState conn, IReadOnlyDictionary<uint, int> player, double at)
    {
        if (!conn.FlaskChargeCarry.CritReady(at)) return;
        var bar = ItemsOf(conn).Items(PlayerInventories.Flask).Select(f => (f.Id,
            Chance: ItemModStats.Local(f.Item, ItemsFor(conn).Mods).GetValueOrDefault("local_recharge_on_crit_%"),
            Full: ItemsFor(conn).ByHash(f.Item.TypeHash) is not { } type || ItemsFor(conn).FlaskChargesOf(type) is not { } basis
                || f.Item.Charges >= FlaskCharges.Of(basis, ItemModStats.Local(f.Item, ItemsFor(conn).Mods)).MaxCharges)).ToList();
        if (bar.All(f => f.Full)) return;
        int Get(string name) => Stats.Key(name) is uint key ? player.GetValueOrDefault(key) : 0;
        bool precision = conn.Auras.Values.Any(a => a.EffectId == "Precision");
        var gains = FlaskCharges.OnCrit(bar, FlaskCharges.CritSources(Get, precision), conn.FlaskChargeCarry, at, Random.Shared.Next);
        if (gains.Count > 0) conn.Minions.FlaskRewards.Add(new(MonsterRarity.Normal, gains));
    }
}
