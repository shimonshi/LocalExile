namespace GameServer.Gateway.Tests;

public class OrdinarySlayerPinnedAuditTests
{
    // Exact 3.29.3b Default PSG membership joined to PassiveSkills and Stats row+1, not the alternate graph.
    [Theory]
    [InlineData(33795, "Slayer", "")]
    [InlineData(61393, "Attack Damage, Critical Strike Chance", "557=15,2655=10")]
    [InlineData(15286, "Attack Damage, Attack Speed", "71=5,2655=10")]
    [InlineData(42293, "Attack Damage, Attack Speed", "71=5,2655=10")]
    [InlineData(34215, "Attack Damage, Life Leech", "2746=40,2655=10")]
    [InlineData(45696, "Attack Damage, Life Leech", "2746=40,2655=10")]
    [InlineData(20954, "Attack Damage, Area of Effect", "437=10,2655=10")]
    [InlineData(3184, "Headsman", "9886=10,9887=10,4766=1")]
    [InlineData(17315, "Overwhelm", "9893=800,9894=10")]
    [InlineData(62817, "Bane of Legends", "9885=20,4768=10,7839=1")]
    [InlineData(34484, "Endless Hunger", "3467=20,10772=1,3470=1,3468=20")]
    [InlineData(10143, "Brutal Fervour", "3469=1,9888=-10,9415=100")]
    [InlineData(38180, "Impact", "9890=5,1237=4,85=50,9954=15")]
    [InlineData(50845, "Attack Damage, Frenzy and Endurance Charge Duration", "424=15,421=15,2655=10")]
    [InlineData(16306, "Masterful Form", "9892=1,183=1")]
    public void Default_node_name_and_every_stat_value_are_pinned(int graphId, string name, string pairs)
    {
        var definition = Program.Ascendancies.Find(4, AscendancyFamily.Normal, "Slayer");
        Assert.NotNull(definition);
        Assert.Contains(checked((ushort)graphId), definition.Nodes.Keys);
        var node = Program.Passives.Find(checked((ushort)graphId));
        Assert.NotNull(node);
        Assert.Equal(name, node.Name);
        Assert.True(node.IsAscendancy);
        Assert.Equal(graphId == 33795, definition.Nodes[checked((ushort)graphId)].IsStart);
        var expected = pairs.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(pair => pair.Split('='))
            .Select(pair => (Key: uint.Parse(pair[0]), Value: int.Parse(pair[1]))).OrderBy(p => p.Key);
        Assert.Equal(expected, node.Stats.OrderBy(p => p.Key));
    }

    [Fact]
    public void Default_graph_contains_exactly_start_seven_small_and_seven_notable_nodes()
    {
        var definition = Program.Ascendancies.Find(4, AscendancyFamily.Normal, "Slayer");
        Assert.NotNull(definition);
        Assert.Equal((ushort)33795, definition.StartNode);
        ushort[] expected = [3184, 10143, 15286, 16306, 17315, 20954, 33795, 34215, 34484, 38180,
            42293, 45696, 50845, 61393, 62817];
        Assert.Equal(expected, definition.Nodes.Keys.Order());
        Assert.Equal(7, definition.Nodes.Keys.Count(n => Program.Passives.Find(n)!.IsNotable));
        Assert.Equal(new ushort[] { 15286, 42293 }, definition.Nodes[62817].Adjacent.Order());
        Assert.Equal(new ushort[] { 34215, 45696 }, definition.Nodes[10143].Adjacent.Order());
        Assert.Equal(new ushort[] { 34215 }, definition.Nodes[34484].Adjacent);
    }
}
