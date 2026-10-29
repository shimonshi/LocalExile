using System.Reflection;

namespace GameServer.Gateway.Tests;

public partial class ConditionalCriticalGatewayTests
{
    [Fact]
    public void Combat_fixture_attributes_qualify_real_hosts_and_raw_source_removal_reverses_admission()
    {
        using var contact = new Contact(new Draws(), "Fireball", "Driftwood Wand");
        var conn = contact.Conn;
        var inventory = PlayerInventories.For(conn.Character!, Program.Items);
        var host = Assert.Single(inventory.Items(PlayerInventories.BodyArmour));
        var @class = Program.Skills.ClassFor(conn.ClassRow);
        var gemsMethod = typeof(Program).GetMethod("GemSkillEffects", BindingFlags.Static | BindingFlags.NonPublic,
            binder: null, types: [typeof(ClientState)], modifiers: null)!;
        (ushort Id, int Effect, string Name, int Level, int Quality, uint Gem)[] Gems() =>
            ((IEnumerable<(ushort, int, string, int, int, uint)>)gemsMethod.Invoke(null, [conn])!).ToArray();

        Assert.Equal((1000, 1000, 1000), (conn.Pools.Strength, conn.Pools.Dexterity, conn.Pools.Intelligence));
        Assert.DoesNotContain(host.Id, Program.InactiveItems[conn.Character!]);
        Assert.Contains(Gems(), gem => gem.Id == contact.Skill);

        contact.Set("base_strength", -@class.Strength);
        contact.Set("base_dexterity", -@class.Dexterity);
        contact.Set("base_intelligence", -@class.Intelligence);
        contact.Refresh();
        Assert.Equal((0, 0, 0), (conn.Pools.Strength, conn.Pools.Dexterity, conn.Pools.Intelligence));
        Assert.Contains(host.Id, Program.InactiveItems[conn.Character!]);
        Assert.DoesNotContain(Gems(), gem => gem.Id == contact.Skill);
        Assert.Equal(host.Item.Sockets, Assert.Single(inventory.Items(PlayerInventories.BodyArmour)).Item.Sockets);

        contact.Set("base_strength", 1000 - @class.Strength);
        contact.Set("base_dexterity", 1000 - @class.Dexterity);
        contact.Set("base_intelligence", 1000 - @class.Intelligence);
        contact.Refresh();
        Assert.Equal((1000, 1000, 1000), (conn.Pools.Strength, conn.Pools.Dexterity, conn.Pools.Intelligence));
        Assert.DoesNotContain(host.Id, Program.InactiveItems[conn.Character!]);
        Assert.Contains(Gems(), gem => gem.Id == contact.Skill);
    }
}
