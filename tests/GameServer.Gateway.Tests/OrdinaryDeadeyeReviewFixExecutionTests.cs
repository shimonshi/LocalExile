using GameServer.Core.Protocol.Instance;
using GameServer.Core.Projectiles;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public partial class ConditionalCriticalGatewayTests
{
    [Theory]
    [InlineData(50, 1.5)] [InlineData(0, 1)] [InlineData(-50, .5)]
    public void DeadeyeReview_explicit_final_action_speed_matches_native_attack_cast_and_movement(int percent, double factor)
    {
        using var f = new Contact(new Draws(), "Fireball", classRow: 3);
        CombinedDeadeye(f, 59837, 24848);
        f.Set(ActionSpeed.Virtual, percent);
        Assert.Equal(factor, Program.PlayerActionFactor(f.Conn), 8);
        f.Conn.Pools = f.Conn.Pools with { MovementVelocityPct = 40 };
        Assert.Equal(1.4 * factor, Program.PlayerMovementFactor(f.Conn), 8);
        Assert.Equal(percent, Program.CharacterPlayerStats(f.Conn)[Program.Stats.Key(ActionSpeed.Virtual)!.Value]);
        f.Conn.PlayerStats.Remove(Program.Stats.Key(ActionSpeed.Virtual)!.Value);
        Assert.Equal(1.08, Program.PlayerActionFactor(f.Conn), 8);
    }

    [Fact]
    public async Task DeadeyeReview_incompatible_supported_Barrage_is_rejected_before_payment_or_Gale_admission()
    {
        using var f = new Contact(new Draws(), "KineticBlast", "Driftwood Wand", distance: 20, classRow: 3);
        CombinedDeadeye(f, 59837, 24848);
        var inventory = PlayerInventories.For(f.Conn.Character!, Program.Items);
        var host = Assert.Single(inventory.Items(PlayerInventories.BodyArmour));
        var gem = Program.Items.Find("Metadata/Items/Gems/SkillGemKineticBlast")!;
        var variant = Program.Items.Gem(gem)!.Variants.Single(v => v.Effect == Program.Skills.EffectRow("KineticBlastAltX"));
        inventory.Change(PlayerInventories.BodyArmour, host.Id, item => item with
        { Sockets = [item.Sockets[0] with { Gem = item.Sockets[0].Gem! with { GemVariant = variant.Index } }, new(SocketColour.White,
            ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SupportGemBarrage")!))], Links = [2] });
        uint mana = f.Conn.PlayerMana;
        await f.Use();
        Assert.Equal(mana, f.Conn.PlayerMana);
        Assert.Equal(0, CombinedGaleCount(f));
        Assert.Equal(f.Target.MaxLife, f.Target.Life);
    }

    [Theory]
    [InlineData(true, 87)] [InlineData(false, 117)]
    public async Task DeadeyeReview_Focal_incoming_ignite_retains_attacker_and_samples_live_mark_nearby_state(bool nearby, int expected)
    {
        using var f = MarkContact(); Focal(f);
        f.Conn.PlayerLife = 1000;
        f.Conn.Pools = f.Conn.Pools with { Life = 1000, EnergyShield = 0, FireResistance = 0 };
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        CastMark(f, runtime);
        var source = ProjectileRuntimeFixture.Monster(nearby ? 55 : 120, 32);
        f.Conn.Monsters[43] = source;
        var stats = Program.CharacterPlayerStats(f.Conn);
        Program.ApplyPlayerAilmentsLocked(f.Conn, DamagePacket.Of(DamageType.Fire, 100, 100), DamagePacket.Empty,
            true, false, 1, stats, sourceId: 43, source: source);
        int damage = Program.AdvancePlayerAilmentsLocked(f.Conn, 1, 2, stats);
        Assert.Equal(expected, damage);
        f.Conn.MainAscendancy = new(); f.Refresh();
        // Same Ignite survives; removing Focal changes its source-dependent damage without reapplication.
        int afterRefund = Program.AdvancePlayerAilmentsLocked(f.Conn, 2, 3, Program.CharacterPlayerStats(f.Conn));
        Assert.Equal(117, afterRefund);
    }

    [Theory]
    [InlineData(true, 52)] [InlineData(false, 70)]
    public async Task DeadeyeReview_Focal_incoming_bleeding_uses_same_attacker_attribution(bool nearby, int expected)
    {
        using var f = MarkContact(); Focal(f);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        CastMark(f, runtime);
        f.Conn.Pools = f.Conn.Pools with { Life = 1000, EnergyShield = 0, Armour = 0 };
        f.Conn.PlayerLife = 1000;
        f.Set("receive_bleeding_chance_%_when_hit_by_attack", 100);
        var source = ProjectileRuntimeFixture.Monster(nearby ? 55 : 120, 32);
        f.Conn.Monsters[43] = source;
        var attack = new MonsterAttack(SkillCatalog.FirstSkillId, 1, 10, DamagePacket.Of(DamageType.Physical, 100, 100));
        var stats = Program.CharacterPlayerStats(f.Conn);
        Program.BleedOnMonsterHitLocked(f.Conn, source, attack, false, false, 1, stats, sourceId: 43);
        Assert.Equal(expected, Program.AdvancePlayerBleedingLocked(f.Conn, 1, 2, stats));
    }

    [Fact]
    public async Task DeadeyeReview_real_contact_uses_marked_ignite_before_applying_projectile_distance_once()
    {
        using var f = MarkContact("PoachersMark");
        CombinedDeadeye(f, 53086, 5443, 28995, 45313);
        f.Set("global_cannot_crit", 1);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        CastMark(f, runtime);
        f.Target.Walk.Teleport(103, 32, 0);
        var source = new DeadeyeMarkHitSource(_ => DamagePacket.Of(DamageType.Fire, 100, 100),
            _ => new(100, 100, 100, 200, 200, 4));
        var offense = new ProjectileOffense(DamagePacket.Of(DamageType.Fire, 100, 100), true,
            int.MaxValue, 0, 150, "marked ignite seam")
        { MarkDirectBasis = source, Ignite = new(100, 1, 1, 2, 2, 4), DistanceDamage = new(30, false) };
        runtime.Release(runtime.Shot(new("Metadata/Projectiles/DefaultArrow")
        { Speed = 100, Range = 100, Radius = 1, CountsAsProjectileDamage = true }, offense,
            new(32, 32), new(130, 32)));
        runtime.Advance(2);
        Assert.True(f.Target.Ignites.Burning);
        // The contact quantum can leave a binary fraction just below the one-second boundary; it carries forward.
        Assert.InRange(f.Target.Ignites.Advance(f.Target.Ignites.Until - 3, 1), 129, 130);
    }

    [Fact]
    public async Task DeadeyeReview_Mirage_Barrage_intervals_use_proxy_attack_time_including_frozen_admission()
    {
        using var f = MirageContact();
        var inventory = PlayerInventories.For(f.Conn.Character!, Program.Items);
        var host = Assert.Single(inventory.Items(PlayerInventories.BodyArmour));
        inventory.Change(PlayerInventories.BodyArmour, host.Id, item => item with
        { Sockets = [.. item.Sockets, new(SocketColour.White,
            ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SupportGemBarrage")!))], Links = [3] });
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var actor = Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target)!;
        var owner = CaptureDeadeyeGem(f, runtime.Session);
        var proxy = Program.CaptureDeadeyeMirage(runtime.Session, actor, (52, 32), 1);
        Assert.True(proxy.Profile.Barrage);
        Assert.InRange(proxy.Profile.SequentialDelay, (long)(owner.Profile.SequentialDelay * 2.5) - 2,
            (long)(owner.Profile.SequentialDelay * 2.5) + 2);
        const double admitted = 3.7;
        var frozen = Program.CaptureDeadeyeMirage(runtime.Session, actor, (52, 32), 1, admitted);
        Assert.Equal(ProjectileRuntime.Time(admitted * .5 / frozen.Profile.Count), frozen.Profile.SequentialDelay);
    }
}
