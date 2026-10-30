using GameServer.Core.Projectiles;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public partial class ConditionalCriticalGatewayTests
{
    [Fact]
    public async Task DeadeyeReview_attached_archer_deals_autonomous_damage_during_continuous_real_owner_hits()
    {
        using var f = MirageContact();
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var use = CaptureDeadeyeGem(f, runtime.Session);
        var owner = (ProjectileOffense)use.Payload!;
        Assert.Equal(owner.Hit.MinTotal, owner.Hit.MaxTotal);
        Assert.Equal(f.Skill, owner.SourceSkill);
        use = use with { Profile = use.Profile with { Speed = 1000, Count = 1 },
            Origin = new(32, 32), Aim = new(120, 32) };
        double ownerPeriod = (double)Method("SupportedAttackSeconds").Invoke(null, [f.Conn, f.Skill])!;
        runtime.Release(use); runtime.Advance(.04);
        var actor = Assert.Single(Program.DeadeyeMirageOf(f.Conn).Actors.Values);
        int generation = actor.AttackGeneration;
        double nextAttack = actor.NextAttack;
        // Native -60% proxy attack speed puts its first release at 1.25 owner periods after the first hit.
        Assert.True(nextAttack * .5 > ownerPeriod);
        runtime.Advance(ownerPeriod); runtime.Release(use); runtime.Advance(ownerPeriod + .04);
        Assert.Same(actor, Assert.Single(Program.DeadeyeMirageOf(f.Conn).Actors.Values));
        Assert.Equal(generation, actor.AttackGeneration); Assert.Equal(nextAttack, actor.NextAttack);
        Assert.False(actor.Retired);
        Assert.Equal(f.Target.MaxLife - (uint)(2 * owner.Hit.MinTotal), f.Target.Life);
        // Both actual owner contacts occurred before the archer's first release. Keep hitting beyond that release.
        runtime.Advance(2 * ownerPeriod); runtime.Release(use);
        runtime.Advance(2 * ownerPeriod + .2);
        Assert.Same(actor, Assert.Single(Program.DeadeyeMirageOf(f.Conn).Actors.Values));
        Assert.True(f.Target.Life < f.Target.MaxLife - (uint)(3 * owner.Hit.MinTotal),
            "The real shared runtime must commit proxy damage in addition to all three owner contacts.");
    }

    [Fact]
    public async Task DeadeyeReview_refresh_resets_only_lifetime_with_native_recipient_rate()
    {
        using var f = MirageContact(); f.Set("buff_time_passed_+%", 50);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var actor = Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target)!;
        runtime.Advance(.2);
        int attack = actor.AttackGeneration; double next = actor.NextAttack;
        Assert.Equal(3.7, actor.Clock.Remaining, 8);
        var refreshed = Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target);
        Assert.Same(actor, refreshed); Assert.False(actor.Retired);
        Assert.Equal(4, actor.Clock.Remaining); Assert.Equal(1.5, actor.Clock.Rate);
        Assert.Equal(.2 + 4 / 1.5, actor.Clock.Deadline, 8);
        Assert.Equal(attack, actor.AttackGeneration); Assert.Equal(next, actor.NextAttack);
    }

    static void ReviewIgniteSources(Contact f)
    {
        f.Set("physical_damage_+%", 100);
        f.Set("base_physical_damage_%_to_convert_to_fire", 100);
        f.Set("base_chance_to_ignite_%", 73);
        f.Set("ignite_duration_+%", 50);
        f.Set("attack_minimum_added_chaos_damage", 0);
        f.Set("attack_maximum_added_chaos_damage", 0);
        f.Refresh();
    }

    static void AssertReviewIgnite(MonsterIgnites.Settings expected, MonsterIgnites.Settings actual)
    {
        Assert.Equal(expected.Chance, actual.Chance); Assert.Equal(expected.Seconds, actual.Seconds);
        Assert.Equal(expected.Min, actual.Min, 8); Assert.Equal(expected.Max, actual.Max, 8);
        Assert.Equal(expected.CriticalMin, actual.CriticalMin, 8); Assert.Equal(expected.CriticalMax, actual.CriticalMax, 8);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeadeyeReview_Poacher_Ignite_matches_real_equivalent_added_physical_and_freezes_hitter(bool area)
    {
        using var f = MirageContact(); Focal(f); ReviewIgniteSources(f);
        ushort markSkill = LinkIndependentMark(f, "PoachersMark");
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        Assert.True(Program.TryDeadeyeMarkCast(runtime.Session, markSkill, 42)); runtime.Advance(.4);
        var mark = Program.DeadeyeMarkOf(f.Conn).Active!;
        var source = Program.CaptureDeadeyeMarkHitSource(f.Conn, f.Skill, area)!;
        var original = (ProjectileOffense)CaptureDeadeyeGem(f, runtime.Session).Payload!;
        var marked = Program.DeadeyeMarkedIgnite(runtime.Session, 42, f.Target, original.Ignite, source);
        Assert.True(marked.Min > original.Ignite.Min); Assert.True(marked.CriticalMax > original.Ignite.CriticalMax);
        Assert.Equal(original.Ignite.Chance, marked.Chance); Assert.Equal(original.Ignite.Seconds, marked.Seconds);
        f.Set("attack_minimum_added_physical_damage", Program.DeadeyeMarkAmount(f.Conn, mark, "minimum_added_physical_damage_taken"));
        f.Set("attack_maximum_added_physical_damage", Program.DeadeyeMarkAmount(f.Conn, mark, "maximum_added_physical_damage_taken"));
        f.Refresh();
        var reference = (ProjectileOffense)CaptureDeadeyeGem(f, runtime.Session).Payload!;
        AssertReviewIgnite(reference.Ignite, marked);
        f.Set("fire_damage_+%", 9999); f.Set("never_ignite", 1);
        AssertReviewIgnite(marked, Program.DeadeyeMarkedIgnite(runtime.Session, 42, f.Target, original.Ignite, source));
        Program.RemoveDeadeyeMark(runtime.Session);
        AssertReviewIgnite(original.Ignite, Program.DeadeyeMarkedIgnite(runtime.Session, 42, f.Target, original.Ignite, source));
    }

    [Fact]
    public async Task DeadeyeReview_ordinary_Poacher_Ignite_uses_raw_conversion_and_preserves_optional_source_callers()
    {
        using var f = MirageContact(); Focal(f); ReviewIgniteSources(f);
        ushort markSkill = LinkIndependentMark(f, "PoachersMark");
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        Assert.True(Program.TryDeadeyeMarkCast(runtime.Session, markSkill, 42)); runtime.Advance(.4);
        var source = Program.CaptureDeadeyeOrdinaryMarkHitSource(f.Conn, false);
        var baseline = source.ResolveIgnite!(default);
        var marked = Program.DeadeyeMarkedIgnite(runtime.Session, 42, f.Target, baseline, source);
        Assert.True(marked.Min > baseline.Min); Assert.True(marked.CriticalMax > baseline.CriticalMax);
        Assert.Equal(baseline.Chance, marked.Chance); Assert.Equal(baseline.Seconds, marked.Seconds);
        AssertReviewIgnite(baseline, Program.DeadeyeMarkedIgnite(runtime.Session, 42, f.Target, baseline,
            new DeadeyeMarkHitSource(_ => DamagePacket.Empty)));
        AssertReviewIgnite(baseline, Program.DeadeyeMarkedIgnite(runtime.Session, 99, f.Target, baseline, source));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeadeyeReview_Mirage_direct_and_area_Poacher_Ignite_scale_all_four_ranges_once(bool area)
    {
        using var f = MirageContact(); Focal(f); ReviewIgniteSources(f);
        ushort markSkill = LinkIndependentMark(f, "PoachersMark");
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        Assert.True(Program.TryDeadeyeMarkCast(runtime.Session, markSkill, 42)); runtime.Advance(.4);
        var actor = Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target)!;
        var owner = (ProjectileOffense)CaptureDeadeyeGem(f, runtime.Session).Payload!;
        var ownerSource = area ? owner.MarkAreaBasis : owner.MarkDirectBasis;
        var ownerMarked = Program.DeadeyeMarkedIgnite(runtime.Session, 42, f.Target, owner.Ignite, ownerSource);
        var proxy = (DeadeyeMirageOffense)Program.CaptureDeadeyeMirage(runtime.Session, actor, (52, 32), 1).Payload!;
        var proxySource = area ? proxy.AreaMarkSource : proxy.DirectMarkSource;
        var proxyMarked = Program.DeadeyeMarkedIgnite(runtime.Session, 42, f.Target, proxy.Shot.Ignite, proxySource);
        AssertReviewIgnite(ownerMarked with { Min = ownerMarked.Min * .6, Max = ownerMarked.Max * .6,
            CriticalMin = ownerMarked.CriticalMin * .6, CriticalMax = ownerMarked.CriticalMax * .6 }, proxyMarked);
        Assert.True(proxyMarked.Min > proxy.Shot.Ignite.Min);
    }
}
