using GameServer.Core.Protocol.Instance;

static partial class Game
{
    // The resource lease owns generation retirement and bounded, single-consumption transfer tickets.
    internal static OrdinaryHierophantState HierophantOf(ClientState conn)
    {
        var owner = ResourceEffectsOf(conn);
        return ResourceEffectsCurrent(conn, owner) ? owner.Hierophant : new();
    }
    static bool IsOrdinaryHierophant(ClientState conn) => OrdinaryHierophantRules.Owns(conn.MainAscendancy.Selection);
    static int HierophantGet(IReadOnlyDictionary<uint, int> raw, string name) =>
        Stats.Key(name) is uint key ? raw.GetValueOrDefault(key) : 0;
    static OrdinaryHierophantState ObserveHierophant(ClientState conn, IReadOnlyDictionary<uint, int> raw)
    {
        var state = HierophantOf(conn);
        state.Observe(IsOrdinaryHierophant(conn), LeagueFor(conn).HardMode, name => HierophantGet(raw, name));
        return state;
    }
    // Coordinator hook: fresh character snapshot, before recovery/leech/panel derivation; no recursive stat construction.
    internal static void AddOrdinaryHierophantSources(ClientState conn, Dictionary<uint, int> raw)
    {
        lock (conn.Monsters)
        {
            var state = ObserveHierophant(conn, raw);
            if (!IsOrdinaryHierophant(conn) || conn.PlayerLife == 0) return;
            double now = ResourceEffectsTime(conn);
            var original = new Dictionary<uint, int>(raw);
            if (Stats.Key("number_of_active_totems") is uint count) raw[count] = conn.Totems.Living(now).Count();
            foreach (var (name, value) in OrdinaryHierophantRules.Sources(name => HierophantGet(original, name),
                HierophantGet(original, "hierophant_area_of_effect_+%_per_50_unreserved_mana_up_to_100%") != 0 ? HierophantManaCapacity(conn) : 0, conn.Totems.Living(now).Count(),
                HierophantOffenseRead.Value is { } use && ReferenceEquals(use.Conn, conn) ? use.Active : state.Active(now)))
                if (value != 0 && Stats.Key(name) is uint key && !conn.PlayerStats.ContainsKey(key))
                    raw[key] = OrdinaryHierophantRules.Clamp((decimal)raw.GetValueOrDefault(key) + value);
        }
    }
    static uint HierophantManaCapacity(ClientState conn)
    {
        var pools = BuildPoolsFor(conn, conn.Pools.Level);
        int reserved = AuraCatalog.RequiredMana(pools, conn.Auras.Values) + PetrifiedBlood.RequiredMana(pools, conn.PetrifiedBlood);
        return (uint)Math.Max(0, (long)pools.Mana - reserved);
    }
    internal static byte[]? HierophantPresentation(ClientState conn)
    {
        lock (conn.Monsters)
        {
            if (!CurrentGameRules || conn.Closed || conn.LeavingArea || conn.RageRetired) return null;
            var owner = ResourceEffectsOf(conn);
            if (!ResourceEffectsCurrent(conn, owner)) return null;
            CharacterPlayerStats(conn); // Reconcile identity, mode and source before comparing presentation.
            var state = owner.Hierophant;
            double now = ResourceEffectsTime(conn), until = state.Active(now) ? state.Until : 0;
            int count = IsOrdinaryHierophant(conn) ? conn.Totems.Living(now).Count() : 0;
            if (until == owner.SentSurgeUntil && count == owner.SentHierophantTotems) return null;
            var bytes = new List<byte>();
            if (owner.SurgeBuff != 0) bytes.AddRange(InstanceEntities.RemoveBuff(PlayerId, owner.SurgeBuff));
            owner.SurgeBuff = 0;
            if (until > now) bytes.AddRange(InstanceEntities.AddBuff(PlayerId, owner.SurgeBuff = conn.NextBuff++, 895,
                PlayerId, u68: BitConverter.SingleToUInt32Bits((float)(until - now))));
            owner.SentSurgeUntil = until; owner.SentHierophantTotems = count;
            bytes.AddRange(PlayerStatsUpdateAtGate(conn, PlayerStatsWithCleared(conn)));
            bytes.AddRange(InstanceEntities.PlayerSkillsChange(PlayerId, PlayerSkills(conn), []));
            return [.. bytes];
        }
    }
    internal static bool OrdinaryHierophantSpellHit(ClientState conn, IReadOnlyDictionary<uint, int> raw,
        bool landed, bool spell, bool playerOrOwnedTotem)
    {
        lock (conn.Monsters)
            return CurrentGameRules && !conn.Paused && !conn.RageRetired && conn.PlayerLife > 0 && !conn.LeavingArea && !conn.Closed
                && ObserveHierophant(conn, raw).SpellHit(ResourceEffectsTime(conn), landed, spell, playerOrOwnedTotem);
    }
    internal static bool OrdinaryHierophantManaSpent(ClientState conn, uint actualMana, IReadOnlyDictionary<uint, int> raw)
    {
        lock (conn.Monsters)
            return CurrentGameRules && !conn.Paused && !conn.RageRetired && conn.PlayerLife > 0 && !conn.LeavingArea && !conn.Closed
                && ObserveHierophant(conn, raw).Spend(ResourceEffectsTime(conn), actualMana);
    }
    internal static uint OrdinaryHierophantManaCost(ClientState conn, uint ordinaryCost, IReadOnlyDictionary<uint, int> raw) =>
        IsOrdinaryHierophant(conn) ? OrdinaryHierophantRules.ManaCost(ordinaryCost, name => HierophantGet(raw, name)) : ordinaryCost;
    internal static double OrdinaryHierophantDamageFactor(ClientState conn, IReadOnlyDictionary<uint, int> raw, bool spell)
    {
        lock (conn.Monsters)
        {
            var state = ObserveHierophant(conn, raw);
            return !IsOrdinaryHierophant(conn) || conn.PlayerLife == 0 ? 1 : OrdinaryHierophantRules.DamageFactor(
                name => HierophantGet(raw, name), conn.Totems.Living(ResourceEffectsTime(conn)).Count(), spell,
                state.Active(ResourceEffectsTime(conn)));
        }
    }
    internal static double OrdinaryHierophantAilmentEffectFactor(ClientState conn, IReadOnlyDictionary<uint, int> raw)
    {
        lock (conn.Monsters)
        {
            var state = ObserveHierophant(conn, raw);
            return !IsOrdinaryHierophant(conn) || conn.PlayerLife == 0 ? 1 : OrdinaryHierophantRules.AilmentEffectFactor(
                name => HierophantGet(raw, name), state.Active(ResourceEffectsTime(conn)));
        }
    }
    internal static OrdinaryHierophantState.Carried CarryOrdinaryHierophant(ClientState conn)
    { lock (conn.Monsters) return HierophantOf(conn).Carry(ResourceEffectsTime(conn)); }
    internal static void RestoreOrdinaryHierophant(ClientState conn, OrdinaryHierophantState.Carried carried,
        IReadOnlyDictionary<uint, int> raw)
    {
        lock (conn.Monsters) ObserveHierophant(conn, raw).Restore(carried, ResourceEffectsTime(conn));
    }
    internal static void ClearOrdinaryHierophant(ClientState conn)
    { lock (conn.Monsters) HierophantOf(conn).Clear(); }
}
