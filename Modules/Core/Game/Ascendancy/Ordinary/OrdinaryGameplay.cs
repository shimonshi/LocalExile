using System.Runtime.CompilerServices;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

static partial class Game
{
    static readonly ConditionalWeakTable<ClientState, OrdinaryResidualSession> OrdinarySessions = new();

    // Only neutral JSON lives on the host. A replacement Game restores clocks at their original epoch.
    internal static OrdinaryResidualSession OrdinaryOf(ClientState conn, Func<string, int> get)
    {
        double now = ProjectileWorldTime(conn);
        var state = OrdinarySessions.GetValue(conn, _ => conn.OrdinaryResidualPayload is { } payload
            ? OrdinaryResidualSession.Restore(payload, 1, now, OrdinaryGathering(conn), get) : new(1));
        if (conn.PlayerLife == 0) { state.Slayer.Clear(); state.Gale.Clear(); state.Marks.Clear(); state.Mirages.Clear(); }
        return state;
    }

    static bool OrdinaryGathering(ClientState conn) => PassiveBuffAuras.For(
        [.. conn.Passives, .. conn.MainAscendancy.Allocated], LeagueFor(conn).HardMode).Any(a => a.Row == 44);

    internal static int OrdinaryAccuracy(ClientState conn, int rating, IReadOnlyDictionary<uint, int>? player)
    {
        player ??= CharacterStatSources(conn).Stats;
        int Get(string name) => Stats.Key(name) is uint key ? player.GetValueOrDefault(key) : 0;
        return (int)Math.Clamp(Math.Floor(rating * Math.Max(0,
            1 + (long)Get("deadeye_accuracy_rating_+%_final_per_frenzy_charge")
                * ChargesOf(conn).Effective(ChargeType.Frenzy) / 100d)), 0, int.MaxValue);
    }

    internal static int OrdinaryEnemyCriticalReduction(ClientState conn, SpawnedMonster source, double now) =>
        -SlayerRules.EnemyCriticalMultiplierDelta(PassiveBuffAuras.For(
            [.. conn.Passives, .. conn.MainAscendancy.Allocated], LeagueFor(conn).HardMode).Any(a => a.Row == 39),
            source.Hostile && !source.Dead && source.Life > 0,
            Dist(conn.Walk.Position(now), source.Walk.Position(now)));

    internal static bool RefreshOrdinaryStatsLocked(ClientState conn)
    {
        _ = CharacterStatSources(conn);
        int tailwind = OrdinaryTailwindSpeed(conn);
        if (tailwind != conn.OrdinaryTailwindObserved)
        { conn.OrdinaryTailwindObserved = tailwind; conn.OrdinaryStatsDirty = true; }
        if (!conn.OrdinaryStatsDirty) return false;
        conn.Pools = PoolsFor(conn, conn.Pools.Level);
        conn.ApplyMovementSpeed();
        conn.OrdinaryStatsDirty = false;
        return true;
    }

    internal static void OrdinaryAcceptedUse(ClientState conn, long action)
    {
        if (!CurrentGameWork || conn.Closed || conn.LeavingArea || conn.PlayerLife == 0) return;
        var player = CharacterStatSources(conn).Stats;
        int Get(string name) => Stats.Key(name) is uint key ? player.GetValueOrDefault(key) : 0;
        double now = ProjectileWorldTime(conn); var state = OrdinaryOf(conn, Get);
        state.Gale.Observe(now, OrdinaryGathering(conn), Get);
        state.Use(now, new(conn.ProjectileEpoch, action, true, true));
        conn.OrdinaryResidualPayload = state.Capture(now);
    }

    internal static int OrdinaryTailwindSpeed(ClientState conn)
    {
        lock (conn.Monsters)
        {
            var player = CharacterStatSources(conn).Stats;
            int Get(string name) => Stats.Key(name) is uint key ? player.GetValueOrDefault(key) : 0;
            double now = ProjectileWorldTime(conn); var state = OrdinaryOf(conn, Get);
            state.Gale.Observe(now, OrdinaryGathering(conn), Get, conn.PlayerLife > 0);
            conn.OrdinaryResidualPayload = state.Capture(now);
            return OrdinaryTailwindReceipt(conn, state.Gale, Get).Amount;
        }
    }

    // Presence is the supported same-instance allied cohort. It has no party membership or hostile-player mode.
    // Publish only the owner's qualified source flag; never enter a peer's combat lock or retain its Game objects.
    internal static (uint Source, int Amount) OrdinaryTailwindReceipt(ClientState conn, DeadeyeGaleState gale,
        Func<string, int> get)
    {
        conn.OrdinarySharesTailwind = gale.Tailwind;
        if (conn.PlayerLife == 0 || conn.Closed || conn.LeavingArea) return default;
        var here = conn.Walk.Position(ProjectileWorldTime(conn));
        uint source = gale.Tailwind ? PlayerId : 0;
        if (source == 0 && conn.Instance is { } instance)
            source = instance.PlayersExcept(conn.PeerId)
                .Where(p => p.Current && p.Life > 0 && p.Owner is { OrdinarySharesTailwind: true } owner
                    && owner.PlayerLife > 0 && ReferenceEquals(owner.Instance?.Presence, instance.Presence)
                    && Dist(here, (p.X, p.Y)) <= DeadeyeGaleAura.Radius)
                .OrderBy(p => p.Id).Select(p => p.Id).FirstOrDefault();
        return source == 0 ? default : (source, (int)Math.Clamp(Math.Floor(8 * Math.Max(0,
            1 + ((long)get("tailwind_effect_+%") + (source == PlayerId ? gale.TailwindEffect : 0)) / 100d)), 0, int.MaxValue));
    }

    internal static double OrdinaryIncomingHit(ClientState conn, IReadOnlyDictionary<uint, int> stats, double now)
    {
        int Get(string name) => Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
        var state = OrdinaryOf(conn, Get);
        state.Gale.Observe(now, OrdinaryGathering(conn), Get, conn.PlayerLife > 0);
        double factor = state.Gale.IncomingHit(now, true);
        conn.OrdinaryResidualPayload = state.Capture(now);
        return factor;
    }

    internal static void ClearOrdinary(ClientState conn)
    {
        conn.OrdinarySharesTailwind = false;
        MarkSessions.Remove(conn);
        OrdinarySessions.Remove(conn); conn.OrdinaryResidualPayload = null;
    }

    internal static void CarryOrdinary(ClientState conn, uint ticket)
    {
        lock (conn.Monsters)
        {
            if (conn.PlayerLife == 0) { ClearOrdinary(conn); return; }
            var player = CharacterStatSources(conn).Stats;
            int Get(string name) => Stats.Key(name) is uint key ? player.GetValueOrDefault(key) : 0;
            double now = conn.RampageTransferAt ?? ProjectileWorldTime(conn);
            string payload = OrdinaryOf(conn, Get).Capture(now);
            foreach (var (key, old) in OrdinaryResidualTransfers.Tickets)
                if (DateTime.UtcNow - old.Created > TimeSpan.FromMinutes(5)) OrdinaryResidualTransfers.Tickets.TryRemove(key, out _);
            OrdinaryResidualTransfers.Tickets.TryAdd(ticket, new(CharacterName(conn), payload, now, DateTime.UtcNow));
        }
    }

    internal static void RestoreOrdinary(ClientState conn)
    {
        lock (conn.Monsters)
        {
            if (conn.PlayerLife == 0 || !OrdinaryResidualTransfers.Tickets.TryGetValue(conn.Ticket, out var carry)
                || carry.Character != CharacterName(conn)) return;
            if (!OrdinaryResidualTransfers.Tickets.TryRemove(new KeyValuePair<uint, OrdinaryResidualTransfers.Carry>(conn.Ticket, carry))) return;
            if (DateTime.UtcNow - carry.Created > TimeSpan.FromMinutes(5)) return;
            OrdinarySessions.Remove(conn);
            conn.OrdinaryResidualPayload = OrdinaryResidualSession.RebaseTransfer(carry.Payload, carry.At, ProjectileWorldTime(conn));
        }
    }

    // Called only from the existing current charge sender's cipher gate, under Monsters.
    internal static byte[] OrdinaryPresentation(ClientState conn)
    {
        if (!CurrentGameWork || conn.Closed || conn.LeavingArea) return [];
        var player = CharacterStatSources(conn).Stats;
        int Get(string name) => Stats.Key(name) is uint key ? player.GetValueOrDefault(key) : 0;
        double now = ProjectileWorldTime(conn); var state = OrdinaryOf(conn, Get);
        state.Gale.Observe(now, OrdinaryGathering(conn), Get, conn.PlayerLife > 0);
        var buffs = state.Slayer.ReadBuffs(now, Get); var bytes = new List<byte>();
        void Sync(SlayerKillState.BuffGrant? grant, ushort row, ref ushort shown, ref double deadline)
        {
            double next = grant?.Deadline ?? 0;
            if (next == deadline && (grant is null) == (shown == 0)) return;
            if (shown != 0) bytes.AddRange(InstanceEntities.RemoveBuff(PlayerId, shown));
            shown = 0; deadline = next;
            if (grant is { } g)
            {
                shown = conn.NextBuff++;
                bytes.AddRange(InstanceEntities.AddBuff(PlayerId, shown, row, PlayerId,
                    u68: BitConverter.SingleToUInt32Bits((float)g.Remaining), f6c: (float)g.Rate));
            }
        }
        Sync(buffs.Movement, 1341, ref conn.OrdinaryMovementBuff, ref conn.OrdinaryMovementDeadline);
        Sync(buffs.Attack, 1342, ref conn.OrdinaryAttackBuff, ref conn.OrdinaryAttackDeadline);
        var receipt = OrdinaryTailwindReceipt(conn, state.Gale, Get);
        if (receipt.Source != conn.OrdinaryTailwindSource || receipt.Amount != conn.OrdinaryTailwindAmount)
        {
            if (conn.OrdinaryTailwindBuff != 0) bytes.AddRange(InstanceEntities.RemoveBuff(PlayerId, conn.OrdinaryTailwindBuff));
            conn.OrdinaryTailwindSource = receipt.Source; conn.OrdinaryTailwindAmount = receipt.Amount;
            conn.OrdinaryTailwindBuff = receipt.Source != 0 ? conn.NextBuff++ : (ushort)0;
            if (receipt.Source != 0) bytes.AddRange(InstanceEntities.AddBuff(PlayerId, conn.OrdinaryTailwindBuff,
                1094, receipt.Source, values: [unchecked((uint)-receipt.Amount)]));
        }
        // Each Gale grant has its own recipient clock. Do not give the oldest nine the newest stack's deadline.
        double[] deadlines = state.Gale.Stacks.Select(s => s.Deadline).ToArray();
        double rate = state.Gale.Stacks.FirstOrDefault()?.Rate ?? 1;
        if (!deadlines.AsSpan().SequenceEqual(conn.OrdinaryGaleDeadlines) || rate != conn.OrdinaryGaleRate)
        {
            foreach (ushort buff in conn.OrdinaryGaleBuffs) bytes.AddRange(InstanceEntities.RemoveBuff(PlayerId, buff));
            conn.OrdinaryGaleBuffs = new ushort[deadlines.Length];
            conn.OrdinaryGaleDeadlines = deadlines; conn.OrdinaryGaleRate = rate;
            for (int i = 0; i < deadlines.Length; i++)
            {
                ushort buff = conn.OrdinaryGaleBuffs[i] = conn.NextBuff++;
                var clock = state.Gale.Stacks[i];
                bytes.AddRange(InstanceEntities.AddBuff(PlayerId, buff, 1095, PlayerId,
                    u68: BitConverter.SingleToUInt32Bits((float)clock.Remaining), f6c: (float)clock.Rate, stacks: 1));
            }
        }
        conn.OrdinaryResidualPayload = state.Capture(now);
        return [.. bytes, .. MarkPresentation(conn)];
    }

    internal static void CommitOrdinaryKillLocked(ClientState conn, SpawnedMonster target,
        IReadOnlyDictionary<uint, int>? player)
    {
        if (!CurrentGameWork || !target.Hostile || conn.PlayerLife == 0) return;
        player ??= CharacterStatSources(conn).Stats;
        int Get(string name) => Stats.Key(name) is uint key ? player.GetValueOrDefault(key) : 0;
        var state = OrdinaryOf(conn, Get);
        state.Kill(ProjectileWorldTime(conn), new(conn.ProjectileEpoch, ++conn.OrdinaryDeathSequence,
            OrdinaryResidualSession.Credit.OwnerDamageOverTime, true, false,
            target.Rarity is MonsterRarity.Rare or MonsterRarity.Unique, target.Life, 0), Get);
        conn.OrdinaryResidualPayload = state.Capture(ProjectileWorldTime(conn));
    }

    internal static double OrdinaryTargetMore(ClientState conn, SpawnedMonster target,
        IReadOnlyDictionary<uint, int> player)
    {
        int Get(string name) => Stats.Key(name) is uint key ? player.GetValueOrDefault(key) : 0;
        return SlayerRules.DamageMultiplier(Get, target.Rarity == MonsterRarity.Unique,
            OrdinaryOf(conn, Get).Slayer.Read(ProjectileWorldTime(conn), Get).KilledRecently);
    }

    internal static MonsterIgnites.Settings OrdinaryIgniteAt(ClientState conn, SpawnedMonster target,
        ProjectileOffense release, double distance, IReadOnlyDictionary<uint, int> player)
    {
        var ignite = release.OrdinaryDistance.IgniteAt(MarkIgnite(conn, target, release.Ignite, release.MarkSource), distance);
        double more = OrdinaryTargetMore(conn, target, player);
        return ignite with { Min = ignite.Min * more, Max = ignite.Max * more,
            CriticalMin = ignite.CriticalMin * more, CriticalMax = ignite.CriticalMax * more };
    }

    internal static int OrdinaryCriticalBonus(ClientState conn, IReadOnlyDictionary<uint, int> player)
    {
        int Get(string name) => Stats.Key(name) is uint key ? player.GetValueOrDefault(key) : 0;
        if (Get(SlayerRules.NearbyCriticalMultiplier) == 0) return 0;
        double now = ProjectileWorldTime(conn); var here = conn.Walk.Position(now);
        return SlayerRules.CriticalMultiplierBonus(Get, conn.Monsters.Values.Count(m =>
            m.Hostile && !m.Dead && m.Life > 0 && Dist(here, m.Walk.Position(now)) <= 70));
    }

    internal static void WithOrdinaryGameplay(ClientState conn, Dictionary<uint, int> stats)
    {
        lock (conn.Monsters)
        {
            if (!CurrentGameWork) return;
            int Get(string name) => Stats.Key(name) is uint key
                ? conn.PlayerStats.TryGetValue(key, out int set) ? set : stats.GetValueOrDefault(key) : 0;
            double now = ProjectileWorldTime(conn);
            var state = OrdinaryOf(conn, Get);
            state.Gale.Observe(now, OrdinaryGathering(conn), Get, conn.PlayerLife > 0);
            conn.OrdinarySharesTailwind = state.Gale.Tailwind;
            var kill = state.Slayer.Read(now, Get);
            var leech = SlayerRules.WhileLeeching(Get, LeechOf(conn), now);
            var signature = (kill.MovementSpeedIncrease, kill.AttackSpeedIncrease + leech.AttackSpeedIncrease,
                kill.AreaIncrease, leech.DamageTakenIncrease, leech.CannotBeStunned, leech.UnaffectedByBleeding);
            if (conn.OrdinaryStats != signature) { conn.OrdinaryStats = signature; conn.OrdinaryStatsDirty = true; }
            void Add(string name, int value)
            {
                if (value != 0 && Stats.Key(name) is uint key) stats[key] = stats.GetValueOrDefault(key) + value;
            }
            Add("base_movement_velocity_+%", kill.MovementSpeedIncrease);
            Add("attack_speed_+%", kill.AttackSpeedIncrease + leech.AttackSpeedIncrease);
            Add("base_skill_area_of_effect_+%", kill.AreaIncrease);
            Add("damage_taken_+%", leech.DamageTakenIncrease);
            if (leech.CannotBeStunned && Stats.Key("cannot_be_stunned") is uint stun) stats[stun] = 1;
            if (leech.UnaffectedByBleeding && Stats.Key("unaffected_by_bleeding") is uint bleed) stats[bleed] = 1;
            conn.OrdinaryResidualPayload = state.Capture(now);
        }
    }
}
