using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed class OrdinaryAssassinRulesTests
{
    readonly Dictionary<string, int> stats = [];
    int Get(string n) => stats.GetValueOrDefault(n);


    public void Choice_conditions_use_pre_hit_life_and_keep_chance_separate_from_multiplier()
    {
        stats[OrdinaryAssassinRules.NotLowChance] = 100;
        stats[OrdinaryAssassinRules.LowMultiplier] = 100;
        Assert.Equal(2000, OrdinaryAssassinRules.CriticalChance(1000, false, 51, 100, Get));
        Assert.Equal(1000, OrdinaryAssassinRules.CriticalChance(1000, false, 50, 100, Get));
        Assert.Equal(250, OrdinaryAssassinRules.CriticalMultiplier(150, 50, 100, Get));
        Assert.Equal(150, OrdinaryAssassinRules.CriticalMultiplier(150, 51, 100, Get));
        Assert.Equal(100, OrdinaryAssassinRules.CriticalMultiplier(150, 50, 100, Get, true));
        stats.Clear(); stats[OrdinaryAssassinRules.LowChance] = 100; stats[OrdinaryAssassinRules.NotLowMultiplier] = 100;
        Assert.Equal(2000, OrdinaryAssassinRules.CriticalChance(1000, false, 50, 100, Get));
        Assert.Equal(250, OrdinaryAssassinRules.CriticalMultiplier(150, 51, 100, Get));
    }


    public void Spell_less_composes_before_cap_and_cannot_crit_wins()
    {
        stats[OrdinaryAssassinRules.SpellChance] = -30;
        Assert.Equal(8400, OrdinaryAssassinRules.CriticalChance(12000, true, 80, 100, Get));
        Assert.Equal(10000, OrdinaryAssassinRules.CriticalChance(12000, false, 80, 100, Get));
        Assert.Equal(0, OrdinaryAssassinRules.CriticalChance(12000, true, 80, 100, Get, true));
    }




    public void Opportunistic_mode_values_and_two_enemy_boundary(int more, int reduced)
    {
        stats[OrdinaryAssassinRules.NearbyDamage] = more; stats[OrdinaryAssassinRules.NearbyTaken] = reduced;
        Assert.Equal(1 + more / 100d, OrdinaryAssassinRules.OutgoingMore(0, Get));
        Assert.Equal(1 + more / 100d, OrdinaryAssassinRules.OutgoingMore(1, Get));
        Assert.Equal(1, OrdinaryAssassinRules.OutgoingMore(2, Get));
        Assert.Equal(0, OrdinaryAssassinRules.IncomingIncreased(1, Get));
        Assert.Equal(reduced, OrdinaryAssassinRules.IncomingIncreased(2, Get));
    }


    public void Poison_uses_ailment_settings_not_hit_multiplier_and_tracks_successes_only()
    {
        stats[OrdinaryAssassinRules.CriticalPoison] = 20; stats[OrdinaryAssassinRules.RecentPoisonDuration] = 5;
        stats["poison_duration_+%"] = 100;
        var state = new OrdinaryAssassinState();
        var ordinary = new MonsterPoisons.Settings(100, 10, 10, 15, 15, 4);
        Assert.Equal(4, OrdinaryAssassinRules.Poison(ordinary, state.RecentPoisons(1), Get).Seconds);
        state.AppliedPoison(1, Get);
        var next = OrdinaryAssassinRules.Poison(ordinary, state.RecentPoisons(1), Get);
        Assert.Equal(4.1, next.Seconds, 8); Assert.Equal(10, next.Min); Assert.Equal(18, next.CriticalMin);
        var target = new MonsterPoisons();
        Assert.True(target.TryApply(next, true, 1, 0, false, new Random(1)));
        Assert.Equal(18, target.Advance(2, 1));
        Assert.Equal(1, state.RecentPoisons(4.999)); Assert.Equal(0, state.RecentPoisons(5));
    }


    public void Poison_eligibility_respects_all_damage_and_chaos_prohibition()
    {
        var fire = DamagePacket.Of(DamageType.Fire, 10, 10);
        Assert.False(OrdinaryAssassinRules.PoisonEligible(fire, Get));
        stats["all_damage_can_poison"] = 1;
        Assert.True(OrdinaryAssassinRules.PoisonEligible(fire, Get));
        stats["cannot_poison_enemies"] = 1;
        Assert.False(OrdinaryAssassinRules.PoisonEligible(fire, Get));
        stats.Clear(); stats["base_deal_no_chaos_damage"] = 1;
        Assert.True(MonsterPoisons.Prohibited(Get));
    }


    public void Poison_kill_recovery_uses_live_stacks_and_cannot_recover()
    {
        stats[OrdinaryAssassinRules.PoisonKillRecovery] = 50;
        Assert.Equal(15, OrdinaryAssassinRules.PoisonKillLife(1000, 3, Get));
        Assert.Equal(0, OrdinaryAssassinRules.PoisonKillLife(1000, 0, Get));
        stats["cannot_recover_life"] = 1;
        Assert.Equal(0, OrdinaryAssassinRules.PoisonKillLife(1000, 3, Get));
    }




    public void Elusive_does_not_refresh_and_expires_at_zero(int effect, double scale)
    {
        stats["gain_elusive_on_crit_%_chance"] = 100; stats["elusive_effect_+%"] = effect;
        stats["self_take_no_extra_damage_from_critical_strikes_while_affected_by_elusive"] = 1;
        var state = new OrdinaryAssassinState();
        Assert.True(state.CriticalHit(1, Get, new Random(1)));
        Assert.Equal(scale, state.ElusiveEffect(1), 8);
        Assert.False(state.CriticalHit(2, Get, new Random(1)));
        Assert.True(state.NoExtraCriticalDamage(2, Get));
        Assert.Equal(0, state.ElusiveEffect(1 + scale / .2), 8);
        Assert.True(state.CriticalHit(20, Get, new Random(1)));
    }


    public void Refund_source_removal_death_and_transfer_do_not_resurrect_state()
    {
        stats["gain_elusive_on_crit_%_chance"] = 100; stats[OrdinaryAssassinRules.RecentPoisonDuration] = 5;
        var state = new OrdinaryAssassinState(); state.CriticalHit(1, Get, new Random(1)); state.AppliedPoison(1, Get);
        var carry = state.Export(2); state.Clear();
        var recipient = new OrdinaryAssassinState(); recipient.Import(carry, 100, 1, Get, true);
        Assert.Equal(.6, recipient.ElusiveEffect(100), 8); Assert.Equal(1, recipient.RecentPoisons(100));
        Assert.Equal(0, recipient.RecentPoisons(102));
        stats.Clear(); recipient.Observe(100, Get, true);
        Assert.Equal(0, recipient.ElusiveEffect(100)); Assert.Equal(0, recipient.RecentPoisons(100));
        stats["gain_elusive_on_crit_%_chance"] = 100; recipient.Import(carry, 100, 0, Get, false);
        Assert.Equal(0, recipient.ElusiveEffect(100));
    }


    public void Bifurcation_reuses_two_independent_action_rolls_and_obeys_prohibition()
    {
        var rng = new SequenceRandom(1000, 8000);
        var action = new OrdinaryAssassinCriticalRoll(0, true);
        Assert.Equal(0, action.Check(5000, true, rng)); Assert.Equal(0, rng.Calls);
        Assert.Equal(1, action.Check(5000, false, rng)); Assert.Equal(2, rng.Calls);
        Assert.Equal(2, action.Check(9000, false, rng)); Assert.Equal(2, rng.Calls);
        Assert.Equal(7500, CriticalStrikePolicy.EffectiveChance(5000, bifurcate: true));
        Assert.Equal(2500, CriticalStrikePolicy.DoubleCriticalChance(5000, bifurcate: true));
    }


    public void Deathmarked_scales_with_maximum_not_held_charges_and_removes_cleanly()
    {
        stats["mark_skills_curse_effect_+%_per_maximum_power_charge"] = 10;
        stats["leech_%_is_instant_per_maximum_power_charge"] = 3;
        Assert.Equal(40, OrdinaryAssassinRules.MarkEffect(4, Get));
        Assert.Equal(12, OrdinaryAssassinRules.InstantLeech(4, Get));
        stats.Clear(); Assert.Equal(0, OrdinaryAssassinRules.MarkEffect(4, Get));
        Assert.Equal(0, OrdinaryAssassinRules.InstantLeech(4, Get));
    }


    public void Adapters_gate_shared_table_identity_and_clear_dead_owner_state()
    {
        using var setup = new OrdinaryHierophantConnectedTests.Setup();
        var conn = setup.Conn;
        var identity = new AscendancyIdentity(AscendancyFamily.Normal, "Assassin", "3.29.3b");
        conn.MainAscendancy = new(identity, [], []); conn.PlayerLife = 100;
        uint K(string n) => Program.Stats.Key(n)!.Value;
        var raw = new Dictionary<uint, int> { [K("gain_elusive_on_crit_%_chance")] = 100,
            [K("self_take_no_extra_damage_from_critical_strikes_while_affected_by_elusive")] = 1 };
        Game.OrdinaryAssassinCriticalHit(conn, raw, 1, new Random(1));
        Assert.True(Game.OrdinaryAssassinNoExtraCriticalDamage(conn, raw, 2));
        conn.MainAscendancy = new(identity with { Family = AscendancyFamily.Phrecia }, [], []);
        Assert.False(Game.OrdinaryAssassinNoExtraCriticalDamage(conn, raw, 2));
        conn.MainAscendancy = new(identity, [], []);
        Assert.False(Game.OrdinaryAssassinNoExtraCriticalDamage(conn, raw, 2));
        Game.OrdinaryAssassinCriticalHit(conn, raw, 3, new Random(1));
        conn.PlayerLife = 0;
        Assert.False(Game.OrdinaryAssassinNoExtraCriticalDamage(conn, raw, 3));
        conn.PlayerLife = 100;
        Assert.False(Game.OrdinaryAssassinNoExtraCriticalDamage(conn, raw, 3));
        Assert.False(Game.OrdinaryAssassinSelected(new(new(1, "test", 1, 1, 0, 0, null, []), 2, 1)));
    }


    public void Shadowed_blood_existing_recoup_consumer_is_physical_hits_only()
    {
        stats["physical_damage_taken_recouped_as_life_%"] = 40;
        Assert.Equal(40, Game.LifeRecoupOf(Get, DamagePacket.Of(DamageType.Physical, 100, 100)));
        Assert.Equal(0, Game.LifeRecoupOf(Get, DamagePacket.Of(DamageType.Fire, 100, 100)));
        stats["cannot_recover_life"] = 1;
        Assert.Equal(0, Game.LifeRecoupOf(Get, DamagePacket.Of(DamageType.Physical, 100, 100)));
    }

    sealed class SequenceRandom(params int[] values) : Random
    {
        public int Calls;
        public override int Next(int maxValue) => values[Calls++];
    }
}
