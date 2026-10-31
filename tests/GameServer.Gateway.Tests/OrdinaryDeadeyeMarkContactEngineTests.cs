using GameServer.Core.Projectiles;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public partial class ConditionalCriticalGatewayTests
{
    [Theory]
    [InlineData("CriticalWeakness", "power", (int)ChargeType.Power)]
    [InlineData("PoachersMark", "frenzy", (int)ChargeType.Frenzy)]
    [InlineData("WarlordsMark", "endurance", (int)ChargeType.Endurance)]
    public async Task OrdinaryDeadeyeMark_native_quality_hit_grants_real_charge_events_with_scaled_chance(
        string item, string charge, int type)
    {
        using var f = MarkContact(item); Focal(f);
        var inventory = PlayerInventories.For(f.Conn.Character!, Program.Items);
        var host = Assert.Single(inventory.Items(PlayerInventories.BodyArmour));
        inventory.Change(PlayerInventories.BodyArmour, host.Id, h => h with
        { Sockets = [new(SocketColour.White, h.Sockets[0].Gem! with { Quality = 20 }), h.Sockets[1]] });
        await using var runtime = new ProjectileRuntimeFixture(f.Conn); var mark = CastMark(f, runtime);
        Assert.Equal(8, Program.DeadeyeMarkAmount(f.Conn, mark, $"grant_attacker_{charge}_charge_when_hit_%_chance"));
        var session = (ChargeSession)Method("ChargesOf").Invoke(null, [f.Conn])!;
        Program.DeadeyeMarkHit(runtime.Session, 42, f.Target, attack: false, random: new ZeroRandom());
        Assert.True(session.Pending);
        var stats = Program.CharacterPlayerStats(f.Conn);
        session.Step(1, name => Program.DeadeyeValue(stats, name),
            () => (WeaponProfile)Method("WeaponProfileFor").Invoke(null, [f.Conn, true])!, true, false, false, new ZeroRandom());
        Assert.Equal(1, session.State.Held((ChargeType)type));
    }
    [Fact]
    public async Task OrdinaryDeadeyeMark_refund_republishes_current_native_values_and_stale_target_removal_is_suppressed()
    {
        using var f = MarkContact(); Focal(f);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var mark = CastMark(f, runtime); await runtime.Read();
        Assert.NotNull(mark.PublishedValues);
        f.Conn.MainAscendancy = new();
        Program.RefreshDeadeyeMarks(runtime.Session);
        byte[] update = await runtime.Read();
        Assert.True(Contains(update, InstanceEntities.RemoveBuff(42, mark.Buff)));
        Assert.Contains(10u, mark.PublishedValues!);
        await runtime.HoldOutput(); Program.RemoveDeadeyeMark(runtime.Session);
        f.Conn.Monsters[42] = ProjectileRuntimeFixture.Monster(52, 32);
        runtime.ReleaseOutput(); byte[] stale = await runtime.Read();
        Assert.False(Contains(stale, InstanceEntities.RemoveBuff(42, mark.Buff)));
    }

    [Theory]
    [InlineData(ProjectileHitResult.Miss)]
    [InlineData(ProjectileHitResult.Blocked)]
    public async Task OrdinaryDeadeyeMark_speculative_target_split_has_no_hit_or_resource_side_effects(ProjectileHitResult admission)
    {
        using var f = MirageContact(); Focal(f);
        ushort skill = LinkIndependentMark(f, "ProjectileWeakness");
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        Assert.True(Program.TryDeadeyeMarkCast(runtime.Session, skill, 42)); runtime.Advance(.4);
        var mark = Program.DeadeyeMarkOf(f.Conn).Active;
        var actor = Assert.IsType<DeadeyeMirageActor>(Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target));
        f.Conn.Monsters[43] = ProjectileRuntimeFixture.Monster(65, 37);
        var bridge = new MirageBridge(runtime.Session) { ForceAdmission = admission };
        var core = new ProjectileRuntime(bridge);
        var shot = Program.CaptureDeadeyeMirage(runtime.Session, actor, (52, 32), 1);
        core.Release(shot with { Profile = shot.Profile with { Speed = 100, Count = 1, Pierce = 0, Chain = 0, Fork = 0 },
            Origin = new(32, 32), Aim = new(100, 32) });
        uint life = f.Conn.PlayerLife, mana = f.Conn.PlayerMana;
        core.AdvanceTo(ProjectileRuntime.Time(.5));
        Assert.NotEmpty(bridge.Contacts);
        Assert.DoesNotContain(bridge.Events, e => e.Reason == ProjectileEndReason.Split);
        Assert.Equal(f.Target.MaxLife, f.Target.Life); Assert.Same(mark, Program.DeadeyeMarkOf(f.Conn).Active);
        Assert.Equal(life, f.Conn.PlayerLife); Assert.Equal(mana, f.Conn.PlayerMana);
    }

    [Fact]
    public async Task OrdinaryDeadeyeMark_connections_own_independent_marks_and_retirement_cancels_pending_cast()
    {
        using var a = MarkContact(); using var b = MarkContact();
        await using var ra = new ProjectileRuntimeFixture(a.Conn); await using var rb = new ProjectileRuntimeFixture(b.Conn);
        var am = CastMark(a, ra); var bm = CastMark(b, rb);
        Program.RetireDeadeyeMarks(ra.Session);
        Assert.Null(Program.DeadeyeMarkOf(a.Conn).Active); Assert.Same(bm, Program.DeadeyeMarkOf(b.Conn).Active);
        Assert.True(Program.TryDeadeyeMarkCast(ra.Session, a.Skill, 42));
        Program.RetireDeadeyeMarks(ra.Session); ra.Advance(2);
        Assert.Null(Program.DeadeyeMarkOf(a.Conn).Active); Assert.Same(bm, Program.DeadeyeMarkOf(b.Conn).Active);
        Assert.NotSame(am, bm);
    }
    static ushort LinkIndependentMark(Contact f, string name)
    {
        var inventory = PlayerInventories.For(f.Conn.Character!, Program.Items);
        var host = Assert.Single(inventory.Items(PlayerInventories.BodyArmour));
        inventory.Change(PlayerInventories.BodyArmour, host.Id, item => item with
        {
            Sockets = [.. item.Sockets, new(SocketColour.White,
                ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SkillGem" + name)!))], Links = [2, 1]
        });
        var skills = (IEnumerable<(ushort Id, int Effect, string Name, int Level, int Quality, uint Gem)>)
            Method("GemSkillEffects").Invoke(null, [f.Conn])!;
        return skills.Single(s => Program.DeadeyeMarks.Find(s.Effect) is not null).Id;
    }

    [Fact]
    public async Task OrdinaryDeadeyeMark_Poacher_flat_uses_frozen_hitter_skill_conversion_effectiveness_and_increases()
    {
        using var f = MirageContact(); Focal(f);
        ushort markSkill = LinkIndependentMark(f, "PoachersMark");
        f.Set("physical_damage_+%", 100);
        f.Set("base_physical_damage_%_to_convert_to_fire", 100);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        Assert.True(Program.TryDeadeyeMarkCast(runtime.Session, markSkill, 42)); runtime.Advance(.4);
        var mark = Assert.IsType<Program.DeadeyeMarkInstance>(Program.DeadeyeMarkOf(f.Conn).Active);
        var baseline = Program.SupportedProjectileHit(f.Conn, f.Skill, false);
        var source = Program.CaptureDeadeyeMarkHitSource(f.Conn, f.Skill, false)!;
        var withMark = Program.DeadeyeMarkedPacket(runtime.Session, 42, f.Target, baseline, true, true, source);
        int min = Program.DeadeyeMarkAmount(f.Conn, mark, "minimum_added_physical_damage_taken");
        int max = Program.DeadeyeMarkAmount(f.Conn, mark, "maximum_added_physical_damage_taken");
        f.Set("attack_minimum_added_physical_damage", min); f.Set("attack_maximum_added_physical_damage", max);
        var reference = Program.SupportedProjectileHit(f.Conn, f.Skill, false);
        foreach (var type in DamageTypes.ConversionOrder)
        {
            Assert.Equal(reference[type].Min, withMark[type].Min, 8);
            Assert.Equal(reference[type].Max, withMark[type].Max, 8);
        }
        Assert.True(withMark[DamageType.Fire].Min > baseline[DamageType.Fire].Min);
        Assert.Equal(0, withMark[DamageType.Physical].Min);
        f.Set("attack_minimum_added_chaos_damage", 9999);
        Assert.Equal(withMark.MinTotal, source.Resolve(new(min, max)).MinTotal, 8); // actual hitter remains frozen
    }

    [Fact]
    public async Task OrdinaryDeadeyeMark_Sniper_joins_existing_target_taken_bucket_after_mitigation_and_never_ignite()
    {
        using var f = MarkContact(); Focal(f);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn); CastMark(f, runtime);
        var target = new SpawnedMonster("mixed target", new(52, 32, 0), 0, 1, 10000)
        {
            Mods = new([], [], new(new Dictionary<string, int>
            {
                ["base_damage_taken_+%"] = -50, ["base_additional_physical_damage_reduction_%"] = 30,
                ["base_fire_damage_resistance_%"] = 50
            }))
        };
        // Retain the source-real mark but place it on the modded object via the real cast path.
        f.Conn.Monsters[43] = target;
        Assert.True(Program.TryDeadeyeMarkCast(runtime.Session, f.Skill, 43)); runtime.Advance(1.4);
        var rolled = DamagePacket.Of(DamageType.Physical, 100, 100).Add(DamageType.Fire, new(100, 100));
        var ordinary = Program.MonsterDamage(target, rolled, false, true, default, default, out var baseTaken);
        var marked = Program.DeadeyeMarkedDamage(runtime.Session, 43, target, rolled, false, true, default, default);
        Assert.Equal(baseTaken.MinTotal * .67 / .5, marked.Taken.MinTotal, 8);
        Assert.Equal((int)Math.Round(marked.Taken.MinTotal), marked.Damage);
        Assert.True(marked.Damage > ordinary.Damage);
        var unqualified = Program.DeadeyeMarkedDamage(runtime.Session, 43, target, rolled, false, true,
            default, default, projectile: false);
        Assert.Equal(ordinary.Damage, unqualified.Damage);
        var ignite = new MonsterIgnites.Settings(100, 20, 30, 40, 60, 4);
        var source = new ProjectileOffense(rolled, false, 1000, 0, 150, "ignite") { Ignite = ignite };
        _ = Program.DeadeyeMarkedPacket(runtime.Session, 43, target, source.Hit, true, true);
        Assert.Equal(ignite, source.Ignite);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public async Task OrdinaryDeadeyeMark_Sniper_actual_core_split_respects_prohibition_and_eligible_targets(
        bool prohibit, bool neighbours, bool split)
    {
        using var f = MirageContact(); Focal(f);
        ushort markSkill = LinkIndependentMark(f, "ProjectileWeakness");
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        Assert.True(Program.TryDeadeyeMarkCast(runtime.Session, markSkill, 42)); runtime.Advance(.4);
        var actor = Assert.IsType<DeadeyeMirageActor>(Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target));
        if (neighbours)
        {
            f.Conn.Monsters[43] = ProjectileRuntimeFixture.Monster(65, 37);
            f.Conn.Monsters[44] = ProjectileRuntimeFixture.Monster(65, 27);
        }
        var bridge = new MirageBridge(runtime.Session); var core = new ProjectileRuntime(bridge);
        var use = Program.CaptureDeadeyeMirage(runtime.Session, actor, (52, 32), 1);
        core.Release(use with { Profile = use.Profile with
        { Speed = 100, Range = 100, Count = 1, CannotSplit = prohibit, Pierce = 0, Chain = 0, Fork = 0 },
            Origin = new(32, 32), Aim = new(100, 32) });
        core.AdvanceTo(ProjectileRuntime.Time(.5));
        Assert.Equal(split, bridge.Events.Any(e => e.Reason == ProjectileEndReason.Split));
        Assert.True(f.Target.Life < f.Target.MaxLife);
        Assert.DoesNotContain(bridge.Events, e => e.Kind == ProjectileEventKind.Area); // no invented area provenance
    }

    [Fact]
    public async Task OrdinaryDeadeyeMirage_Tailwind_is_ally_eight_within60_and_does_not_copy_owner_Gale()
    {
        using var f = MirageContact();
        var definition = Program.Ascendancies.Find(2, AscendancyFamily.Normal, "Deadeye")!;
        f.Conn.MainAscendancy = new(definition.Identity, [59837, 24848], AscendancyRules.AwardKeys);
        for (int i = 0; i < 10; i++) Assert.True(Program.DeadeyeGaleAdmitUse(f.Conn, true));
        var source = Program.DeadeyeTailwindSource(f.Conn);
        var self = DeadeyeGaleAura.ForRecipient([source], source.Owner, 1, source.Position, world: source.World)!.Value;
        var ally = DeadeyeGaleAura.ForRecipient([source], new(12345, 1), 1, source.Position, world: source.World)!.Value;
        Assert.Equal(1.16, self.ActionFactor(_ => 0), 8); Assert.Equal(1.08, ally.ActionFactor(_ => 0), 8);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var actor = Assert.IsType<DeadeyeMirageActor>(Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target));
        Program.DeadeyeMirageStep(runtime.Session);
        var seconds = (double)Method("SupportedAttackSeconds").Invoke(null, [f.Conn, f.Skill])!;
        Assert.Equal(seconds * Program.PlayerActionFactor(f.Conn) / .4 / 1.08, actor.NextAttack, 8);
    }
}
