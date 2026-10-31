using GameServer.Core.Projectiles;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed partial class AscendancyGatewayIntegrationTests
{
    [Fact]
    public async Task Deadeye_paid_refund_preserves_flying_snapshot_and_next_production_release_loses_source()
    {
        Assert.True(DataAvailable());
        using var game = new GatewayCharacter(classRow: 3, baseClass: 2, choice: "Deadeye",
            family: AscendancyFamily.Normal, refundPoints: 20, level: 60);
        Assert.Null((await game.Change([28995, 45313], true)).Error);
        var gem = ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SkillGemFireball")!);
        game.Items.Add(PlayerInventories.BodyArmour, 0, 0,
            ItemCatalog.Create(Program.Items.Find("Metadata/Items/Armours/BodyArmours/BodyStr1")!) with
                { Sockets = [new(SocketColour.White, gem)] });
        game.State.PlayerStats[Program.Stats.Key("global_cannot_crit")!.Value] = 1;
        game.State.PlayerStats[Program.Stats.Key("spell_minimum_added_chaos_damage")!.Value] = 1000;
        game.State.PlayerStats[Program.Stats.Key("spell_maximum_added_chaos_damage")!.Value] = 1000;
        game.State.PlayerStats[Program.Stats.Key("elemental_damage_+%")!.Value] = -10000;
        game.State.Monsters[42] = new("refund target", new(112, 32, 30), 0, 2, 100000);
        await using var runtime = new ProjectileRuntimeFixture(game.State);
        ProjectileUse Capture()
        {
            var gems = ProgramValue<IEnumerable<(ushort Id, int Effect, string Name, int Level, int Quality, uint Gem)>>(
                "GemSkillEffects", game.State);
            var skill = Assert.Single(gems);
            var use = ProgramValue<ProjectileUse>("CaptureGemProjectile", game.State, runtime.Session, skill.Id, skill,
                (120, 32), new SkillCriticalRoll(0));
            return use with { Profile = use.Profile with { Speed = 10, Radius = 0, Range = 100 }, Origin = new(42, 32) };
        }
        var old = Capture();
        var oldShot = (ProjectileOffense)old.Payload!;
        runtime.Release(old);
        runtime.Advance(1);
        Assert.Equal(10, Assert.Single(runtime.Session.Runtime.Projectiles).Distance, 6);
        Assert.Null((await game.Change([45313], false, refundPoints: true)).Error);
        Assert.Equal((ushort)15, game.State.RefundPoints);
        Assert.Equal(0, DeadeyeStat(game.Reload(), DeadeyeProjectileDamage.FarShot));
        var next = Capture();
        Assert.Equal(30, oldShot.DistanceDamage.MaximumMorePercent);
        Assert.Equal(0, ((ProjectileOffense)next.Payload!).DistanceDamage.MaximumMorePercent);
        runtime.Advance(8);
        var target = game.State.Monsters[42];
        Assert.Equal((uint)Math.Floor(oldShot.AreaHit!.MinTotal * 1.3 + 1e-9), target.MaxLife - target.Life);
        uint before = target.Life;
        runtime.Release(next);
        runtime.Advance(16);
        Assert.Equal((uint)Math.Floor(((ProjectileOffense)next.Payload!).AreaHit!.MinTotal + 1e-9), before - target.Life);
    }
}

public partial class SpellNoDamageTests
{
    [Theory]
    [InlineData("Fireball", null)]
    [InlineData("KineticBlast", "Driftwood Wand")]
    [InlineData("KineticBlastAltX", "Driftwood Wand")]
    public async Task Deadeye_audited_area_snapshots_keep_projectile_and_area_buckets_scoped(string effect, string? weapon)
    {
        using var f = new Fixture(effect, weapon, targetX: 110);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        foreach (string stat in new[] { "damage_+%", "attack_damage_+%", "projectile_damage_+%", "spell_damage_+%" })
            f.Set(stat, 0);
        f.Set("physical_damage_+%", -10000); f.Set("elemental_damage_+%", -10000);
        f.Set("attack_minimum_added_chaos_damage", 1000); f.Set("attack_maximum_added_chaos_damage", 1000);
        f.Set("spell_minimum_added_chaos_damage", 1000); f.Set("spell_maximum_added_chaos_damage", 1000);
        var gems = (IEnumerable<(ushort Id, int Effect, string Name, int Level, int Quality, uint Gem)>)
            Method("GemSkillEffects").Invoke(null, [f.Conn])!;
        var gem = gems.Single(g => g.Id == f.Skill);
        ProjectileUse Capture() => (ProjectileUse)Method("CaptureGemProjectile").Invoke(null,
            [f.Conn, runtime.Session, f.Skill, gem, (180, 32), new SkillCriticalRoll(0)])!;
        var baseline = (ProjectileOffense)Capture().Payload!;
        f.Set("projectile_damage_+%", 100);
        var projectile = (ProjectileOffense)Capture().Payload!;
        Assert.Equal(baseline.Hit.MinTotal * 2, projectile.Hit.MinTotal, 6);
        Assert.Equal(baseline.AreaHit!.MinTotal * 2, projectile.AreaHit!.MinTotal, 6);
        f.Set("area_damage_+%", 100);
        f.Set(DeadeyeProjectileDamage.FarShot, 30);
        var use = Capture();
        var offense = (ProjectileOffense)use.Payload!;
        Assert.Equal(projectile.Hit.MinTotal, offense.Hit.MinTotal, 6);
        Assert.Equal(baseline.AreaHit.MinTotal * 3, offense.AreaHit!.MinTotal, 6);
        Assert.True(use.Profile.CountsAsProjectileDamage);
        var area = use.Profile.EndArea!;
        Assert.True(area.CountsAsProjectileDamage);
        if (use.Profile.ContactArea is { } contact) Assert.True(contact.CountsAsProjectileDamage);
        // Resolve the actual audited area recipe at range expiry; no placement randomness hides snapshot selection.
        use = use with { Profile = use.Profile with { Count = 1, DirectDamage = false, Speed = 100, Range = 70,
            EndArea = area with { Count = 1, Offset = 0, Radius = 3 } }, Origin = new(40, 32) };
        runtime.Release(use); runtime.Advance(1);
        Assert.Equal((uint)Math.Floor(offense.AreaHit.MinTotal * area.Multiplier * 1.3 + 1e-9), f.Target.MaxLife - f.Target.Life);
    }
}
