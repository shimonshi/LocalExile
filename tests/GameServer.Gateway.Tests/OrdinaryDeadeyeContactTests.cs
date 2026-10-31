using GameServer.Core.Projectiles;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public partial class ConditionalCriticalGatewayTests
{
    static void Deadeye(Contact f)
    {
        var definition = Program.Ascendancies.Find(2, AscendancyFamily.Normal, "Deadeye")!;
        Assert.NotNull(definition);
        f.Conn.MainAscendancy = new(definition.Identity, [28995, 45313], AscendancyRules.AwardKeys);
        new AscendancyRules(Program.Ascendancies).Validate(2, f.Conn.MainAscendancy);
        f.Set("global_cannot_crit", 1);
        f.Set("global_always_hit", 1);
        f.Set("physical_damage_+%", -10000);
        f.Set("elemental_damage_+%", -10000);
        f.Refresh();
    }

    static ProjectileUse CaptureDeadeyeGem(Contact f, Program.ProjectileSession session)
    {
        var gems = (IEnumerable<(ushort Id, int Effect, string Name, int Level, int Quality, uint Gem)>)
            Method("GemSkillEffects").Invoke(null, [f.Conn])!;
        return (ProjectileUse)Method("CaptureGemProjectile").Invoke(null,
            [f.Conn, session, f.Skill, gems.Single(g => g.Id == f.Skill), (180, 32), new SkillCriticalRoll(0)])!;
    }

    // Only geometry is controlled here. Damage, accuracy, crit, leech, ailment and provenance use production snapshots.
    static ProjectileUse AtDistance(ProjectileUse use, double distance, ProjectileProfile? profile = null) => use with
    {
        Profile = (profile ?? use.Profile) with { Count = 1, Speed = 100, Range = 180, Radius = 1 },
        Origin = new(179 - distance, 32), Aim = new(190, 32)
    };

    [Theory]
    [InlineData(null, "Crude Bow")]
    [InlineData(null, "Driftwood Wand")]
    [InlineData("BurningArrow", "Crude Bow")]
    [InlineData("Fireball", null)]
    public async Task Deadeye_parsed_release_installs_frozen_distance_on_ordinary_and_supported_hits(string? gem, string? weapon)
    {
        using var f = new Contact(new Draws(), gem, weapon, distance: 100, classRow: 3);
        Deadeye(f);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        Task action;
        ProjectileEntity flight;
        using (Program.ProjectileMutation(f.Conn, advance: false))
        {
            action = f.Use();
            f.Conn.Clock.Stop();
            long deadline = runtime.Session.Runtime.Now + ProjectileRuntime.Time(2);
            while (runtime.Session.Runtime.Projectiles.Count == 0 && runtime.Session.Runtime.Now < deadline)
                runtime.Session.AdvanceTo(runtime.Session.Runtime.Now + ProjectileRuntime.TickQuanta / 20);
            flight = Assert.Single(runtime.Session.Runtime.Projectiles);
            var shot = Assert.IsType<ProjectileOffense>(flight.Use.Payload);
            Assert.Equal(new DeadeyeProjectileDamage(30, false), shot.DistanceDamage);
            Assert.True(flight.Profile.CountsAsProjectileDamage);
            Assert.Equal(f.Target.MaxLife, f.Target.Life);
            double basis = (flight.Profile.EndArea is not null ? shot.AreaHit! : shot.Hit).MinTotal;
            Assert.True(basis > 0);
            runtime.Session.AdvanceTo(runtime.Session.Runtime.Now + ProjectileRuntime.Time(3));
            Assert.Equal((uint)Math.Floor(basis * 1.3 + 1e-9), f.Target.MaxLife - f.Target.Life);
            Assert.Equal(30, shot.DistanceDamage.MaximumMorePercent);
        }
        await action;
    }

    [Theory]
    [InlineData("BurningArrow", "Crude Bow", 35, 1)]
    [InlineData("BurningArrow", "Crude Bow", 52.5, 1.15)]
    [InlineData("BurningArrow", "Crude Bow", 70, 1.3)]
    [InlineData("Fireball", null, 35, 1)]
    [InlineData("Fireball", null, 52.5, 1.15)]
    [InlineData("Fireball", null, 70, 1.3)]
    public async Task Deadeye_production_snapshots_reach_exact_contact_bounds(string gem, string? weapon, double distance, double factor)
    {
        using var f = new Contact(new Draws(), gem, weapon, distance: 148, classRow: 3);
        Deadeye(f);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var use = AtDistance(CaptureDeadeyeGem(f, runtime.Session), distance);
        var shot = (ProjectileOffense)use.Payload!;
        Assert.Equal(shot.Hit.MinTotal, shot.Hit.MaxTotal);
        runtime.Release(use); runtime.Advance(2);
        Assert.Equal((uint)Math.Floor(shot.Hit.MinTotal * factor + 1e-9), f.Target.MaxLife - f.Target.Life);
        Assert.Equal(shot.Hit.MinTotal, shot.Hit.MaxTotal); // No saved basis mutation.
    }

    [Theory]
    [InlineData("BurningArrow", "Crude Bow", true)]
    [InlineData("Fireball", null, false)]
    public async Task Deadeye_Point_Blank_and_legacy_Far_Shot_compose_only_on_attack_hits(string gem, string? weapon, bool attack)
    {
        using var f = new Contact(new Draws(), gem, weapon, distance: 148, classRow: 3);
        Deadeye(f); f.Set("player_far_shot", 1); f.Set(DeadeyeProjectileDamage.PointBlankStat, 1);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var use = AtDistance(CaptureDeadeyeGem(f, runtime.Session), 70);
        var shot = (ProjectileOffense)use.Payload!;
        runtime.Release(use); runtime.Advance(2);
        double factor = 1.3 * (attack ? (1 - .3 * 35 / 85) * AntiquarianEffects.FarShot(70) : 1);
        Assert.Equal((uint)Math.Floor(shot.Hit.MinTotal * factor + 1e-9), f.Target.MaxLife - f.Target.Life);
    }

    [Theory]
    [InlineData(false, false, false)] [InlineData(true, false, false)]
    [InlineData(false, true, false)] [InlineData(true, true, false)]
    [InlineData(false, false, true)] [InlineData(true, false, true)]
    public async Task Deadeye_accepted_contact_scales_independent_ignite_ranges_once_and_preserves_admission(
        bool critical, bool immune, bool spell)
    {
        using var f = new Contact(critical ? new Draws(0) : new Draws(), spell ? "Fireball" : "BurningArrow", "Crude Bow",
            distance: 148, classRow: 3, targetMods: new([], [], new(new Dictionary<string, int>
                { ["immune_to_ignite"] = immune ? 1 : 0 })));
        Deadeye(f);
        f.Set("global_cannot_crit", critical ? 0 : 1);
        f.Set("extra_critical_rolls_while_on_low_life", 0);
        f.Set(DeadeyeProjectileDamage.PointBlankStat, 1);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var captured = CaptureDeadeyeGem(f, runtime.Session);
        var shot = (ProjectileOffense)captured.Payload!;
        // Distinct frozen ranges make critical selection and independent scaling observable in the real ignite engine.
        var settings = new MonsterIgnites.Settings(100, 100, 200, 300, 500, 4);
        f.Conn.IgniteRandom = new IgniteDraw(.25);
        var use = AtDistance(captured with { Payload = shot with
            { Hit = DamagePacket.Of(DamageType.Fire, 100, 100), AreaHit = null, Ignite = settings,
                CriticalChance = critical ? 10000 : 0 } }, 52.5);
        runtime.Release(use); runtime.Advance(.53);
        Assert.Equal(!immune, f.Target.Ignites.Burning);
        if (!immune)
        {
            Assert.Equal(4.525, f.Target.Ignites.Until, 6);
            Assert.Equal((int)Math.Floor((critical ? 350 : 125) * 1.15 + 1e-9),
                f.Target.Ignites.Advance(1.525, 1));
        }
        Assert.Equal(settings, ((ProjectileOffense)use.Payload!).Ignite);
    }

    sealed class IgniteDraw(double value) : Random
    {
        public override double NextDouble() => value;
        public override int Next(int maxValue) => 0;
    }

    [Theory]
    [InlineData("pierce")] [InlineData("chain")] [InlineData("return")]
    [InlineData("fork")] [InlineData("split")]
    public async Task Deadeye_production_offense_keeps_cumulative_travel_without_repeated_scaling(string continuation)
    {
        using var f = new Contact(new Draws(), "BurningArrow", "Crude Bow", distance: 28, classRow: 3);
        Deadeye(f);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var captured = CaptureDeadeyeGem(f, runtime.Session);
        var shot = (ProjectileOffense)captured.Payload!;
        var profile = captured.Profile with { Count = 1, Speed = 10, Radius = 1, Range = 80,
            Lifetime = ProjectileRuntime.Time(30),
            Pierce = continuation is "pierce" or "return" ? -1 : 0, Chain = continuation == "chain" ? 1 : 0,
            Fork = continuation == "fork" ? 1 : 0, Split = continuation == "split" ? 1 : 0,
            Returns = continuation == "return", ChainRange = 100 };
        var origin = new ProjectileVector(40, 32);
        var use = captured with { Profile = profile, Origin = origin, Aim = new(180, 32) };
        if (continuation != "return")
        {
            int x = continuation == "fork" ? 105 : 110;
            int y = continuation == "fork" ? 58 : continuation is "split" or "chain" ? 52 : 32;
            f.Conn.Monsters[43] = new("second", new(x, y, 30), 0, 2, 100000);
        }
        runtime.Release(use); runtime.Advance(20);
        uint first = (uint)Math.Floor(shot.Hit.MinTotal + 1e-9);
        if (continuation == "return")
            Assert.Equal(first + (uint)Math.Floor(shot.Hit.MinTotal * 1.3 + 1e-9), f.Target.MaxLife - f.Target.Life);
        else
        {
            Assert.Equal(first, f.Target.MaxLife - f.Target.Life);
            var second = f.Conn.Monsters[43];
            double distance = continuation == "fork" ? 20 + 52 : continuation is "chain" or "split"
                ? 19 + Math.Sqrt(51 * 51 + 20 * 20) - 1 : 69;
            double factor = 1 + .3 * Math.Clamp((distance - 35) / 35, 0, 1);
            Assert.Equal((uint)Math.Floor(shot.Hit.MinTotal * factor + 1e-9), second.MaxLife - second.Life);
        }
    }

    [Theory]
    [InlineData("area")] [InlineData("targeted")] [InlineData("pulse")]
    [InlineData("end-child")] [InlineData("travel-child")]
    public async Task Deadeye_unqualified_secondary_recipes_and_emission_children_stay_neutral(string stage)
    {
        using var f = new Contact(new Draws(), "BurningArrow", "Crude Bow", distance: 148, classRow: 3);
        Deadeye(f); f.Set(DeadeyeProjectileDamage.PointBlankStat, 1);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var captured = CaptureDeadeyeGem(f, runtime.Session);
        var shot = (ProjectileOffense)captured.Payload!;
        var root = captured.Profile with { Count = 1, Speed = 100, Radius = 0, Range = 70 };
        var child = new ProjectileProfile(root.Id) { Speed = 100, Range = 100, Radius = 0 };
        var emission = new ProjectileEmissionRecipe(child, Interval: ProjectileRuntime.Time(.7));
        var profile = stage switch
        {
            "area" => root with { EndArea = new("direct", 2) }, // Terminal replacement must use the area's false grant.
            "targeted" => root with { ContactTargets = new("direct", 1, 10) },
            "pulse" => root with { DirectDamage = false, Range = 71, Pulse = new("direct", 2, ProjectileRuntime.Time(.7)) },
            "end-child" => root with { DirectDamage = false, EndEmission = emission },
            _ => root with { DirectDamage = false, TravelEmission = emission, Range = 71 }
        };
        if (stage == "targeted") f.Conn.Monsters[43] = new("trigger", new(180, 30, 30), 0, 2, 100000);
        var use = captured with { Profile = profile, Origin = new(110, stage == "targeted" ? 30 : 32), Aim = new(190, stage == "targeted" ? 30 : 32) };
        // Emissions travel over 70 of their own cells: a false grant matters even with a far child contact.
        if (stage.Contains("child")) use = use with { Origin = new(30, 32) };
        runtime.Release(use); runtime.Advance(1.6);
        Assert.Equal((uint)Math.Floor(shot.Hit.MinTotal + 1e-9), f.Target.MaxLife - f.Target.Life);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Deadeye_terminal_and_delayed_area_use_the_triggering_frozen_distance(bool delayed)
    {
        using var f = new Contact(new Draws(), "Fireball", distance: 148, classRow: 3);
        Deadeye(f);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var captured = CaptureDeadeyeGem(f, runtime.Session);
        var shot = (ProjectileOffense)captured.Payload!;
        double continuingDistance = 0;
        if (delayed)
            runtime.Session.Runtime.Schedule(ProjectileRuntime.Time(.72), () =>
                continuingDistance = Assert.Single(runtime.Session.Runtime.Projectiles).Distance);
        var area = captured.Profile.EndArea! with { Radius = 3, Delay = delayed ? ProjectileRuntime.Time(1) : 0,
            ExcludeContactTarget = delayed };
        var profile = captured.Profile with { Count = 1, Speed = 100, Radius = 1, Range = delayed ? 180 : 52.5,
            DirectDamage = delayed,
            EndArea = delayed ? null : area, ContactArea = delayed ? area : null, Pierce = delayed ? -1 : 0 };
        if (delayed) f.Conn.Monsters[43] = new("trigger", new(180, 30, 30), 0, 2, 100000);
        runtime.Release(captured with { Profile = profile, Origin = new(delayed ? 126.5 : 127.5, delayed ? 30 : 32), Aim = new(190, delayed ? 30 : 32) });
        runtime.Advance(1.6);
        Assert.Equal((uint)Math.Floor(shot.AreaHit!.MinTotal * 1.15 + 1e-9), f.Target.MaxLife - f.Target.Life);
        if (delayed) Assert.True(continuingDistance > 70); // The flight can end against terrain before the delayed blast.
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Deadeye_evasion_and_block_reject_before_distance_ignite_and_leech(bool block)
    {
        using var f = new Contact(new Draws(), "BurningArrow", "Crude Bow", distance: 148, classRow: 3,
            targetMods: new([], [], new(new Dictionary<string, int> { ["monster_base_block_%"] = block ? 100 : 0 })));
        Deadeye(f); f.Set("global_always_hit", 0); f.Set("life_leech_from_any_damage_permyriad", 10000);
        if (!block) { f.Conn.Pools = f.Conn.Pools with { Accuracy = 0 }; f.Conn.AttackAccuracyRandom = new AccuracyDraws(0); }
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var use = AtDistance(CaptureDeadeyeGem(f, runtime.Session), 70);
        runtime.Release(use); runtime.Advance(1);
        Assert.Equal(f.Target.MaxLife, f.Target.Life);
        Assert.False(f.Target.Ignites.Burning);
        Assert.Equal(0, Program.LeechOf(f.Conn).Life.Count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Deadeye_distance_precedes_Slayer_direct_overkill_and_cull_but_not_reflected_Impale(bool cull)
    {
        using var f = new Contact(new Draws(), "BurningArrow", "Crude Bow", distance: 148, classRow: 4);
        Slayer(f, 3184, 34484);
        f.Set(DeadeyeProjectileDamage.FarShot, 30); // Source-neutral combined control; no impossible second class selection.
        f.Set("physical_damage_+%", -10000); f.Set("elemental_damage_+%", -10000);
        f.Set("global_always_hit", 1); f.Set("leech_%_is_instant", 100);
        f.Conn.Pools = f.Conn.Pools with { Life = 10000 };
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var use = AtDistance(CaptureDeadeyeGem(f, runtime.Session), 70);
        var shot = (ProjectileOffense)use.Payload!;
        int direct = (int)Math.Floor(shot.Hit.MinTotal * 1.3 + 1e-9);
        f.Target.Life = (uint)(cull ? direct + 1 : direct - 100);
        if (!cull) f.Target.Impale.Apply(1000, 0); // Reflected 100 must never add distance-scaled overkill.
        runtime.Release(use); runtime.Advance(1);
        Assert.True(f.Target.Dead, $"direct={direct}, remaining={f.Target.Life}, shot={shot.Hit.MinTotal}");
        uint ordinary = (uint)Math.Floor(shot.Leech.Amounts(shot.Hit.Scale(1.3), direct).Life);
        Assert.Equal(50u + ordinary + (cull ? 0u : 20u), f.Conn.PlayerLife);
        Assert.Equal(0, Program.LeechOf(f.Conn).Life.Count);
    }
}
