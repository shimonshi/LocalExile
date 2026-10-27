using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public partial class RecentEnemyHitGatewayTests
{
    [Fact]
    public async Task Final_attributes_and_converted_mana_feed_one_incoming_armour_decision_beside_spell_restriction()
    {
        using var f = new Fixture();
        f.Set("strength_+%", 25);
        f.Set(StrengthMana.PerFourStrength, 8);
        f.Set(PowerOfPurpose.RawFlag, 1);
        f.Set(PlayerHitMitigation.ArmourAppliesToChaos, 1);
        f.Set("spell_skills_deal_no_damage", 1);
        f.Set(RecentEnemyHits.GenericChance, 100);
        f.Conn.Pools = Program.PoolsFor(f.Conn, 100);
        _ = f.Chance;
        Assert.Equal((18, 32, 136u, 1091),
            (f.Conn.Pools.Strength, f.Conn.Pools.Intelligence, f.Conn.Pools.Mana, f.Conn.Pools.Armour));
        f.Conn.PlayerLife = f.Conn.PlayerMaxLife;
        uint before = f.Conn.PlayerLife;
        var random = new TraceRandom(armourRoll: 0, blockRoll: .99);
        f.Conn.IncomingHitRandom = random;
        f.Conn.EvasionEntropy = 99;
        f.Conn.LastEvasionAt = f.Conn.Clock.Elapsed.TotalSeconds;
        await f.Swing(f.Monster(DamagePacket.Of(DamageType.Physical, 200, 200)
            .Add(DamageType.Chaos, new(200, 200)))).WaitAsync(TimeSpan.FromSeconds(5));
        // One decision doubles 1091 to 2182: the existing rounded 69% reduction leaves 62 of each type.
        Assert.Equal(before - 124u, f.Conn.PlayerLife);
        Assert.Equal(1, f.Count);
        Assert.Single(random.Draws, d => d == "next:100");
        Assert.Equal((136u, 1091), (f.Conn.Pools.Mana, f.Conn.Pools.Armour));
        await f.Command("/set_stat strength_+% 300");
        Assert.Equal((56, 152u, 1219), (f.Conn.Pools.Strength, f.Conn.Pools.Mana, f.Conn.Pools.Armour));
        before = f.Conn.PlayerLife;
        f.Conn.EvasionEntropy = 99;
        f.Conn.LastEvasionAt = f.Conn.Clock.Elapsed.TotalSeconds;
        await f.Swing(f.Monster(DamagePacket.Of(DamageType.Physical, 200, 200)
            .Add(DamageType.Chaos, new(200, 200)))).WaitAsync(TimeSpan.FromSeconds(5));
        // Updated 1219 doubles to 2438: the rounded 71% reduction leaves 58 of each type; Pools stays unchanged.
        Assert.Equal(before - 116u, f.Conn.PlayerLife);
        Assert.Equal(2, f.Count);
        Assert.Equal(2, random.Draws.Count(d => d == "next:100"));
        Assert.Equal((152u, 1219), (f.Conn.Pools.Mana, f.Conn.Pools.Armour));
        Assert.Equal(1, Program.CharacterPlayerStats(f.Conn)[StatKeys.SpellSkillsDealNoDamage]);
    }
}
