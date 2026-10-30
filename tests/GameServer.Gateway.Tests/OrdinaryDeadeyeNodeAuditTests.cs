namespace GameServer.Gateway.Tests;

public sealed class OrdinaryDeadeyeNodeAuditTests
{
    [Fact]
    public void Pinned_Default_graph_contains_exact_Deadeye_nodes_and_source_values_not_Whisperer()
    {
        var definition = Program.Ascendancies.Find(2, AscendancyFamily.Normal, "Deadeye");
        Assert.NotNull(definition);
        Assert.Equal(AscendancyCatalog.Revision, definition.Identity.Revision);
        Assert.Equal((ushort)56856, definition.StartNode);
        Assert.Equal((byte)2, definition.ClassNo);
        var expected = new Dictionary<ushort, (uint Key, int Value)[]>
        {
            [56856] = [],
            [53086] = [(501, 10), (13258, 25)],
            [5443] = [(13529, 75), (13530, -25), (12622, 100)],
            [59837] = [(501, 10), (71, 5)],
            [61627] = [(175, 1), (13457, 30)],
            [5082] = [(501, 10), (85, 10)],
            [26067] = [(176, 2)],
            [28995] = [(501, 10), (178, 10)],
            [45313] = [(17928, 30), (13972, 1)],
            [44482] = [(183, 1), (20752, 1), (20754, 5)],
            [55985] = [(501, 10), (85, 10)],
            [62136] = [(501, 10), (13480, 15)],
            [24848] = [(7841, 10)],
            [1729] = [(501, 10), (13480, 15)],
            [2872] = [(13479, 1), (13481, 2), (13482, 35)],
            [56134] = [(501, 10), (85, 10)],
            [64028] = [(501, 10), (178, 10)],
            [22852] = [(501, 10), (71, 5)],
            [23169] = [(13557, -4), (13558, 1)],
        };
        Assert.Equal(expected.Keys.Order(), definition.Nodes.Keys.Order());
        foreach (var (id, stats) in expected) Assert.Equal(stats, definition.Nodes[id].Stats);
        Assert.Equal(DeadeyeProjectileDamage.FarShot, Program.Stats.Name(17928));
        Assert.Equal("no_barrage_projectile_spread", Program.Stats.Name(13972));
        Assert.Equal("projectile_chain_from_terrain_chance_%", Program.Stats.Name(13457));
        Assert.Equal("Whisperer", Program.Ascendancies.Find(2, AscendancyFamily.Phrecia, "Whisperer")!.Name);
        Assert.DoesNotContain((ushort)45313,
            Program.Ascendancies.Find(2, AscendancyFamily.Phrecia, "Whisperer")!.Nodes.Keys);
    }
}
