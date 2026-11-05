namespace GameServer.Gateway.Tests;

public sealed class OrdinaryPathfinderPinnedTests
{
    public void Entire_Default_class_has_fifteen_nodes_and_Ruthless_replaces_not_adds_sources()
    {
        // Integration fixture uses the production catalogs and repo-relative public data. Missing data is explicit.
        Assert.True(File.Exists("data/passive-skill-graphs.json"), "Pinned passive graph extraction required");
        var catalog = new AscendancyCatalog();
        var definition = Assert.Single(catalog.Definitions, d => d.Identity.Family == AscendancyFamily.Normal
            && d.Identity.Id == "Pathfinder");
        ushort[] ids = [64111, 36242, 51101, 14156, 63293, 32640, 65296, 40631, 61805, 59800, 6038, 32662, 40813, 20480, 1697];
        Assert.Equal(ids.Order(), definition.Nodes.Keys.Order());
        var normal = new PassiveTree();
        var ruthless = new PassiveTree(hardMode: true);
        var stats = new StatCatalog();
        int Value(PassiveTree tree, ushort id, string name) => tree.Find(id)!.Stats
            .Where(s => s.Key == stats.Key(name)).Sum(s => s.Value);
        Assert.Equal(3, Value(normal, 51101, "flask_charges_recovered_per_3_seconds"));
        Assert.Equal(1, Value(ruthless, 51101, "flask_charges_recovered_per_3_seconds"));
        Assert.Equal(30, Value(normal, 65296, "magic_utility_flask_effect_+%"));
        Assert.Equal(20, Value(ruthless, 65296, "magic_utility_flask_effect_+%"));
        Assert.Equal(50, Value(normal, 61805, OrdinaryPathfinderRules.Preserve));
        Assert.Equal(0, Value(ruthless, 61805, OrdinaryPathfinderRules.Preserve));
        Assert.Equal(1, Value(ruthless, 61805, "hard_mode_utility_flask_gain_charges_while_active"));
        Assert.Equal(25, Value(normal, 40813, "withered_on_hit_for_2_seconds_%_chance"));
        Assert.Equal(0, Value(ruthless, 40813, "withered_on_hit_for_2_seconds_%_chance"));
        Assert.Equal(35, Value(ruthless, 40813, "withered_effect_+%"));
        Assert.Equal(0, Value(ruthless, 1697, OrdinaryPathfinderRules.DoublePoison));
        Assert.Equal(1, Value(ruthless, 1697, OrdinaryPathfinderRules.SpreadAlways));
        foreach (var tree in new[] { normal, ruthless })
        {
            int Get(string name) => Value(tree, 6038, name);
            var plan = OrdinaryPathfinderRules.Distill([
                new(1, 0, OrdinaryPathfinderRules.Kind.Diamond, 3),
                new(2, 1, OrdinaryPathfinderRules.Kind.Amethyst, 3)], true, Get);
            Assert.Contains(("critical_strike_chance_+%", tree == normal ? 250 : 150), plan.Stats);
            Assert.Contains(("physical_damage_%_to_add_as_chaos", tree == normal ? 37 : 23), plan.Stats);
            Assert.Equal(-50, Value(tree, 63293, OrdinaryPathfinderRules.Surgeon));
        }
        // A Phrecia class shares the table row, but has different graph membership and is not this fixture.
        Assert.DoesNotContain(definition.Nodes.Values, n => n.PassiveId.StartsWith("AscendancyCustomPathfinder"));
    }
}
