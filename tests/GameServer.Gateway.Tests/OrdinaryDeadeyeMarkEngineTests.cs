using GameServer.Core.Projectiles;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public partial class ConditionalCriticalGatewayTests
{
    static bool Contains(byte[] wire, byte[] expected) => wire.AsSpan().IndexOf(expected) >= 0;
    static Contact MarkContact(string name = "ProjectileWeakness", uint life = 10000) =>
        new(new Draws(), name, distance: 20, targetLife: life, classRow: 3);

    static void Focal(Contact f)
    {
        var definition = Program.Ascendancies.Find(2, AscendancyFamily.Normal, "Deadeye")!;
        f.Conn.MainAscendancy = new(definition.Identity, [53086, 5443], AscendancyRules.AwardKeys);
        new AscendancyRules(Program.Ascendancies).Validate(2, f.Conn.MainAscendancy);
        f.Refresh();
    }

    static Program.DeadeyeMarkInstance CastMark(Contact f, ProjectileRuntimeFixture runtime, uint id = 42)
    {
        Assert.True(Program.TryDeadeyeMarkCast(runtime.Session, f.Skill, id));
        runtime.Advance(1);
        return Assert.IsType<Program.DeadeyeMarkInstance>(Program.DeadeyeMarkOf(f.Conn).Active);
    }

    [Theory]
    [InlineData("ProjectileWeakness", "SnipersMark", 1214, 78)]
    [InlineData("CriticalWeakness", "AssassinsMark", 1205, 77)]
    [InlineData("PoachersMark", "PoachersMark", 4044, 80)]
    [InlineData("WarlordsMark", "WarlordsMark", 1176, 74)]
    public async Task OrdinaryDeadeyeMark_native_cast_pays_times_and_publishes_buff(string item, string effect, int row, int buff)
    {
        using var f = MarkContact(item); Focal(f);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        uint mana = f.Conn.PlayerMana;
        Assert.True(Program.TryDeadeyeMarkCast(runtime.Session, f.Skill, 42),
            string.Join(";", ((IEnumerable<(ushort Id, int Effect, string Name, int Level, int Quality, uint Gem)>)
                Method("GemSkillEffects").Invoke(null, [f.Conn])!).Select(g => $"{g.Id}/{g.Effect}/{g.Name}:"
                + Method("GemSkillInstanceDisabled").Invoke(null, [f.Conn, g.Effect, g.Id])))
                + $" skill={f.Skill} town={Method("InTown").Invoke(null, [f.Conn])} factor={Program.PlayerActionFactor(f.Conn)}"
                + $" active={Method("DeadeyeActive").Invoke(null, [f.Conn])} mark={Program.DeadeyeMarks.All.Count} mana={f.Conn.PlayerMana}");
        Assert.Null(Program.DeadeyeMarkOf(f.Conn).Active);
        Assert.True(f.Conn.PlayerMana < mana);
        runtime.Advance(.399);
        Assert.Null(Program.DeadeyeMarkOf(f.Conn).Active);
        runtime.Advance(.4);
        var mark = Assert.IsType<Program.DeadeyeMarkInstance>(Program.DeadeyeMarkOf(f.Conn).Active);
        Assert.Equal(row, mark.Source.Effect);
        var native = Program.DeadeyeMarks.Find(row)!;
        Assert.Equal(effect, native.Id); Assert.Equal(buff, native.Buff);
        Assert.Equal(Program.PlayerId, mark.Caster); Assert.Equal(f.Skill, mark.Source.Skill);
        byte[] wire = await runtime.Read();
        int offset = wire.AsSpan().IndexOf(Convert.FromHexString("01C10000002A000000000000"));
        Assert.True(offset >= 0);
        int end = OrdinaryBuffWire.End(wire, offset);
        Assert.Equal(offset + 27, end); // The non-recovery mark has no value words or duration fields.
        OrdinaryBuffWire.AssertFollowingSentinel(wire[offset..end]);
        Assert.True(double.IsPositiveInfinity(mark.Clock.Remaining));
    }

    [Fact]
    public async Task OrdinaryDeadeyeMark_failed_casts_have_no_cost_or_mark_and_repeated_cast_is_admitted_after_windup()
    {
        using var f = MarkContact();
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        f.Conn.PlayerMana = 0;
        Assert.False(Program.TryDeadeyeMarkCast(runtime.Session, f.Skill, 42));
        Assert.Null(Program.DeadeyeMarkOf(f.Conn).Active);
        f.Conn.PlayerMana = 1000;
        Assert.False(Program.TryDeadeyeMarkCast(runtime.Session, f.Skill, 999));
        Assert.Equal(1000u, f.Conn.PlayerMana);
        Assert.True(Program.TryDeadeyeMarkCast(runtime.Session, f.Skill, 42));
        uint after = f.Conn.PlayerMana;
        Assert.False(Program.TryDeadeyeMarkCast(runtime.Session, f.Skill, 42)); Assert.Equal(after, f.Conn.PlayerMana);
        runtime.Advance(.5); Assert.NotNull(Program.DeadeyeMarkOf(f.Conn).Active);
        Assert.True(Program.TryDeadeyeMarkCast(runtime.Session, f.Skill, 42));
    }

    [Fact]
    public async Task OrdinaryDeadeyeMark_focal_scales_native_effect_and_refund_removes_scaler_without_removing_skill()
    {
        using var f = MarkContact(); Focal(f);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var mark = CastMark(f, runtime);
        Assert.Equal(17, Program.DeadeyeMarkAmount(f.Conn, mark, "projectile_damage_taken_+%"));
        Assert.Equal(3, Program.DeadeyeMarkSplit(runtime.Session, 42, f.Target));
        var packet = Program.DeadeyeMarkedPacket(runtime.Session, 42, f.Target,
            DamagePacket.Of(DamageType.Fire, 100, 100), false, true);
        Assert.Equal(100, packet.MinTotal, 8); // Sniper never changes the pre-mitigation packet.
        Assert.Equal(117, Program.DeadeyeMarkTakenComponents(runtime.Session, 42, f.Target, packet, true, true).MinTotal, 8);
        Assert.Equal(100, Program.DeadeyeMarkedPacket(runtime.Session, 42, f.Target,
            DamagePacket.Of(DamageType.Fire, 100, 100), false, false).MinTotal);
        f.Conn.MainAscendancy = new(); f.Refresh();
        Assert.Equal(10, Program.DeadeyeMarkAmount(f.Conn, mark, "projectile_damage_taken_+%"));
        Assert.Equal(2, Program.DeadeyeMarkSplit(runtime.Session, 42, f.Target));
        Assert.Same(mark, Program.DeadeyeMarkOf(f.Conn).Active);
    }

    [Theory]
    [InlineData(42, 52, 1)]
    [InlineData(43, 112, .75)]
    [InlineData(43, 113, 1)]
    public async Task OrdinaryDeadeyeMark_focal_nearby_enemy_defence_excludes_marked_enemy(uint attacker, int x, double expected)
    {
        using var f = MarkContact(); Focal(f);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        CastMark(f, runtime);
        var enemy = attacker == 42 ? f.Target : ProjectileRuntimeFixture.Monster(x, 32);
        Assert.Equal(expected, Program.DeadeyeMarkIncoming(runtime.Session, attacker, enemy));
        var friendly = new SpawnedMonster("friend", new(52, 32, 1), 0, 1, 10) { Hostile = false };
        Assert.Equal(1, Program.DeadeyeMarkIncoming(runtime.Session, 44, friendly));
        f.Conn.MainAscendancy = new();
        Assert.Equal(1, Program.DeadeyeMarkIncoming(runtime.Session, attacker, enemy));
    }

    [Fact]
    public async Task OrdinaryDeadeyeMark_transfer_preserves_caster_and_skill_skips_dead_and_friendly_and_is_once()
    {
        using var f = MarkContact(); Focal(f);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var mark = CastMark(f, runtime);
        f.Conn.Monsters[43] = new("friend", new(53, 32, 0), 0, 1, 10) { Hostile = false };
        f.Conn.Monsters[44] = new("corpse", new(54, 32, 0), 0, 1, 10) { Dead = true };
        f.Conn.Monsters[45] = ProjectileRuntimeFixture.Monster(55, 32);
        f.Target.Dead = true; f.Target.Life = 0;
        Program.DeadeyeMarkDeath(runtime.Session, 42, f.Target, new Random(1));
        var moved = Assert.IsType<Program.DeadeyeMarkInstance>(Program.DeadeyeMarkOf(f.Conn).Active);
        Assert.Equal(45u, moved.Target); Assert.Equal(mark.Source, moved.Source); Assert.Equal(mark.Caster, moved.Caster);
        Assert.NotEqual(mark.Buff, moved.Buff);
        Program.DeadeyeMarkDeath(runtime.Session, 42, f.Target, new Random(1));
        Assert.Same(moved, Program.DeadeyeMarkOf(f.Conn).Active);
    }

    [Theory]
    [InlineData("CriticalWeakness")]
    [InlineData("PoachersMark")]
    [InlineData("WarlordsMark")]
    public async Task OrdinaryDeadeyeMark_hit_critical_leech_and_death_consumers_use_native_stats(string item)
    {
        using var f = MarkContact(item); Focal(f);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var mark = CastMark(f, runtime);
        if (item == "CriticalWeakness")
        {
            Assert.Equal((362, 202), Program.DeadeyeMarkedCritical(runtime.Session, 42, f.Target, 100, 150));
            f.Target.Dead = true; f.Target.Life = 0;
            Program.DeadeyeMarkDeath(runtime.Session, 42, f.Target, new Random(1));
            Assert.Equal(100u, f.Conn.PlayerLife); // scaled native on-death life, capped at maximum
        }
        if (item == "PoachersMark")
        {
            var hit = Program.DeadeyeMarkedPacket(runtime.Session, 42, f.Target, DamagePacket.Empty, true, false);
            Assert.Equal(0, hit.MinTotal); // No final-hit flat addition without the real source snapshot.
            Assert.Equal(5, Program.DeadeyeMarkAmount(f.Conn, mark, "minimum_added_physical_damage_taken"));
            Assert.Equal(7, Program.DeadeyeMarkAmount(f.Conn, mark, "maximum_added_physical_damage_taken"));
            uint life = f.Conn.PlayerLife; Program.DeadeyeMarkHit(runtime.Session, 42, f.Target, true);
            Assert.Equal(life + 26, f.Conn.PlayerLife);
            Assert.Equal(-35, Program.DeadeyeMarkedPhysicalReduction(runtime.Session, 42, f.Target));
            Assert.Equal(100, Program.DeadeyeMarkedDamage(runtime.Session, 42, f.Target,
                DamagePacket.Of(DamageType.Physical, 100, 100), false, true, default, default).Damage);
        }
        if (item == "WarlordsMark")
        {
            var leech = Program.DeadeyeMarkedLeech(runtime.Session, 42, f.Target,
                new(DamagePacket.Empty, DamagePacket.Empty, DamagePacket.Empty), true);
            Assert.Equal(350, leech.Life[DamageType.Fire].Min); Assert.Equal(350, leech.Mana[DamageType.Chaos].Min);
            Assert.Equal(.8, Program.DeadeyeMarkStun(runtime.Session, 42, f.Target, .4, new ZeroRandom()));
            Assert.True(f.Conn.Rage.Read(Program.ProjectileWorldTime(f.Conn), new()).Current > 0);
        }
        Assert.Equal(0, Program.DeadeyeMarkedCritical(runtime.Session, 99, f.Target, 0, 150).Chance);
    }

    sealed class ZeroRandom : Random { public override int Next(int maxValue) => 0; }

    [Fact]
    public async Task OrdinaryDeadeyeMark_source_removal_cancels_cast_active_mark_and_queued_publication()
    {
        using var f = MarkContact();
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        await runtime.HoldOutput();
        var mark = CastMark(f, runtime);
        var inventory = PlayerInventories.For(f.Conn.Character!, Program.Items);
        inventory.Remove(PlayerInventories.BodyArmour, Assert.Single(inventory.Items(PlayerInventories.BodyArmour)).Id);
        Program.RefreshDeadeyeMarks(runtime.Session);
        Assert.Null(Program.DeadeyeMarkOf(f.Conn).Active);
        runtime.ReleaseOutput(); var wire = await runtime.Read();
        var native = Program.DeadeyeMarks.Find(mark.Source.Effect)!;
        Assert.False(Contains(wire, InstanceEntities.AddBuff(42, mark.Buff, native.Buff, Program.PlayerId, f.Skill)));
        Program.RetireDeadeyeMarks(runtime.Session);
        Assert.False(Program.TryDeadeyeMarkCast(runtime.Session, f.Skill, 42));
    }
}
