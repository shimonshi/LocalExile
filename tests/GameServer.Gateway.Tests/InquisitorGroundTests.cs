namespace GameServer.Gateway.Tests;

public class InquisitorGroundTests
{
    static readonly InquisitorGround.Sources Sanctuary = new(18, 15, 0, false);
    static readonly InquisitorGround.Sources Pious = new(18, 15, 4000, true);

    [Fact]
    public void Sanctuary_only_exists_while_stationary_and_checks_the_radius_boundary()
    {
        var state = new InquisitorGround();
        state.Observe(0, (100, 200), false, true, Sanctuary);
        Assert.True(state.OwnerAffected(0));
        Assert.True(state.Covers((118, 200)));
        Assert.False(state.Covers((119, 200)));
        Assert.Equal(15, state.EnemyDamageTaken(0, 1, (118, 200), true));
        state.Observe(1, (100, 200), true, true, Sanctuary);
        Assert.False(state.HasStationaryGround);
        Assert.False(state.OwnerAffected(1));
        Assert.Equal(0, state.EnemyDamageTaken(1, 1, (118, 200), true));
    }

    [Fact]
    public void Pious_preserves_effects_on_existing_recipients_but_not_the_old_ground()
    {
        var state = new InquisitorGround();
        state.Observe(0, (0, 0), false, true, Pious);
        Assert.Equal(15, state.EnemyDamageTaken(0, 1, (10, 0), true));
        state.Observe(1, (0, 0), true, true, Pious);
        Assert.False(state.Covers((0, 0)));
        Assert.True(state.OwnerAffected(4.999));
        Assert.True(state.OwnerMirrorsLifeRegeneration(4.999));
        Assert.Equal(15, state.EnemyDamageTaken(4.999, 1, (10, 0), true));
        Assert.Equal(0, state.EnemyDamageTaken(2, 2, (10, 0), true));
        Assert.False(state.OwnerAffected(5));
        Assert.Equal(0, state.EnemyDamageTaken(5, 1, (10, 0), true));
    }

    [Fact]
    public void An_enemy_leaving_an_active_ground_has_its_own_linger_deadline()
    {
        var state = new InquisitorGround();
        state.Observe(0, (0, 0), false, true, Pious);
        Assert.Equal(15, state.EnemyDamageTaken(0, 1, (1, 0), true));
        Assert.Equal(15, state.EnemyDamageTaken(2, 1, (19, 0), true));
        Assert.Equal(15, state.EnemyDamageTaken(5.999, 1, (19, 0), true));
        Assert.Equal(0, state.EnemyDamageTaken(6, 1, (19, 0), true));
        Assert.True(state.OwnerAffected(6));
    }

    [Fact]
    public void Refunding_pious_clears_linger_while_retaining_stationary_sanctuary()
    {
        var state = new InquisitorGround();
        state.Observe(0, (0, 0), false, true, Pious);
        state.Observe(1, (0, 0), true, true, Pious);
        state.Observe(2, (0, 0), true, true, Sanctuary);
        Assert.False(state.OwnerAffected(2));
        Assert.False(state.OwnerMirrorsLifeRegeneration(2));
        state.Observe(3, (0, 0), false, true, Sanctuary);
        Assert.True(state.OwnerAffected(3));
        Assert.False(state.OwnerMirrorsLifeRegeneration(3));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Death_or_removing_all_sources_clears_recipients_without_reviving_linger(bool death)
    {
        var state = new InquisitorGround();
        state.Observe(0, (0, 0), false, true, Pious);
        Assert.Equal(15, state.EnemyDamageTaken(0, 1, (0, 0), true));
        state.Observe(1, (0, 0), true, !death, death ? Pious : default);
        Assert.False(state.OwnerAffected(1));
        Assert.Equal(0, state.EnemyDamageTaken(1, 1, (0, 0), true));
        state.Observe(2, (0, 0), true, true, Pious);
        Assert.False(state.OwnerAffected(2));
    }

    [Fact]
    public void Own_external_ground_has_lingering_effects_even_without_stationary_creation()
    {
        var state = new InquisitorGround();
        var piousOnly = Pious with { Radius = 0 };
        state.Observe(0, (0, 0), true, true, piousOnly, externalOwnGround: true);
        Assert.True(state.OwnerAffected(0));
        Assert.False(state.HasStationaryGround);
        Assert.Equal(15, state.EnemyDamageTaken(0, 1, (1000, 0), true, externalOwnGround: true));
        state.Observe(1, (0, 0), true, true, piousOnly);
        Assert.True(state.OwnerAffected(4.999));
        Assert.False(state.OwnerAffected(5));
    }

    [Fact]
    public void Regeneration_integral_stops_at_exact_expiry_and_never_copies_other_recovery()
    {
        var state = new InquisitorGround();
        state.Observe(10, (0, 0), false, true, Pious);
        state.Observe(11, (0, 0), true, true, Pious);
        Assert.Equal(50, state.MirroredLifeRegeneration(9, 16, 10));
        Assert.Equal(5, state.MirroredLifeRegeneration(14.5, 16, 10));
        Assert.Equal(0, state.MirroredLifeRegeneration(15, 16, 10));
        Assert.Equal(0, state.MirroredLifeRegeneration(10, 14, 0));
    }

    [Fact]
    public void Reentering_after_a_gap_does_not_recover_the_gap_and_paused_time_does_not_expire()
    {
        var state = new InquisitorGround();
        state.Observe(0, (0, 0), false, true, Pious);
        state.Observe(1, (0, 0), true, true, Pious);
        state.Observe(3, (0, 0), true, true, Pious);
        state.Observe(3, (0, 0), true, true, Pious);
        Assert.Equal(5, state.OwnerEffectUntil);
        state.Observe(10, (0, 0), false, true, Pious);
        Assert.Equal(10, state.MirroredLifeRegeneration(5, 11, 10));
    }

    [Fact]
    public void Friendly_or_dead_targets_never_receive_the_increased_damage_taken_effect()
    {
        var state = new InquisitorGround();
        state.Observe(0, (0, 0), false, true, Pious);
        Assert.Equal(0, state.EnemyDamageTaken(0, 1, (0, 0), false));
        Assert.Equal(15, state.EnemyDamageTaken(0, 2, (0, 0), true));
        Assert.Equal(0, state.EnemyDamageTaken(1, 2, (0, 0), false));
        state.Observe(2, (0, 0), true, true, Pious);
        Assert.Equal(0, state.EnemyDamageTaken(2, 2, (0, 0), true));
    }
}
