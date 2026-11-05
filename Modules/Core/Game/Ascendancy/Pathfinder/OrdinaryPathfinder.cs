using GameServer.Core.Protocol.Instance;

static partial class Game
{
    internal static OrdinaryPathfinderRules.Distillation PathfinderDistillationLocked(ClientState conn, bool eligibleSkill,
        IReadOnlyDictionary<uint, int> stats, IReadOnlyList<InventoryItem>? snapshot = null)
    {
        var bar = (snapshot ?? ItemsOf(conn).Items(PlayerInventories.Flask)).Select(f =>
            new OrdinaryPathfinderRules.ChargeSource(f.Id, (byte)f.X, ItemsFor(conn).ByHash(f.Item.TypeHash)?.Id switch
            {
                "Metadata/Items/Flasks/FlaskUtility15" => OrdinaryPathfinderRules.Kind.Bismuth,
                "Metadata/Items/Flasks/FlaskUtility1" => OrdinaryPathfinderRules.Kind.Diamond,
                "Metadata/Items/Flasks/FlaskUtility7" => OrdinaryPathfinderRules.Kind.Amethyst,
                _ => OrdinaryPathfinderRules.Kind.Other,
            }, f.Item.Charges));
        return OrdinaryPathfinderRules.Distill(bar, eligibleSkill && !conn.Closed && !conn.LeavingArea && conn.PlayerLife > 0,
            name => Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0);
    }

    // Feature-specific adapters; skill admission owns the Distillation transaction.
    internal static FlaskRecovery.Use PathfinderRecovery(FlaskRecovery.Use recovery, IReadOnlyDictionary<uint, int> stats) =>
        OrdinaryPathfinderRules.Recovery(recovery, name => Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0);

    internal static int? PathfinderFlaskUseCost(Item flask, Flasks.Flask use, IReadOnlyDictionary<uint, int> stats, Random random) =>
        OrdinaryPathfinderRules.UseCost(flask.Charges, use.ChargesPerUse,
            name => Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0, random.Next);

    // Called for an accepted life/mana/utility use under Monsters, even if the charge-preservation roll succeeded.
    internal static void PathfinderAcceptedFlaskLocked(ClientState conn, IReadOnlyDictionary<uint, int> stats)
    {
        if (Stats.Key(OrdinaryPathfinderRules.Dispel) is not uint key || stats.GetValueOrDefault(key) <= 0) return;
        double now = ResourceEffectsTime(conn);
        var before = conn.ElementalAilments.Snapshot(now);
        double action = conn.ElementalAilments.ActionFactor(now);
        conn.ElementalAilments.Clear();
        PlayerAilmentsChangedLocked(conn, action, now);
        foreach (var (packet, log) in PlayerAilmentBuffs(before, conn.ElementalAilments.Snapshot(now)))
            Automation(conn).Packets.Add(new(packet, log));
    }

    // Caller must settle due effects and supply actual active recovery/utility/Mageblood state; Any alone includes
    // queued or expired effects. Flask ownership is deliberately not inferred from a stat or a visible buff.
    internal static double PathfinderPoisonDps(double dps, bool flaskActive, IReadOnlyDictionary<uint, int> stats, Random random) =>
        OrdinaryPathfinderRules.PoisonDps(dps, flaskActive,
            name => Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0, random.Next);

    internal static bool PathfinderSpreadPoisonLocked(SpawnedMonster target, OrdinaryPathfinderRules.PoisonSnapshot poison,
        double now, bool flaskActive, IReadOnlyDictionary<uint, int> stats, Random random) =>
        !target.Dead && target.Life > 0 && OrdinaryPathfinderRules.Spread(target.Poisons,
            poison with { Remaining = poison.Remaining * MonsterPoisons.SelfDuration(name => target.Mods.Effects[name]) },
            now, flaskActive,
            name => Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0,
            target.Mods.Effects["cannot_be_poisoned"] != 0, target.Mods.Effects["avoid_poison_%"], random.Next);
}
