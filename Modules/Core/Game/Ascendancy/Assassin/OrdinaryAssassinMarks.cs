using GameServer.Core.World;
using GameServer.Core.Protocol.Instance;

static partial class Game
{
    // Authority is the pinned passive grant, never the local_display stat or a fabricated socketed gem.
    internal static PassiveGrantedSkills.Grant? DeathmarkedGrant(ClientState c) => OrdinaryAssassinSelected(c)
        ? (LeagueFor(c).HardMode ? RuthlessGrantedSkills : GrantedSkills).Value.For(c.MainAscendancy.Allocated)
            .Where(g => g.Node == 48239 && g.EffectId == "AssassinsMarkTriggeredAssassin" && g.Level == 30)
            .Select(g => (PassiveGrantedSkills.Grant?)g).SingleOrDefault() : null;

    static IReadOnlyDictionary<uint,int> MarkOwn(ClientState c, MarkSource source) => source.Node == 0
        ? SupportedSkill(c, source.Skill).Stats
        : SkillsFor(c).EffectStats(source.Effect, source.Level).ToDictionary(v => v.Item1, v => v.Item2);

    static int MarkCasterStat(ClientState c, string name)
    {
        var raw = CharacterPlayerStats(c);
        if (name != "mark_skills_curse_effect_+%") return MarkStat(raw, name);
        var get = OrdinaryAssassinSources(c, raw);
        int maximum = c.PlayerStats.TryGetValue(Stats.Key("virtual_maximum_power_charges")!.Value, out int replacement)
            ? replacement : Charges.LimitsOf(ChargeType.Power, n => MarkStat(raw, n)).Maximum;
        return (int)Math.Clamp((long)MarkStat(raw, name) + OrdinaryAssassinRules.MarkEffect(maximum, get), int.MinValue, int.MaxValue);
    }

    internal static bool TriggerDeathmarkedLocked(ClientState c, SpawnedMonster target)
    {
        lock (c.Monsters)
        {
            if (!MarkOwnerCurrent(c) || c.Paused || InTown(c) || target.Rarity is not (MonsterRarity.Rare or MonsterRarity.Unique)
                || DeathmarkedGrant(c) is not { } grant || ActorSkillDisabledCore(c, grant.Effect, null) is not null)
                return false;
            var pair = c.Monsters.FirstOrDefault(p => ReferenceEquals(p.Value, target));
            if (!MarkTargetCurrent(c, pair.Key, target)) return false;
            var source = new MarkSource(0, grant.Effect, grant.Level, 0, 0, grant.Node);
            var own = MarkOwn(c, source); var raw = CharacterPlayerStats(c);
            int cost = MarkStat(raw, "mark_skills_cost_no_mana") != 0 ? 0
                : ManaRecoveryRules.Cost(MarkStat(own, "mana_cost"), n => MarkStat(raw, n));
            int life = PetrifiedBlood.LifeCost(c, cost);
            if (c.PlayerMana < cost || life > 0 && c.PlayerLife <= life) return false;
            double now = ProjectileWorldTime(c);
            int ms = MarkStat(own, "base_skill_effect_duration");
            double seconds = ms > 0 ? ms / 1000d * Math.Max(0, 1 + ((long)MarkStat(raw, "skill_effect_duration_+%")
                + MarkStat(raw, "curse_skill_effect_duration_+%") + MarkStat(raw, "curse_duration_+%")) / 100d) : double.PositiveInfinity;
            if (seconds <= 0) return false;
            c.PlayerMana -= (uint)cost; c.PlayerLife -= (uint)life;
            ManaSpentLocked(c, (uint)cost, raw);
            var state = MarksOf(c); state.Pending = null;
            state.Active = new(pair.Key, target, source,
                new(seconds, now, OrdinaryEffectClock.RecipientRate(14, n => target.Mods.Effects[n])));
            return true; // Existing ordinary presentation publishes replacement/removal and values once.
        }
    }
}
