using GameServer.Core.Projectiles;

namespace GameServer.Core.Tests;

public sealed class DeadeyeGaleProjectileRuntimeTests
{
    sealed class World : IProjectileWorld
    {
        public readonly List<ProjectileActor> Targets = [];
        public readonly List<ProjectileDamage> Hits = [];
        public readonly List<(ProjectileEventKind Kind, long At, ProjectileVector Position, long Emission)> Events = [];
        public Func<ProjectileEntity, ProjectileActor, int> Split = (_, _) => 0;
        public Func<ProjectileDamage, ProjectileHitResult> Accept = _ => ProjectileHitResult.Hit;
        public IReadOnlyList<ProjectileActor> Actors(long from, long until) => Targets;
        public TerrainContact? TraceTerrain(ProjectileVector from, ProjectileVector to) =>
            from.X < 20 && to.X >= 20 ? new((20 - from.X) / (to.X - from.X), new(-1, 0)) : null;
        public ProjectileHitResult Hit(ProjectileDamage damage)
        { var result = Accept(damage); if (result == ProjectileHitResult.Hit) Hits.Add(damage); return result; }
        public int TargetSplitCount(ProjectileEntity p, ProjectileActor actor) => Split(p, actor);
        public void Publish(ProjectileEvent e) => Events.Add((e.Kind, e.At, e.Position, e.Projectile.EmissionId));
        public void Target(uint id, double x, double y, int generation = 0, int team = 2) =>
            Targets.Add(new(new(id, generation), team, _ => new(x, y), .5));
    }
    static ProjectileProfile Arrow => new("arrow") { Speed = 100, Range = 100, Radius = .1, ChainRange = 30,
        Chain = 1, TerrainChainChancePercent = 100 };
    static ProjectileUse Use(ProjectileProfile profile, int seed = 1) =>
        new(profile, new(1), new(1), 1, new(0, 0), new(100, 0)) { Seed = seed };

    [Theory]
    [InlineData(0, false, 1, 0)] [InlineData(100, false, 1, 1)]
    [InlineData(100, true, 1, 0)] [InlineData(100, false, 0, 0)]
    public void Terrain_chance_and_chain_prohibitions_control_actual_wall_to_enemy_contact(int chance, bool cannot,
        int chain, int hits)
    {
        var world = new World(); world.Target(2, 10, 10);
        var runtime = new ProjectileRuntime(world);
        runtime.Release(Use(Arrow with { TerrainChainChancePercent = chance, CannotChain = cannot, Chain = chain }));
        runtime.AdvanceTo(ProjectileRuntime.Time(2));
        Assert.Equal(hits, world.Hits.Count);
        if (hits > 0) { Assert.Equal(2u, world.Hits[0].Target.Id); Assert.True(world.Hits[0].Projectile.Distance > 30); }
    }

    [Fact]
    public void Chance_30_is_repeatable_and_does_not_consume_combat_reward_or_reaction_random_streams()
    {
        var outcomes = new List<int>();
        for (int seed = 1; seed <= 64; seed++)
        {
            int Run(int chance)
            {
                var w = new World(); w.Target(2, 10, 10);
                var r = new ProjectileRuntime(w); r.Release(Use(Arrow with { TerrainChainChancePercent = chance }, seed));
                var p = Assert.Single(r.Projectiles);
                var expectedDamage = new Random(unchecked(seed * 397 ^ (int)p.Id) ^ 0x2d53a1b7);
                var expectedReward = new Random(unchecked(seed * 397 ^ (int)p.Id) ^ 0x5e418ac3);
                var expectedReaction = new Random(unchecked(seed * 397 ^ (int)p.Id) ^ 0x1a736d49);
                r.AdvanceTo(ProjectileRuntime.Time(2));
                Assert.Equal(expectedDamage.Next(), p.RandomSource.Next());
                Assert.Equal(expectedReward.Next(), p.RewardRandomSource.Next());
                Assert.Equal(expectedReaction.Next(), p.ReactionRandomSource.Next());
                return w.Hits.Count;
            }
            int outcome = Run(30); Assert.Equal(outcome, Run(30)); outcomes.Add(outcome);
        }
        Assert.Contains(0, outcomes); Assert.Contains(1, outcomes);
        Assert.InRange(outcomes.Sum(), 8, 32);
    }

    [Fact]
    public void Successful_terrain_chain_precedes_terminal_area_return_and_emission_and_preserves_chain_budget()
    {
        var w = new World(); w.Target(2, 10, 10); w.Target(3, 0, 10); w.Target(1, 0, 0, team: 1);
        var r = new ProjectileRuntime(w);
        r.Release(Use(Arrow with { Returns = true, EndArea = new("terminal", 0),
            EndEmission = new(new("child") { DirectDamage = false }) }) with { ReturnAnchor = new(1) });
        r.AdvanceTo(ProjectileRuntime.Time(.21));
        Assert.Contains(w.Events, e => e.Kind == ProjectileEventKind.Redirect);
        Assert.DoesNotContain(w.Events, e => e.Kind is ProjectileEventKind.Area or ProjectileEventKind.End);
        r.AdvanceTo(ProjectileRuntime.Time(.5));
        Assert.Contains(w.Hits, h => h.Target.Id == 2);
        // Terrain consumes the remaining chain, so enemy 3 is not a second enemy-chain target.
        Assert.DoesNotContain(w.Hits, h => h.Target.Id == 3);
    }

    [Fact]
    public void Target_selection_excludes_seen_actors_and_inaccessible_or_out_of_range_enemies()
    {
        var w = new World(); w.Target(2, 10, 0); w.Target(3, 25, 5); w.Target(4, 10, 12); w.Target(5, -50, 0);
        var r = new ProjectileRuntime(w);
        r.Release(Use(Arrow with { Pierce = 1 }));
        r.AdvanceTo(ProjectileRuntime.Time(2));
        Assert.Equal(new uint[] { 2, 4 }, w.Hits.Select(h => h.Target.Id));
    }

    [Fact]
    public void Short_remaining_range_cannot_chain_to_an_unreachable_enemy()
    {
        var w = new World(); w.Target(2, 10, 10);
        var r = new ProjectileRuntime(w); r.Release(Use(Arrow with { Range = 21 }));
        r.AdvanceTo(ProjectileRuntime.Time(1));
        Assert.Empty(w.Hits); Assert.DoesNotContain(w.Events, e => e.Kind == ProjectileEventKind.Redirect);
    }

    [Theory]
    [InlineData(false, false, 3)] [InlineData(true, false, 1)] [InlineData(false, true, 0)]
    public void Target_split_query_uses_existing_continuation_before_terminal_substitution(bool cannot, bool blocked,
        int expectedHits)
    {
        var w = new World { Split = (_, target) => target.Key.Id == 2 ? 2 : 0,
            Accept = _ => blocked ? ProjectileHitResult.Blocked : ProjectileHitResult.Hit };
        w.Target(2, 5, 0); w.Target(3, 10, 5); w.Target(4, 10, -5);
        var r = new ProjectileRuntime(w);
        r.Release(Use(Arrow with { Chain = 0, CannotSplit = cannot, EndArea = new("terminal", 0) }));
        r.AdvanceTo(ProjectileRuntime.Time(1));
        Assert.Equal(expectedHits, w.Hits.Count);
        if (!cannot && !blocked) Assert.Equal("direct", w.Hits[0].Stage);
    }

    [Fact]
    public void Barrage_preserves_sequential_spawn_times_and_separate_hit_admission_for_one_use()
    {
        var w = new World(); w.Target(2, 10, 0);
        var r = new ProjectileRuntime(w);
        long use = r.Release(Use(Arrow with { Barrage = true, Count = 3, SpreadRadians = 0,
            SequentialDelay = ProjectileRuntime.Time(.1), TerrainChainChancePercent = 0, Chain = 0 }));
        Assert.Single(r.Projectiles);
        r.AdvanceTo(ProjectileRuntime.Time(1));
        Assert.Equal(3, w.Hits.Count); Assert.All(w.Hits, h => Assert.Equal(use, h.Projectile.UseId));
        Assert.Equal(new long[] { 0, 12000, 24000 }, w.Events.Where(e => e.Kind == ProjectileEventKind.Spawn).Select(e => e.At));
        Assert.Equal(3, w.Hits.Select(h => h.Projectile.EmissionId).Distinct().Count());
    }
}
