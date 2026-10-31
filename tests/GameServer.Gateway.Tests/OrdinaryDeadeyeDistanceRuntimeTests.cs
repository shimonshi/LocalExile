using GameServer.Core.Projectiles;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

// Explicit composition harness: real Core contacts plus the proposed helper, not an installed Gateway hook.
public sealed class OrdinaryDeadeyeDistanceRuntimeTests
{
    sealed record Contact(uint Target, double Distance, int Phase, long Parent, bool Area, double Hit, double Ignite);

    static (ProjectileRuntimeProbe Probe, List<Contact> Hits) World()
    {
        var probe = new ProjectileRuntimeProbe();
        var hits = new List<Contact>();
        var inputs = new DeadeyeProjectileDamage(30, false);
        probe.Accept = c =>
        {
            double distance = c.Projectile.Distance;
            var packet = DamagePacket.Of(DamageType.Fire, 100, 100).Scale(inputs.HitMultiplier(distance, true));
            var ignite = inputs.IgniteAt(new(100, 100, 100, 200, 200, 4), distance);
            hits.Add(new(c.Target.Id, distance, c.Phase, c.Projectile.ParentId, c.IsArea,
                packet[DamageType.Fire].Min, ignite.Min));
            return ProjectileHitResult.Hit;
        };
        return (probe, hits);
    }

    static ProjectileProfile Profile => new("deadeye-composition")
        { Speed = 10, Radius = 1, Range = 100, Lifetime = ProjectileRuntime.Time(30) };

    [Fact]
    public void Pierce_keeps_travel_and_contact_multiplier_is_not_fed_into_the_next_hit()
    {
        var (probe, hits) = World();
        probe.Target(2, 30, 10); probe.Target(3, 80, 10);
        probe.Release(Profile with { Pierce = 1 }); probe.Advance(12);
        Assert.Equal(2, hits.Count);
        Assert.InRange(hits[0].Distance, 19, 19.001);
        Assert.Equal(100, hits[0].Hit);
        Assert.InRange(hits[1].Distance, 69, 69.001);
        Assert.InRange(hits[1].Hit, 129.14, 129.15);
        Assert.Equal(hits[1].Hit, hits[1].Ignite);
    }

    [Fact]
    public void Chain_uses_cumulative_path_not_straight_line_to_origin_or_live_owner()
    {
        var (probe, hits) = World();
        probe.Target(2, 30, 10); probe.Target(3, 30, 60);
        probe.Release(Profile with { Chain = 1, ChainRange = 80 }); probe.Advance(12);
        Assert.Equal(new uint[] { 2, 3 }, hits.Select(h => h.Target));
        Assert.InRange(hits[1].Distance, 68, 68.1);
        Assert.InRange(hits[1].Hit, 128.28, 128.4);
        Assert.Equal(0, hits[1].Parent); // Redirection reuses the same entity.
    }

    [Fact]
    public void Fork_children_inherit_parent_distance_and_offensive_basis_without_rescaling_it()
    {
        var (probe, hits) = World();
        probe.Target(2, 30, 10); probe.Target(3, 75, 36); probe.Target(4, 75, -16);
        probe.Release(Profile with { Fork = 1 }); probe.Advance(12);
        Assert.Equal(3, hits.Count);
        Assert.Equal(100, hits[0].Hit);
        foreach (var child in hits.Skip(1))
        {
            Assert.True(child.Parent > 0);
            Assert.InRange(child.Distance, 70, 72);
            Assert.Equal(130, child.Hit);
            Assert.Equal(130, child.Ignite);
        }
    }

    [Fact]
    public void Split_continuations_inherit_the_parent_distance_unlike_distinct_emission_children()
    {
        var (probe, hits) = World();
        probe.Target(2, 30, 10); probe.Target(3, 80, 30);
        probe.Release(Profile with { Split = 1, ChainRange = 80 }); probe.Advance(12);
        Assert.Equal(2, hits.Count);
        Assert.True(hits[1].Parent > 0);
        Assert.True(hits[1].Distance > 70);
        Assert.Equal(130, hits[1].Hit);
    }

    [Fact]
    public void Return_keeps_outbound_travel_and_gets_a_fresh_phase_admission()
    {
        var (probe, hits) = World();
        probe.Target(2, 30, 10);
        var owner = new ProjectileActorKey(1000);
        probe.Targets.Add(new(owner, 1, _ => new(10, 10), 0));
        probe.Runtime.Release(new(Profile with { Pierce = -1, Range = 80, Returns = true },
            owner, owner, 1, new(10, 10), new(110, 10)) { ReturnAnchor = owner });
        probe.Advance(20);
        Assert.Equal(2, hits.Count);
        Assert.Equal(0, hits[0].Phase); Assert.Equal(1, hits[1].Phase);
        Assert.Equal(100, hits[0].Hit);
        Assert.True(hits[1].Distance > 120);
        Assert.Equal(130, hits[1].Hit);
    }

    [Fact]
    public void Terminal_projectile_area_uses_flight_distance_not_its_blast_radius()
    {
        var (probe, hits) = World();
        probe.Target(2, 82, 10);
        probe.Release(Profile with { DirectDamage = false, Range = 70, EndArea = new("projectile-explosion", 5) });
        probe.Advance(10);
        var hit = Assert.Single(hits);
        Assert.True(hit.Area);
        Assert.Equal(70, hit.Distance, 6);
        Assert.Equal(130, hit.Hit);
    }

    [Fact]
    public void Delayed_contact_area_freezes_trigger_distance_while_piercing_flight_continues()
    {
        var (probe, hits) = World();
        probe.Target(2, 30, 10); probe.Target(3, 30, 13);
        probe.Release(Profile with { Pierce = -1, ContactArea = new("delayed-projectile-area", 5)
            { Delay = ProjectileRuntime.Time(6), ExcludeContactTarget = true } });
        probe.Advance(9);
        var area = Assert.Single(hits, h => h.Area);
        Assert.Equal(3u, area.Target);
        Assert.InRange(area.Distance, 19, 19.001);
        Assert.Equal(100, area.Hit);
        Assert.True(Assert.Single(probe.Runtime.Projectiles).Distance > 70);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Distinct_end_or_travel_emission_child_starts_its_own_distance(bool travel)
    {
        var (probe, hits) = World();
        probe.Target(2, 90, 10);
        var emission = new ProjectileEmissionRecipe(Profile with { Range = 40 }, Interval: ProjectileRuntime.Time(7));
        var parent = Profile with { DirectDamage = false, Range = travel ? 71 : 70,
            EndEmission = travel ? null : emission, TravelEmission = travel ? emission : null };
        probe.Release(parent); probe.Advance(10);
        var hit = Assert.Single(hits);
        Assert.True(hit.Parent > 0);
        Assert.InRange(hit.Distance, 9, 9.001);
        Assert.Equal(100, hit.Hit);
        Assert.Equal(100, hit.Ignite);
    }
}
