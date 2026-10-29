namespace GameServer.Gateway.Tests;

public sealed partial class AscendancyGatewayIntegrationTests
{
    [Fact]
    public async Task Inquisitor_318_Pious_copies_life_regeneration_before_life_recovery_and_only_ES_recovery_scales_it()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        Assert.Null((await game.Change(InquisitorPath(32816), true)).Error);
        Program.CharacterPlayerStats(game.State);
        var inputs = new Dictionary<string, int>
        {
            ["base_life_regeneration_rate_per_minute"] = 6000,
            ["life_regeneration_rate_+%"] = 100,
            ["life_recovery_rate_+%"] = 200,
            ["energy_shield_regeneration_rate_+%"] = 100,
            ["energy_shield_recovery_rate_+%"] = 50,
        };
        int Get(string name) => inputs.GetValueOrDefault(name);
        Assert.Equal(18000, ProgramValue<double>("InquisitorMirroredPerMinute", game.State, (Func<string, int>)Get));
        inputs["cannot_recover_life"] = 1;
        inputs["no_energy_shield_recharge_or_regeneration"] = 1;
        Assert.Equal(18000, ProgramValue<double>("InquisitorMirroredPerMinute", game.State, (Func<string, int>)Get));
        inputs["cannot_recover_energy_shield"] = 1;
        Assert.Equal(0, ProgramValue<double>("InquisitorMirroredPerMinute", game.State, (Func<string, int>)Get));
        inputs.Remove("cannot_recover_energy_shield");
        inputs["no_life_regeneration"] = 1;
        Assert.Equal(0, ProgramValue<double>("InquisitorMirroredPerMinute", game.State, (Func<string, int>)Get));
    }
}
