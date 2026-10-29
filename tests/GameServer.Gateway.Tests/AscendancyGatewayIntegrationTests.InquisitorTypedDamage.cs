using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed partial class AscendancyGatewayIntegrationTests
{
    [Fact]
    public async Task Inquisitor_Augury_reduces_real_incoming_ignite_before_its_own_critical_multiplier_and_refund_removes_it()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        Assert.Null((await game.Change(InquisitorPath(40059), true)).Error);
        Assert.Equal(6, game.State.Pools.FireResistance); // The connected small node retains its real resistance.
        async Task Check(int distance, double expectedDps, int expectedDamage)
        {
            game.State.ElementalAilments.Clear();
            game.State.PlayerLife = game.State.PlayerMaxLife;
            await game.InquisitorIncomingIgnite(distance);
            double now = Program.ProjectileWorldTime(game.State);
            Assert.Equal(expectedDps, game.State.ElementalAilments.Magnitude(PlayerElementalAilments.Kind.Ignite, now), 8);
            uint before = game.State.PlayerLife;
            int damage = Program.AdvancePlayerAilmentsLocked(game.State, now, now + 1, Program.CharacterPlayerStats(game.State));
            Assert.Equal(expectedDamage, damage);
            Assert.Equal(before - (uint)expectedDamage, game.State.PlayerLife);
        }
        // Base100, ordinary ignite90%, monster critical ailment multiplier130%, and Augury92% once.
        await Check(2, 107.64, 101); // floor(107.64 * .94) after the actual six-percent fire resistance.
        await Check(100, 117, 109);
        Assert.Null((await game.Change([(ushort)40059], false, refundPoints: true)).Error);
        await Check(2, 117, 109);
    }

    sealed partial class GatewayCharacter
    {
        public Task<(string? Error, byte[] Bytes)> InquisitorIncomingIgnite(int distance) => Exchange(async (stream, cipher) =>
        {
            State.EvasionEntropy = 100;
            State.LastEvasionAt = State.Clock.Elapsed.TotalSeconds;
            var at = State.Walk.Position(State.Clock.Elapsed.TotalSeconds);
            var monster = new SpawnedMonster("Augury incoming ignite", new(at.X + distance, at.Y, 30), 0, 2, 1000)
            {
                Attack = new(SkillCatalog.FirstSkillId, .002, 200, DamagePacket.Of(DamageType.Fire, 100, 100)),
                Mods = new([], [], new(new Dictionary<string, int> { ["always_crit"] = 1 })),
            };
            State.Monsters[0x7f003] = monster;
            await ProgramValue<Task>("MonsterSwing", "inquisitor-ignite-integration", stream, cipher, State,
                0x7f003u, monster, (at.X + distance, at.Y), at, State.IncomingHitEpoch);
            return null;
        });
    }

    [Fact]
    public async Task Inquisitor_Sanctuary_applies_to_actual_reflected_impale_then_stops_outside_ground_or_after_refund()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        Assert.Null((await game.Change(InquisitorPath(39790), true)).Error);
        game.State.Clock.Stop();
        double now = Program.ProjectileWorldTime(game.State);
        var at = game.State.Walk.Position(now);
        var target = new SpawnedMonster("Sanctuary impale target", new(at.X, at.Y, 37), 0, 1, 1000)
        {
            Mods = new([], [], new(new Dictionary<string, int>
            {
                ["base_additional_physical_damage_reduction_%"] = 20,
                ["base_damage_taken_+%"] = -30,
            }))
        };
        game.State.Monsters[0x7f002] = target;
        Assert.True(target.Impale.Apply(1000, now: now)); // Existing stack reflects 100 on each later hit.
        (int Damage, double Reflected, bool Applied) Trigger() => ProgramValue<(int, double, bool)>(
            "WithImpaleHitLocked", game.State, target, 0, DamagePacket.Empty, false, 100, new ImpaleStats.Settings(0), 1d);
        var onGround = Trigger();
        Assert.Equal(68, onGround.Damage); // 100 * .8 * (1 - .30 + .15), including Sanctuary once.
        Assert.Equal(100, onGround.Reflected);
        Assert.False(onGround.Applied);
        target.Walk = new(at.X + 100, at.Y, 37);
        Assert.Equal(56, Trigger().Damage);
        target.Walk = new(at.X, at.Y, 37);
        Assert.Equal(68, Trigger().Damage);
        Assert.Null((await game.Change([(ushort)39790], false, refundPoints: true)).Error);
        Assert.Equal(56, Trigger().Damage);
    }

    [Fact]
    public async Task Inquisitor_Battlemage_copies_all_actual_equipped_local_types_with_quality_and_one_spell_effectiveness()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        EquipFamilyGem(game, FamilyGem("Fireball"));
        // Qualify and isolate spell scaling without replacing the weapon's local packet.
        foreach (string stat in new[] { "damage_+%", "spell_damage_+%", "physical_damage_+%", "elemental_damage_+%",
            "fire_damage_+%", "cold_damage_+%", "lightning_damage_+%", "chaos_damage_+%", "projectile_damage_+%" })
            InquisitorSet(game.State, stat, 0);
        var wand = ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!) with
        {
            Quality = 20,
            Mods =
            [
                ItemWeaponStatsTests.Mod(("local_physical_damage_+%", 100)),
                ItemWeaponStatsTests.Mod(("local_minimum_added_fire_damage", 1), ("local_maximum_added_fire_damage", 3)),
                ItemWeaponStatsTests.Mod(("local_minimum_added_cold_damage", 2), ("local_maximum_added_cold_damage", 4)),
                ItemWeaponStatsTests.Mod(("local_minimum_added_lightning_damage", 3), ("local_maximum_added_lightning_damage", 5)),
                ItemWeaponStatsTests.Mod(("local_minimum_added_chaos_damage", 4), ("local_maximum_added_chaos_damage", 6)),
                ItemWeaponStatsTests.Mod(("local_elemental_damage_+%", 50)),
            ]
        };
        game.Items.Add(PlayerInventories.Weapon1, 0, 0, wand);
        game.State.Pools = Program.PoolsFor(game.State, 60);
        ushort skill = Program.GemSkillIdPool.Value[0];
        var baseline = ProgramValue<DamagePacket>("SupportedHit", game.State, skill);
        Assert.Null((await game.Change(InquisitorPath(19417), true)).Error);
        var actual = ProgramValue<DamagePacket>("SupportedHit", game.State, skill);
        var local = new Dictionary<DamageType, DamageRange>
        {
            [DamageType.Physical] = new(12, 22), [DamageType.Fire] = new(2, 5),
            [DamageType.Cold] = new(3, 6), [DamageType.Lightning] = new(5, 8), [DamageType.Chaos] = new(4, 6),
        };
        foreach (var (type, range) in local)
        {
            Assert.Equal(range.Min * 2, actual[type].Min - baseline[type].Min, 8);
            Assert.Equal(range.Max * 2, actual[type].Max - baseline[type].Max, 8);
        }
        Assert.Null((await game.Change([(ushort)19417], false, refundPoints: true)).Error);
        Assert.Equal(baseline.Components, ProgramValue<DamagePacket>("SupportedHit", game.State, skill).Components);
    }
}
