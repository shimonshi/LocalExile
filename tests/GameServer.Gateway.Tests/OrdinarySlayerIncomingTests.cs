using GameServer.Core.World;
using GameServer.Core.Protocol.Instance;

namespace GameServer.Gateway.Tests;

public partial class ConditionalCriticalGatewayTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Slayer_Bane_immunity_consumes_real_pinned_Hiltless_item_modifier_at_accepted_attack(bool immune)
    {
        using var f = new Contact(new Draws(), weaponName: "Rusted Sword", classRow: 4);
        f.Set("global_cannot_crit", 1);
        var inventory = PlayerInventories.For(f.Conn.Character!, Program.Items);
        var weapon = Assert.Single(inventory.Items(PlayerInventories.Weapon1));
        var mod = Program.Items.Mods.ById("ReflectPhysicalDamageToSelfOnHitUnique__1")!;
        Assert.NotNull(mod);
        inventory.Change(PlayerInventories.Weapon1, weapon.Id,
            item => item with { Mods = [new(ModSection.Explicit, mod.Hash, [100])] });
        if (immune) Slayer(f, 62817); else f.Refresh();
        f.Conn.Pools = f.Conn.Pools with { Life = 1000, Armour = 0, EnergyShield = 0 };
        f.Conn.PlayerLife = 1000;
        await f.Use();
        Assert.Equal(immune ? 1000u : 900u, f.Conn.PlayerLife);
        Assert.Equal(0, SlayerValue(f).RecentKills);
    }
    [Theory]
    [InlineData("normal")] [InlineData("block")] [InlineData("evade")] [InlineData("immune")] [InlineData("avoid")]
    public void Slayer_bleeding_admitted_by_actual_monster_attack_raw_source_ignores_hit_armour_for_ailment_base(string mode)
    {
        using var f = new Contact(new Draws(), "Cyclone", classRow: 4);
        if (mode == "block") f.Conn.IncomingHitRandom = new CombinedIncomingDraw();
        f.Conn.Pools = f.Conn.Pools with { Life = 10000, Armour = 10000, BlockChance = mode == "block" ? 100 : 0,
            Evasion = mode == "evade" ? int.MaxValue : 0 };
        f.Conn.PlayerLife = 10000;
        if (mode == "immune") f.Set("immune_to_bleeding", 1);
        if (mode == "avoid") f.Set("avoid_bleed_%", 100);
        var mod = Program.Items.Mods.ById("MonsterAddedPhysicalDamageAndBleed")!;
        Assert.NotNull(mod);
        var mods = new MonsterModCatalog(Program.Items.Mods).Create(
            [mod.Roll(ModSection.Explicit, new Random(1))], [], GameServer.Core.Protocol.Instance.MonsterRarity.Normal, 1);
        Assert.Equal(1, mods.Effects["global_bleed_on_hit"]);
        var m = new SpawnedMonster("bleeding attacker", new(32, 32, 0), 0, 2, 100) { Mods = mods };
        f.Conn.Clock.Stop();
        double now = f.Conn.Clock.Elapsed.TotalSeconds;
        f.Conn.EvasionEntropy = 0; f.Conn.LastEvasionAt = now;
        var hit = Program.CommitMonsterHitLocked("slayer-bleed", f.Conn, 42, m,
            new(1, 1, 5, DamagePacket.Of(DamageType.Physical, 100, 100)), now, (32, 32), (32, 32), true);
        bool bleeding = mode == "normal";
        Assert.Equal(bleeding, f.Conn.Bleeding.Active(now));
        if (bleeding)
        {
            Assert.NotNull(hit);
            Assert.True(hit.Damage < 100);
            uint life = f.Conn.PlayerLife;
            Assert.Equal(70, Program.AdvancePlayerBleedingLocked(f.Conn, now, now + 1, Program.CharacterPlayerStats(f.Conn)));
            Assert.Equal(life - 70, f.Conn.PlayerLife); // Armour's hit reduction was never used to construct this bleed.
        }
    }

    [Fact]
    public void Slayer_bleeding_uses_strongest_instance_movement_reduction_ES_and_can_commit_lethal_death()
    {
        using var f = new Contact(new Draws(), "Cyclone", classRow: 4);
        f.Conn.Pools = f.Conn.Pools with { Life = 1000, EnergyShield = 100 };
        f.Conn.EnergyShield.SetMaximum(100); f.Conn.EnergyShield.Gain(100);
        f.Conn.PlayerLife = 50;
        f.Set("base_additional_physical_damage_reduction_%", 50);
        f.Conn.Bleeding.Apply(100, 0, 5, _ => 0);
        f.Conn.Bleeding.Apply(200, 0, .5, _ => 0);
        var stats = Program.CharacterPlayerStats(f.Conn);
        Assert.Equal(52, Program.AdvancePlayerBleedingLocked(f.Conn, 0, 1, stats)); // (140*.5 + 70*.5)*.5
        Assert.Equal(50u, f.Conn.PlayerLife);
        f.Conn.Walk.Order(100, 32, 1);
        Assert.Equal(105, Program.AdvancePlayerBleedingLocked(f.Conn, 1, 2, stats)); // 70 * 3 * .5
        Assert.Equal(0u, f.Conn.PlayerLife);
        Assert.False(f.Conn.Bleeding.Active(2));
        Assert.Equal(0, SlayerValue(f).RecentKills);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Slayer_proxy_and_independent_actor_damage_have_explicit_distinct_credit_cull_and_leech_policy(bool proxy)
    {
        using var f = new Contact(new Draws(), "Cyclone", classRow: 4);
        Slayer(f, 3184, 34484);
        f.Target.Life = 1;
        var hit = new Program.OwnerSkillHit(0, DamagePacket.Empty, Program.HitLeechSources(new Dictionary<uint, int>(), true),
            Program.CharacterPlayerStats(f.Conn)) { Attribution = proxy ? Program.SkillDamageAttribution.Mirage
                : Program.SkillDamageAttribution.IndependentActor };
        Method("ApplyMonsterHitLocked").Invoke(null, [f.Conn, f.Target, 0, hit]);
        Assert.Equal(proxy, f.Target.Dead);
        Assert.Equal(proxy ? 1 : 0, SlayerValue(f).RecentKills);
        Assert.Equal(0, Program.LeechOf(f.Conn).Life.Count);
    }
}
