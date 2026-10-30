using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public class OrdinarySlayerRulesTests
{
    internal static Dictionary<string, int> Sources(params ushort[] nodes)
    {
        var result = new Dictionary<string, int>();
        foreach (ushort id in nodes)
        {
            var node = Assert.IsType<PassiveTree.Node>(Program.Passives.Find(id));
            foreach (var (key, value) in node.Stats)
            {
                string name = Program.Stats.Name(key);
                result[name] = result.GetValueOrDefault(name) + value;
            }
        }
        return result;
    }

    [Theory]
    [InlineData(201, 1000, false)]
    [InlineData(200, 1000, true)]
    [InlineData(199, 1000, true)]
    [InlineData(0, 1000, false)]
    [InlineData(1, 0, false)]
    [InlineData(858993459, uint.MaxValue, true)]
    [InlineData(858993460, uint.MaxValue, false)]
    public void Headsman_cull_uses_post_hit_life_and_inclusive_exact_twenty_percent(uint life, uint maximum, bool culls)
    {
        var raw = Sources(3184);
        Assert.Equal(culls, SlayerRules.Culls(life, maximum, raw.GetValueOrDefault));
        Assert.False(SlayerRules.Culls(life, maximum, _ => 0));
    }

    [Theory]
    [InlineData(1200, 1000, 40)]
    [InlineData(1000, 1000, 0)]
    [InlineData(800, 1000, 0)]
    [InlineData(-1, 1000, 0)]
    [InlineData(1200, 0, 0)]
    [InlineData(int.MaxValue, uint.MaxValue, 0)]
    public void Endless_Hunger_uses_only_positive_direct_hit_overkill(int damage, uint life, double expected)
    {
        var raw = Sources(34484);
        Assert.Equal(expected, SlayerRules.OverkillLifeLeech(damage, life, raw.GetValueOrDefault));
        Assert.Equal(0, SlayerRules.OverkillLifeLeech(damage, life, _ => 0));
    }

    [Fact]
    public void Overkill_combines_with_attack_leech_before_one_existing_instance_cap()
    {
        var raw = Sources(45696, 10143, 34215, 34484);
        int Get(string n) => raw.GetValueOrDefault(n);
        var session = new LeechSession();
        var limits = LeechLimits.Resolve(Get, 1000, 1000, 1000);
        session.Configure(0, limits, Get, _ => null);
        var amounts = LeechSources.From(Get, attack: true).Amounts(DamagePacket.Of(DamageType.Fire, 10000, 10000), 10000);
        Assert.Equal(80, amounts.Life); // Two attack-leech small nodes: 0.8% of the direct hit.
        amounts.Life += SlayerRules.OverkillLifeLeech(10000, 100, Get);
        session.Hit(0, amounts, true, true, true, attack: true);
        Assert.Equal(1, session.Life.Count); // Not one ordinary plus another overkill instance.
        Assert.Equal(200, limits.Resources[0].MaximumPerInstance);
        Assert.Equal(200, limits.Resources[0].MaximumPerSecond); // +100% instance amount is not doubled rate.
        Assert.True(session.Active(9.999).Life);
        Assert.False(session.Active(10).Life);
        Assert.Equal(200, session.TakeRecovery(10).Life);
    }

    [Fact]
    public void Spell_overkill_grants_life_without_attack_leech_and_obeys_existing_conversion_and_prohibition()
    {
        var raw = Sources(34484);
        int Get(string n) => raw.GetValueOrDefault(n);
        var amounts = LeechSources.From(Get, attack: false).Amounts(DamagePacket.Of(DamageType.Cold, 1200, 1200), 1200);
        Assert.Equal(0, amounts.Life);
        amounts.Life += SlayerRules.OverkillLifeLeech(1200, 1000, Get);
        var session = new LeechSession();
        raw["keystone_ghost_reaver"] = 1;
        session.Configure(0, LeechLimits.Resolve(Get, 1000, 1000, 1000), Get, _ => null);
        session.Hit(0, amounts, false, false, false);
        Assert.Equal((false, false, true), session.Active(0));
        Assert.Equal((0d, 0d, 40d), session.TakeRecovery(2));
        raw["base_cannot_leech_energy_shield"] = 1;
        session.Configure(2, LeechLimits.Resolve(Get, 1000, 1000, 1000), Get, _ => null);
        session.Hit(2, amounts, false, false, false);
        Assert.Equal(default, session.Active(2));
        Assert.Equal(default, session.TakeRecovery(10));
    }

    [Fact]
    public void Culling_extra_life_and_impale_are_never_overkill_damage()
    {
        var raw = Sources(3184, 34484);
        int Get(string n) => raw.GetValueOrDefault(n);
        Assert.True(SlayerRules.Culls(200, 1000, Get)); // Direct hit for 800 from full Life, then cull.
        Assert.Equal(0, SlayerRules.OverkillLifeLeech(800, 1000, Get));
        Assert.Equal(0, SlayerRules.OverkillLifeLeech(900, 1000, Get)); // Another 200 reflected by impale is excluded.
        Assert.Equal(0, SlayerRules.OverkillLifeLeech(0, 200, Get)); // Accepted zero-damage culling hit.
    }

    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 1.2)]
    [InlineData(false, true, 1.1)]
    [InlineData(true, true, 1.32)]
    public void Bane_unique_and_recent_damage_are_independent_more_buckets(bool unique, bool recent, double factor)
    {
        var raw = Sources(62817);
        Assert.Equal(factor, SlayerRules.DamageMultiplier(raw.GetValueOrDefault, unique, recent), 10);
    }

    [Theory]
    [InlineData(-1, 0)] [InlineData(0, 0)] [InlineData(1, 10)] [InlineData(9, 90)]
    [InlineData(10, 100)] [InlineData(11, 100)] [InlineData(int.MaxValue, 100)]
    public void Overwhelm_caps_bonus_critical_multiplier_points(int count, int bonus)
    {
        var raw = Sources(17315);
        Assert.Equal(800, raw[AttackBaseCriticalStrike.WeaponOverrideStat]);
        Assert.Equal(bonus, SlayerRules.CriticalMultiplierBonus(raw.GetValueOrDefault, count));
    }

    [Fact]
    public void Masterful_Form_replaces_endurance_after_frenzy_additions_and_before_existing_charge_limits()
    {
        var raw = Sources(50845, 16306);
        raw["max_endurance_charges"] = 8;
        raw["max_frenzy_charges"] += 2;
        int Get(string n) => raw.GetValueOrDefault(n);
        var standard = ChargePanelStats.Compute(Get).ToDictionary(r => r.Stat, r => r.Value);
        int endurance = SlayerRules.EnduranceMaximum(Get,
            standard["virtual_maximum_endurance_charges"], standard["virtual_maximum_frenzy_charges"]);
        Assert.Equal(6, endurance); // Own Endurance +8 is replaced, not added to Frenzy's completed maximum.
        raw["max_endurance_charges"] = endurance - ChargePanelStats.BaseMaximumCharges;
        raw["base_minimum_endurance_charges"] = 20;
        var limits = Charges.LimitsOf(ChargeType.Endurance, Get);
        Assert.Equal(new Charges.Limits(6, 6, 11500), limits);
        var state = new Charges.State();
        state.Gain(ChargeType.Endurance, 100, 0, limits);
        Assert.Equal(6, state.Effective(ChargeType.Endurance, limits));
        raw.Remove(SlayerRules.EnduranceEqualsFrenzy);
        Assert.Equal(11, SlayerRules.EnduranceMaximum(Get, 11, 6));
        Assert.Equal(0, SlayerRules.EnduranceMaximum(_ => 1, 11, -1));
    }

    [Theory]
    [InlineData(true, false, false)] [InlineData(false, true, false)] [InlineData(false, false, true)]
    public void Leech_conditions_accept_each_resource_and_are_not_confused_with_life_only(bool life, bool mana, bool es)
    {
        var raw = Sources(34484, 10143);
        Assert.Equal(new SlayerRules.LeechConditions(20, -10, true, true),
            SlayerRules.WhileLeeching(raw.GetValueOrDefault, life, mana, es));
        Assert.Equal(default, SlayerRules.WhileLeeching(raw.GetValueOrDefault, false, false, false));
    }

    [Fact]
    public void Brutal_Fervour_retained_full_life_instance_keeps_leech_conditions_until_exact_expiry_or_refund()
    {
        var raw = Sources(45696, 10143, 34215, 34484);
        int Get(string n) => raw.GetValueOrDefault(n);
        var session = new LeechSession();
        void Configure(double now) => session.Configure(now, LeechLimits.Resolve(Get, 1000, 1000, 1000), Get, _ => null);
        Configure(0);
        session.Hit(0, (200, 0, 0), true, true, true, attack: true);
        Assert.True(SlayerRules.WhileLeeching(Get, session.Active(9.999).Life, false, false).CannotBeStunned);
        Assert.Equal(default, SlayerRules.WhileLeeching(Get, session.Active(10).Life, false, false));
        session.Hit(10, (200, 0, 0), true, true, true, attack: true);
        raw.Remove("life_leech_does_not_stop_at_full_life");
        Configure(11);
        session.ClearFull(11, true, true, true);
        Assert.False(session.Active(11).Life);
        Assert.Equal(default, SlayerRules.WhileLeeching(Get, session.Active(11).Life, false, false));
    }
}
