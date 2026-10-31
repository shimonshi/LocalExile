using GameServer.Core.Projectiles;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed partial class AscendancyGatewayIntegrationTests
{
    static readonly ushort[][] DeadeyeBuilds =
    [
        [5082, 61627, 56134, 26067, 28995, 45313, 53086, 5443],
        [59837, 24848, 22852, 23169, 55985, 44482, 1729, 2872],
        [28995, 45313, 64028, 26067, 62136, 2872, 5082, 61627],
    ];

    static int DeadeyeStat(ClientState state, string name) =>
        Program.Stats.Key(name) is uint key ? Program.CharacterPlayerStats(state).GetValueOrDefault(key) : 0;

    static DeadeyeProjectileDamage DeadeyeInputs(ClientState state)
    {
        var stats = Program.CharacterPlayerStats(state);
        return DeadeyeProjectileDamage.Capture(s => stats.GetValueOrDefault(Program.Stats.Key(s)!.Value));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task Ordinary_Deadeye_connected_build_paid_refund_reload_and_reallocation(int index)
    {
        // These fixtures require public extracted data, and never report a missing-data early return as a pass.
        Assert.True(DataAvailable());
        using var game = new GatewayCharacter(classRow: 3, baseClass: 2, choice: "Deadeye",
            family: AscendancyFamily.Normal, refundPoints: 100, level: 60);
        var build = DeadeyeBuilds[index];
        Assert.Null((await game.Change(build, true)).Error);
        var definition = Program.Ascendancies.Find(game.State.MainAscendancy.Selection!)!;
        Assert.Equal(AscendancyFamily.Normal, definition.Identity.Family);
        Assert.Equal((byte)2, definition.ClassNo);
        Assert.Equal((ushort)56856, definition.StartNode);
        var packet = PassivePacket(game.LastBytes, 8);
        Assert.Equal((byte)0x82, packet.ClassByte);
        Assert.Equal((ushort)8, packet.AscendancyPoints);
        Assert.Equal(build.Order(), packet.Nodes);
        Assert.Empty(game.State.Passives);
        var before = DeadeyeInputs(game.State);
        Assert.Equal(index == 1 ? 0 : 30, before.MaximumMorePercent);
        Assert.Equal(40, DeadeyeStat(game.State, "projectile_damage_+%"));
        var loaded = game.Reload();
        Assert.Equal(build.Order(), loaded.MainAscendancy.Allocated.Order());
        Assert.Equal(before, DeadeyeInputs(loaded));
        Assert.Null((await game.Change(build.Reverse().ToArray(), false, refundPoints: true)).Error);
        Assert.Equal((ushort)60, game.State.RefundPoints);
        Assert.Empty(game.State.MainAscendancy.Allocated);
        Assert.Equal(default, DeadeyeInputs(game.State));
        Assert.Equal(0, DeadeyeStat(game.State, "projectile_damage_+%"));
        Assert.Equal(default, DeadeyeInputs(game.Reload()));
        Assert.Null((await game.Change(build, true)).Error);
        Assert.Equal(before, DeadeyeInputs(game.State));
        Assert.Equal(before, DeadeyeInputs(game.Reload()));
        Assert.Equal((ushort)60, game.Reload().RefundPoints);
    }

    [Fact]
    public async Task Endless_Munitions_quantity_and_Ricochet_chain_reach_existing_release_and_real_gateway_contacts()
    {
        Assert.True(DataAvailable());
        using var game = new GatewayCharacter(classRow: 3, baseClass: 2, choice: "Deadeye", family: AscendancyFamily.Normal);
        Assert.Null((await game.Change(DeadeyeBuilds[0], true)).Error);
        var stats = Program.CharacterPlayerStats(game.State);
        int Get(string name) => Program.Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
        var profile = GatewayProjectileDefaults.Resolve("Metadata/Projectiles/DefaultArrow", Get, 10, attack: true);
        Assert.Equal(3, profile.Count); Assert.Equal(1, profile.Chain);
        await using var wire = new ProjectileRuntimeFixture(game.State);
        wire.Release(wire.Shot(profile));
        Assert.Equal(3, wire.Session.Runtime.Projectiles.Count);
        wire.Advance(20);
        game.State.Monsters[2] = ProjectileRuntimeFixture.Monster(30, 10);
        game.State.Monsters[3] = ProjectileRuntimeFixture.Monster(30, 35);
        // Isolate the real +1 chain consumer from volley admission; quantity was checked at actual emission above.
        wire.Release(wire.Shot(profile with { Count = 1, Radius = 1 },
            new(DamagePacket.Of(DamageType.Chaos, 10, 10), false, 0, 0, 150, "Deadeye existing chain")
                { AlwaysHits = true }));
        wire.Advance(28);
        Assert.Equal(90u, game.State.Monsters[2].Life);
        Assert.Equal(90u, game.State.Monsters[3].Life);
    }

    [Fact]
    public async Task Ordinary_Deadeye_failed_paid_refund_keeps_save_and_distance_inputs_unchanged()
    {
        Assert.True(DataAvailable());
        using var game = new GatewayCharacter(classRow: 3, baseClass: 2, choice: "Deadeye",
            family: AscendancyFamily.Normal, refundPoints: 0);
        Assert.Null((await game.Change([28995, 45313], true)).Error);
        string saved = game.SavedJson;
        var release = DeadeyeInputs(game.State);
        Assert.NotNull((await game.Change([45313], false, refundPoints: true)).Error);
        Assert.Equal(saved, game.SavedJson);
        Assert.Equal(release, DeadeyeInputs(game.State));
        Assert.Equal(release, DeadeyeInputs(game.Reload()));
        Assert.Equal(1.3, release.HitMultiplier(70, attack: false));
        Assert.Equal((ushort)0, game.State.RefundPoints);
    }

    [Fact]
    public async Task Deadeye_existing_quantity_consumer_keeps_nonprimary_counts_and_chain_prohibitions()
    {
        Assert.True(DataAvailable());
        using var game = new GatewayCharacter(classRow: 3, baseClass: 2, choice: "Deadeye", family: AscendancyFamily.Normal);
        Assert.Null((await game.Change(DeadeyeBuilds[0], true)).Error);
        var stats = Program.CharacterPlayerStats(game.State);
        int Get(string name) => name == "virtual_projectiles_cannot_chain" ? 1
            : Program.Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
        var profile = GatewayProjectileModifiers.Resolve(new("projectile-contact-areas")
            { Count = 1, CountConsumer = ProjectileCountConsumer.ContactAreas, ContactArea = new("areas", 5, Count: 4) },
            Get, attack: false);
        Assert.Equal(1, profile.Count);
        Assert.Equal(6, profile.ContactArea!.Count);
        Assert.True(profile.CannotChain);
        Assert.Equal(1, profile.Chain); // The prohibition, not destructive rewriting of the grant, wins at runtime.
    }

    [Fact]
    public async Task Small_node_damage_speed_accuracy_and_charge_generation_use_existing_production_consumers()
    {
        Assert.True(DataAvailable());
        using var game = new GatewayCharacter(classRow: 3, baseClass: 2, choice: "Deadeye", family: AscendancyFamily.Normal);
        int initialAccuracy = game.State.Pools.Accuracy;
        Assert.Null((await game.Change([5082, 61627, 56134, 26067, 28995, 45313, 55985, 44482], true)).Error);
        var stats = Program.CharacterPlayerStats(game.State);
        int Get(string name) => stats.GetValueOrDefault(Program.Stats.Key(name)!.Value);
        Assert.Equal(initialAccuracy * 130 / 100, game.State.Pools.Accuracy);
        Assert.Equal(10, Get("projectile_speed_+%"));
        Assert.Equal(253, ProjectileSpeed.CellsPerSecond(2500, Get, _ => 0), 3);
        var weapon = new SkillCatalog.Unarmed(1000, 100, 100, 120) { Melee = false };
        // Physical damage also has Strength's ordinary increase; isolate the projectile bucket with a chaos weapon.
        var chaos = weapon with { LocalDamage = DamagePacket.Of(DamageType.Chaos, 100, 100) };
        var shot = OrdinaryRangedShot.Capture(chaos, stats, [], Program.Stats, game.State.Pools.Accuracy, 0, 150);
        Assert.Equal(140, shot.Hit[DamageType.Chaos].Min);
        var charges = ProgramValue<ChargeSession>("ChargesOf", game.State);
        Assert.Equal(4, charges.Limits(ChargeType.Frenzy).Maximum);
        var rng = new Random(1);
        charges.Step(0, Get, () => new(), true, true, false, rng);
        charges.Step(1, Get, () => new(), true, true, false, rng);
        Assert.Equal(1, charges.Effective(ChargeType.Frenzy));
        charges.Step(2, Get, () => new(), true, false, false, rng);
        Assert.Equal(1, charges.Effective(ChargeType.Frenzy));
        charges.Step(3, Get, () => new(), true, true, false, rng);
        Assert.Equal(2, charges.Effective(ChargeType.Frenzy));
        // Avidity's distinct more-accuracy stat has no consumer on this base; this test does not claim one.
        Assert.Equal(5, Get("deadeye_accuracy_rating_+%_final_per_frenzy_charge"));
    }
}
