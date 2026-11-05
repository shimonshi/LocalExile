using System.Runtime.CompilerServices;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

static partial class Game
{
    internal static readonly DeadeyeMarkCatalog MarkCatalog = new();
    internal sealed record MarkSource(ushort Skill, int Effect, int Level, int Quality, uint Gem, ushort Node = 0);
    internal sealed record MarkTarget(uint Id, SpawnedMonster Monster, MarkSource Source, OrdinaryEffectClock Clock);
    internal sealed record PendingMark(MarkSource Source, uint Id, SpawnedMonster Monster, double At, int Action);
    internal sealed class MarkSession
    {
        internal MarkTarget? Active;
        internal PendingMark? Pending;
        internal double BusyUntil;
    }
    static readonly ConditionalWeakTable<ClientState, MarkSession> MarkSessions = new();
    internal static MarkSession MarksOf(ClientState conn) => MarkSessions.GetValue(conn, _ => new());
    static int MarkStat(IReadOnlyDictionary<uint, int> stats, string name) =>
        Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
    static bool MarkOwnerCurrent(ClientState c) => CurrentGameWork && !c.Closed && !c.LeavingArea
        && !c.RageRetired && c.PlayerLife > 0;
    static bool MarkSourceCurrent(ClientState c, MarkSource s) => s.Node != 0 ? DeathmarkedGrant(c) is { } grant && grant.Node == s.Node && grant.Effect == s.Effect && grant.Level == s.Level : GemSkillEffects(c).Any(g =>
        g.Id == s.Skill && g.Effect == s.Effect && g.Level == s.Level && g.Quality == s.Quality && g.Gem == s.Gem)
        && GemSkillInstanceDisabled(c, s.Effect, s.Skill) is null;
    static bool MarkTargetCurrent(ClientState c, uint id, SpawnedMonster m) =>
        c.Monsters.TryGetValue(id, out var current) && ReferenceEquals(current, m) && m.Hostile && !m.Dead && m.Life > 0 && m.Mods.Effects["immune_to_curses"] == 0 && m.Mods.Effects["virtual_immune_to_curses"] == 0;

    // Preflight is before the shared cooldown and payment transaction. Ground casts never pay for a mark.
    internal static bool MarkAdmission(ClientState c, ushort skill, uint id)
    {
        lock (c.Monsters)
        {
            double now = ProjectileWorldTime(c);
            var gem = GemSkillEffects(c).FirstOrDefault(g => g.Id == skill);
            return MarkOwnerCurrent(c) && !c.Paused && !InTown(c) && gem.Name is not null
                && MarkCatalog.Find(gem.Effect) is not null && now >= MarksOf(c).BusyUntil
                && GemSkillInstanceDisabled(c, gem.Effect, skill) is null
                && c.ElementalAilments.ActionFactor(now) > 0 && !c.HitRecovery.Active(now)
                && c.Monsters.TryGetValue(id, out var m) && MarkTargetCurrent(c, id, m)
                && Dist(c.Walk.Position(now), m.Walk.Position(now)) <= 120;
        }
    }

    // Called inside the shared payment lock, after its single successful payment and accepted manual use.
    internal static void AdmitPaidMark(ClientState c, ushort skill, uint id, int action)
    {
        var gem = GemSkillEffects(c).Single(g => g.Id == skill);
        var state = MarksOf(c);
        double now = ProjectileWorldTime(c);
        double duration = (CastMs(c, gem.Effect, skill) ?? 500) / 1000d;
        state.BusyUntil = now + duration;
        state.Pending = new(new(skill, gem.Effect, gem.Level, gem.Quality, gem.Gem), id,
            c.Monsters[id], state.BusyUntil, action);
    }

    internal static MarkTarget? CurrentMark(ClientState c)
    {
        var state = MarksOf(c); double now = ProjectileWorldTime(c);
        if (!MarkOwnerCurrent(c)) { state.Active = null; state.Pending = null; return null; }
        if (state.Pending is { } pending && now >= pending.At && !c.Paused)
        {
            state.Pending = null;
            if (c.ActionCount == pending.Action && MarkSourceCurrent(c, pending.Source)
                && c.ElementalAilments.ActionFactor(now) > 0 && !c.HitRecovery.Active(now)
                && MarkTargetCurrent(c, pending.Id, pending.Monster))
            {
                var own = SupportedSkill(c, pending.Source.Skill).Stats;
                var all = SupportedStats(c, pending.Source.Skill);
                int ms = MarkStat(own, "base_skill_effect_duration");
                double seconds = ms > 0 ? ms / 1000d * Math.Max(0,
                    1 + ((long)MarkStat(all, "skill_effect_duration_+%") + MarkStat(all, "curse_skill_effect_duration_+%")
                        + MarkStat(all, "curse_duration_+%")) / 100d) : double.PositiveInfinity;
                state.Active = seconds <= 0 ? null : new(pending.Id, pending.Monster, pending.Source,
                    new(seconds, pending.At, OrdinaryEffectClock.RecipientRate(14, n => pending.Monster.Mods.Effects[n])));
            }
        }
        if (state.Active is not { } mark) return null;
        mark.Clock.ChangeRate(now, OrdinaryEffectClock.RecipientRate(14, n => mark.Monster.Mods.Effects[n]));
        if (!MarkSourceCurrent(c, mark.Source) || mark.Clock.Remaining <= 0 || !MarkTargetCurrent(c, mark.Id, mark.Monster))
            return state.Active = null;
        return mark;
    }

    internal static int MarkAmount(ClientState c, MarkTarget mark, string name) => DeadeyeMarkRules.Amount(
        MarkStat(MarkOwn(c, mark.Source), name),
        n => MarkCasterStat(c, n), n => MarkStat(MarkOwn(c, mark.Source), n));

    internal static double MarkIncoming(ClientState c, uint attacker, SpawnedMonster enemy,
        IReadOnlyDictionary<uint, int> stats)
    {
        if (!enemy.Hostile || enemy.Dead || enemy.Life == 0 || CurrentMark(c) is not { } mark) return 1;
        // A reused object id is never the marked attacker.
        bool marked = attacker == mark.Id && ReferenceEquals(enemy, mark.Monster);
        return DeadeyeMarkRules.Incoming(n => MarkStat(stats, n), marked,
            Dist(enemy.Walk.Position(ProjectileWorldTime(c)), mark.Monster.Walk.Position(ProjectileWorldTime(c))),
            LeagueFor(c).HardMode);
    }

    // The accepted lethal transaction calls this before publishing zero Life. No speculative death callback can transfer.
    internal static void MarkDeath(ClientState c, SpawnedMonster dead)
    {
        if (CurrentMark(c) is not { } mark || !ReferenceEquals(mark.Monster, dead)) return;
        c.PlayerLife = (uint)Math.Min(c.PlayerMaxLife, (long)c.PlayerLife + Math.Max(0, MarkAmount(c, mark, "life_granted_when_killed")));
        c.PlayerMana = (uint)Math.Min(UnreservedMana(c), (long)c.PlayerMana + Math.Max(0, MarkAmount(c, mark, "mana_granted_when_killed")));
        foreach (var (type, name) in new[] { (ChargeType.Power, "power"), (ChargeType.Frenzy, "frenzy"), (ChargeType.Endurance, "endurance") })
            if (MarkAmount(c, mark, $"chance_to_grant_{name}_charge_on_death_%") is > 0 and var chargeChance
                && (chargeChance >= 100 || Random.Shared.Next(100) < chargeChance))
                ChargesOf(c).Enqueue(new ChargeEvent.Grant(type, 1, "marked enemy death"));
        var state = MarksOf(c); state.Active = null;
        var stats = CharacterPlayerStats(c);
        int chance = MarkStat(stats, "your_marks_transfer_to_nearby_enemies_on_death_%_chance");
        if (chance <= 0 || chance < 100 && Random.Shared.Next(100) >= chance) return;
        double now = ProjectileWorldTime(c); var origin = dead.Walk.Position(now);
        var next = c.Monsters.Where(p => !ReferenceEquals(p.Value, dead) && MarkTargetCurrent(c, p.Key, p.Value))
            .Select(p => (p.Key, Monster: p.Value, Distance: Dist(origin, p.Value.Walk.Position(now))))
            .Where(p => p.Distance <= 60).OrderBy(p => p.Distance).ThenBy(p => p.Key).FirstOrDefault();
        if (next.Monster is not null)
        {
            mark.Clock.ChangeRate(now, OrdinaryEffectClock.RecipientRate(14, n => next.Monster.Mods.Effects[n]));
            state.Active = mark with { Id = next.Key, Monster = next.Monster }; // same clock, no payment
        }
    }

    // Host primitive receipts survive Game replacement; a new assembly removes the old target buff once.
    internal static byte[] MarkPresentation(ClientState c)
    {
        var mark = CurrentMark(c); var bytes = new List<byte>();
        uint[] keys = mark is null ? [] : MarkCatalog.Find(mark.Source.Effect)!.BuffKeys;
        uint[] values = mark is null ? [] : [.. keys
            .Select(k => unchecked((uint)MarkAmount(c, mark, Stats.Name(k) ?? "")))];
        double deadline = mark?.Clock.Deadline ?? 0;
        if (ReferenceEquals(c.OrdinaryMarkMonster, mark?.Monster) && c.OrdinaryMarkDeadline == deadline
            && c.OrdinaryMarkSkill == (mark?.Source.Skill ?? 0) && c.OrdinaryMarkGem == (mark?.Source.Gem ?? 0)
            && c.OrdinaryMarkKeys.SequenceEqual(keys) && c.OrdinaryMarkValues.SequenceEqual(values)) return [];
        if (c.OrdinaryMarkBuff != 0 && c.Monsters.TryGetValue(c.OrdinaryMarkTarget, out var old)
            && ReferenceEquals(old, c.OrdinaryMarkMonster))
        {
            bytes.AddRange(InstanceEntities.RemoveBuff(c.OrdinaryMarkTarget, c.OrdinaryMarkBuff));
            if (c.OrdinaryMarkKeys.Length > 0)
                bytes.AddRange(InstanceEntities.StatsUpdate(c.OrdinaryMarkTarget, c.OrdinaryMarkKeys
                    .Select(k => (k, old.Mods.Effects[Stats.Name(k) ?? ""])).ToArray()));
        }
        c.OrdinaryMarkBuff = 0; c.OrdinaryMarkMonster = mark?.Monster;
        c.OrdinaryMarkSkill = mark?.Source.Skill ?? 0; c.OrdinaryMarkGem = mark?.Source.Gem ?? 0;
        c.OrdinaryMarkDeadline = deadline; c.OrdinaryMarkKeys = keys; c.OrdinaryMarkValues = values;
        if (mark is not null)
        {
            c.OrdinaryMarkTarget = mark.Id; c.OrdinaryMarkBuff = c.NextBuff++;
            bytes.AddRange(InstanceEntities.StatsUpdate(mark.Id, keys.Select((key, i) =>
                (key, (int)Math.Clamp((long)mark.Monster.Mods.Effects[Stats.Name(key) ?? ""]
                    + unchecked((int)values[i]), int.MinValue, int.MaxValue))).ToArray()));
            // Mark rows are non-recovery: their values belong to Stats, never the buff's trailing list count.
            bytes.AddRange(InstanceEntities.AddBuff(mark.Id, c.OrdinaryMarkBuff,
                MarkCatalog.Find(mark.Source.Effect)!.Buff, PlayerId, mark.Source.Skill,
                u68: double.IsFinite(mark.Clock.Remaining) ? BitConverter.SingleToUInt32Bits((float)mark.Clock.Remaining) : null,
                f6c: (float)mark.Clock.Rate));
        }
        return [.. bytes];
    }
}
