using GameServer.Core.World;
using GameServer.Core.Protocol.Instance;

static partial class Game
{
    // The existing resource lease owns generation, death, retirement and transfer.

    internal static bool OrdinaryAssassinSelected(ClientState conn) => conn.MainAscendancy.Selection is
        { Family: AscendancyFamily.Normal, Id: "Assassin", Revision: AscendancyCatalog.Revision };
    internal static OrdinaryAssassinState OrdinaryAssassinOf(ClientState conn) => ResourceEffectsCurrent(conn, ResourceEffectsOf(conn)) ? ResourceEffectsOf(conn).Assassin : new();
    internal static Func<string, int> OrdinaryAssassinSources(ClientState conn, IReadOnlyDictionary<uint, int> raw) =>
        name => OrdinaryAssassinSelected(conn) && Stats.Key(name) is uint key ? raw.GetValueOrDefault(key) : 0;

    internal static MonsterPoisons.Settings OrdinaryAssassinPoison(ClientState conn, MonsterPoisons.Settings ordinary,
        IReadOnlyDictionary<uint, int> raw, double now)
    {
        lock (conn.Monsters)
        {
            if (!OrdinaryAssassinSelected(conn)) return ordinary;
            var get = OrdinaryAssassinSources(conn, raw);
            var state = OrdinaryAssassinOf(conn);
            state.Observe(now, get, conn.PlayerLife > 0);
            return OrdinaryAssassinRules.Poison(ordinary, state.RecentPoisons(now), get);
        }
    }
    internal static void OrdinaryAssassinAppliedPoison(ClientState conn, IReadOnlyDictionary<uint, int> raw, double now)
    {
        lock (conn.Monsters)
        {
            var get = OrdinaryAssassinSources(conn, raw); var state = OrdinaryAssassinOf(conn);
            state.Observe(now, get, conn.PlayerLife > 0);
            if (conn.PlayerLife > 0) state.AppliedPoison(now, get);
        }
    }
    internal static void OrdinaryAssassinCriticalHit(ClientState conn, IReadOnlyDictionary<uint, int> raw, double now, Random random)
    {
        lock (conn.Monsters)
        {
            var get = OrdinaryAssassinSources(conn, raw); var state = OrdinaryAssassinOf(conn);
            state.Observe(now, get, conn.PlayerLife > 0);
            if (conn.PlayerLife > 0) state.CriticalHit(now, get, random);
        }
    }
    internal static void ClearOrdinaryAssassin(ClientState conn)
    {
        lock (conn.Monsters)
        {
            if (conn.ResourceEffectsState is ResourceEffectsState owner) owner.Assassin.Clear();
        }
    }

    internal static void OrdinaryAssassinOwnerKillLocked(ClientState conn, OwnerKillReceipt receipt)
    {
        lock (conn.Monsters)
        {
            if (!CurrentGameRules || !receipt.PlayerKill || !receipt.Hostile || conn.PlayerLife == 0
                || conn.Closed || conn.LeavingArea || conn.RageRetired || conn.Paused) return;
            var raw = CharacterPlayerStats(conn);
            double amount = OrdinaryAssassinRules.PoisonKillLife(conn.PlayerMaxLife, receipt.Poison.Stacks,
                OrdinaryAssassinSources(conn, raw));
            if (amount <= 0) return;
            var leech = LeechOf(conn); uint before = conn.PlayerLife;
            conn.PlayerLife = PetrifiedBlood.RecoverLife(conn, before, amount, ref leech.LifeInstantCarry, minimum: 1);
            leech.InstantLifeChanged |= before != conn.PlayerLife;
            TrimLeechLocked(conn);
        }
    }
    internal static void ReconcileOrdinaryAssassinSources(ClientState conn, Dictionary<uint, int> raw)
    {
        lock (conn.Monsters)
        {
            double now = ResourceEffectsTime(conn);
            var state = OrdinaryAssassinOf(conn);
            string identity = $"{conn.MainAscendancy.Selection}:{LeagueFor(conn).HardMode}";
            if (state.SourceIdentity is not null && state.SourceIdentity != identity) state.Clear(); state.SourceIdentity = identity;
            state.Observe(now, OrdinaryAssassinSources(conn, raw), conn.PlayerLife > 0);
            if (Stats.Key(OrdinaryAssassinRules.NearbyDamage) is uint key && OrdinaryAssassinNearby(conn, now) > 1)
                raw[key] = 0;
        }
    }
    internal static int OrdinaryAssassinMovement(ClientState conn)
    {
        lock (conn.Monsters)
        {
            if (Stats.Key("movement_velocity_+%") is uint movement && conn.PlayerStats.ContainsKey(movement)) return 0;
            var raw = new Dictionary<uint,int>(CharacterStatSources(conn).Stats); foreach (var row in conn.PlayerStats) raw[row.Key] = row.Value;
            var state = OrdinaryAssassinOf(conn); double now = ResourceEffectsTime(conn);
            state.Observe(now, OrdinaryAssassinSources(conn, raw), conn.PlayerLife > 0);
            return (int)Math.Floor(30 * state.ElusiveEffect(now));
        }
    }
    internal static bool OrdinaryAssassinAvoidHit(ClientState conn, IReadOnlyDictionary<uint,int> raw, double now, Random random)
    {
        lock (conn.Monsters)
        {
            var state = OrdinaryAssassinOf(conn);
            state.Observe(now, OrdinaryAssassinSources(conn,raw), conn.PlayerLife > 0);
            return RandomRolls.Chance((int)Math.Clamp(Math.Floor(15 * state.ElusiveEffect(now)),0,100),100,random);
        }
    }
    internal static byte[]? OrdinaryAssassinPresentation(ClientState conn)
    {
        lock (conn.Monsters)
        {
            if (!CurrentGameRules || conn.Closed || conn.LeavingArea || conn.RageRetired) return null;
            var raw = CharacterPlayerStats(conn); var owner = ResourceEffectsOf(conn);
            if (!ResourceEffectsCurrent(conn,owner)) return null;
            int effect = (int)Math.Round(owner.Assassin.ElusiveEffect(ResourceEffectsTime(conn)) * 100);
            if (owner.SentElusive == effect) return null;
            var bytes = new List<byte>();
            if (owner.ElusiveBuff != 0) bytes.AddRange(InstanceEntities.RemoveBuff(PlayerId,owner.ElusiveBuff));
            owner.ElusiveBuff = 0; owner.SentElusive = effect;
            if (effect > 0) bytes.AddRange(InstanceEntities.AddBuff(PlayerId,owner.ElusiveBuff = conn.NextBuff++,1374,PlayerId,
                values: [(uint)(effect * 30 / 100), (uint)(effect * 15 / 100), 0],
                u68: BitConverter.SingleToUInt32Bits(effect / 20f)));
            conn.ApplyMovementSpeedAt(ResourceEffectsTime(conn));
            bytes.AddRange(PlayerStatsUpdateAtGate(conn,PlayerStatsWithCleared(conn)));
            return [.. bytes];
        }
    }
    // Nearby is a local 70-cell policy, not a client PSG value. Re-evaluate at contact, not action capture.
    internal static int OrdinaryAssassinNearby(ClientState conn, double now)
    {
        lock (conn.Monsters)
        {
            var at = conn.Walk.Position(now);
            return conn.Monsters.Values.Count(m => m.Hostile && !m.Dead && m.Life > 0
                && m.Rarity is MonsterRarity.Rare or MonsterRarity.Unique && Dist(m.Walk.Position(now), at) <= 70);
        }
    }
    internal static int OrdinaryAssassinCriticalChance(ClientState conn, SpawnedMonster target, double uncapped,
        bool spell, bool prohibited, IReadOnlyDictionary<uint, int> raw) => OrdinaryAssassinRules.CriticalChance(
            uncapped, spell, target.Life, target.MaxLife, OrdinaryAssassinSources(conn, raw), prohibited);
    internal static int OrdinaryAssassinCriticalMultiplier(ClientState conn, SpawnedMonster target, int ordinary,
        IReadOnlyDictionary<uint, int> raw) => OrdinaryAssassinRules.CriticalMultiplier(ordinary, target.Life, target.MaxLife,
            OrdinaryAssassinSources(conn, raw), target.Mods.Effects.NoExtraCriticalDamage);
    internal static double OrdinaryAssassinOutgoingMore(ClientState conn, IReadOnlyDictionary<uint, int> raw, double now) =>
        OrdinaryAssassinRules.OutgoingMore(OrdinaryAssassinNearby(conn, now), OrdinaryAssassinSources(conn, raw));
    internal static int OrdinaryAssassinIncomingIncreased(ClientState conn, IReadOnlyDictionary<uint, int> raw, double now) =>
        OrdinaryAssassinRules.IncomingIncreased(OrdinaryAssassinNearby(conn, now), OrdinaryAssassinSources(conn, raw));
    internal static bool OrdinaryAssassinNoExtraCriticalDamage(ClientState conn, IReadOnlyDictionary<uint, int> raw, double now)
    {
        lock (conn.Monsters)
        {
            var get = OrdinaryAssassinSources(conn, raw); var state = OrdinaryAssassinOf(conn);
            state.Observe(now, get, conn.PlayerLife > 0);
            return state.NoExtraCriticalDamage(now, get);
        }
    }
}
