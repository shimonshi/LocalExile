namespace GameServer.Gateway.Tests;

public sealed partial class AscendancyGatewayIntegrationTests
{
    [Theory]
    [InlineData(3184, 17315, 38180)]
    [InlineData(34484, 16306, 38180)]
    [InlineData(3184, 34484)]
    public async Task Slayer_legal_eight_point_builds_cover_every_notable_and_preserve_failed_save_then_paid_refund(
        int first, int second, int third = 0)
    {
        var definition = Program.Ascendancies.Find(4, AscendancyFamily.Normal, "Slayer")!;
        ushort[] nodes = new[] { first, second, third }.Where(n => n > 0)
            .SelectMany(n => PathTo(definition, (ushort)n)).Distinct().ToArray();
        Assert.Equal(8, nodes.Length);
        using var game = new GatewayCharacter(choice: "Slayer", family: AscendancyFamily.Normal, level: 60, refundPoints: 100);
        Assert.Null((await game.Change(nodes, true)).Error);
        Assert.Equal(nodes.Order(), game.Reload().MainAscendancy.Allocated.Order());
        Assert.NotNull((await game.Change(definition.Nodes.Keys.Where(n => !nodes.Contains(n) && n != definition.StartNode)
            .Take(1).ToArray(), true)).Error);
        int Get(string name) => Program.Stats.Key(name) is uint key ? Program.CharacterPlayerStats(game.State).GetValueOrDefault(key) : 0;
        game.State.SlayerKills.RecordKill(0, true, Get);
        game.State.Clock.Stop();
        var before = game.State.SlayerKills.Carry(0);
        string saved = game.SavedJson;
        var progress = game.State.MainAscendancy;
        Saves.FailWrite = name => name == game.Character;
        try { await Assert.ThrowsAsync<IOException>(() => game.Change([(ushort)first], false, refundPoints: true)); }
        finally { Saves.FailWrite = null; }
        Assert.Equal(saved, game.SavedJson);
        Assert.Equal(progress, game.State.MainAscendancy);
        Assert.Equal((ushort)100, game.State.RefundPoints);
        Assert.Equal(before.Movement, game.State.SlayerKills.Carry(0).Movement);
        Assert.Null((await game.Change([(ushort)first], false, refundPoints: true)).Error);
        Assert.Equal((ushort)95, game.State.RefundPoints);
        Assert.DoesNotContain((ushort)first, game.Reload().MainAscendancy.Allocated);
        if (first == 3184)
        {
            Assert.Equal(0, game.State.SlayerKills.Carry(0).Movement);
            Assert.Null((await game.Change([3184], true)).Error);
            Assert.Equal(0, game.State.SlayerKills.Carry(0).Movement);
        }
        // A genuine relog has no in-memory timers, even though all durable source/refund changes survived.
        Assert.Empty(game.Reload().SlayerKills.Carry(0).Kills);
    }
}
