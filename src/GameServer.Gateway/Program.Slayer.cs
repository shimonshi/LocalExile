using GameServer.Core.World;
using System.Collections.Concurrent;
using GameServer.Core.Protocol.Instance;

partial class Program
{
    static readonly ConcurrentDictionary<uint, (string Character, DateTime At, SlayerKillState.Carried State)> CarriedSlayer = new();
    internal static SlayerKillState.Snapshot SlayerSnapshot(ClientState conn, Func<string, int> get)
    {
        lock (conn.Monsters)
        {
            if (conn.Closed || conn.RageRetired || conn.PlayerLife == 0) { conn.SlayerKills.Clear(); return default; }
            return conn.SlayerKills.Read(conn.SlayerFrozenAt ?? ProjectileWorldTime(conn), get);
        }
    }

    static Dictionary<uint, int> WithSlayer(ClientState conn, Dictionary<uint, int> sources)
    {
        lock (conn.Monsters)
        {
            int Get(string name) => Stats.Key(name) is uint k
                ? conn.PlayerStats.TryGetValue(k, out int value) ? value : sources.GetValueOrDefault(k) : 0;
            var value = SlayerSnapshot(conn, Get);
            foreach (var (name, added) in new[] { ("base_movement_velocity_+%", value.MovementSpeedIncrease),
                ("attack_speed_+%", value.AttackSpeedIncrease), ("base_skill_area_of_effect_+%", value.AreaIncrease) })
                if (Stats.Key(name) is uint key) sources[key] = (int)Math.Clamp((long)sources.GetValueOrDefault(key)
                    + added, int.MinValue, int.MaxValue);
            return sources;
        }
    }

    internal static double SlayerTargetMore(ClientState conn, SpawnedMonster target,
        IReadOnlyDictionary<uint, int> sources, bool melee = false)
    {
        int Get(string name) => Stats.Key(name) is uint k ? sources.GetValueOrDefault(k) : 0;
        double more = SlayerRules.DamageMultiplier(Get, target.Rarity == MonsterRarity.Unique,
            SlayerSnapshot(conn, Get).KilledRecently);
        if (melee)
        {
            double now = ProjectileWorldTime(conn);
            // Pinned PoB MeleeProximity: maximum through 15 units, linear to zero at 40.
            double distance = Dist(conn.Walk.Position(now), target.Walk.Position(now));
            more *= 1 + Get("slayer_damage_+%_final_from_distance") / 100.0 * Math.Clamp((40 - distance) / 25, 0, 1);
        }
        return Math.Max(0, more);
    }

    internal static int SlayerCriticalMultiplier(ClientState conn, int multiplier,
        IReadOnlyDictionary<uint, int> sources)
    {
        int Get(string name) => Stats.Key(name) is uint k ? sources.GetValueOrDefault(k) : 0;
        if (Get(SlayerRules.NearbyCriticalMultiplier) == 0) return multiplier;
        double now = ProjectileWorldTime(conn);
        var at = conn.Walk.Position(now);
        // LocalExile policy: 70 cells centre-to-centre, hostile living enemies only.
        int count = conn.Monsters.Values.Count(m => m.Hostile && !m.Dead && m.Life > 0
            && m.OwnerId is null && !m.Daemon && Dist(at, m.Walk.Position(now)) <= 70);
        return (int)Math.Clamp((long)multiplier + SlayerRules.CriticalMultiplierBonus(Get, count), 0, int.MaxValue);
    }

    internal static int SlayerMeleeRange(ClientState conn, int range)
    {
        if (!WeaponProfileFor(conn, activeSet: true).Melee) return range;
        return Math.Max(0, range + (Stats.Key("melee_range_+") is uint key
            ? CharacterPlayerStats(conn).GetValueOrDefault(key) : 0));
    }

    static void RefreshSlayerPools(ClientState conn)
    {
        var updated = PoolsFor(conn, conn.Pools.Level);
        conn.Pools = conn.Pools with { AttackSpeedPct = updated.AttackSpeedPct,
            MovementVelocityPct = updated.MovementVelocityPct };
        conn.ApplyMovementSpeed();
    }

    static void CarrySlayer(ClientState conn, uint ticket)
    {
        lock (conn.Monsters)
        {
            if (conn.PlayerLife > 0) CarriedSlayer[ticket] = (CharacterName(conn), DateTime.UtcNow,
                conn.SlayerKills.Carry(conn.SlayerFrozenAt ?? ProjectileWorldTime(conn)));
            conn.SlayerKills.Clear();
        }
        foreach (var (old, value) in CarriedSlayer)
            if (DateTime.UtcNow - value.At > TimeSpan.FromMinutes(5)) CarriedSlayer.TryRemove(old, out _);
    }

    internal static void RestoreSlayer(ClientState conn)
    {
        lock (conn.Monsters)
            if (CarriedSlayer.TryGetValue(conn.Ticket, out var value)
                && value.Character.Equals(CharacterName(conn), StringComparison.OrdinalIgnoreCase)
                && CarriedSlayer.TryRemove(new KeyValuePair<uint, (string, DateTime, SlayerKillState.Carried)>(conn.Ticket, value))
                && DateTime.UtcNow - value.At <= TimeSpan.FromMinutes(5))
                conn.SlayerKills.Restore(value.State, ProjectileWorldTime(conn));
    }
    // Only admitted player skill hits carry this context: never owned actors, DoT or reflection.
    // Total damage may include Impale; DirectDamage and Taken never include it or culled Life.
    internal readonly record struct SkillDamageAttribution(bool CountsAsYourSkill, bool KillCreditToOwner,
        bool AllowCull, bool GrantOwnerLeech, bool GrantOwnerRage, bool GrantOwnerOnHit,
        bool GrantOwnerChargeOnHit, bool RecursiveMirage)
    {
        public static readonly SkillDamageAttribution Player = new(true, true, true, true, true, true, true, true);
        public static readonly SkillDamageAttribution Mirage = new(true, true, true, false, false, false, false, false);
        public static readonly SkillDamageAttribution IndependentActor = default;
    }
    internal sealed record OwnerSkillHit(int DirectDamage, DamagePacket Taken, LeechSources Leech,
        IReadOnlyDictionary<uint, int> Sources)
    {
        public SkillDamageAttribution Attribution { get; init; } = SkillDamageAttribution.Player;
        public bool Melee { get; init; }
        public bool Attack { get; init; }
    }

    static (uint Life, bool StageStarts) TakeOwnerDamageLocked(ClientState conn, SpawnedMonster target,
        int damage, OwnerSkillHit? hit = null, SkillDamageAttribution? attribution = null)
    {
        uint before = target.Life;
        var result = TakeDamageLocked(target, damage);
        var sources = hit?.Sources ?? CharacterPlayerStats(conn);
        var origin = attribution ?? hit?.Attribution ?? SkillDamageAttribution.Player;
        int Get(string name) => Stats.Key(name) is uint key ? sources.GetValueOrDefault(key) : 0;
        bool eligible = target.Hostile && target.OwnerId is null && !target.Daemon
            && !result.StageStarts && !target.StageRunning;
        // Resolve interception first; grant before assigning post-hit Life so the existing target gate still applies.
        // One session.Hit handles ordinary + overkill, target prohibitions, conversion, instant recovery and caps.
        if (hit is not null && origin.GrantOwnerLeech) LeechOnHitLocked(conn, target, hit.Taken, hit.DirectDamage, hit.Leech,
            eligible ? SlayerRules.OverkillLifeLeech(hit.DirectDamage, before, Get) : 0);
        if (hit is not null && origin.AllowCull && eligible && SlayerRules.Culls(result.Life, target.MaxLife, Get)) result.Life = 0;
        if (before > 0 && result.Life == 0 && !target.Dead && eligible && target.OwnerId is null && !target.Daemon
            && origin.KillCreditToOwner
            && conn.PlayerLife > 0 && !conn.Closed && !conn.LeavingArea && !conn.RageRetired)
        {
            var prior = SlayerSnapshot(conn, Get);
            conn.SlayerKills.RecordKill(ProjectileWorldTime(conn), target.Rarity is MonsterRarity.Rare or MonsterRarity.Unique, Get);
            var current = SlayerSnapshot(conn, Get);
            if (prior.MovementSpeedIncrease != current.MovementSpeedIncrease || prior.AttackSpeedIncrease != current.AttackSpeedIncrease)
                RefreshSlayerPools(conn);
        }
        if (before > 0 && result.Life == 0 && !target.Dead && eligible
            && conn.ProjectileRuntime is { } session && DeadeyeMarkOf(conn).Active is { } mark
            && ReferenceEquals(mark.Monster, target))
            DeadeyeMarkDeath(session, mark.Target, target, Random.Shared, origin.KillCreditToOwner);
        return result;
    }

    static double PlayerDamageTakenFactor(ClientState conn, double now, IReadOnlyDictionary<uint, int> stats,
        int increasedTaken = 0) =>
        conn.ElementalAilments.DamageTakenFactor(now, (int)Math.Clamp((long)increasedTaken
            + (Stats.Key("base_damage_taken_+%") is uint key ? stats.GetValueOrDefault(key) : 0),
            int.MinValue, int.MaxValue)) * DeadeyeGaleIncoming(conn, acceptedHit: false);
}
