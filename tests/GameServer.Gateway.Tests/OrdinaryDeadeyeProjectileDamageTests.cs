namespace GameServer.Gateway.Tests;

public sealed class OrdinaryDeadeyeProjectileDamageTests
{
    [Theory]
    [InlineData(0, 1)] [InlineData(10, 1)] [InlineData(35, 1)]
    [InlineData(52.5, 1.15)] [InlineData(70, 1.3)] [InlineData(120, 1.3)]
    public void Distinct_Far_Shot_ramp_has_no_close_penalty_and_includes_spell_hits_and_ailments(double distance, double more)
    {
        var source = DeadeyeProjectileDamage.Capture(s => s == DeadeyeProjectileDamage.FarShot ? 30 : 0);
        Assert.Equal(more, source.HitMultiplier(distance, attack: true), 12);
        Assert.Equal(more, source.HitMultiplier(distance, attack: false), 12);
        Assert.Equal(more, source.AilmentMultiplier(distance), 12);
        Assert.Equal(1, default(DeadeyeProjectileDamage).HitMultiplier(distance, attack: true));
    }

    [Theory]
    [InlineData(0, 1.3)] [InlineData(10, 1.3)] [InlineData(22.5, 1.15)]
    [InlineData(35, 1)] [InlineData(70, 1.3 * (1 - .3 * 35 / 85))]
    [InlineData(120, .91)] [InlineData(150, .91)]
    public void Point_Blank_composes_multiplicatively_for_attack_hits_only(double distance, double expected)
    {
        var source = new DeadeyeProjectileDamage(30, PointBlank: true);
        Assert.Equal(expected, source.HitMultiplier(distance, attack: true), 12);
        Assert.Equal(source.AilmentMultiplier(distance), source.HitMultiplier(distance, attack: false));
    }

    [Fact]
    public void Legacy_Far_Shot_remains_an_independent_attack_hit_multiplier()
    {
        var source = new DeadeyeProjectileDamage(30, false);
        Assert.Equal(.8, source.HitMultiplier(10, true) * AntiquarianEffects.FarShot(10), 12);
        Assert.Equal(2.08, source.HitMultiplier(70, true) * AntiquarianEffects.FarShot(70), 12);
        Assert.Equal(1.3, source.AilmentMultiplier(70));
        Assert.Equal(1.3, source.HitMultiplier(70, false));
    }

    [Fact]
    public void Capture_freezes_sources_and_ignite_scales_each_range_once_without_touching_chance_or_duration()
    {
        var raw = new Dictionary<string, int> { [DeadeyeProjectileDamage.FarShot] = 30, [DeadeyeProjectileDamage.PointBlankStat] = 1 };
        var source = DeadeyeProjectileDamage.Capture(s => raw.GetValueOrDefault(s));
        raw.Clear();
        var ignite = new MonsterIgnites.Settings(37, 10, 20, 30, 40, 7);
        Assert.Equal(new(37, 13, 26, 39, 52, 7), source.IgniteAt(ignite, 70));
        Assert.Equal(ignite, source.IgniteAt(ignite, 35));
        Assert.Equal(ignite, DeadeyeProjectileDamage.Capture(s => raw.GetValueOrDefault(s)).IgniteAt(ignite, 70));
        Assert.Equal(30, source.MaximumMorePercent);
        Assert.True(source.PointBlank);
        Assert.Equal(0, DeadeyeProjectileDamage.Capture(_ => -100).MaximumMorePercent);
    }

    [Theory]
    [InlineData(false, 13)] [InlineData(true, 39)]
    public void Scaled_ignite_reaches_existing_strongest_ignite_engine_with_critical_choice_preserved(bool critical, int expected)
    {
        var source = new DeadeyeProjectileDamage(30, true);
        var ignite = new MonsterIgnites();
        var settings = source.IgniteAt(new(100, 10, 10, 30, 30, 4), 70);
        Assert.True(ignite.TryApply(settings, critical, 0, 0, false, new Random(1)));
        Assert.Equal(expected, ignite.Advance(1, 1));
        Assert.Equal(4, ignite.Until);
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(-1)]
    public void Invalid_contact_distance_is_refused(double distance) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new DeadeyeProjectileDamage(30, true).HitMultiplier(distance, true));
}
