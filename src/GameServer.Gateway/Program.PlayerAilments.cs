using GameServer.Core.World;

partial class Program
{
    internal static void ApplyPlayerAilmentsLocked(ClientState conn, DamagePacket baseHit, DamagePacket taken,
        bool critical, bool blocked, double now, IReadOnlyDictionary<uint, int> stats,
        uint sourceId = 0, SpawnedMonster? source = null)
    {
        if (blocked || conn.Closed || conn.LeavingArea || conn.PlayerLife == 0 || InGracePeriod(conn)
            || !baseHit.Components.Any(c => DamageTypes.IsElemental(c.Type))) return;
        int Get(string name) => Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
        double previousAction = conn.ElementalAilments.ActionFactor(now);
        bool frozen = previousAction == 0;
        // Ailment damage is a separate ordinary roll from the pre-hit range. Hit luck and the hit's critical
        // multiplier cannot alter its distribution; non-damaging ailments still use the actual mitigated hit.
        int fire = critical ? baseHit[DamageType.Fire].Roll(conn.IncomingHitRandom) : 0;
        var ailmentBase = DamagePacket.Of(DamageType.Fire, fire, fire);
        conn.ElementalAilments.Apply(ailmentBase, taken, critical, conn.PlayerMaxLife, now, Get, conn.IncomingHitRandom,
            source is null ? null : () => IncomingMarkDamageFactor(conn, sourceId, source));
        if (!frozen && conn.ElementalAilments.ActionFactor(now) == 0)
        {
            conn.FreezeGeneration++;
            Interlocked.Increment(ref conn.ActionCount);
            conn.ChannelSkill = 0;
            var at = conn.Walk.Position(now); conn.Walk.Teleport(at.X, at.Y, now);
        }
        if (previousAction != conn.ElementalAilments.ActionFactor(now)) conn.ApplyMovementSpeed();
    }

    internal static int AdvancePlayerAilmentsLocked(ClientState conn, double from, double until,
        IReadOnlyDictionary<uint, int> stats)
    {
        lock (conn.Monsters) return AdvancePlayerAilmentsCore(conn, from, until, stats);
    }

    static int AdvancePlayerAilmentsCore(ClientState conn, double from, double until,
        IReadOnlyDictionary<uint, int> stats)
    {
        if (conn.Closed || conn.LeavingArea || conn.PlayerLife == 0) { conn.ElementalAilments.Clear(); conn.AilmentBypassCarry = 0; return 0; }
        int bleeding = AdvancePlayerBleedingLocked(conn, from, until, stats);
        if (!conn.ElementalAilments.HasEffects || conn.PlayerLife == 0) return bleeding;
        double previousAction = conn.ElementalAilments.ActionFactor(from);
        int Get(string name) => Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
        int damage = conn.ElementalAilments.Advance(from, until, Get, Resistance(conn, DamageType.Fire),
            MaxResistance(conn, DamageType.Fire), PlayerImmunities(conn).Contains(DamageType.Fire),
            DeadeyeGaleIncoming(conn, acceptedHit: false));
        int lifeDamage = HarbingerDefences.DamageOverTime(conn.EnergyShield, damage,
            Get("base_non_chaos_damage_bypass_energy_shield_%"), until, ref conn.AilmentBypassCarry);
        conn.PlayerLife = (uint)Math.Max(0, (long)conn.PlayerLife - lifeDamage);
        if (conn.PlayerLife == 0)
        {
            conn.InvalidateIncomingHits();
            // Commit the mode outcome inside lethal damage, before disconnects or network writes can intervene.
            RecordHardcoreDeath(conn);
        }
        if (previousAction != conn.ElementalAilments.ActionFactor(until)) conn.ApplyMovementSpeed();
        return damage + bleeding;
    }

    internal static bool PlayerActionBlocked(ClientState conn, double requestedAt, double now) =>
        conn.HitRecovery.BlocksInput(requestedAt, now) || conn.ElementalAilments.ActionFactor(now) == 0;

    internal static double PlayerActionFactor(ClientState conn)
    {
        lock (conn.Monsters)
        {
            if (Stats.Key(ActionSpeed.Virtual) is uint key && conn.PlayerStats.TryGetValue(key, out int final))
                return Math.Max(.01, 1 + final / 100d);
            return Math.Max(.01, PlayerRawActionSpeed(conn).Factor * PlayerAilmentActionFactor(conn));
        }
    }

    // Weapon/skill timing already consumes the raw action multiplier. Only the separate ailment clock remains.
    internal static double PlayerAilmentActionFactor(ClientState conn) =>
        Stats.Key(ActionSpeed.Virtual) is uint key && conn.PlayerStats.ContainsKey(key) ? 1
            : Math.Max(.01, conn.ElementalAilments.ActionFactor(conn.Clock.Elapsed.TotalSeconds));

    internal static double NativePlayerActionFactor(ClientState conn, IReadOnlyDictionary<uint, int> stats) =>
        ActionSpeed.Multiplier(stats, Stats.Key)
            * (Stats.Key(ActionSpeed.Virtual) is uint key && conn.PlayerStats.ContainsKey(key) ? 1
                : conn.ElementalAilments.ActionFactor(conn.Clock.Elapsed.TotalSeconds));

    internal static ActionSpeed PlayerRawActionSpeed(ClientState conn)
    {
        lock (conn.Monsters)
        {
            var raw = conn.Character is null ? new Dictionary<uint, int>() : CharacterStatSources(conn).Stats;
            int Get(string name) => Stats.Key(name) is uint key
                ? conn.PlayerStats.TryGetValue(key, out int replacement) ? replacement : raw.GetValueOrDefault(key) : 0;
            return ActionSpeed.From(Get);
        }
    }

    internal static double PlayerMovementFactor(ClientState conn) =>
        (1 + PlayerRawActionSpeed(conn).MovementPercent(conn.Pools.MovementVelocityPct) / 100d) * PlayerActionFactor(conn);

    internal static double IncomingMarkDamageFactor(ClientState conn, uint sourceId, SpawnedMonster source) =>
        conn.ProjectileRuntime is { Active: true } session
        && conn.Monsters.TryGetValue(sourceId, out var current) && ReferenceEquals(source, current)
            ? DeadeyeMarkIncoming(session, sourceId, source) : 1;
}
