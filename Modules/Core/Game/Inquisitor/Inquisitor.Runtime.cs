using GameServer.Core.World;
using GameServer.Core.Protocol.Instance;

static partial class Game
{
    internal static SkillCatalog.Unarmed? InquisitorMainHand(ClientState conn) =>
        ActiveMainHand(conn) is { } hand ? WeaponIn(conn, hand) : null;

    internal static int? InquisitorLocalCritical(ClientState conn) =>
        WeaponProfileFor(conn, activeSet: true) is { Unarmed: false } weapon ? weapon.Critical : null;

    static double InquisitorNow(ClientState conn) => conn.InquisitorLoading ? conn.InquisitorObservedAt
        : conn.LeavingArea && conn.RampageTransferAt is double frozen
        ? frozen : conn.ProjectileRuntimeState is { Active: true } runtime ? runtime.NowTicks / 120000d : ProjectileWorldTime(conn);

    internal static InquisitorCombatState.Snapshot InquisitorRead(ClientState conn,
        IReadOnlyDictionary<uint, int> stats, InquisitorCombatState.AcceptedUse? use = null)
    {
        lock (conn.Monsters)
        {
            if (conn.InquisitorRetired || conn.Closed || conn.RageRetired || !CurrentGameWork) return default;
            if (conn.PlayerLife == 0) { ClearInquisitor(conn); return default; }
            int Get(string name) => Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
            var inputs = InquisitorCombatState.Inputs.From(Get, InquisitorCombatState.BaseFanaticismSeconds);
            double now = InquisitorNow(conn);
            var state = new InquisitorCombatState();
            if (conn.InquisitorCombat.Count > 0)
                state.RestoreNeutral(conn.InquisitorCombat, conn.InquisitorObservedAt, inputs);
            if (use is { } accepted) state.Used(now, inputs, accepted);
            var result = state.Read(now, inputs);
            conn.InquisitorCombat = state.CaptureNeutral(now, inputs);
            conn.InquisitorObservedAt = now;
            return result;
        }
    }

    internal static void InquisitorApplyCurrent(ClientState conn, Dictionary<uint, int> stats)
    {
        var current = InquisitorRead(conn, stats);
        void Set(string name, int value) { if (Stats.Key(name) is uint key) stats[key] = value; }
        Set("current_fanaticism_charges", current.FanaticCharges);
        int effect = Stats.Key("buff_effect_on_self_+%") is uint buff ? stats.GetValueOrDefault(buff) : 0;
        var modifiers = InquisitorOffense.Fanaticism(current.Fanaticism(InquisitorNow(conn)), true, effect);
        Set("fanaticism_hand_cast_speed_+%_final", modifiers.CastSpeedMorePct);
        Set("fanaticism_hand_cast_mana_cost_+%", modifiers.CostMorePct);
        Set("fanaticism_hand_cast_area_of_effect_+%", modifiers.AreaIncreasePct);
    }

    internal static InquisitorOffense.FanaticismModifiers InquisitorModifiers(ClientState conn, int effect,
        IReadOnlyDictionary<uint, int>? snapshot = null)
    {
        var stats = snapshot ?? CharacterPlayerStats(conn);
        var value = InquisitorRead(conn, stats);
        int buff = Stats.Key("buff_effect_on_self_+%") is uint key ? stats.GetValueOrDefault(key) : 0;
        return InquisitorOffense.Fanaticism(value.Fanaticism(InquisitorNow(conn)),
            SkillsFor(conn).IsSpell(effect) && !SkillsFor(conn).IsAttack(effect)
                && (Stats.Key("is_not_hand_cast") is not uint proxy || stats.GetValueOrDefault(proxy) == 0)
                && (Stats.Key("is_totem") is not uint totem || stats.GetValueOrDefault(totem) == 0), buff);
    }

    internal static void InquisitorAcceptedUse(ClientState conn, int effect, bool repeat = false)
    {
        var stats = CharacterPlayerStats(conn);
        InquisitorRead(conn, stats, new(SkillsFor(conn).IsAttack(effect), SkillsFor(conn).IsSpell(effect),
            SkillsFor(conn).CastMs(effect) <= 0, false, repeat));
    }

    internal static int InquisitorSkillCost(int basis, IReadOnlyDictionary<uint, int> player,
        InquisitorOffense.FanaticismModifiers modifiers)
    {
        int Get(string n) => Stats.Key(n) is uint k ? player.GetValueOrDefault(k) : 0;
        return (int)Math.Clamp(decimal.Floor(Math.Max(0, basis) * Math.Max(0m, 100m - Get("base_mana_cost_-%"))
            * Math.Max(0m, 100m + Get("mana_cost_+%")) * modifiers.CostFactor / 10000m), 0, int.MaxValue);
    }

    internal static void ClearInquisitor(ClientState conn)
    {
        conn.ClearInquisitorState();
    }

    internal static void CarryInquisitor(ClientState conn, uint ticket)
    {
        lock (conn.Monsters)
        {
            if (conn.InquisitorRetired) return;
            InquisitorRead(conn, CharacterPlayerStats(conn));
            foreach (var pair in ClientState.InquisitorTickets.Where(p => p.Value.Created < DateTime.UtcNow.AddMinutes(-5)))
                ClientState.InquisitorTickets.TryRemove(pair);
            ClientState.InquisitorTickets.TryAdd(ticket, new(CharacterName(conn), DateTime.UtcNow, conn.InquisitorCombat));
            conn.InquisitorCombat = []; ClearInquisitor(conn); conn.InquisitorRetired = true;
        }
    }

    internal static void RestoreInquisitor(ClientState conn)
    {
        lock (conn.Monsters)
        {
            if (!ClientState.InquisitorTickets.TryGetValue(conn.Ticket, out var entry)
                || entry.Character != CharacterName(conn) || entry.Created < DateTime.UtcNow.AddMinutes(-5)
                || !ClientState.InquisitorTickets.TryRemove(new KeyValuePair<uint, ClientState.InquisitorTicket>(conn.Ticket, entry))) return;
            conn.InquisitorCombat = entry.Combat; conn.InquisitorObservedAt = InquisitorNow(conn);
            conn.InquisitorLoading = true;
        }
    }

    internal static void ActivateInquisitor(ClientState conn, double at)
    {
        if (!conn.InquisitorLoading) return;
        conn.InquisitorObservedAt = at; conn.InquisitorLoading = false;
    }

    internal static byte[] InquisitorPresentation(ClientState conn)
    {
        var value = InquisitorRead(conn, CharacterPlayerStats(conn));
        double now = InquisitorNow(conn), until = value.Fanaticism(now) ? value.FanaticismUntil : 0;
        var bytes = new List<byte>();
        if (conn.InquisitorShownUntil != until)
        {
            if (conn.InquisitorFanaticBuff != 0) bytes.AddRange(InstanceEntities.RemoveBuff(PlayerId, conn.InquisitorFanaticBuff));
            conn.InquisitorFanaticBuff = 0; conn.InquisitorShownUntil = until;
            if (until > now) bytes.AddRange(InstanceEntities.AddBuff(PlayerId, conn.InquisitorFanaticBuff = conn.NextBuff++, 1719,
                PlayerId, u68: BitConverter.SingleToUInt32Bits((float)(until - now))));
        }
        if (conn.InquisitorShownCharges != value.FanaticCharges)
        {
            if (conn.InquisitorChargeBuff != 0) bytes.AddRange(InstanceEntities.RemoveBuff(PlayerId, conn.InquisitorChargeBuff));
            conn.InquisitorChargeBuff = 0; conn.InquisitorShownCharges = value.FanaticCharges;
            if (value.FanaticCharges > 0) bytes.AddRange(InstanceEntities.AddBuff(PlayerId,
                conn.InquisitorChargeBuff = conn.NextBuff++, 1700, PlayerId, stacks: (ushort)value.FanaticCharges));
        }
        if (conn.InquisitorShownMore != value.AttackMorePct || bytes.Count > 0)
        { conn.InquisitorShownMore = value.AttackMorePct; conn.Minions.StatRevision++; }
        return [.. bytes];
    }
}
