namespace GameServer.Gateway.Tests;

public sealed partial class AscendancyGatewayIntegrationTests
{
    [Theory]
    [InlineData(null, null, 4)] [InlineData(0, null, 0)] [InlineData(7, null, 7)]
    [InlineData(7, 0, 0)] [InlineData(0, 5, 5)] [InlineData(-2, null, 0)]
    public async Task Slayer_Masterful_Form_uses_one_maximum_for_panel_limits_minimum_and_special_mirrors(
        int? frenzy, int? endurance, int expected)
    {
        using var game = new GatewayCharacter(choice: "Slayer", family: AscendancyFamily.Normal, level: 60, refundPoints: 100);
        var path = PathTo(Program.Ascendancies.Find(4, AscendancyFamily.Normal, "Slayer")!, 16306);
        Assert.Null((await game.Change(path, true)).Error);
        var conn = game.State;
        void Set(string name, int value) => conn.PlayerStats[Program.Stats.Key(name)!.Value] = value;
        Set("max_endurance_charges", 9);
        Set("base_minimum_endurance_charges", 30);
        Set("maximum_brutal_charges_is_equal_to_maximum_endurance_charges", 1);
        Set("gain_brutal_charges_instead_of_endurance_charges", 1);
        if (frenzy is int f) Set("virtual_maximum_frenzy_charges", f);
        if (endurance is int e) Set("virtual_maximum_endurance_charges", e);
        var stats = Program.CharacterPlayerStats(conn);
        int Value(string name) => stats.GetValueOrDefault(Program.Stats.Key(name)!.Value);
        Assert.Equal(expected, Value("virtual_maximum_endurance_charges"));
        Assert.Equal(expected, Value("virtual_maximum_brutal_charges"));
        var session = ProgramValue<ChargeSession>("ChargesOf", conn);
        Assert.Equal(expected, session.Limits(ChargeType.Endurance).Maximum);
        Assert.Equal(expected, session.Limits(ChargeType.Endurance).Minimum);
        session.Enqueue(new ChargeEvent.Set(ChargeType.Endurance, 100));
        session.Step(0, _ => 0, () => default, true, false, false, new Random(1));
        Assert.Equal(expected, session.Effective(ChargeType.Endurance));
        Assert.True(session.Converted(ChargeType.Endurance));
        Assert.Null((await game.Change([16306], false, refundPoints: true)).Error);
        stats = Program.CharacterPlayerStats(conn);
        int after = endurance is int final ? Math.Max(0, final) : 12;
        Assert.Equal(after, Value("virtual_maximum_endurance_charges"));
        Assert.Equal(after, session.Limits(ChargeType.Endurance).Maximum);
    }

    [Fact]
    public void Slayer_charge_override_presence_survives_session_gain_recomputation_and_removal_clamps_held_state()
    {
        var raw = new Dictionary<string, int> { [SlayerRules.EnduranceEqualsFrenzy] = 1,
            ["max_frenzy_charges"] = 4, ["base_minimum_endurance_charges"] = 2 };
        var overrides = new Dictionary<string, int> { ["max_frenzy_charges_override"] = 0 };
        var session = new ChargeSession();
        void Observe() => session.Observe(raw.GetValueOrDefault, n => overrides.TryGetValue(n, out int v) ? v : null);
        void Step(double now) => session.Step(now, _ => 0, () => default, true, false, false, new Random(1));
        Observe();
        Assert.Equal(new Charges.Limits(0, 0, 10000), session.Limits(ChargeType.Endurance));
        overrides.Clear(); Observe();
        session.Enqueue(new ChargeEvent.Set(ChargeType.Endurance, 7)); Step(0);
        Assert.Equal(7, session.Effective(ChargeType.Endurance));
        raw.Remove(SlayerRules.EnduranceEqualsFrenzy); Observe(); Step(1);
        Assert.Equal(3, session.State.Held(ChargeType.Endurance));
        Assert.Equal(3, session.Effective(ChargeType.Endurance));
        overrides["virtual_maximum_endurance_charges"] = 0; Observe(); Step(2);
        Assert.Equal(0, session.Effective(ChargeType.Endurance));
        Assert.Equal(0, session.State.Held(ChargeType.Endurance));
    }

    [Fact]
    public async Task Slayer_failed_paid_refund_preserves_connected_maximum_and_durable_allocation()
    {
        using var game = new GatewayCharacter(choice: "Slayer", family: AscendancyFamily.Normal, level: 60, refundPoints: 0);
        var path = PathTo(Program.Ascendancies.Find(4, AscendancyFamily.Normal, "Slayer")!, 16306);
        Assert.Null((await game.Change(path, true)).Error);
        string before = game.SavedJson;
        Assert.NotNull((await game.Change([16306], false, refundPoints: true)).Error);
        Assert.Equal(before, game.SavedJson);
        uint key = Program.Stats.Key("virtual_maximum_endurance_charges")!.Value;
        Assert.Equal(4, Program.CharacterPlayerStats(game.State)[key]);
        Assert.Equal(4, Program.CharacterPlayerStats(game.Reload())[key]);
        Assert.Equal(path.Order(), game.State.MainAscendancy.Allocated.Order());
    }

    [Theory]
    [InlineData(0, true)] [InlineData(100, false)]
    public async Task Slayer_paid_Headsman_refund_and_rollback_control_actual_accepted_contacts(ushort refundPoints, bool retained)
    {
        using var game = new GatewayCharacter(choice: "Slayer", family: AscendancyFamily.Normal, level: 60, refundPoints: refundPoints);
        var path = PathTo(Program.Ascendancies.Find(4, AscendancyFamily.Normal, "Slayer")!, 3184);
        Assert.Null((await game.Change(path, true)).Error);
        SpawnedMonster Target() => new("Slayer refund contact", new(32, 32, 30), 0, 2, 1000) { Life = 200 };
        var before = Target();
        Assert.Equal(0u, Program.HitUnlessBlockedLocked(game.State, before, 0)!.Value.Life);
        var refund = await game.Change([3184], false, refundPoints: true);
        Assert.Equal(retained, refund.Error is not null);
        var after = Target();
        Assert.Equal(retained ? 0u : 200u, Program.HitUnlessBlockedLocked(game.State, after, 0)!.Value.Life);
        var reload = game.Reload();
        var reloadedTarget = Target();
        Assert.Equal(retained ? 0u : 200u, Program.HitUnlessBlockedLocked(reload, reloadedTarget, 0)!.Value.Life);
    }
}
