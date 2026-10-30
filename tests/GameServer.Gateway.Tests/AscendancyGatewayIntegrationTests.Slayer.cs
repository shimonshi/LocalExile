namespace GameServer.Gateway.Tests;

public sealed partial class AscendancyGatewayIntegrationTests
{
    [Theory]
    [InlineData(3184)] [InlineData(17315)] [InlineData(62817)] [InlineData(34484)]
    [InlineData(10143)] [InlineData(38180)] [InlineData(16306)]
    public async Task Ordinary_Slayer_real_connected_notable_sources_reload_paid_refund_and_reallocate(int notable)
    {
        Assert.True(DataAvailable(), "Slayer lifecycle regression requires extracted pinned public data.");
        var definition = Program.Ascendancies.Find(4, AscendancyFamily.Normal, "Slayer")!;
        ushort[] path = PathTo(definition, checked((ushort)notable));
        using var game = new GatewayCharacter(choice: "Slayer", family: AscendancyFamily.Normal, level: 60,
            refundPoints: 100);
        Assert.Null((await game.Change(path, true)).Error);
        Assert.Equal(path.Order(), game.State.MainAscendancy.Allocated.Order());
        Assert.DoesNotContain(definition.StartNode, game.State.MainAscendancy.Allocated);
        var node = Program.Passives.Find(checked((ushort)notable))!;
        foreach (var (key, value) in node.Stats)
            Assert.Equal(value, Program.CharacterPlayerStats(game.State).GetValueOrDefault(key));
        var reload = game.Reload();
        Assert.Equal(path.Order(), reload.MainAscendancy.Allocated.Order());
        foreach (var (key, value) in node.Stats)
            Assert.Equal(value, Program.CharacterPlayerStats(reload).GetValueOrDefault(key));
        Assert.Null((await game.Change([checked((ushort)notable)], false, refundPoints: true)).Error);
        Assert.Equal((ushort)95, game.State.RefundPoints);
        Assert.DoesNotContain(checked((ushort)notable), game.Reload().MainAscendancy.Allocated);
        foreach (var (key, _) in node.Stats)
            Assert.Equal(0, Program.CharacterPlayerStats(game.State).GetValueOrDefault(key));
        Assert.Null((await game.Change([checked((ushort)notable)], true)).Error);
        Assert.Equal(path.Order(), game.Reload().MainAscendancy.Allocated.Order());
        Assert.Equal((ushort)95, game.Reload().RefundPoints);
    }
}
