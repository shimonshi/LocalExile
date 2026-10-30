using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public partial class ConditionalCriticalGatewayTests
{
    static SlayerKillState.Snapshot SlayerValue(Contact f)
    {
        var stats = Program.CharacterPlayerStats(f.Conn);
        return Program.SlayerSnapshot(f.Conn, n => Program.Stats.Key(n) is uint k ? stats.GetValueOrDefault(k) : 0);
    }

    [Theory]
    [InlineData(null)] [InlineData("Cyclone")] [InlineData("IceNova")] [InlineData("Fireball")]
    public async Task Slayer_accepted_lethal_contacts_grant_once_and_refresh_real_speed_area_and_recent_damage(string? gem)
    {
        using var f = new Contact(new Draws(), gem, classRow: 4, targetLife: 10000);
        Slayer(f, 3184, 38180); // Legal six-point path, includes Bane.
        int speed = f.Conn.Pools.AttackSpeedPct, movement = f.Conn.Pools.MovementVelocityPct;
        int area = Program.CharacterPlayerStats(f.Conn)[Program.Stats.Key("skill_area_of_effect_+%")!.Value];
        f.Target.Life = 1;
        await SlayerContact(f, gem);
        Assert.True(f.Target.Dead);
        Assert.Equal(1, SlayerValue(f).RecentKills);
        Assert.Equal(10, SlayerValue(f).MovementSpeedIncrease);
        Assert.Equal(movement + 10, f.Conn.Pools.MovementVelocityPct);
        Assert.Equal(speed, f.Conn.Pools.AttackSpeedPct); // Normal kill never grants rare attack speed.
        Assert.Equal(area + 5, Program.CharacterPlayerStats(f.Conn)[Program.Stats.Key("skill_area_of_effect_+%")!.Value]);
        f.Conn.NextAttackAt = 0;
        await SlayerContact(f, gem); // Dead target admission cannot award again.
        Assert.Equal(gem == "Fireball" ? 0 : 1, SlayerValue(f).RecentKills); // Launch fixture advances another four seconds.
    }

    [Theory]
    [InlineData(MonsterRarity.Normal, 0)] [InlineData(MonsterRarity.Magic, 0)]
    [InlineData(MonsterRarity.Rare, 10)] [InlineData(MonsterRarity.Unique, 10)]
    public async Task Slayer_rare_unique_kills_publish_attack_speed_and_remove_the_grant_on_source_removal(
        MonsterRarity rarity, int expected)
    {
        using var f = new Contact(new Draws(), "Cyclone", classRow: 4);
        Slayer(f, 3184);
        var m = new SpawnedMonster("rarity", new(34, 32, 0), 0, 2, 100) { Rarity = rarity };
        f.Conn.Monsters.Clear(); f.Conn.Monsters[42] = m;
        int speed = f.Conn.Pools.AttackSpeedPct;
        await f.Sweep();
        Assert.True(m.Dead);
        Assert.Equal(expected, SlayerValue(f).AttackSpeedIncrease);
        Assert.Equal(speed + (expected == 0 ? 0 : 11), f.Conn.Pools.AttackSpeedPct); // unarmed inherent 10% MORE
        f.Conn.MainAscendancy = new(); f.Refresh();
        Assert.Equal(0, SlayerValue(f).AttackSpeedIncrease);
        Slayer(f, 3184);
        Assert.Equal(0, SlayerValue(f).MovementSpeedIncrease); // Refunding/reallocating cannot revive the grant.
    }

    [Theory]
    [InlineData("nonhostile")] [InlineData("minion")] [InlineData("daemon")] [InlineData("phase")]
    public async Task Slayer_rejects_non_enemy_and_phase_kill_credit(string kind)
    {
        using var f = new Contact(new Draws(), "Cyclone", classRow: 4);
        Slayer(f, 3184);
        var m = new SpawnedMonster(kind, new(34, 32, 0), 0, 2, 100)
        { Hostile = kind != "nonhostile", OwnerId = kind == "minion" ? 2u : null,
            Daemon = kind == "daemon", Stage = kind == "phase" ? new(50, 2612, 3, 2) : null };
        f.Conn.Monsters.Clear(); f.Conn.Monsters[42] = m;
        await f.Sweep();
        Assert.Equal(0, SlayerValue(f).RecentKills);
    }

    [Theory]
    [InlineData(null)] [InlineData("Cyclone")] [InlineData("IceNova")] [InlineData("Fireball")]
    public async Task Slayer_Bane_live_unique_and_recent_MORE_scale_actual_hits_once_before_reflected_Impale(string? gem)
    {
        using var f = new Contact(new Draws(), gem, classRow: 4, targetLife: 100000);
        Slayer(f, 62817);
        f.Set("physical_damage_+%", -10000); f.Set("elemental_damage_+%", -10000);
        await SlayerContact(f, gem);
        uint normal = f.Target.MaxLife - f.Target.Life;
        Assert.True(normal > 0);
        var unique = new SpawnedMonster("unique", new(34, 32, 0), 0, 2, f.Target.MaxLife) { Rarity = MonsterRarity.Unique };
        f.Conn.Monsters.Clear(); f.Conn.Monsters[42] = unique;
        f.Conn.NextAttackAt = 0;
        f.Conn.SlayerKills.RecordKill(f.Conn.Clock.Elapsed.TotalSeconds, false, _ => 0);
        unique.Impale.Apply(1000, f.Conn.Clock.Elapsed.TotalSeconds);
        await SlayerContact(f, gem);
        Assert.InRange(unique.MaxLife - unique.Life, (uint)Math.Floor(normal * 1.32) + 99, (uint)Math.Ceiling(normal * 1.32) + 101);
    }

    [Theory]
    [InlineData(15, 1.15)] [InlineData(20, 1.12)] [InlineData(40, 1)] [InlineData(70, 1)]
    public void Slayer_Impact_melee_only_proximity_matches_pinned_PoB_geometry_and_range_removal(int distance, double expected)
    {
        using var f = new Contact(new Draws(), "Cyclone", distance: distance, classRow: 4);
        Slayer(f, 38180);
        var stats = Program.CharacterPlayerStats(f.Conn);
        Assert.Equal(expected, Program.SlayerTargetMore(f.Conn, f.Target, stats, true), 8);
        Assert.Equal(1, Program.SlayerTargetMore(f.Conn, f.Target, stats, false));
        Assert.Equal(14, Program.SlayerMeleeRange(f.Conn, 10));
        f.Conn.MainAscendancy = new(); f.Refresh();
        Assert.Equal(10, Program.SlayerMeleeRange(f.Conn, 10));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Slayer_Overwhelm_counts_live_hostiles_before_target_critical_mitigation_on_real_hits(bool immune)
    {
        using var f = new Contact(new Draws(0, 0, 0, 0), "Cyclone", classRow: 4,
            targetMods: new([], [], new(new Dictionary<string, int> { ["self_take_no_extra_damage_from_critical_strikes"] = immune ? 1 : 0 })));
        Slayer(f, 17315); f.Set("global_cannot_crit", 0); f.Set("extra_critical_rolls_while_on_low_life", 0);
        f.Set("physical_damage_+%", -10000); f.Set("elemental_damage_+%", -10000);
        f.Set("critical_strike_chance_+%", 10000);
        var stats = Program.CharacterPlayerStats(f.Conn);
        int source = f.Multiplier;
        int baseline = Program.SlayerCriticalMultiplier(f.Conn, source, stats);
        Assert.Equal(source + 10, baseline);
        await f.Sweep();
        uint first = f.Target.MaxLife - f.Target.Life;
        f.Target.Life = f.Target.MaxLife; f.Conn.NextAttackAt = 0;
        for (uint id = 43; id < 54; id++)
            f.Conn.Monsters[id] = new("near", new(90, 32, 0), 0, 2, 1000);
        Assert.Equal(source + 100, Program.SlayerCriticalMultiplier(f.Conn, source, stats));
        await f.Sweep();
        uint second = f.Target.MaxLife - f.Target.Life;
        if (immune) Assert.Equal(first, second);
        else Assert.InRange(second / (double)first, (source + 100.0) / (source + 10) - .005,
            (source + 100.0) / (source + 10) + .005);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Slayer_Bane_physical_immunity_has_actual_enemy_reflection_and_refund_negative_control(bool immune)
    {
        using var f = new Contact(new Draws(), "Cyclone", classRow: 4,
            targetMods: new([], [], new(new Dictionary<string, int> { ["physical_damage_to_return_when_hit"] = 20 })));
        if (immune) Slayer(f, 62817); else f.Set("global_cannot_crit", 1);
        f.Conn.Pools = f.Conn.Pools with { Armour = 0, EnergyShield = 0 };
        await f.Sweep();
        Assert.Equal(immune ? 50u : 30u, f.Conn.PlayerLife);
        Assert.Equal(!immune, f.Conn.ReflectionDirty);
        if (immune)
        {
            f.Conn.MainAscendancy = new(); f.Refresh(); f.Conn.NextAttackAt = 0;
            f.Conn.Pools = f.Conn.Pools with { Armour = 0, EnergyShield = 0 };
            await f.Sweep(); Assert.Equal(30u, f.Conn.PlayerLife);
        }
        Assert.Equal(0, SlayerValue(f).RecentKills);
    }

    [Fact]
    public async Task Slayer_Endless_unaffected_bleeding_retains_ailment_and_damage_resumes_when_leech_ends()
    {
        using var f = new Contact(new Draws(), "Cyclone", classRow: 4);
        Slayer(f, 34484);
        f.Conn.Pools = f.Conn.Pools with { Life = 1000, EnergyShield = 0 };
        f.Conn.PlayerLife = 1000;
        await f.Sweep(); // Real retained full-Life leech instance.
        double now = f.Conn.Clock.Elapsed.TotalSeconds;
        var stats = Program.CharacterPlayerStats(f.Conn);
        Assert.True(f.Conn.Bleeding.Apply(100, now, 5, _ => 0));
        Assert.Equal(0, Program.AdvancePlayerBleedingLocked(f.Conn, now, now + 1, stats));
        Assert.True(f.Conn.Bleeding.Active(now + 1));
        Program.LeechOf(f.Conn).Clear(now);
        stats = Program.CharacterPlayerStats(f.Conn);
        Assert.Equal(70, Program.AdvancePlayerBleedingLocked(f.Conn, now + 1, now + 2, stats));
        Assert.Equal(930u, f.Conn.PlayerLife);
        f.Set("immune_to_bleeding", 1);
        Program.AdvancePlayerBleedingLocked(f.Conn, now + 2, now + 3, Program.CharacterPlayerStats(f.Conn));
        Assert.False(f.Conn.Bleeding.Active(now + 3));
    }
}
