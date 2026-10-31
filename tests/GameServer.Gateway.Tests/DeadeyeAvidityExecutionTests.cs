namespace GameServer.Gateway.Tests;

public sealed class DeadeyeAvidityExecutionTests
{
    [Theory]
    [InlineData(101, 10, 5, 1, 116)]
    [InlineData(101, 10, 5, 0, 111)]
    [InlineData(101, 10, 0, 4, 111)]
    [InlineData(101, 10, 5, 4, 133)]
    [InlineData(1, 99, 5, 1, 2)] // floor(1.99*1.05)=2, never floor(1.99)*1.05=1
    [InlineData(-1, 999, 5, 10, 0)] [InlineData(100, -100, 5, 10, 0)]
    [InlineData(100, 0, -100, 2, 0)]
    public void More_accuracy_runs_after_unrounded_inputs_and_before_one_floor(long basis, long increased,
        int per, int count, int expected) =>
        Assert.Equal(expected, DeadeyeAvidityAccuracy.Calculate(basis, increased, per, count));

    [Theory]
    [InlineData(0, 0)] [InlineData(-1, 0)] [InlineData(123, 123)]
    public void Explicit_final_replacement_including_zero_wins(int replacement, int expected) =>
        Assert.Equal(expected, DeadeyeAvidityAccuracy.Calculate(1000, 100, 5, 4, replacement));

    [Fact]
    public void Wide_intermediates_saturate_after_the_only_floor() =>
        Assert.Equal(int.MaxValue, DeadeyeAvidityAccuracy.Calculate(long.MaxValue, long.MaxValue, int.MaxValue, int.MaxValue));

    [Fact]
    public void Actual_charge_engine_counts_current_standard_Frenzy_and_suppresses_converted_Affliction()
    {
        var stats = new Dictionary<string, int> { ["max_frenzy_charges"] = 7 };
        var charges = new ChargeSession(); charges.Observe(stats.GetValueOrDefault);
        Assert.Equal(0, DeadeyeAvidityAccuracy.StandardFrenzy(charges));
        charges.State.Set(ChargeType.Frenzy, 3, 0, charges.Limits(ChargeType.Frenzy));
        Assert.Equal(3, DeadeyeAvidityAccuracy.StandardFrenzy(charges)); // maximum is10, not3
        stats[Charges.ConversionFlag(ChargeType.Frenzy)] = 1; charges.Observe(stats.GetValueOrDefault);
        Assert.Equal(3, charges.Effective(ChargeType.Frenzy));
        Assert.Equal(0, DeadeyeAvidityAccuracy.StandardFrenzy(charges));
        stats[Charges.ConversionFlag(ChargeType.Frenzy)] = 0; charges.Observe(stats.GetValueOrDefault);
        Assert.Equal(3, DeadeyeAvidityAccuracy.StandardFrenzy(charges));
        charges.State.Expire(10); Assert.Equal(0, DeadeyeAvidityAccuracy.StandardFrenzy(charges));
    }

    [Fact]
    public void Actual_accuracy_support_inputs_enter_same_flat_increased_buckets_before_Avidity_and_target_evasion()
    {
        var skills = Program.Skills; var supports = new GemSupports();
        var support = Assert.Single(supports.Resolve(skills.EffectRow("BurningArrow"),
            [new(skills.EffectRow("SupportAdditionalAccuracy"), 1, 0, 1)], skills, weaponClass: 13));
        long Own(string name) => Program.Stats.Key(name) is uint key
            ? support.Stats.Where(v => v.Key == key).Sum(v => (long)v.Value) : 0;
        Assert.True(Own("accuracy_rating") > 0);
        var pools = new CharacterStats.Pools(1, 100, 100, 1, 0, AccuracyPct: 99) { AccuracyBase = 1 };
        var weapon = new WeaponProfile(false, false, false, true, false, LocalAccuracy: 500);
        int expected = (int)Math.Floor((1 + Own("accuracy_rating")) * (1 + (99 + Own("accuracy_rating_+%")) / 100d) * 1.2);
        int rating = DeadeyeAvidityAccuracy.Selected(pools, weapon, Own, 5, 4);
        Assert.Equal(expected, rating); // local500 already belongs in the character base, never added again
        var accuracy = new AttackAccuracyProfile(rating, 0, 0, false);
        var plain = accuracy with { Rating = DeadeyeAvidityAccuracy.Selected(pools, weapon, Own, 0, 4) };
        Assert.True(accuracy.Chance(500, false, false) >= plain.Chance(500, false, false));
        Assert.Equal(100, (accuracy with { CannotBeEvaded = true }).Chance(int.MaxValue, false, false));
    }
}
