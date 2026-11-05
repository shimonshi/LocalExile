namespace GameServer.Gateway.Tests;

public sealed class OrdinaryPathfinderRulesTests
{
    static Func<string, int> Get(params (string Key, int Value)[] stats) => k => stats.FirstOrDefault(s => s.Key == k).Value;
    static int NoRoll(int _) => throw new InvalidOperationException("Unexpected roll");

    public void Preservation_requires_admitted_cost_and_rolls_at_strict_boundary()
    {
        var get = Get((OrdinaryPathfinderRules.Preserve, 50));
        Assert.Null(OrdinaryPathfinderRules.UseCost(9, 10, get, NoRoll));
        Assert.Equal(0, OrdinaryPathfinderRules.UseCost(10, 10, get, _ => 49));
        Assert.Equal(10, OrdinaryPathfinderRules.UseCost(10, 10, get, _ => 50));
        Assert.Equal(10, OrdinaryPathfinderRules.UseCost(10, 10, Get(), NoRoll));
        Assert.Equal(0, OrdinaryPathfinderRules.UseCost(0, 0, get, NoRoll));
    }

    public void Surgeon_halves_only_life_amount_not_duration_or_mana_and_removal_restores_future_uses()
    {
        var original = new FlaskRecovery.Use(100, 80, 4, 20, 10, 7, 9, 30, true, true, 40,
            LifeSeconds: 4, ManaSeconds: 2);
        var reduced = OrdinaryPathfinderRules.Recovery(original, Get((OrdinaryPathfinderRules.Surgeon, -50)));
        Assert.Equal(original with { Life = 50, InstantLife = 10, ExtraLife = 20 }, reduced);
        Assert.Equal(original, OrdinaryPathfinderRules.Recovery(original, Get()));
        // Existing accepted immutable snapshots retain their earned amounts after refund/transfer.
        Assert.Equal(50, reduced.Life);
    }

    public void Toxicist_is_per_poison_chance_during_real_flask_effect_only()
    {
        var get = Get((OrdinaryPathfinderRules.DoublePoison, 20));
        Assert.Equal(200, OrdinaryPathfinderRules.PoisonDps(100, true, get, _ => 19));
        Assert.Equal(100, OrdinaryPathfinderRules.PoisonDps(100, true, get, _ => 20));
        Assert.Equal(100, OrdinaryPathfinderRules.PoisonDps(100, false, get, NoRoll));
        Assert.Equal(100, OrdinaryPathfinderRules.PoisonDps(100, true, Get(), NoRoll));
        Assert.Equal(0, OrdinaryPathfinderRules.PoisonDps(0, true, get, NoRoll));
    }

    public void Spread_uses_existing_engine_once_without_rerolling_or_doubling_and_respects_expiry_immunity()
    {
        var get = Get((OrdinaryPathfinderRules.SpreadDuringFlask, 1), (OrdinaryPathfinderRules.DoublePoison, 100));
        var poisons = new MonsterPoisons();
        var snapshot = new OrdinaryPathfinderRules.PoisonSnapshot(120, 1.5);
        Assert.False(OrdinaryPathfinderRules.Spread(poisons, snapshot, 10, false, get, false, 0, NoRoll));
        Assert.False(OrdinaryPathfinderRules.Spread(poisons, snapshot, 10, true, get, true, 0, NoRoll));
        Assert.False(OrdinaryPathfinderRules.Spread(poisons, snapshot, 10, true, get, false, 100, _ => 99));
        Assert.False(OrdinaryPathfinderRules.Spread(poisons, snapshot with { Remaining = 0 }, 10, true, get, false, 0, NoRoll));
        Assert.True(OrdinaryPathfinderRules.Spread(poisons, snapshot, 10, true, get, false, 0, NoRoll));
        Assert.Equal(120, poisons.TotalDps(10));
        Assert.Equal(180, poisons.Advance(12, 1));
        Assert.Equal(0, poisons.Stacks(12));
        Assert.False(OrdinaryPathfinderRules.CanSpread(true, Get())); // refund stops new spread
        Assert.True(OrdinaryPathfinderRules.CanSpread(false, Get((OrdinaryPathfinderRules.SpreadAlways, 1)))); // Ruthless
        poisons.Add(13, 100, 2);
        poisons.Clear(); // death / area teardown removes world effects, no cross-area ledger
        Assert.Equal(0, poisons.Advance(20, 1));
    }

    public void Distiller_selects_first_affordable_of_each_kind_and_never_spends_on_triggers()
    {
        var bar = new[] {
            new OrdinaryPathfinderRules.ChargeSource(1, 0, OrdinaryPathfinderRules.Kind.Bismuth, 2),
            new OrdinaryPathfinderRules.ChargeSource(2, 1, OrdinaryPathfinderRules.Kind.Bismuth, 3),
            new OrdinaryPathfinderRules.ChargeSource(3, 2, OrdinaryPathfinderRules.Kind.Bismuth, 10),
            new OrdinaryPathfinderRules.ChargeSource(4, 3, OrdinaryPathfinderRules.Kind.Diamond, 3),
            new OrdinaryPathfinderRules.ChargeSource(5, 4, OrdinaryPathfinderRules.Kind.Amethyst, 3) };
        var get = Get((OrdinaryPathfinderRules.DistillCost, 3),
            ("pathfinder_skills_penetrate_elemental_resistances_%_if_charges_consumed_from_bismuth_flask", 20),
            ("pathfinder_skills_critical_strike_chance_+%_if_charges_consumed_from_diamond_flask", 150),
            ("pathfinder_skills_physical_damage_%_to_add_as_chaos_if_charges_consumed_from_amethyst_flask", 23));
        var plan = OrdinaryPathfinderRules.Distill(bar, true, get);
        Assert.Equal(new[] { (2u, 3), (4u, 3), (5u, 3) }, plan.Payments);
        Assert.Equal(5, plan.Stats.Count);
        Assert.Contains(("base_reduce_enemy_fire_resistance_%", 20), plan.Stats);
        Assert.Contains(("critical_strike_chance_+%", 150), plan.Stats);
        Assert.Contains(("physical_damage_%_to_add_as_chaos", 23), plan.Stats);
        Assert.Empty(OrdinaryPathfinderRules.Distill(bar, false, get).Payments);
        Assert.Empty(OrdinaryPathfinderRules.Distill(bar, true, Get()).Stats);
        Assert.Equal(3, bar[1].Charges); // plan is immutable preparation, not a charge mutation
    }

    public void Duration_alias_is_additive_and_saturates_without_affecting_other_ailments()
    {
        Assert.Equal(("poison_duration_+%", 35), OrdinaryPathfinderRules.PoisonDuration(Get(
            ("base_poison_duration_+%", 10), ("poison_duration_+%", 25))));
        Assert.Equal(("poison_duration_+%", int.MaxValue), OrdinaryPathfinderRules.PoisonDuration(Get(
            ("base_poison_duration_+%", 10), ("poison_duration_+%", int.MaxValue))));
    }
}
