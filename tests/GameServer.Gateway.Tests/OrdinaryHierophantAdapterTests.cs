namespace GameServer.Gateway.Tests;

public sealed class OrdinaryHierophantAdapterTests
{

    public void Candidate_adapter_projects_live_totem_sources_and_does_not_leak_to_phrecia_or_dead_player()
    {
        var conn = new ClientState(new(1, "hierophant", 300, 300, 100, 100, null, []), 1, 1)
        {
            Pools = new(1, 1000, 100, 2, 0), PlayerLife = 500,
            MainAscendancy = new(new(AscendancyFamily.Normal, "Hierophant", AscendancyCatalog.Revision)),
        };
        uint source = Program.Stats.Key("you_and_totem_life_regeneration_rate_per_minute_%_per_active_totem")!.Value;
        uint output = Program.Stats.Key("life_regeneration_rate_per_minute_%")!.Value;
        var now = conn.Clock.Elapsed.TotalSeconds;
        conn.Totems.Place(new(101, 1, (100, 100), 100, now + 100, null), 2, now);
        conn.Totems.Place(new(102, 1, (100, 100), 100, now, null), 2, now); // expired, never contributes
        Dictionary<uint, int> Raw() => new() { [source] = 60, [output] = 5 };
        var raw = Raw(); Game.AddOrdinaryHierophantSources(conn, raw); Assert.Equal(65, raw[output]);
        conn.Totems.KillAll(conn.Clock.Elapsed.TotalSeconds);
        raw = Raw(); Game.AddOrdinaryHierophantSources(conn, raw); Assert.Equal(5, raw[output]);
        conn.MainAscendancy = new(new(AscendancyFamily.Phrecia, "Puppeteer", AscendancyCatalog.Revision));
        raw = Raw(); Game.AddOrdinaryHierophantSources(conn, raw); Assert.Equal(5, raw[output]);
        conn.PlayerLife = 0;
        Assert.False(Game.OrdinaryHierophantSpellHit(conn, raw, true, true, true));
        Assert.Equal(1, Game.OrdinaryHierophantDamageFactor(conn, raw, true));
    }

    public void Audit_covers_exactly_the_pinned_Default_graph_members()
    {
        var definition = new AscendancyCatalog().Find(new(AscendancyFamily.Normal, "Hierophant", AscendancyCatalog.Revision))!;
        ushort[] audited = [44797, 26714, 14870, 29994, 11046, 22637, 922, 29026, 1105, 34434,
            25651, 60462, 33167, 40510, 38387, 51492, 30940];
        Assert.Equal(audited.Order(), definition.Nodes.Keys.Order());
        Assert.Equal((ushort)30940, definition.StartNode);
        Assert.Empty(definition.Nodes[30940].Stats);
    }
}
