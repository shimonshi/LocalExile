using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public partial class ConditionalCriticalGatewayTests
{
    sealed partial class Contact
    {
        public Task SlayerBurn(bool ignite) => (Task)Method(ignite ? "BurnIgnited" : "BurnAround").Invoke(null,
            ignite ? ["slayer-dot", writer.GetStream(), ciphers, Conn]
                : ["slayer-dot", writer.GetStream(), ciphers, Conn,
                    new RighteousFireState(0, 0, null, Skill, 6000, 0, 50), 1.0])!;
    }
    [Fact]
    public async Task Slayer_Headsman_clock_freezes_pause_loading_retry_and_carries_once_to_matching_character()
    {
        using var f = new Contact(new Draws(), "Cyclone", classRow: 4);
        Slayer(f, 3184, 38180);
        f.Target.Life = 1; await f.Sweep();
        f.Conn.Clock.Stop();
        double now = f.Conn.Clock.Elapsed.TotalSeconds;
        var before = f.Conn.SlayerKills.Carry(now);
        await Task.Delay(30);
        Assert.Equal(before.Movement, f.Conn.SlayerKills.Carry(now).Movement);
        var change = new PendingTransfer(0, 0, false);
        Assert.True(f.Conn.TryBeginAreaChange(change, out _));
        Assert.False(f.Conn.TryBeginAreaChange(change, out _));
        f.Conn.Clock.Start(); await Task.Delay(30);
        f.Conn.ResumeAfterFailedTransfer();
        f.Conn.Clock.Stop();
        var resumed = f.Conn.SlayerKills.Carry(f.Conn.Clock.Elapsed.TotalSeconds);
        Assert.InRange(resumed.Movement, before.Movement - .01, before.Movement);
        Assert.InRange(resumed.Kills.Single(), before.Kills.Single() - .01, before.Kills.Single());
        uint ticket = unchecked((uint)Random.Shared.Next(100000, int.MaxValue));
        Method("CarrySlayer").Invoke(null, [f.Conn, ticket]);
        Assert.Equal(0, SlayerValue(f).RecentKills);
        var wrong = new ClientState(f.Conn.Layout, ticket, 1) { Character = "wrong", PlayerLife = 100 };
        Program.RestoreSlayer(wrong);
        Assert.Empty(wrong.SlayerKills.Carry(0).Kills);
        var destination = new ClientState(f.Conn.Layout, ticket, 1) { Character = f.Conn.Character, PlayerLife = 100 };
        Program.RestoreSlayer(destination);
        Assert.Single(destination.SlayerKills.Carry(0).Kills);
        Program.RestoreSlayer(destination);
        Assert.Single(destination.SlayerKills.Carry(0).Kills); // ticket consumption is idempotent
        destination.PlayerLife = 0;
        Assert.Empty(destination.SlayerKills.Carry(0).Kills);
        Assert.Equal(0, destination.SlayerKills.Carry(0).Movement);
        f.Conn.Retire(); Assert.Equal(0, SlayerValue(f).MovementSpeedIncrease);
    }

    [Fact]
    public void Slayer_Headsman_shared_recipient_rate_changes_only_remaining_buff_time_and_recent_history_uses_world_time()
    {
        var state = new SlayerKillState();
        var stats = new Dictionary<string, int> { [SlayerRules.KillMovementSpeed] = 10,
            [SlayerRules.RareKillAttackSpeed] = 10, [SlayerRules.RecentKillArea] = 5 };
        int Get(string n) => stats.GetValueOrDefault(n);
        state.RecordKill(0, true, Get);
        stats["buff_time_passed_+%_only_buff_category"] = 100;
        Assert.True(state.Read(3, Get).KilledRecently);
        stats["buff_time_passed_+%"] = 100;
        state.Read(5, Get); // 15 effect seconds remain; now expire at 12.5.
        Assert.False(state.Read(5, Get).KilledRecently);
        Assert.Equal(10, state.Read(12.49, Get).AttackSpeedIncrease);
        Assert.Equal(0, state.Read(12.5, Get).AttackSpeedIncrease);
        state.RecordKill(20, true, Get);
        var carried = state.Carry(22);
        Assert.Equal(16, carried.Movement);
        state.Restore(carried, 100);
        Assert.Equal(10, state.Read(107.99, Get).MovementSpeedIncrease);
        Assert.Equal(0, state.Read(108, Get).MovementSpeedIncrease);
    }

    [Fact]
    public async Task Slayer_Impact_actual_melee_reach_stops_the_attack_approach_four_cells_further_and_removes_with_source()
    {
        using var f = new Contact(new Draws(), distance: 15, classRow: 4);
        Slayer(f, 38180);
        int range = Program.CharacterPlayerStats(f.Conn)[StatKeys.MainHandMaxAttackDistance];
        await f.Use();
        int withImpact = f.Conn.Walk.Position(f.Conn.Clock.Elapsed.TotalSeconds).X;
        f.Conn.MainAscendancy = new(); f.Refresh(); f.Conn.NextAttackAt = 0;
        f.Conn.Walk.Teleport(32, 32, f.Conn.Clock.Elapsed.TotalSeconds);
        f.Set("global_cannot_crit", 1);
        await f.Use();
        Assert.InRange(f.Conn.Walk.Position(f.Conn.Clock.Elapsed.TotalSeconds).X - withImpact, 3, 5);
        Assert.Equal(range - 4, Program.CharacterPlayerStats(f.Conn)[StatKeys.MainHandMaxAttackDistance]);
    }

    [Fact]
    public async Task Slayer_Impact_rolling_kills_expire_individually_and_real_area_contact_admits_the_larger_radius()
    {
        using var f = new Contact(new Draws(), "Cyclone", distance: 20, classRow: 4);
        Slayer(f, 38180);
        // The actual Cyclone radius starts below 20 cells; ten recent kills bring it above the target distance.
        await f.Sweep(); Assert.Equal(f.Target.MaxLife, f.Target.Life);
        for (int i = 0; i < 12; i++) f.Conn.SlayerKills.RecordKill(f.Conn.Clock.Elapsed.TotalSeconds, false, _ => 0);
        Assert.Equal(50, SlayerValue(f).AreaIncrease);
        f.Conn.NextAttackAt = 0;
        await f.Sweep(); Assert.True(f.Target.Life < f.Target.MaxLife);
        double now = f.Conn.Clock.Elapsed.TotalSeconds;
        f.Conn.SlayerKills.Read(now + 4, _ => 0);
        Assert.Equal(0, SlayerValue(f).AreaIncrease);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Slayer_Headsman_held_output_rebuilds_expired_or_refunded_grants(bool expire)
    {
        using var f = new Contact(new Draws(), "Cyclone", classRow: 4);
        Slayer(f, 3184);
        f.Conn.SlayerKills.RecordKill(f.Conn.Clock.Elapsed.TotalSeconds, true, n =>
            n is SlayerRules.KillMovementSpeed or SlayerRules.RareKillAttackSpeed ? 10 : 0);
        f.Refresh();
        await f.SendGate.WaitAsync();
        var pending = f.RefreshStats(charges: false);
        Assert.False(pending.IsCompleted);
        if (expire)
        {
            f.Set("buff_time_passed_+%", 1000000);
            SlayerValue(f); // Change the same recipient clock before the queued packet can be assembled.
            await Task.Delay(30);
        }
        else f.Conn.MainAscendancy = new();
        f.Refresh();
        f.SendGate.Release();
        await pending;
        await f.AssertNextPlayerStats();
        Assert.Equal(0, SlayerValue(f).MovementSpeedIncrease);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Slayer_Bane_applies_unique_and_recent_damage_to_real_ignite_and_RF_without_rebuilding_from_hits(bool ignite)
    {
        using var f = new Contact(new Draws(), "Fireball", classRow: 4);
        Slayer(f, 62817);
        var m = new SpawnedMonster("unique burn", new(34, 32, 0), 0, 2, 100000) { Rarity = MonsterRarity.Unique };
        f.Conn.Monsters.Clear(); f.Conn.Monsters[42] = m;
        f.Conn.Clock.Stop();
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        runtime.Advance(1); // Recipient-clock integration uses non-negative monotonic world timestamps.
        const double now = 1;
        f.Conn.SlayerKills.RecordKill(now, false, _ => 0);
        if (ignite) Assert.True(m.Ignites.TryApply(new(100, 100, 100, 100, 100, 4), false, now - 1, 0, false, new Random(1)));
        await f.SlayerBurn(ignite);
        Assert.Equal(132u, m.MaxLife - m.Life);
        m.Life = 1;
        if (ignite)
        {
            m.Ignites.Clear();
            m.Ignites.TryApply(new(100, 100, 100, 100, 100, 4), false, now, 0, false, new Random(1));
            runtime.Advance(2);
        }
        await f.SlayerBurn(ignite);
        Assert.True(m.Dead);
        Assert.Equal(2, SlayerValue(f).RecentKills); // One initial history entry plus exactly one accepted DoT kill.
        await f.SlayerBurn(ignite);
        Assert.Equal(2, SlayerValue(f).RecentKills);
    }
}
