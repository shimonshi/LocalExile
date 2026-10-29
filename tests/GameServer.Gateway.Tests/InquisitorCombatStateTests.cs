namespace GameServer.Gateway.Tests;

public class InquisitorCombatStateTests
{
    static InquisitorCombatState.Inputs Enabled => new(10, 3, 1, true, true, 5);

    [Fact]
    public void Virtue_counts_manual_non_instant_spells_in_a_rolling_eight_second_window()
    {
        var state = new InquisitorCombatState();
        state.Spell(0, Enabled, instant: false);
        state.Spell(1, Enabled, instant: true);
        state.Spell(2, Enabled, instant: false, triggered: true);
        state.Spell(3, Enabled, instant: false);
        state.Spell(4, Enabled, instant: false);
        state.Spell(5, Enabled, instant: false);
        Assert.Equal(30, state.Read(7.9, Enabled).AttackMorePct);
        Assert.Equal(30, state.Read(8, Enabled).AttackMorePct);
        Assert.Equal(20, state.Read(11, Enabled).AttackMorePct);
        Assert.Equal(10, state.Read(12, Enabled).AttackMorePct);
        Assert.Equal(0, state.Read(13, Enabled).AttackMorePct);
    }

    [Fact]
    public void Casts_before_the_source_and_refunded_casts_do_not_resurrect()
    {
        var state = new InquisitorCombatState();
        var noVirtue = Enabled with { AttackMorePerSpell = 0 };
        state.Spell(0, noVirtue, instant: false);
        Assert.Equal(0, state.Read(1, Enabled).AttackMorePct);
        state.Spell(2, Enabled, instant: false);
        Assert.Equal(10, state.Read(2, Enabled).AttackMorePct);
        Assert.Equal(0, state.Read(3, noVirtue).AttackMorePct);
        Assert.Equal(0, state.Read(4, Enabled).AttackMorePct);
    }

    [Fact]
    public void Repeated_attacks_in_one_interval_award_one_charge_and_three_intervals_trigger_the_buff()
    {
        var state = new InquisitorCombatState();
        state.Attack(0, Enabled);
        state.Attack(0.2, Enabled);
        state.Attack(0.9, Enabled);
        Assert.Equal(0, state.Read(0.99, Enabled).FanaticCharges);
        Assert.Equal(1, state.Read(1, Enabled).FanaticCharges);
        state.Attack(1.5, Enabled);
        Assert.Equal(2, state.Read(2, Enabled).FanaticCharges);
        state.Attack(2.5, Enabled);
        var active = state.Read(3, Enabled);
        Assert.Equal(0, active.FanaticCharges);
        Assert.Equal(8, active.FanaticismUntil);
        Assert.True(active.Fanaticism(7.999));
        Assert.False(state.Read(8, Enabled).Fanaticism(8));
    }

    [Fact]
    public void A_late_read_does_not_award_charges_for_seconds_with_no_attack()
    {
        var state = new InquisitorCombatState();
        state.Attack(0, Enabled);
        Assert.Equal(1, state.Read(1000, Enabled).FanaticCharges);
        state.Attack(1000, Enabled);
        Assert.Equal(1, state.Read(1000, Enabled).FanaticCharges);
        Assert.Equal(2, state.Read(1001, Enabled).FanaticCharges);
    }

    [Fact]
    public void An_attack_at_a_tick_is_not_retroactively_counted_for_that_tick()
    {
        var state = new InquisitorCombatState();
        state.Configure(0, Enabled);
        state.Attack(1, Enabled);
        Assert.Equal(0, state.Read(1, Enabled).FanaticCharges);
        Assert.Equal(1, state.Read(2, Enabled).FanaticCharges);
    }

    [Fact]
    public void Removing_zeal_clears_charges_and_the_active_buff_and_does_not_retain_old_attacks()
    {
        var state = Active();
        var refunded = Enabled with { GainFanaticismAtMaximum = false };
        Assert.False(state.Read(3.5, refunded).Fanaticism(3.5));
        Assert.Equal(0, state.Read(4, Enabled).FanaticCharges);
        Assert.False(state.Read(5, Enabled).Fanaticism(5));
        state.Clear();
        Assert.Equal(0, state.Read(6, Enabled).FanaticCharges);
    }

    [Fact]
    public void Area_carry_freezes_loading_time_and_retires_the_old_owner()
    {
        var old = Active();
        old.Spell(3.25, Enabled, instant: false);
        var carried = old.Carry(3.5, Enabled);
        Assert.Equal(0, old.Read(4, Enabled).AttackMorePct);
        old.Spell(4, Enabled, instant: false);
        old.Attack(4, Enabled);
        Assert.Equal(0, old.Read(5, Enabled).FanaticCharges);
        Assert.Equal(0, old.Carry(5, Enabled).FanaticismRemaining);
        var next = new InquisitorCombatState();
        next.Restore(carried, 0, Enabled);
        Assert.Equal(10, next.Read(0, Enabled).AttackMorePct);
        Assert.True(next.Read(4.499, Enabled).Fanaticism(4.499));
        Assert.False(next.Read(4.5, Enabled).Fanaticism(4.5));
        Assert.Equal(0, next.Read(7.75, Enabled).AttackMorePct);
    }

    [Fact]
    public void A_refunded_destination_cannot_restore_carried_sources()
    {
        var carried = Active().Carry(3.5, Enabled);
        var state = new InquisitorCombatState();
        state.Restore(carried, 10, default);
        Assert.Equal(0, state.Read(10, default).FanaticCharges);
        Assert.False(state.Read(10, default).Fanaticism(10));
    }

    static InquisitorCombatState Active()
    {
        var state = new InquisitorCombatState();
        state.Attack(0, Enabled);
        state.Attack(1, Enabled);
        state.Attack(2, Enabled);
        Assert.True(state.Read(3, Enabled).Fanaticism(3));
        return state;
    }
}
