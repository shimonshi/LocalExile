using GameServer.Core.Projectiles;

namespace GameServer.Core.Tests;

public class ProjectileDamageProvenanceTests
{
    sealed class World : IProjectileWorld
    {
        public readonly List<ProjectileDamage> Hits = [];
        public IReadOnlyList<ProjectileActor> Actors(long from, long until) =>
            [new(new(2), 2, _ => new(70, 0), 0), new(new(3), 2, _ => new(70, 2), 0)];
        public TerrainContact? TraceTerrain(ProjectileVector from, ProjectileVector to) => null;
        public ProjectileHitResult Hit(ProjectileDamage damage) { Hits.Add(damage); return ProjectileHitResult.Hit; }
        public void Publish(ProjectileEvent value) { }
        public void Run(ProjectileProfile profile)
        {
            var runtime = new ProjectileRuntime(this);
            runtime.Release(new(profile, new(1000), new(1000), 1, new(0, 0), new(100, 0)));
            runtime.AdvanceTo(ProjectileRuntime.Time(2));
        }
    }

    static ProjectileProfile Profile => new("same-art") { Speed = 100, Range = 100, Radius = 0 };

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void Terminal_collision_uses_terminal_grant_even_when_direct_profile_disagrees(bool direct, bool area)
    {
        var world = new World();
        world.Run(Profile with { CountsAsProjectileDamage = direct,
            EndArea = new("direct", 3) { CountsAsProjectileDamage = area } });
        Assert.Equal(2, world.Hits.Count);
        Assert.All(world.Hits, hit => { Assert.True(hit.IsArea); Assert.Equal(area, hit.CountsAsProjectileDamage); });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Targeted_and_pulse_grants_are_recipe_owned_and_not_inferred_from_stage(bool qualified)
    {
        var targeted = new World();
        targeted.Run(Profile with { CountsAsProjectileDamage = !qualified,
            ContactTargets = new("direct", 1, 5) { CountsAsProjectileDamage = qualified } });
        var secondary = Assert.Single(targeted.Hits, h => h.Target.Id == 3);
        Assert.False(secondary.IsArea);
        Assert.Equal(qualified, secondary.CountsAsProjectileDamage);
        var pulse = new World();
        pulse.Run(Profile with { DirectDamage = false, CountsAsProjectileDamage = !qualified,
            Pulse = new("direct", 3, ProjectileRuntime.Time(.7)) { CountsAsProjectileDamage = qualified } });
        Assert.NotEmpty(pulse.Hits);
        Assert.All(pulse.Hits, h => { Assert.True(h.IsArea); Assert.Equal(qualified, h.CountsAsProjectileDamage); });
    }

    [Fact]
    public void Raw_profiles_and_all_secondary_recipes_default_to_unqualified()
    {
        Assert.False(Profile.CountsAsProjectileDamage);
        Assert.False(new ProjectileAreaRecipe("direct", 1).CountsAsProjectileDamage);
        Assert.False(new ProjectileTargetedRecipe("direct", 1, 1).CountsAsProjectileDamage);
        Assert.False(new ProjectilePulseRecipe("direct", 1, 1).CountsAsProjectileDamage);
        var world = new World(); world.Run(Profile);
        Assert.False(Assert.Single(world.Hits).CountsAsProjectileDamage);
    }
}
