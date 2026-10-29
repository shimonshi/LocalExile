using System.Text.Json;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed partial class AscendancyGatewayIntegrationTests
{
    static GatewayCharacter InquisitorGame(int level = 60)
    {
        using var characters = JsonDocument.Parse(File.ReadAllBytes("data/tables/Characters.json"));
        int row = characters.RootElement.EnumerateArray().Select((value, index) => (value, index))
            .Single(c => c.value.GetProperty("Name").GetString() == "Templar").index;
        return new GatewayCharacter(classRow: row, baseClass: 5, choice: "Inquisitor",
            family: AscendancyFamily.Normal, refundPoints: 100, level: level);
    }

    static ushort[] InquisitorPath(ushort node) => PathTo(
        Program.Ascendancies.Find(5, AscendancyFamily.Normal, "Inquisitor")!, node);

    static uint InquisitorKey(string name) => Program.Stats.Key(name)!.Value;
    static int InquisitorStat(ClientState state, string name) =>
        Program.CharacterPlayerStats(state).GetValueOrDefault(InquisitorKey(name));
    static void InquisitorSet(ClientState state, string name, int value) => state.PlayerStats[InquisitorKey(name)] = value;

    [Fact]
    public async Task Inquisitor_Providence_allocation_paid_refund_and_reload_keep_attributes_and_crit_consistent()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        var baseline = game.State.Pools;
        ushort[] path = InquisitorPath(53884);
        Assert.Null((await game.Change(path, true)).Error);
        Assert.Equal(baseline.Strength + 50, game.State.Pools.Strength);
        Assert.Equal(baseline.Intelligence + 50, game.State.Pools.Intelligence);
        int lower = Math.Min(game.State.Pools.Strength, game.State.Pools.Intelligence);
        int raw = Program.Passives.Stats(path).GetValueOrDefault(InquisitorKey("critical_strike_chance_+%"));
        Assert.Equal(baseline.CriticalStrikeChancePct + raw + lower, game.State.Pools.CriticalStrikeChancePct);
        Assert.Equal(raw + lower, InquisitorStat(game.State, "critical_strike_chance_+%"));
        Assert.Equal(game.State.Pools.SpellCriticalStrikeChance,
            SpellCriticalStrike.Chance(500, Program.Stats, Program.CharacterPlayerStats(game.State), new Dictionary<uint, int>()));
        var reloaded = game.Reload();
        Assert.Equal(game.State.Pools.Strength, reloaded.Pools.Strength);
        Assert.Equal(game.State.Pools.CriticalStrikeChancePct, reloaded.Pools.CriticalStrikeChancePct);
        Assert.Null((await game.Change(path.Reverse().ToArray(), false, refundPoints: true)).Error);
        Assert.Equal(100 - path.Length * 5, game.State.RefundPoints);
        Assert.Equal(baseline.Strength, game.State.Pools.Strength);
        Assert.Equal(baseline.Intelligence, game.State.Pools.Intelligence);
        Assert.Equal(baseline.CriticalStrikeChancePct, game.Reload().Pools.CriticalStrikeChancePct);
    }

    [Fact]
    public async Task Inquisitor_Justice_uses_main_weapon_local_crit_and_respects_unarmed_and_global_prohibition()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        EquipFamilyGem(game, FamilyGem("Fireball"));
        Assert.Null((await game.Change(InquisitorPath(3154), true)).Error);
        ushort skill = Program.GemSkillIdPool.Value[0];
        Assert.Equal(0, ProgramValue<int>("SupportedCriticalChance", game.State, skill));
        var wandType = Program.Items.Find("Driftwood Wand")!;
        var localCritical = Program.Items.Mods.All.First(m => m.Domain == 1 && m.Level <= 60 && m.Stats.Count == 1
            && m.Stats[0].Id == "local_critical_strike_chance_+%");
        var wand = ItemCatalog.Create(wandType) with
        { Mods = [localCritical.With(ModSection.Explicit, [50], Random.Shared)] };
        var placed = game.Items.Add(PlayerInventories.Weapon1, 0, 0, wand);
        game.State.Pools = Program.PoolsFor(game.State, 60);
        int localBase = Program.Items.WeaponCritical(wandType) * 150 / 100;
        Assert.Equal(DamageModel.CriticalStrikeChancePerMyriad(localBase, game.State.Pools.SpellCriticalStrikeChancePct),
            ProgramValue<int>("SupportedCriticalChance", game.State, skill));
        InquisitorSet(game.State, "global_cannot_crit", 1);
        game.State.Pools = Program.PoolsFor(game.State, 60);
        Assert.Equal(0, ProgramValue<int>("SupportedCriticalChance", game.State, skill));
        InquisitorSet(game.State, "global_cannot_crit", 0);
        game.Items.Remove(PlayerInventories.Weapon1, placed.Id);
        game.State.Pools = Program.PoolsFor(game.State, 60);
        Assert.Equal(0, ProgramValue<int>("SupportedCriticalChance", game.State, skill));
        Assert.Null((await game.Change([(ushort)3154], false, refundPoints: true)).Error);
        Assert.True(ProgramValue<int>("SupportedCriticalChance", game.State, skill) > 0);
        Assert.True(ProgramValue<int>("SupportedCriticalChance", game.Reload(), skill) > 0);
    }

    [Fact]
    public async Task Inquisitor_Battlemage_changes_actual_supported_hit_and_paid_refund_removes_added_weapon_damage()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        EquipFamilyGem(game, FamilyGem("Fireball"));
        game.Items.Add(PlayerInventories.Weapon1, 0, 0, ItemCatalog.Create(Program.Items.Find("Driftwood Sceptre")!));
        game.State.Pools = Program.PoolsFor(game.State, 60);
        ushort skill = Program.GemSkillIdPool.Value[0];
        Assert.True(ProgramValue<DamagePacket>("SupportedHit", game.State, skill)[DamageType.Physical].IsZero);
        Assert.Null((await game.Change(InquisitorPath(19417), true)).Error);
        var actual = ProgramValue<DamagePacket>("SupportedHit", game.State, skill);
        var weapon = ProgramValue<SkillCatalog.Unarmed>("WeaponFor", game.State);
        int effect = Program.Skills.EffectRow("Fireball");
        double effectiveness = ProgramValue<double>("AddedEffectiveness", game.State, effect, 1);
        var stats = Program.CharacterPlayerStats(game.State);
        double increase = 1 + (stats.GetValueOrDefault(InquisitorKey("damage_+%"))
            + stats.GetValueOrDefault(InquisitorKey("spell_damage_+%"))
            + stats.GetValueOrDefault(InquisitorKey("physical_damage_+%"))) / 100d;
        Assert.Equal(weapon.MinDamage * effectiveness * increase, actual[DamageType.Physical].Min, precision: 8);
        Assert.Equal(weapon.MaxDamage * effectiveness * increase, actual[DamageType.Physical].Max, precision: 8);
        Assert.Equal(actual[DamageType.Physical], ProgramValue<DamagePacket>("SupportedHit", game.Reload(), skill)[DamageType.Physical]);
        Assert.Null((await game.Change([(ushort)19417], false, refundPoints: true)).Error);
        Assert.True(ProgramValue<DamagePacket>("SupportedHit", game.State, skill)[DamageType.Physical].IsZero);
    }

    [Fact]
    public async Task Inquisitor_Sanctuary_and_Pious_project_real_life_regeneration_to_ES_and_clear_after_refund()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        InquisitorSet(game.State, "base_maximum_energy_shield", 100);
        game.State.Pools = Program.PoolsFor(game.State, 60);
        Assert.Equal(0, InquisitorStat(game.State, "base_energy_shield_regeneration_rate_per_minute"));
        Assert.Null((await game.Change(InquisitorPath(32816), true)).Error);
        var stats = Program.CharacterPlayerStats(game.State);
        int Get(string name) => stats.GetValueOrDefault(InquisitorKey(name));
        Assert.True(Get("life_regeneration_rate_per_minute_%") >= 300);
        Assert.Equal((int)PanelStats.LifePerMinute(Get, game.State.Pools.Life),
            Get("base_energy_shield_regeneration_rate_per_minute"));
        Assert.True(InquisitorStat(game.Reload(), "base_energy_shield_regeneration_rate_per_minute") > 0);
        Assert.Null((await game.Change([(ushort)32816], false, refundPoints: true)).Error);
        Assert.Equal(0, InquisitorStat(game.State, "base_energy_shield_regeneration_rate_per_minute"));
        Assert.True(InquisitorStat(game.State, "life_regeneration_rate_per_minute_%") >= 300);
        Assert.Equal(0, InquisitorStat(game.Reload(), "base_energy_shield_regeneration_rate_per_minute"));
    }

    [Fact]
    public async Task Inquisitor_Augury_samples_current_target_range_and_only_elemental_damage_in_both_directions()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        Assert.Null((await game.Change(InquisitorPath(40059), true)).Error);
        var at = game.State.Walk.Position(game.State.Clock.Elapsed.TotalSeconds);
        var enemy = new SpawnedMonster("Inquisitor target", new(at.X + 5, at.Y, 30), 0, 2, 1000);
        game.State.Monsters[42] = enemy;
        var hit = DamagePacket.Of(DamageType.Physical, 100, 100).Add(DamagePacket.Of(DamageType.Fire, 100, 100));
        var outgoing = Program.InquisitorTargetDamage(game.State, enemy, hit);
        var incoming = Program.InquisitorEnemyDamage(game.State, enemy, hit);
        Assert.Equal(100, outgoing[DamageType.Physical].Min);
        Assert.Equal(116, outgoing[DamageType.Fire].Min, precision: 8);
        Assert.Equal(100, incoming[DamageType.Physical].Min);
        Assert.Equal(92, incoming[DamageType.Fire].Min, precision: 8);
        enemy.Walk = new(at.X + 100, at.Y, 30);
        Assert.Equal(100, Program.InquisitorTargetDamage(game.State, enemy, hit)[DamageType.Fire].Min);
        Assert.Equal(100, Program.InquisitorEnemyDamage(game.State, enemy, hit)[DamageType.Fire].Min);
        enemy.Walk = new(at.X + 5, at.Y, 30);
        Assert.Null((await game.Change([(ushort)40059], false, refundPoints: true)).Error);
        Assert.Equal(100, Program.InquisitorTargetDamage(game.State, enemy, hit)[DamageType.Fire].Min);
        Assert.Equal(100, Program.InquisitorEnemyDamage(game.State, enemy, hit)[DamageType.Fire].Min);
    }

    [Fact]
    public async Task Inquisitor_Virtue_accepted_cast_hook_excludes_instant_uses_and_paid_refund_clears_its_history()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        Assert.Null((await game.Change(InquisitorPath(19417), true)).Error);
        Assert.Equal(1, ProgramValue<double>("InquisitorAttackFactor", game.State));
        Program.InquisitorSkillUsed(game.State, attack: false, spell: true, instant: true);
        Assert.Equal(1, ProgramValue<double>("InquisitorAttackFactor", game.State));
        for (int i = 0; i < 4; i++) Program.InquisitorSkillUsed(game.State, attack: false, spell: true);
        Assert.Equal(1.3, ProgramValue<double>("InquisitorAttackFactor", game.State));
        Assert.Equal(1, ProgramValue<double>("InquisitorAttackFactor", game.Reload()));
        Assert.Null((await game.Change([(ushort)19417], false, refundPoints: true)).Error);
        Assert.Equal(1, ProgramValue<double>("InquisitorAttackFactor", game.State));
        Assert.Null((await game.Change([(ushort)19417], true)).Error);
        Assert.Equal(1, ProgramValue<double>("InquisitorAttackFactor", game.State));
    }

    [Fact]
    public async Task Inquisitor_native_effect_sender_is_idempotent_and_clears_paid_refunds()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        Assert.Null((await game.Change(InquisitorPath(32816), allocate: true)).Error);
        Assert.NotEmpty((await game.InquisitorPresentation()).Bytes);
        Assert.Empty((await game.InquisitorPresentation()).Bytes);
        Assert.Null((await game.Change(game.State.MainAscendancy.Allocated.ToArray(), allocate: false, refundPoints: true)).Error);
        Assert.NotEmpty((await game.InquisitorPresentation()).Bytes);
        Assert.Empty((await game.InquisitorPresentation()).Bytes);
    }

    sealed partial class GatewayCharacter
    {
        public Task<(string? Error, byte[] Bytes)> InquisitorPresentation() => Exchange(async (stream, cipher) =>
        {
            await ProgramValue<Task>("SendInquisitor", "Inquisitor presentation test", stream, cipher, State);
            return null;
        });
    }
}
