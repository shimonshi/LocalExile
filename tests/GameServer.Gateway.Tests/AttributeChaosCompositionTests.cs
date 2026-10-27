namespace GameServer.Gateway.Tests;

public class AttributeChaosCompositionTests
{
    [Fact]
    public void Increased_final_Strength_feeds_mana_without_dropping_chaos_armour_or_CI()
    {
        var pools = CharacterStats.For(SkillCatalog.ClassInfo.Witch, 1, new(
            ManaPerFourStrength: 8, ArmourAppliesToChaosDamage: true, ChaosInoculation: true,
            Attributes: new(StrengthPct: 100)));
        Assert.Equal(28, pools.Strength);
        Assert.Equal(112u, pools.Mana);
        Assert.Equal(1u, pools.Life);
        Assert.True(pools.ArmourAppliesToChaosDamage);
    }

    [Fact]
    public void Shared_production_sources_keep_all_three_consumers_independent()
    {
        TestSaves.Ensure();
        var conn = new ClientState(new(ushort.MaxValue, "Composition", 128, 128, 32, 32, null, []), 0, 1)
        {
            Character = "AttributeChaos" + Guid.NewGuid().ToString("N"), ClassRow = 1,
            MainAscendancy = new(Nodes: [38080, 29437])
        };
        uint Key(string name) => Program.Stats.Key(name)!.Value;
        try
        {
            var ordinary = Program.PoolsFor(conn, 1);
            Assert.Equal(14, ordinary.Strength);
            Assert.Equal(80u, ordinary.Mana);
            Assert.True(ordinary.ArmourAppliesToChaosDamage);
            conn.PlayerStats[Key("strength_+%")] = 100;
            var doubled = Program.PoolsFor(conn, 1);
            Assert.Equal(28, doubled.Strength);
            Assert.Equal(112u, doubled.Mana);
            Assert.True(doubled.ArmourAppliesToChaosDamage);
            conn.PlayerStats[Key(PlayerHitMitigation.ArmourAppliesToChaos)] = 0;
            var unprotected = Program.PoolsFor(conn, 1);
            Assert.Equal(112u, unprotected.Mana);
            Assert.False(unprotected.ArmourAppliesToChaosDamage);
            conn.PlayerStats[Key(StrengthMana.PerFourStrength)] = 0;
            Assert.Equal(56u, Program.PoolsFor(conn, 1).Mana);
            conn.PlayerStats.Remove(Key(PlayerHitMitigation.ArmourAppliesToChaos));
            Assert.True(Program.PoolsFor(conn, 1).ArmourAppliesToChaosDamage);
            conn.PlayerStats.Remove(Key(StrengthMana.PerFourStrength));
            Assert.Equal(112u, Program.PoolsFor(conn, 1).Mana);
        }
        finally { PlayerInventories.Forget(conn.Character!); }
    }
}
