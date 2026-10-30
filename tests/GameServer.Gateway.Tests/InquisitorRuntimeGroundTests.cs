using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed partial class AscendancyGatewayIntegrationTests
{
    static AscendancyCatalog.Definition OrdinaryInquisitor() =>
        Program.Ascendancies.Find(5, AscendancyFamily.Normal, "Inquisitor")!;

    static GatewayCharacter InquisitorWith(params ushort[] ends) => new(nodes:
        ends.SelectMany(id => PathTo(OrdinaryInquisitor(), id)).Distinct().ToArray(),
        classRow: 6, baseClass: 5, choice: "Inquisitor", family: AscendancyFamily.Normal, level: 20);

    [Fact]
    public void Inquisitor_sanctuary_and_pious_apply_one_ground_and_one_regeneration_copy()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorWith(32816);
        var state = game.State;
        state.Clock.Stop();
        var raw = Program.Passives.Stats(state.MainAscendancy.Allocated);
        int Raw(string name) => Program.Stats.Key(name) is uint key ? raw.GetValueOrDefault(key) : 0;
        var first = Program.CharacterPlayerStats(state);
        var second = Program.CharacterPlayerStats(state);
        int Value(IReadOnlyDictionary<uint, int> values, string name) => values.GetValueOrDefault(Program.Stats.Key(name)!.Value);
        int percent = Raw("life_regeneration_rate_per_minute_%") + 300;
        int expectedMirror = (int)(Raw("base_life_regeneration_rate_per_minute") + state.Pools.Life * percent / 100d);
        Assert.Equal(percent, Value(first, "life_regeneration_rate_per_minute_%"));
        Assert.Equal(Raw("energy_shield_recovery_per_minute") + expectedMirror,
            Value(first, "energy_shield_recovery_per_minute"));
        Assert.Equal(1, Value(first, "on_consecrated_ground"));
        Assert.Equal(first.OrderBy(kv => kv.Key), second.OrderBy(kv => kv.Key));
    }

    [Fact]
    public async Task Inquisitor_paid_pious_refund_removes_es_copy_and_keeps_sanctuary_regeneration()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorWith(32816);
        var before = Program.CharacterPlayerStats(game.State);
        uint es = Program.Stats.Key("energy_shield_recovery_per_minute")!.Value;
        Assert.True(before.GetValueOrDefault(es) > 0);
        var refunded = await game.Change([32816], allocate: false, refundPoints: true);
        Assert.Null(refunded.Error);
        Assert.Equal((ushort)15, game.State.RefundPoints);
        Assert.Contains((ushort)39790, game.State.MainAscendancy.Allocated);
        var after = Program.CharacterPlayerStats(game.State);
        Assert.Equal(0, after.GetValueOrDefault(es));
        Assert.Equal(1, after.GetValueOrDefault(Program.Stats.Key("on_consecrated_ground")!.Value));
        var reloaded = game.Reload();
        Assert.Equal(0, Program.CharacterPlayerStats(reloaded).GetValueOrDefault(es));
    }

    [Theory]
    [InlineData(false, 101)]
    [InlineData(true, 61)]
    public void Inquisitor_target_increases_add_to_existing_damage_taken_before_separate_spell_less(bool spellLess, int expected)
    {
        if (!DataAvailable()) return;
        using var game = InquisitorWith(39790, 40059);
        var state = game.State;
        state.Clock.Stop();
        var at = state.Walk.Position(state.Clock.Elapsed.TotalSeconds);
        var effects = new Dictionary<string, int> { ["base_damage_taken_+%"] = -30 };
        if (spellLess) effects["rare_monster_mod_spell_damage_taken_+%_final"] = -40;
        var target = new SpawnedMonster("Inquisitor target", new WalkEstimator(at.X, at.Y, 37), 0, 1, 1000)
        { Mods = new([], [], new(effects)) };
        state.Monsters[0x7f001] = target;
        int damage = Program.MonsterDamage(target, DamagePacket.Of(DamageType.Fire, 100, 100),
            critical: false, attack: false, mods: default, owner: state).Damage;
        Assert.Equal(expected, damage);
    }

    [Fact]
    public void Inquisitor_augury_does_not_affect_friendly_or_dead_monsters()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorWith(40059);
        var at = game.State.Walk.Position(game.State.Clock.Elapsed.TotalSeconds);
        var dead = new SpawnedMonster("Dead target", new WalkEstimator(at.X, at.Y, 37), 0, 1, 100)
            { Dead = true, Life = 0 };
        var friendly = new SpawnedMonster("Friendly target", new WalkEstimator(at.X, at.Y, 37), 0, 1, 100)
            { Hostile = false };
        var hit = DamagePacket.Of(DamageType.Fire, 100, 100);
        Assert.Equal(hit.Components, Program.InquisitorTargetDamage(game.State, dead, hit).Components);
        Assert.Equal(hit.Components, Program.InquisitorTargetDamage(game.State, friendly, hit).Components);
        Assert.Equal(hit.Components, Program.InquisitorEnemyDamage(game.State, dead, hit).Components);
    }
}
