using System.Text.Json;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed class InquisitorCandidateTests
{
    static Dictionary<string, int> Sources(params ushort[] nodes)
    {
        var definition = Assert.Single(new AscendancyCatalog().Definitions,
            d => d.Identity.Family == AscendancyFamily.Normal && d.Name == "Inquisitor");
        var values = new Dictionary<uint, int>();
        foreach (ushort node in nodes)
            foreach (var (key, value) in definition.Nodes[node].Stats)
                values[key] = values.GetValueOrDefault(key) + value;
        var result = new Dictionary<string, int>();
        foreach (var name in new[] { InquisitorOffense.Providence, InquisitorOffense.Justice,
            InquisitorOffense.Battlemage, InquisitorOffense.Virtue, "maximum_fanaticism_charges",
            "gain_x_fanatic_charges_every_second_if_have_attacked_in_past_second",
            "gain_fanaticism_for_4_seconds_on_reaching_maximum_fanatic_charges",
            "lose_all_fanatic_charges_on_reaching_maximum_fanatic_charges", InquisitorRecipients.EnemyTaken,
            InquisitorRecipients.Mirror, FlaskGrounds.LingerStat })
            result[name] = Program.Stats.Key(name) is uint key ? values.GetValueOrDefault(key) : 0;
        return result;
    }

    [Fact]
    public void Qualified_offense_composes_with_local_weapon_spell_and_critical_engines()
    {
        var sources = Sources(53884, 3154, 19417);
        int Get(string name) => sources.GetValueOrDefault(name);
        Assert.Equal(150, Game.InquisitorProvidence(Get, 200, 150));
        sources[InquisitorOffense.Providence] = 2;
        Assert.Equal(150, Game.InquisitorProvidence(Get, 200, 150)); // boolean, not twice
        var items = new ItemCatalog();
        var main = Assert.IsType<ItemCatalog.BaseType>(items.Find("Driftwood Wand"));
        using var rows = JsonDocument.Parse(File.ReadAllBytes("data/tables/WeaponTypes.json"));
        var row = Assert.Single(rows.RootElement.EnumerateArray(), r => r.GetProperty("BaseItemTypesKey").GetInt32() == main.Row);
        var weapon = ItemWeapons.Of(row.GetProperty("Speed").GetInt32(), row.GetProperty("DamageMin").GetInt32(),
            row.GetProperty("DamageMax").GetInt32(), row.GetProperty("RangeMax").GetInt32(), new Dictionary<string, int>(), 20);
        int critical = items.WeaponCritical(main);
        Assert.Equal(AttackBaseCriticalStrike.Chance(true, critical, 150, Get),
            Game.InquisitorAttackCritical(true, critical, 0, 200, 150, Get));
        int chance = Game.InquisitorSpellCritical(600, critical, 200, 150, Get);
        Assert.Equal(SpellCriticalStrike.Chance(critical, Get, playerIncrease: 150), chance);
        Assert.Equal(0, Game.InquisitorSpellCritical(600, null, 200, 150, Get));
        sources["global_cannot_crit"] = 1;
        Assert.Equal(0, Game.InquisitorSpellCritical(600, critical, 200, 150, Get));
        sources.Remove("global_cannot_crit");
        sources["attack_damage_+%"] = 900;
        sources["spell_damage_+%"] = 100;
        var intrinsic = DamagePacket.Of(DamageType.Fire, 10, 10);
        var packet = Game.InquisitorSpellPacket(intrinsic, weapon, Get, 3);
        Assert.Equal(weapon.LocalDamage![DamageType.Physical].Min * 6, packet[DamageType.Physical].Min, 8);
        Assert.Equal(20, packet[DamageType.Fire].Min, 8);
        sources[InquisitorOffense.Battlemage] = 0;
        Assert.Equal(0, Game.InquisitorSpellPacket(intrinsic, weapon, Get, 3)[DamageType.Physical].Min);
        Assert.Equal(0, Game.InquisitorSpellPacket(intrinsic, null, Get, 3)[DamageType.Physical].Min);
        sources[InquisitorOffense.Providence] = 0;
        sources[InquisitorOffense.Justice] = 0;
        Assert.Equal(600, Game.InquisitorSpellCritical(600, critical, 200, 150, Get));
    }

    [Fact]
    public void Accepted_uses_reload_refund_and_recipient_expiry_preserve_clock_and_recovery_boundaries()
    {
        var sources = Sources(19417, 13851, 39790, 32816);
        int Get(string name) => sources.GetValueOrDefault(name);
        var inputs = InquisitorCombatState.Inputs.From(Get, InquisitorCombatState.BaseFanaticismSeconds);
        Assert.True(inputs.Zeal);
        var state = new InquisitorCombatState();
        state.Used(0, inputs, new(false, true, false, false, false));
        state.Used(0, inputs, new(false, true, false, true, false));
        state.Used(0, inputs, new(false, true, false, false, true));
        state.Used(0, inputs, new(false, true, true, false, false));
        Assert.Equal(inputs.AttackMorePerSpell, state.Read(0, inputs).AttackMorePct);
        for (int i = 0; i < inputs.MaximumFanaticCharges; i++)
            state.Used(i + .1, inputs, new(true, false, false, false, false));
        double reached = inputs.MaximumFanaticCharges;
        Assert.True(state.Read(reached, inputs).Fanaticism(reached));
        Assert.Equal(0, state.Read(reached, inputs).FanaticCharges);
        var neutral = state.CaptureNeutral(reached + 1, inputs);
        Assert.Equal(default, state.Read(reached + 1, inputs));
        var restored = new InquisitorCombatState();
        restored.RestoreNeutral(neutral, 100, inputs);
        Assert.True(restored.Read(103.999, inputs).Fanaticism(103.999));
        Assert.False(restored.Read(104, inputs).Fanaticism(104));
        Assert.Equal(0, restored.Read(108, inputs).AttackMorePct);
        restored.Read(108, default); // refund
        Assert.False(restored.Read(108, inputs).Fanaticism(108));
        restored.Retire();
        restored.Used(109, inputs, new(false, true, false, false, false));
        Assert.Equal(default, restored.Read(109, inputs));

        var ground = new InquisitorRecipients();
        ground.ObserveOwner(0, true, true, Get);
        Assert.True(ground.ObserveEnemy(0, 42, true, true, Get) > 0);
        Assert.Equal(20, ground.MirroredRecovery(0, 2, 10, Get), 8);
        ground.ObserveOwner(2, true, false, Get);
        int sanctuary = ground.ObserveEnemy(2, 42, true, false, Get);
        sources["life_recovery_rate_+%"] = 900; // Life-only recovery must not scale the mirror
        sources["energy_shield_regeneration_rate_+%"] = 900;
        sources["energy_shield_recovery_rate_+%"] = 50;
        double expiry = 2 + Get(FlaskGrounds.LingerStat) / 1000d;
        Assert.Equal(15 * (expiry - 2), ground.MirroredRecovery(2, expiry + 1, 10, Get), 8);
        Assert.Equal(0, ground.ObserveEnemy(expiry, 42, true, false, Get));
        sources["cannot_recover_energy_shield"] = 1;
        Assert.Equal(0, ground.MirroredRecovery(2, expiry, 10, Get));
        sources.Remove("cannot_recover_energy_shield");
        sources["no_life_regeneration"] = 1;
        Assert.Equal(0, ground.MirroredRecovery(2, expiry, 10, Get));
        ground.ObserveOwner(expiry, false, false, Get);
        Assert.Equal(0, ground.MirroredRecovery(expiry, expiry + 1, 10, Get));
        Assert.Equal(sanctuary + 16, InquisitorNearby.TakenIncrease(DamageType.Fire, sanctuary,
            InquisitorNearby.AuguryIncrease(true, false, true, 60)));
        Assert.Equal(sanctuary, InquisitorNearby.TakenIncrease(DamageType.Chaos, sanctuary, 16));
        Assert.Equal(8, InquisitorNearby.AuguryIncrease(true, true, true, 60));
        Assert.Equal(0, InquisitorNearby.AuguryIncrease(true, false, true, 60.01));
        Assert.Equal(.92, InquisitorNearby.IncomingFactor(DamageType.Fire, true));
        Assert.Equal(1, InquisitorNearby.IncomingFactor(DamageType.Physical, true));
    }
}
