using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public class InquisitorOffenseTests
{
    static Func<string, int> Values(params (string Name, int Value)[] entries) =>
        name => entries.FirstOrDefault(e => e.Name == name).Value;

    [Fact]
    public void Providence_uses_the_lower_final_attribute_as_one_global_increase()
    {
        var enabled = Values((InquisitorOffense.Providence, 1));
        Assert.Equal(125, InquisitorOffense.RighteousProvidenceIncrease(enabled, 200, 125));
        Assert.Equal(125, InquisitorOffense.RighteousProvidenceIncrease(enabled, 125, 200));
        Assert.Equal(0, InquisitorOffense.RighteousProvidenceIncrease(Values(), 200, 125));
        Assert.Equal(0, InquisitorOffense.RighteousProvidenceIncrease(enabled, -1, 125));
        // One global increase applies to the spell's own base, without becoming a final chance override.
        Assert.Equal(1125, SpellCriticalStrike.Chance(500, Values(), playerIncrease: 125));
    }

    [Fact]
    public void Justice_replaces_the_spell_base_before_generic_increases_and_cannot_crit_still_wins()
    {
        var enabled = Values((InquisitorOffense.Justice, 1));
        int? chance = InquisitorOffense.SpellBase(500, 750, enabled);
        Assert.Equal(750, chance);
        Assert.Equal(1500, SpellCriticalStrike.Chance(chance, Values(("spell_critical_strike_chance_+%", 100))));
        Assert.Equal(0, SpellCriticalStrike.Chance(chance, Values(("global_cannot_crit", 1))));
        Assert.Equal(0, InquisitorOffense.SpellBase(500, null, enabled));
        Assert.Equal(500, InquisitorOffense.SpellBase(500, 750, Values()));
    }

    [Fact]
    public void Battlemage_enters_as_added_spell_damage_before_effectiveness_increases_and_conversion()
    {
        var rawWeapon = DamagePacket.Of(DamageType.Physical, 20, 40).Add(DamagePacket.Of(DamageType.Fire, 4, 8));
        var added = InquisitorOffense.BattlemageDamage(rawWeapon, Values((InquisitorOffense.Battlemage, 1)));
        // A 150%-effectiveness spell with 100% spell increases; physical is subsequently converted to lightning.
        var spell = SpellDamage.Packet(DamagePacket.Of(DamageType.Cold, 10, 10).Add(added.Scale(1.5)),
            Values(("spell_damage_+%", 100))).Convert(new DamageConversion().AddGlobal(DamageType.Physical, DamageType.Lightning, 100));
        Assert.Equal(new DamageRange(60, 120), spell[DamageType.Lightning]);
        Assert.Equal(new DamageRange(12, 24), spell[DamageType.Fire]);
        Assert.Equal(new DamageRange(20, 20), spell[DamageType.Cold]);
        Assert.True(InquisitorOffense.BattlemageDamage(rawWeapon, Values()).IsZero);
        Assert.True(InquisitorOffense.BattlemageDamage(null, Values((InquisitorOffense.Battlemage, 1))).IsZero);
    }

    [Fact]
    public void Fanaticism_has_self_cast_spell_scope_and_a_separate_less_cost_factor()
    {
        var buff = InquisitorOffense.Fanaticism(active: true, selfCastSpell: true);
        Assert.Equal(1.75, buff.CastSpeedFactor);
        Assert.Equal(0.25m, buff.CostFactor);
        Assert.Equal(75, buff.AreaIncreasePct);
        // Existing 50% increased cost and 50% increased cast speed remain distinct buckets.
        Assert.Equal(37.5m, 100m * 1.5m * buff.CostFactor);
        Assert.Equal(2.625, 1.5 * buff.CastSpeedFactor);
        Assert.Equal(default, InquisitorOffense.Fanaticism(active: true, selfCastSpell: false));
        Assert.Equal(default, InquisitorOffense.Fanaticism(active: false, selfCastSpell: true));
        Assert.Equal(90, InquisitorOffense.Fanaticism(true, true, buffEffectPct: 20).CastSpeedMorePct);
    }

    [Fact]
    public void Target_tables_keep_the_exact_inquisitor_sources_and_native_fanaticism_stat_names()
    {
        var stats = new StatCatalog("data");
        var tree = new PassiveTree("data");
        Assert.Contains(tree.Find(53884)!.Stats, s => s.Key == stats.Key(InquisitorOffense.Providence) && s.Value == 1);
        Assert.Contains(tree.Find(3154)!.Stats, s => s.Key == stats.Key(InquisitorOffense.Justice) && s.Value == 1);
        Assert.Contains(tree.Find(19417)!.Stats, s => s.Key == stats.Key(InquisitorOffense.Battlemage) && s.Value == 1);
        Assert.Contains(tree.Find(13851)!.Stats, s => s.Key == stats.Key("maximum_fanaticism_charges") && s.Value == 3);
        Assert.Equal(13535u, stats.Key("fanaticism_hand_cast_area_of_effect_+%"));
        Assert.Equal(13536u, stats.Key("fanaticism_hand_cast_speed_+%_final"));
        Assert.Equal(13575u, stats.Key("fanaticism_hand_cast_mana_cost_+%"));
    }

    [Fact]
    public void Fanaticism_cost_rounds_once_after_supports_and_buff_effect()
    {
        Assert.Equal(3, InquisitorOffense.Cost(7, 2, InquisitorOffense.Fanaticism(true, true)));
        Assert.Equal(2, InquisitorOffense.Cost(7, 1.25, new(76, -76, 76)));
        Assert.Equal(8, InquisitorOffense.Cost(7, 1.25, default));
    }

    [Fact]
    public void Additional_pious_recovery_is_not_scaled_as_es_regeneration()
    {
        var shield = new EnergyShield();
        shield.SetMaximum(1000, refill: true);
        shield.Damage(500, false, 0);
        int Get(string name) => name == "energy_shield_regeneration_rate_+%" ? 100 : 0;
        shield.Advance(0, 1, Get, alive: true, extraRecovery: 150);
        Assert.Equal((uint)650, shield.Current);
    }
}
