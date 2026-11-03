using GameServer.Core.World;
using GameServer.Core.Protocol.Instance;

static partial class Game
{
    static readonly string[] InquisitorGroundKeys = [InquisitorRecipients.EnemyTaken, InquisitorRecipients.Mirror,
        FlaskGrounds.LingerStat, FlaskGrounds.ProfaneLingerStat, "consecrated_ground_while_stationary_radius",
        "consecrated_ground_while_stationary_radius_if_highest_attribute_is_strength",
        "consecrated_ground_while_stationary_radius_if_2_crusader_items", "strength", "dexterity", "intelligence",
        "number_of_equipped_crusader_items"];

    internal static void InquisitorMotion(ClientState conn, double at, bool after)
    {
        lock (conn.Monsters)
        {
            if (!CurrentGameWork || conn.Closed || conn.LeavingArea || conn.RageRetired) return;
            InquisitorGroundStep(conn, at);
        }
    }

    internal static void InquisitorGroundSources(ClientState conn, IReadOnlyDictionary<uint, int> stats)
    {
        lock (conn.Monsters)
        {
            if (!CurrentGameWork || conn.Closed || conn.LeavingArea || conn.RageRetired || conn.InquisitorRetired) return;
            double now = InquisitorNow(conn);
            InquisitorSettleMirror(conn, now);
            var sources = InquisitorGroundKeys.ToDictionary(n => n,
                n => Stats.Key(n) is uint k ? stats.GetValueOrDefault(k) : 0);
            bool changed = !sources.OrderBy(p => p.Key).SequenceEqual(conn.InquisitorGroundSources.OrderBy(p => p.Key));
            conn.InquisitorGroundSources = sources;
            conn.FlaskGrounds.Configure(FlaskGrounds.StationaryRadius(n => sources.GetValueOrDefault(n)),
                sources[FlaskGrounds.LingerStat], sources[FlaskGrounds.ProfaneLingerStat]);
            if (changed) conn.InquisitorWasMoving = false;
            InquisitorGroundStep(conn, now);
        }
    }

    internal static void InquisitorGroundStep(ClientState conn, double now)
    {
        if (!CurrentGameWork || conn.Closed || conn.LeavingArea || conn.RageRetired || conn.InquisitorRetired) return;
        int Get(string n) => conn.InquisitorGroundSources.GetValueOrDefault(n);
        bool alive = conn.PlayerLife > 0, moving = conn.Walk.Moving(now);
        double transition = alive && !moving && conn.InquisitorWasMoving
            && conn.Walk.ArrivesAt >= conn.InquisitorGroundObservedAt && conn.Walk.ArrivesAt <= now
                ? conn.Walk.ArrivesAt : now;
        var state = conn.FlaskGrounds;
        InquisitorSettleMirror(conn, transition);
        var here = conn.Walk.Position(now);
        var pending = Automation(conn);
        var (stale, make) = state.Stationary(alive && !moving && !InTown(conn), here);
        if (stale?.Object is uint old) pending.Packets.Add(InstanceProtocol.RemoveObject(old));
        if (make > 0) state.PlaceStationary(new(FlaskGrounds.Kind.ConsecratedGround, here.X, here.Y, make,
            double.PositiveInfinity, QueueGroundArtLocked(conn, FlaskGrounds.Kind.ConsecratedGround, here, make)));
        double linger = Math.Max(0, Get(FlaskGrounds.LingerStat)) / 1000d;
        bool covered = alive && state.Covers(FlaskGrounds.Kind.ConsecratedGround, here);
        if (covered && !conn.InquisitorOwnerInside && transition >= conn.InquisitorOwnerUntil)
            conn.InquisitorOwnerEntered = transition;
        if (covered) conn.InquisitorOwnerUntil = double.PositiveInfinity;
        else if (conn.InquisitorOwnerInside) conn.InquisitorOwnerUntil = transition + linger;
        else if (linger == 0) conn.InquisitorOwnerUntil = double.NegativeInfinity;
        conn.InquisitorOwnerInside = covered;
        bool genericOn = state.OnConsecrated(alive, here, now);
        bool on = alive && (covered || now < conn.InquisitorOwnerUntil || linger == 0 && genericOn);
        if (state.SetConsecrated(on, () => conn.NextBuff++) is { } change)
        {
            pending.Packets.Add(change.Added
                ? InstanceEntities.AddBuff(PlayerId, change.Buff, FlaskGrounds.ConsecratedBuffRow, PlayerId)
                : InstanceEntities.RemoveBuff(PlayerId, change.Buff));
            pending.StatRevision++;
        }
        foreach (var (id, m) in conn.Monsters)
        {
            m.InquisitorOwner = new(conn);
            bool inside = alive && m is { Dead: false, Life: > 0, Hostile: true }
                && state.Covers(FlaskGrounds.Kind.ConsecratedGround, m.Walk.Position(now));
            if (inside) { conn.InquisitorEnemyInside.Add(id); conn.InquisitorEnemyUntil[id] = double.PositiveInfinity; }
            else if (conn.InquisitorEnemyInside.Remove(id)) conn.InquisitorEnemyUntil[id] = transition + linger;
            if (!alive || !m.Hostile || m.Dead || m.Life == 0 || Get(InquisitorRecipients.EnemyTaken) == 0)
                conn.InquisitorEnemyUntil.Remove(id);
        }
        conn.InquisitorGroundObservedAt = now; conn.InquisitorWasMoving = moving;
        if (!alive) ClearInquisitor(conn);
    }

    internal static int InquisitorTaken(ClientState conn, SpawnedMonster m, double now, bool elemental)
    {
        if (conn.PlayerLife == 0 || m.Dead || m.Life == 0 || !m.Hostile) return 0;
        InquisitorGroundStep(conn, now);
        var entry = conn.Monsters.FirstOrDefault(p => ReferenceEquals(p.Value, m));
        int sanctuary = entry.Value is not null && conn.InquisitorEnemyUntil.GetValueOrDefault(entry.Key, double.NegativeInfinity) > now
            ? conn.InquisitorGroundSources.GetValueOrDefault(InquisitorRecipients.EnemyTaken) : 0;
        int augury = elemental ? InquisitorNearby.AuguryIncrease(conn.MainAscendancy.Allocated.Contains((ushort)40059),
            LeagueFor(conn).HardMode, true, Dist(conn.Walk.Position(now), m.Walk.Position(now))) : 0;
        return sanctuary + augury;
    }

    internal static bool InquisitorAugury(ClientState conn, SpawnedMonster m, double now) => conn.PlayerLife > 0
        && conn.MainAscendancy.Allocated.Contains((ushort)40059) && m is { Hostile: true, Dead: false, Life: > 0 }
        && Dist(conn.Walk.Position(now), m.Walk.Position(now)) <= 60;

    internal static void InquisitorSettleMirror(ClientState conn, double now)
    {
        double until = Math.Min(now, Math.Min(conn.InquisitorOwnerUntil, conn.InquisitorMirrorUntil));
        foreach (var segment in conn.InquisitorMirrorSegments)
            conn.InquisitorMirrorPending += Math.Max(0, Math.Min(until, segment[1])
                - Math.Max(segment[0], Math.Max(conn.InquisitorMirrorAt, conn.InquisitorOwnerEntered))) * segment[2];
        conn.InquisitorMirrorAt = Math.Max(conn.InquisitorMirrorAt, now);
    }

    internal static double InquisitorMirrorRate(ClientState conn, Func<string, int> get)
    {
        if (get(InquisitorRecipients.Mirror) == 0 || get("no_life_regeneration") != 0
            || get("cannot_recover_energy_shield") != 0 || conn.PlayerLife == 0) return 0;
        var moving = PanelStats.WhileMoving(get, conn.Walk.Moving(InquisitorNow(conn)));
        int Raw(string n) => n is "life_recovery_rate_+%" or "cannot_recover_life" ? 0 : moving(n);
        return PanelStats.LifePerMinute(Raw, conn.PlayerMaxLife) / 60
            * Math.Max(0, 1 + get("energy_shield_recovery_rate_+%") / 100d);
    }

    internal static void InquisitorRecoverySources(ClientState conn, Func<string, int> get)
    {
        if (!CurrentGameWork || conn.Closed || conn.LeavingArea || conn.RageRetired || conn.InquisitorRetired) return;
        double now = InquisitorNow(conn);
        InquisitorSettleMirror(conn, now);
        conn.InquisitorMirrorRate = InquisitorMirrorRate(conn, get);
        conn.InquisitorMirrorUntil = double.PositiveInfinity;
        var fort = FortificationOf(conn);
        var grants = fort.State.GrantSnapshot().Select(g => new FortificationState.Carried(g.Count,
            Math.Max(0, g.ExpiresAt - conn.Clock.Elapsed.TotalSeconds))).ToArray();
        double cry = conn.EnduringCryRecovery.Rate(now), turtle = conn.Antiquarian.RecoveryPerSecond(now);
        double cryUntil = conn.EnduringCryRecovery.Until, turtleUntil = conn.Antiquarian.RecoveryUntil(now);
        int currentFort = fort.Value.LifeRegenerationPerMinutePercent(fort.RegenPerStack);
        bool replacedFort = Stats.Key(FortificationSession.Regeneration) is uint key && conn.PlayerStats.ContainsKey(key);
        var edges = grants.Select(g => now + g.Remaining).Concat([cryUntil, turtleUntil, now, double.PositiveInfinity])
            .Where(at => at >= now).Distinct().Order().ToArray();
        conn.InquisitorMirrorSegments.Clear();
        for (int i = 0; i + 1 < edges.Length; i++)
        {
            double at = edges[i];
            int count = Math.Min(fort.Value.Maximum, grants.Where(g => now + g.Remaining > at).Sum(g => g.Count));
            int Segment(string name) => name switch
            {
                "base_life_regeneration_rate_per_minute" => (int)Math.Clamp(get(name) - (cry + turtle) * 60
                    + ((at < cryUntil ? cry : 0) + (at < turtleUntil ? turtle : 0)) * 60, int.MinValue, int.MaxValue),
                "life_regeneration_rate_per_minute_%" when !replacedFort => get(name) - currentFort + count * fort.RegenPerStack,
                _ => get(name),
            };
            conn.InquisitorMirrorSegments.Add([at, edges[i + 1], InquisitorMirrorRate(conn, Segment)]);
        }
    }
}
