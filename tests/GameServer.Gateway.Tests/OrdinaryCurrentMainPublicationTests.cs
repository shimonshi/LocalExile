using GameServer.Core.World;
using GameServer.Core.Protocol.Instance;

namespace GameServer.Gateway.Tests;

public partial class ConditionalCriticalGatewayTests
{
    [Theory]
    [InlineData(null, null)] [InlineData("Cyclone", null)]
    [InlineData("Fireball", null)] [InlineData("BurningArrow", "Crude Bow")]
    public async Task OrdinaryCurrentMain_rare_hit_multiplier_survives_Slayer_and_mark_contact_composition(
        string? gem, string? weapon)
    {
        async Task<uint> Damage(bool rareBonus)
        {
            using var f = new Contact(new Draws(), gem, weapon, classRow: 4);
            Slayer(f, 62817);
            f.Set("physical_damage_+%", -10000);
            f.Set("elemental_damage_+%", -10000);
            f.Set("damage_+%_vs_rare_monsters", rareBonus ? 100 : 0);
            var target = new SpawnedMonster("rare contact", new(34, 32, 0), 0, 2, 100000)
                { Rarity = MonsterRarity.Rare };
            f.Conn.Monsters[42] = target;
            f.Conn.SlayerKills.RecordKill(Program.ProjectileWorldTime(f.Conn), false, _ => 0);
            await SlayerContact(f, gem);
            return target.MaxLife - target.Life;
        }
        uint ordinary = await Damage(false);
        Assert.True(ordinary > 0);
        Assert.Equal(ordinary * 2, await Damage(true));
    }

    [Fact]
    public async Task OrdinaryCurrentMain_projectile_cull_keeps_rare_modifier_theft_and_one_Slayer_kill()
    {
        using var f = new Contact(new Draws(), "BurningArrow", "Crude Bow", classRow: 4);
        Slayer(f, 3184);
        f.Set("damage_+%", -10000);
        f.Set(StolenMods.SourceStat, 1);
        var affix = Program.Items.Mods.ById("MonsterModAttackCastMovementSpeed")!;
        var target = new SpawnedMonster("culled rare", new(34, 32, 0), 0, 2, 10000)
        {
            Life = 2000, Rarity = MonsterRarity.Rare,
            Variety = Program.Monsters.Find("Metadata/Monsters/Goatman/GoatmanLeapSlam")!,
            Mods = new([affix.Roll(ModSection.Explicit, new Random(1))], [], MonsterModEffects.None)
        };
        f.Conn.Monsters[42] = target;
        await f.Launch();
        Assert.True(target.Dead);
        Assert.Equal(1, SlayerValue(f).RecentKills);
        Assert.Equal(40, f.Conn.StolenMods.Stats()["base_movement_velocity_+%"]);
        Assert.True(Program.PoolsFor(f.Conn, 40).MovementVelocityPct >= 40);
    }
}
