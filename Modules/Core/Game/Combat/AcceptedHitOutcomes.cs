using GameServer.Core.Protocol.Instance;

static partial class Game
{
    // Accepted hit receipts contain only host values. The existing current area role drains one charge batch per sweep.
    static byte[]? AcceptedChargePresentationLocked(ClientState conn, IReadOnlyList<PlayerMinions.AcceptedCharges> accepted)
    {
        if (conn.PlayerLife == 0) return null;
        var session = ChargesOf(conn);
        foreach (var e in accepted)
        {
            if (e.KillEvent) session.Enqueue(new ChargeEvent.Kill(e.Rarity));
            else session.Enqueue(new ChargeEvent.Hit(e.Critical, true, null, e.Rarity, e.Effect,
                name => Stats.Key(name) is uint key ? e.Skill!.GetValueOrDefault(key) : 0, e.Killed, e.Primary));
        }
        var final = CharacterPlayerStats(conn);
        int Get(string name) => Stats.Key(name) is uint key ? final.GetValueOrDefault(key) : 0;
        double at = accepted.Max(e => e.At);
        var step = session.Step(at, Get, () => WeaponProfileFor(conn), conn.PlayerLife > 0,
            at < conn.Walk.ArrivesAt, InTown(conn), Random.Shared);
        foreach (string line in step.Log) Console.WriteLine($"[gateway] {conn.Wire.Remote} {line} (t={at:0.000})");
        var packets = session.Presentation(ProjectileWorldTime(conn), () => conn.NextBuff++)
            .SelectMany(b => ChargeBuffs.Packets(b, PlayerId).SelectMany(p => p.Packet)).ToArray();
        if (step.CountsChanged)
        {
            conn.Pools = PoolsFor(conn, conn.Pools.Level); conn.ApplyMovementSpeed(); conn.Minions.StatRevision++;
        }
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
