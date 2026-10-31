namespace GameServer.Gateway.Tests;

public class OrdinarySlayerKillStateTests
{
    [Fact]
    public void Recently_expires_each_kill_at_four_seconds_but_Headsman_buffs_last_twenty()
    {
        var raw = OrdinarySlayerRulesTests.Sources(3184, 38180);
        int Get(string n) => raw.GetValueOrDefault(n);
        var state = new SlayerKillState();
        state.RecordKill(0, false, Get);
        state.RecordKill(2, true, Get);
        Assert.Equal(new SlayerKillState.Snapshot(2, 10, 10, 10), state.Read(3.999, Get));
        Assert.Equal(new SlayerKillState.Snapshot(1, 10, 10, 5), state.Read(4, Get));
        Assert.Equal(new SlayerKillState.Snapshot(0, 10, 10, 0), state.Read(6, Get));
        Assert.Equal(new SlayerKillState.Snapshot(0, 10, 10, 0), state.Read(21.999, Get));
        Assert.Equal(default, state.Read(22, Get));
    }

    [Fact]
    public void Normal_kill_refreshes_movement_only_and_neither_buff_stacks()
    {
        var raw = OrdinarySlayerRulesTests.Sources(3184);
        int Get(string n) => raw.GetValueOrDefault(n);
        var state = new SlayerKillState();
        state.RecordKill(0, true, Get);
        state.RecordKill(19, false, Get);
        Assert.Equal(10, state.Read(19, Get).MovementSpeedIncrease);
        Assert.Equal(10, state.Read(19, Get).AttackSpeedIncrease);
        Assert.Equal(0, state.Read(20, Get).AttackSpeedIncrease);
        Assert.Equal(10, state.Read(38.999, Get).MovementSpeedIncrease);
        Assert.Equal(default, state.Read(39, Get));
    }

    [Fact]
    public void Impact_caps_fifty_increased_area_and_old_kills_expire_independently()
    {
        var raw = OrdinarySlayerRulesTests.Sources(38180);
        int Get(string n) => raw.GetValueOrDefault(n);
        var state = new SlayerKillState();
        for (int i = 0; i < 12; i++) state.RecordKill(i / 10.0, false, Get);
        Assert.Equal(12, state.Read(1.1, Get).RecentKills);
        Assert.Equal(50, state.Read(1.1, Get).AreaIncrease);
        Assert.Equal(50, state.Read(4.1, Get).AreaIncrease);
        Assert.Equal(45, state.Read(4.2, Get).AreaIncrease);
        Assert.Equal(default, state.Read(5.1, Get));
        Assert.Equal(50, SlayerRules.RecentKillAreaBonus(_ => int.MaxValue, int.MaxValue));
    }

    [Fact]
    public void Kill_before_Headsman_allocation_does_not_retroactively_grant_twenty_second_buffs()
    {
        var state = new SlayerKillState();
        state.RecordKill(0, true, _ => 0);
        var raw = OrdinarySlayerRulesTests.Sources(3184);
        Assert.Equal(new SlayerKillState.Snapshot(1, 0, 0, 0), state.Read(1, raw.GetValueOrDefault));
    }

    [Fact]
    public void Paid_refund_then_reallocation_cannot_resurrect_a_buff_and_clear_discards_all_windows()
    {
        var raw = OrdinarySlayerRulesTests.Sources(3184, 38180);
        int Get(string n) => raw.GetValueOrDefault(n);
        var state = new SlayerKillState();
        state.RecordKill(0, true, Get);
        raw.Remove(SlayerRules.KillMovementSpeed);
        raw.Remove(SlayerRules.RareKillAttackSpeed);
        Assert.Equal(new SlayerKillState.Snapshot(1, 0, 0, 5), state.Read(1, Get));
        raw[SlayerRules.KillMovementSpeed] = raw[SlayerRules.RareKillAttackSpeed] = 10;
        Assert.Equal(new SlayerKillState.Snapshot(1, 0, 0, 5), state.Read(2, Get));
        state.RecordKill(3, true, Get);
        state.Clear();
        Assert.Equal(default, state.Read(3, Get));
    }

    [Fact]
    public void Repeated_reads_at_paused_time_do_not_advance_or_duplicate_kill_effects()
    {
        var raw = OrdinarySlayerRulesTests.Sources(3184, 38180);
        var state = new SlayerKillState();
        state.RecordKill(10, true, raw.GetValueOrDefault);
        for (int i = 0; i < 10; i++)
            Assert.Equal(new SlayerKillState.Snapshot(1, 10, 10, 5), state.Read(10, raw.GetValueOrDefault));
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Read(double.NaN, raw.GetValueOrDefault));
    }
}
