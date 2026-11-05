static partial class Game
{
    // Consumed synchronously at the accepted live -> dead transition, before poison cleanup and kill callbacks.
    // Successors can consume this same value here; never retain the corpse as a replay key.
    internal readonly record struct OwnerKillReceipt(SpawnedMonster Corpse, (int X, int Y) Position, bool Hostile, double At, bool PlayerKill,
        MonsterPoisons.Snapshot Poison);

    internal static bool ActualFlaskEffectLocked(ClientState conn, double now) =>
        conn.PlayerLife > 0 && !conn.Closed && !conn.LeavingArea
        && (conn.FlaskEffects.ActiveAt(now, conn.PlayerLife >= conn.PlayerMaxLife,
                conn.PlayerMana >= UnreservedMana(conn))
            || conn.UtilityFlaskEffects.ActiveAt(now)
            || MagebloodBelt(conn) is { } belt
                && MagebloodFlasks(ItemsOf(conn).Items(PlayerInventories.Flask),
                    MagebloodFlaskCount(belt, ItemsFor(conn)), ItemsFor(conn)).Count > 0);

    // Explicit LocalExile model, not established official radius/duration parity: fixed 15 cells, remaining duration.
    internal const int PathfinderSpreadRadius = 15;

    internal static void PathfinderOwnerKillLocked(ClientState conn, OwnerKillReceipt receipt)
    {
        if (CombatLifetime is not null && !CurrentGameWork) return;
        if (!receipt.PlayerKill || !receipt.Hostile || receipt.Poison.StrongestDps <= 0
            || conn.Closed || conn.LeavingArea || conn.Paused || conn.RageRetired
            || conn.PlayerLife == 0 || HardcoreProgressBlocked(conn)) return;
        var stats = CharacterPlayerStats(conn);
        bool active = ActualFlaskEffectLocked(conn, receipt.At);
        int Get(string name) => Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
        if (!OrdinaryPathfinderRules.CanSpread(active, Get)) return;
        var at = receipt.Position;
        var poison = new OrdinaryPathfinderRules.PoisonSnapshot(receipt.Poison.StrongestDps, receipt.Poison.Remaining);
        foreach (var target in conn.Monsters.Values)
            if (!ReferenceEquals(target, receipt.Corpse) && target.Hostile && !target.Dead && target.Life > 0
                && Dist(at, target.Walk.Position(receipt.At)) <= PathfinderSpreadRadius)
                PathfinderSpreadPoisonLocked(target, poison, receipt.At, active, stats, Random.Shared);
    }
}
