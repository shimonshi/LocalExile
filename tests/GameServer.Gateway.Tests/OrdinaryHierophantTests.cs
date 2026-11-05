namespace GameServer.Gateway.Tests;

public sealed class OrdinaryHierophantTests
{
    static Func<string, int> Get(params (string, int)[] stats)
    { var map = stats.ToDictionary(x => x.Item1, x => x.Item2); return key => map.GetValueOrDefault(key); }
    static readonly Func<string, int> Normal = Get((OrdinaryHierophantRules.HitSurge, 1));
    static readonly Func<string, int> Ruthless = Get((OrdinaryHierophantRules.ManaSurge, 200));


    public void Identity_separates_families_classes_and_revisions()
    {
        Assert.True(OrdinaryHierophantRules.Owns(new(AscendancyFamily.Normal, "Hierophant", AscendancyCatalog.Revision)));
        Assert.False(OrdinaryHierophantRules.Owns(new(AscendancyFamily.Phrecia, "Puppeteer", AscendancyCatalog.Revision)));
        Assert.False(OrdinaryHierophantRules.Owns(new(AscendancyFamily.Phrecia, "Hierophant", AscendancyCatalog.Revision)));
        Assert.False(OrdinaryHierophantRules.Owns(new(AscendancyFamily.Normal, "Hierophant", "old")));
        Assert.False(OrdinaryHierophantRules.Owns(null));
    }

    public void Only_landed_owned_spell_hits_grant_normal_surge_and_refresh_exact_expiry()
    {
        var state = new OrdinaryHierophantState(); state.Observe(true, false, Normal);
        Assert.False(state.SpellHit(1, false, true, true));
        Assert.False(state.SpellHit(1, true, false, true));
        Assert.False(state.SpellHit(1, true, true, false));
        Assert.False(state.Spend(1, 400));
        Assert.False(state.Active(1));
        Assert.True(state.SpellHit(1, true, true, true));
        Assert.True(state.Active(4.999)); Assert.False(state.Active(5));
        Assert.True(state.SpellHit(5, true, true, true)); Assert.True(state.Active(8.999));
        Assert.False(state.Active(9));
    }

    public void Ruthless_spends_actual_mana_with_threshold_remainder_and_no_normal_hit_trigger()
    {
        var state = new OrdinaryHierophantState(); state.Observe(true, true, Ruthless);
        Assert.False(state.SpellHit(0, true, true, true));
        Assert.False(state.Spend(0, 199)); Assert.Equal(199, state.ManaRemainder);
        Assert.False(state.Spend(0, 0)); Assert.True(state.Spend(1, 1));
        Assert.Equal(0, state.ManaRemainder); Assert.True(state.Active(4.999)); Assert.False(state.Active(5));
        Assert.True(state.Spend(6, 450)); Assert.Equal(50, state.ManaRemainder);
        Assert.True(state.Spend(7, uint.MaxValue)); Assert.InRange(state.ManaRemainder, 0, 199);
    }

    public void Refund_mode_change_identity_removal_and_death_clear_owned_progress()
    {
        var state = new OrdinaryHierophantState(); state.Observe(true, true, Ruthless);
        state.Spend(1, 250); state.Observe(true, true, Get());
        Assert.False(state.Active(1)); Assert.Equal(0, state.ManaRemainder);
        state.Observe(true, false, Normal); state.SpellHit(2, true, true, true);
        state.Observe(true, true, Ruthless); Assert.False(state.Active(2));
        state.Spend(2, 250); state.Observe(false, true, Ruthless);
        Assert.False(state.Active(2)); Assert.Equal(0, state.ManaRemainder);
        state.Observe(true, true, Ruthless); state.Spend(3, 250); state.Clear();
        Assert.False(state.Active(3)); Assert.Equal(0, state.ManaRemainder);
    }

    public void Transfer_freezes_remaining_duration_and_progress_but_cannot_restore_refunded_source()
    {
        var state = new OrdinaryHierophantState(); state.Observe(true, true, Ruthless); state.Spend(10, 250);
        var carried = state.Carry(11); Assert.False(state.Active(11)); Assert.Equal(0, state.ManaRemainder);
        var destination = new OrdinaryHierophantState(); destination.Observe(true, true, Ruthless);
        destination.Restore(carried, 100); Assert.True(destination.Active(102.999)); Assert.False(destination.Active(103));
        Assert.Equal(50, destination.ManaRemainder);
        destination.Observe(true, true, Get()); destination.Restore(carried, 200);
        Assert.False(destination.Active(200)); Assert.Equal(0, destination.ManaRemainder);
    }



    public void Area_uses_unreserved_maximum_complete_groups_and_cap(uint mana, int expected)
    {
        var get = Get(("hierophant_area_of_effect_+%_per_50_unreserved_mana_up_to_100%", 1));
        Assert.Equal(expected, OrdinaryHierophantRules.Area(get, mana));
        Assert.Equal(0, OrdinaryHierophantRules.Area(Get(), mana));
    }

    public void Mana_conversion_final_cost_and_damage_preserve_scope_and_other_multipliers()
    {
        var get = Get(("additive_mana_modifiers_apply_to_damage_at_30%_value", 1), ("maximum_mana_+%", 39),
            (OrdinaryHierophantRules.CostMore, -25), (OrdinaryHierophantRules.TotemMore, 3),
            (OrdinaryHierophantRules.SurgeMore, 20), ("arcane_surge_effect_+%", 40));
        Assert.Equal(11, OrdinaryHierophantRules.ManaDamage(get)); Assert.Equal(0, OrdinaryHierophantRules.ManaDamage(Get()));
        Assert.Equal(3u, OrdinaryHierophantRules.ManaCost(5, get)); Assert.Equal(5u, OrdinaryHierophantRules.ManaCost(5, Get()));
        Assert.Equal(1.06, OrdinaryHierophantRules.DamageFactor(get, 2, false, true), 8);
        Assert.Equal(1.06 * 1.28, OrdinaryHierophantRules.DamageFactor(get, 2, true, true), 8);
        Assert.Equal(1.06, OrdinaryHierophantRules.DamageFactor(get, 2, true, false), 8);
    }

    public void Conditional_sources_reach_existing_leech_and_area_contracts_and_expire_cleanly()
    {
        var get = Get(("arcane_surge_effect_+%", 40), ("area_of_effect_+%_while_you_have_arcane_surge", 30),
            ("life_leech_from_spell_damage_permyriad_while_you_have_arcane_surge", 50),
            ("you_and_totem_life_regeneration_rate_per_minute_%_per_active_totem", 60),
            ("mana_regeneration_rate_per_minute_%_per_active_totem", 30));
        var sources = OrdinaryHierophantRules.Sources(get, 0, 2, true).GroupBy(x => x.Stat)
            .ToDictionary(x => x.Key, x => x.Sum(v => v.Value));
        Assert.Equal(28, sources["cast_speed_+%"]); Assert.Equal(42, sources["mana_regeneration_rate_+%"]);
        Assert.Equal(120, sources["life_regeneration_rate_per_minute_%"]);
        Assert.Equal(60, sources["mana_regeneration_rate_per_minute_%"]);
        var spell = LeechSources.From(k => sources.GetValueOrDefault(k), attack: false);
        var attack = LeechSources.From(k => sources.GetValueOrDefault(k), attack: true);
        Assert.Equal(50, spell.Life.MinTotal / 5); Assert.Equal(0, attack.Life.MinTotal);
        Assert.DoesNotContain(OrdinaryHierophantRules.Sources(get, 0, 0, false), x => x.Stat == "has_arcane_surge");
        Assert.All(OrdinaryHierophantRules.Sources(Get(), 0, 0, false), x => Assert.Equal(0, x.Value));
    }

    public void Existing_charge_contract_handles_normal_and_ruthless_minima_without_custom_charge_engine()
    {
        var normal = new ChargeSession(); normal.Observe(Get(("maximum_power_and_endurance_charges_+", 1),
            ("base_minimum_endurance_charges", 4), ("base_minimum_power_charges", 4)));
        Assert.Equal(4, normal.Effective(ChargeType.Power)); Assert.Equal(4, normal.Effective(ChargeType.Endurance));
        normal.Die(); Assert.Equal(4, normal.Effective(ChargeType.Power));
        normal.Observe(Get(("base_minimum_power_charges", 3))); Assert.Equal(3, normal.Effective(ChargeType.Power));
        normal.Observe(Get()); Assert.Equal(0, normal.Effective(ChargeType.Power));
    }
}
