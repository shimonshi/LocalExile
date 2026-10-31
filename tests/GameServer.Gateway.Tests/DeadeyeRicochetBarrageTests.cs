using GameServer.Core.Projectiles;

namespace GameServer.Gateway.Tests;

public sealed class DeadeyeRicochetBarrageTests
{
    [Fact]
    public void Pinned_Barrage_stats_emit_six_sequential_projectiles_and_Far_Shot_only_removes_their_spread()
    {
        int row = Program.Skills.EffectRow("Barrage"); Assert.True(row >= 0);
        var stats = Program.Skills.EffectStats(row, 1, 0).ToDictionary(v => v.Item1, v => v.Item2);
        int Get(string name) => Program.Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
        Assert.Equal(6, Get("base_number_of_projectiles")); Assert.Equal(1, Get("projectiles_barrage"));
        var baseProfile = GatewayProjectileDefaults.Resolve("Metadata/Projectiles/DefaultArrow", Get, 100, true);
        var barrage = DeadeyeRicochetProfile.Barrage(baseProfile, "Barrage", true, Get, 1);
        Assert.True(barrage.Barrage); Assert.Equal(6, barrage.Count); Assert.True(barrage.RandomSpread);
        Assert.True(barrage.SequentialDelay > 0); Assert.True(barrage.SpreadRadians > 0);
        stats[Program.Stats.Key(DeadeyeRicochetProfile.NoSpread)!.Value] = 1;
        var straight = DeadeyeRicochetProfile.Barrage(baseProfile, "Barrage", true, Get, 1);
        Assert.Equal(0, straight.SpreadRadians); Assert.Equal(barrage.Count, straight.Count);
        Assert.Equal(barrage.SequentialDelay, straight.SequentialDelay);
    }

    [Fact]
    public void Actual_Barrage_support_changes_supported_bow_recipe_quantity_timing_and_damage_without_extra_uses()
    {
        var skills = Program.Skills; var supports = new GemSupports();
        var applied = Assert.Single(supports.Resolve(skills.EffectRow("BurningArrow"),
            [new(skills.EffectRow("SupportBarrage"), 1, 0, 1)], skills, weaponClass: 13));
        var stats = applied.Stats.ToDictionary(v => v.Key, v => v.Value);
        int Get(string name) => Program.Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
        Assert.Equal(1, Get("projectiles_barrage")); Assert.Equal(4, Get("number_of_additional_projectiles"));
        Assert.Equal(-50, Get("support_barrage_damage_+%_final"));
        var p = GatewayProjectileDefaults.Resolve("Metadata/Projectiles/DefaultArrow", Get, 100, true);
        p = DeadeyeRicochetProfile.Barrage(p, "BurningArrow", true, Get, 1);
        Assert.Equal(5, p.Count); Assert.Equal(1.5, DeadeyeRicochetProfile.AttackSeconds(1, p, Get));
        Assert.Equal(ProjectileRuntime.Time(.15), p.SequentialDelay);
        Assert.Equal(200, applied.CostMultiplier); // pinned data: one supported use, not once per projectile
    }

    [Theory]
    [InlineData(3)] [InlineData(9)]
    public void Ordinary_small_and_large_fans_keep_spread_quantity_and_simultaneous_admission(int count)
    {
        int Get(string key) => key == DeadeyeRicochetProfile.NoSpread ? 1 : 0;
        var fan = new ProjectileProfile("ordinary") { Count = count, SpreadRadians = .6 };
        Assert.Equal(fan, DeadeyeRicochetProfile.Barrage(fan, "SplitArrow", true, Get, 1));
        Assert.Equal(fan, DeadeyeRicochetProfile.Barrage(fan, "Fireball", false, Get, 1));
    }

    [Fact]
    public void Barrage_preserves_primary_modifiers_and_rejects_conflicting_nonprimary_count_consumer()
    {
        int Get(string key) => key switch { "projectiles_barrage" => 1,
            "number_of_additional_projectiles" => 2, "virtual_projectiles_cannot_chain" => 1, _ => 0 };
        var p = GatewayProjectileModifiers.Resolve(new("arrow") { Chain = 1 }, Get, true);
        var b = DeadeyeRicochetProfile.Barrage(p, "BurningArrow", true, Get, 1);
        Assert.Equal(3, b.Count); Assert.True(b.CannotChain); Assert.Equal(1, b.Chain);
        Assert.Throws<InvalidOperationException>(() => DeadeyeRicochetProfile.Barrage(
            b with { CountConsumer = ProjectileCountConsumer.ContactAreas }, "BurningArrow", true, Get, 1));
    }

    [Fact]
    public void Released_terrain_input_is_frozen_source_removal_only_changes_the_next_profile()
    {
        var source = new Dictionary<string, int> { [DeadeyeRicochetProfile.TerrainChance] = 30 };
        var released = DeadeyeRicochetProfile.Terrain(new("arrow"), source.GetValueOrDefault);
        source.Clear();
        Assert.Equal(30, released.TerrainChainChancePercent);
        Assert.Equal(0, DeadeyeRicochetProfile.Terrain(new("arrow"), source.GetValueOrDefault).TerrainChainChancePercent);
    }
}
