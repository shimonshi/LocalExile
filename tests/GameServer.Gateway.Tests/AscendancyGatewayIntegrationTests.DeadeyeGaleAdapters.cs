using GameServer.Core.Projectiles;

namespace GameServer.Gateway.Tests;

public sealed partial class AscendancyGatewayIntegrationTests
{
    // Adapter tests deliberately invoke the owned adapter: common production admission/timing hooks are unapplied.
    static Dictionary<uint, int> GaleProjection(ClientState conn) =>
        Program.WithDeadeyeGale(conn, Program.CharacterPlayerStats(conn));
    static int GaleCount(ClientState conn) => GaleProjection(conn)[Program.Stats.Key("gale_force_stacks")!.Value];

    [Fact]
    public async Task Deadeye_Gale_actual_allocation_paid_refund_failure_reallocation_and_reload_drive_owned_adapter()
    {
        Assert.True(DataAvailable());
        using var game = new GatewayCharacter(classRow: 3, baseClass: 2, choice: "Deadeye",
            family: AscendancyFamily.Normal, refundPoints: 20);
        Assert.False(Program.HasGatheringWinds(game.State));
        Assert.Null((await game.Change([59837, 24848], true)).Error);
        Assert.True(Program.HasGatheringWinds(game.State));
        Assert.True(Program.DeadeyeGaleAdmitUse(game.State, true)); Assert.Equal(1, GaleCount(game.State));
        Assert.False(Program.DeadeyeGaleAdmitUse(game.State, false)); Assert.Equal(1, GaleCount(game.State));
        Assert.Null((await game.Change([24848], false, refundPoints: true)).Error);
        Assert.Equal(0, GaleCount(game.State)); Assert.False(Program.HasGatheringWinds(game.State));
        Assert.Null((await game.Change([24848], true)).Error);
        Assert.Equal(0, GaleCount(game.State));
        Assert.True(Program.DeadeyeGaleAdmitUse(game.State, true)); Assert.Equal(1, GaleCount(game.State));
        Assert.Equal(0, GaleCount(game.Reload())); // runtime stacks are not persisted
        game.State.RefundPoints = 0;
        Assert.NotNull((await game.Change([24848], false, refundPoints: true)).Error);
        Assert.Equal(1, GaleCount(game.State));
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    public async Task Deadeye_Gale_blocked_and_zero_damage_hits_clear_after_preloss_factor(bool blocked, int damage)
    {
        Assert.True(DataAvailable());
        using var game = new GatewayCharacter(classRow: 3, baseClass: 2, choice: "Deadeye", family: AscendancyFamily.Normal);
        Assert.Null((await game.Change([59837, 24848, 22852, 23169], true)).Error);
        for (int i = 0; i < 10; i++) Assert.True(Program.DeadeyeGaleAdmitUse(game.State, true));
        Assert.Equal(.6, Program.DeadeyeGaleIncoming(game.State, false), 9); // evaded / DoT query, no loss
        Assert.Equal(10, GaleCount(game.State));
        // Both block outcomes reach defender's accepted-hit commit, independent of damage >0.
        Assert.True(blocked || damage == 0);
        Assert.Equal(.6, Program.DeadeyeGaleIncoming(game.State, true), 9);
        Assert.Equal(0, GaleCount(game.State)); Assert.Equal(1, Program.DeadeyeGaleIncoming(game.State, true));
    }

    [Fact]
    public async Task Deadeye_Gale_transfer_is_once_only_failed_transfer_resumes_death_and_retirement_clear()
    {
        Assert.True(DataAvailable());
        using var game = new GatewayCharacter(classRow: 3, baseClass: 2, choice: "Deadeye", family: AscendancyFamily.Normal);
        Assert.Null((await game.Change([59837, 24848], true)).Error);
        Program.DeadeyeGaleAdmitUse(game.State, true);
        const uint ticket = 0xddd1392;
        Program.DeadeyeGaleCarry(game.State, ticket); Assert.Equal(0, GaleCount(game.State));
        Program.DeadeyeGaleResumeFailedTransfer(game.State, ticket); Assert.Equal(1, GaleCount(game.State));
        Program.DeadeyeGaleCarry(game.State, ticket);
        var destination = game.Reload(); Program.DeadeyeGaleReceive(destination, ticket);
        Assert.Equal(1, GaleCount(destination));
        var replay = game.Reload(); Program.DeadeyeGaleReceive(replay, ticket); Assert.Equal(0, GaleCount(replay));
        destination.PlayerLife = 0; Assert.Equal(0, GaleCount(destination));
        Program.DeadeyeGaleClear(game.State, retire: true); game.State.LeavingArea = false;
        Assert.False(Program.DeadeyeGaleAdmitUse(game.State, true)); Assert.Equal(0, GaleCount(game.State));
    }

    [Fact]
    public async Task Deadeye_Gale_delayed_native_send_resamples_refund_instead_of_adding_stale_buffs()
    {
        Assert.True(DataAvailable());
        using var game = new GatewayCharacter(classRow: 3, baseClass: 2, choice: "Deadeye", family: AscendancyFamily.Normal);
        Assert.Null((await game.Change([59837, 24848], true)).Error);
        Program.DeadeyeGaleAdmitUse(game.State, true);
        await using var wire = new ProjectileRuntimeFixture(game.State); await wire.HoldOutput();
        var queued = Program.Send("gale-delayed", wire.Stream, wire.Ciphers, [], null,
            atGate: () => Program.DeadeyeGalePacketsAtGate(game.State));
        Assert.False(queued.IsCompleted);
        Assert.Null((await game.Change([24848], false, refundPoints: true)).Error);
        wire.ReleaseOutput(); await queued;
        Assert.Empty(await wire.ReadWire());
        Assert.Equal(0, GaleCount(game.State));
    }

    [Fact]
    public async Task Pinned_Ricochet_and_Far_Shot_sources_drive_frozen_release_adapter_after_paid_refund()
    {
        Assert.True(DataAvailable());
        using var game = new GatewayCharacter(classRow: 3, baseClass: 2, choice: "Deadeye", family: AscendancyFamily.Normal);
        Assert.Null((await game.Change([5082, 61627, 28995, 45313], true)).Error);
        ProjectileProfile Capture()
        {
            var stats = Program.CharacterPlayerStats(game.State);
            int Get(string name) => Program.Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
            var p = GatewayProjectileDefaults.Resolve("Metadata/Projectiles/DefaultArrow", Get, 100, true);
            return DeadeyeRicochetProfile.Barrage(DeadeyeRicochetProfile.Terrain(p, Get), "Barrage", true, Get, 1);
        }
        var release = Capture(); Assert.Equal(30, release.TerrainChainChancePercent); Assert.Equal(0, release.SpreadRadians);
        Assert.Null((await game.Change([61627, 45313], false, refundPoints: true)).Error);
        var next = Capture(); Assert.Equal(0, next.TerrainChainChancePercent); Assert.True(next.SpreadRadians > 0);
        Assert.Equal(30, release.TerrainChainChancePercent); Assert.Equal(0, release.SpreadRadians);
    }
}
