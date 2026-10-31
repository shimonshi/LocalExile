using GameServer.Core.Projectiles;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public partial class ConditionalCriticalGatewayTests
{
    static Contact MirageContact(string gem = "BurningArrow", string weapon = "Crude Bow", bool linked = true, int classRow = 3)
    {
        var f = new Contact(new Draws(), gem, weapon, distance: 20, targetLife: 100000, classRow: classRow);
        var inventory = PlayerInventories.For(f.Conn.Character!, Program.Items);
        var host = Assert.Single(inventory.Items(PlayerInventories.BodyArmour));
        inventory.Change(PlayerInventories.BodyArmour, host.Id, item => item with
        {
            Sockets = [item.Sockets[0], new(SocketColour.White,
                ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SupportGemMirageArcher")!))],
            Links = linked ? [2] : [1, 1]
        });
        f.Set("global_always_hit", 1); f.Set("global_cannot_crit", 1);
        f.Set("physical_damage_+%", -10000); f.Refresh();
        return f;
    }

    static void Occupying(Contact f, bool occupy = true)
    {
        var definition = Program.Ascendancies.Find(2, AscendancyFamily.Normal, "Deadeye")!;
        f.Conn.MainAscendancy = new(definition.Identity, occupy ? [1729, 2872, 62136] : [1729],
            AscendancyRules.AwardKeys);
        new AscendancyRules(Program.Ascendancies).Validate(2, f.Conn.MainAscendancy); f.Refresh();
    }

    [Theory]
    [InlineData("BurningArrow", "Crude Bow", true, true)]
    [InlineData("BurningArrow", "Crude Bow", false, false)]
    [InlineData("BurningArrow", "Driftwood Wand", true, false)]
    [InlineData("Fireball", "Crude Bow", true, false)]
    public async Task OrdinaryDeadeyeMirage_requires_actual_compatible_linked_support_and_current_bow(
        string gem, string weapon, bool linked, bool eligible)
    {
        using var f = MirageContact(gem, weapon, linked); Occupying(f);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        Assert.Equal(eligible, Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target) is not null);
        Assert.Null(Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target, directPlayer: false));
    }

    [Fact]
    public async Task OrdinaryDeadeyeMirage_native_owned_actor_is_published_and_autonomously_releases_real_skill()
    {
        using var f = MirageContact();
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var actor = Assert.IsType<DeadeyeMirageActor>(Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target));
        Assert.False(actor.Detached); Assert.Equal(4, actor.Clock.Remaining, 8);
        var wire = await runtime.Read(); Assert.True(actor.Created);
        Assert.True(Contains(wire, InstanceProtocol.EnterWakeZone(actor.Id)));
        Assert.DoesNotContain(f.Conn.Monsters, p => p.Key == actor.Id); // no hostile/minion AI ownership
        runtime.Advance(.1);
        Assert.True(actor.NextAttack > 0);
        runtime.Advance(actor.NextAttack * .5 + .001);
        var shot = Assert.Single(runtime.Session.Runtime.Projectiles);
        var proxy = Assert.IsType<DeadeyeMirageOffense>(shot.Use.Payload);
        Assert.Equal(actor.Id, shot.Use.Emitter.Id); Assert.Equal(Program.PlayerId, shot.Use.Owner.Id);
        Assert.False(proxy.Shot.ClientPredicted); Assert.Equal("BurningArrow", Program.Skills.EffectId(proxy.Shot.Effect));
        Assert.True(proxy.Shot.Hit.MinTotal > 0); Assert.Equal(0, proxy.Shot.Leech.Life.MinTotal);
        Assert.True(proxy.Attribution.AllowCull); Assert.True(proxy.Attribution.CountsAsYourSkill);
        Assert.False(proxy.Attribution.GrantOwnerRage); Assert.False(proxy.Attribution.RecursiveMirage);
    }

    [Fact]
    public async Task OrdinaryDeadeyeMirage_attached_refresh_follows_owner_and_duration_small_executes()
    {
        using var f = MirageContact(); Occupying(f, false);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var first = Assert.IsType<DeadeyeMirageActor>(Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target));
        Assert.Equal(4.6, first.Clock.Remaining, 8);
        f.Conn.Walk.Teleport(80, 32, 0);
        Program.DeadeyeMirageStep(runtime.Session);
        Assert.Equal(new ProjectileVector(80, 32), first.Position);
        var next = Assert.IsType<DeadeyeMirageActor>(Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target));
        Assert.False(first.Retired); Assert.Single(Program.DeadeyeMirageOf(f.Conn).Actors);
        Assert.Same(first, next);
        runtime.Advance(4.6); Assert.Empty(Program.DeadeyeMirageOf(f.Conn).Actors);
    }

    [Fact]
    public async Task OrdinaryDeadeyeMirage_occupying_detaches_three_archers_and_inclusive_35_gate()
    {
        using var f = MirageContact(); Occupying(f);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var first = Assert.IsType<DeadeyeMirageActor>(Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target));
        Assert.True(first.Detached); Assert.Equal(5.2, first.Clock.Remaining, 8);
        f.Conn.Walk.Teleport(67, 32, 0);
        Assert.Null(Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target));
        f.Conn.Walk.Teleport(68, 32, 0);
        Assert.NotNull(Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target));
        f.Conn.Walk.Teleport(104, 32, 0);
        Assert.NotNull(Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target));
        Assert.Equal(3, Program.DeadeyeMirageOf(f.Conn).Actors.Count);
        f.Conn.Walk.Teleport(140, 32, 0);
        Assert.NotNull(Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target));
        Assert.Equal(3, Program.DeadeyeMirageOf(f.Conn).Actors.Count); Assert.True(first.Retired);
        Assert.Equal(new ProjectileVector(32, 32), first.Position);
    }

    // This forwards actual Core trajectories into the production proxy adapter pending the one-line shared dispatch.
    sealed class MirageBridge(Program.ProjectileSession session) : IProjectileWorld
    {
        public readonly List<ProjectileDamage> Contacts = [];
        public readonly List<ProjectileEvent> Events = [];
        public ProjectileHitResult? ForceAdmission;
        public IReadOnlyList<ProjectileActor> Actors(long from, long until) => session.Actors(from, until);
        public TerrainContact? TraceTerrain(ProjectileVector from, ProjectileVector to) => session.TraceTerrain(from, to);
        public int TargetSplitCount(ProjectileEntity projectile, ProjectileActor target) =>
            session.Conn.Monsters.TryGetValue(target.Key.Id, out var monster)
                ? Program.DeadeyeMarkSplit(session, target.Key.Id, monster) : 0;
        public ProjectileHitResult Hit(ProjectileDamage damage)
        {
            Contacts.Add(damage);
            return ForceAdmission ?? Program.DeadeyeMirageHit(session, damage, (DeadeyeMirageOffense)damage.Projectile.Use.Payload!);
        }
        public void Publish(ProjectileEvent value) => Events.Add(value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryDeadeyeMirage_actual_contact_deals_support_scaled_owner_damage_and_has_no_owner_onhit(bool retire)
    {
        using var f = MirageContact();
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var actor = Assert.IsType<DeadeyeMirageActor>(Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target));
        var use = Program.CaptureDeadeyeMirage(runtime.Session, actor, (52, 32), 1);
        var proxy = (DeadeyeMirageOffense)use.Payload!;
        var native = CaptureDeadeyeGem(f, runtime.Session);
        Assert.Equal(((ProjectileOffense)native.Payload!).Hit.MinTotal * .6, proxy.Shot.Hit.MinTotal, 8);
        var bridge = new MirageBridge(runtime.Session); var core = new ProjectileRuntime(bridge);
        core.Release(use with { Profile = use.Profile with { Speed = 100, Range = 100, Count = 1 },
            Origin = new(32, 32), Aim = new(100, 32) });
        if (retire) Program.RetireDeadeyeMirages(runtime.Session);
        uint life = f.Conn.PlayerLife; int rage = f.Conn.Rage.Read(0, new()).Current;
        core.AdvanceTo(ProjectileRuntime.Time(.5));
        Assert.NotEmpty(bridge.Contacts);
        Assert.Equal(retire ? f.Target.MaxLife : f.Target.MaxLife - (uint)proxy.Shot.Hit.MinTotal, f.Target.Life);
        Assert.Equal(life, f.Conn.PlayerLife); Assert.Equal(rage, f.Conn.Rage.Read(0, new()).Current);
        Assert.Equal(retire ? 0 : 1, Program.DeadeyeMirageOf(f.Conn).Actors.Count);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("weapon")]
    [InlineData("refund")]
    [InlineData("death")]
    [InlineData("transfer")]
    public async Task OrdinaryDeadeyeMirage_source_lifecycle_retires_and_suppresses_queued_create(string removal)
    {
        using var f = MirageContact(); Occupying(f);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn); await runtime.HoldOutput();
        var actor = Assert.IsType<DeadeyeMirageActor>(Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target));
        var inventory = PlayerInventories.For(f.Conn.Character!, Program.Items);
        if (removal is "source" or "weapon")
        {
            byte row = removal == "source" ? PlayerInventories.BodyArmour : PlayerInventories.Weapon1;
            inventory.Remove(row, Assert.Single(inventory.Items(row)).Id);
        }
        if (removal == "refund") f.Conn.MainAscendancy = new();
        if (removal == "death") f.Conn.PlayerLife = 0;
        if (removal == "transfer") f.Conn.LeavingArea = true;
        Program.DeadeyeMirageStep(runtime.Session);
        Assert.True(actor.Retired); Assert.Empty(Program.DeadeyeMirageOf(f.Conn).Actors);
        runtime.ReleaseOutput(); byte[] wire = removal == "transfer" ? await runtime.ReadWire() : await runtime.Read();
        Assert.False(Contains(wire, InstanceProtocol.EnterWakeZone(actor.Id)));
    }
}
