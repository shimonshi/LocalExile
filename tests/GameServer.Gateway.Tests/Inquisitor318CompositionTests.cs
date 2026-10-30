using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed partial class AscendancyGatewayIntegrationTests
{
    [Fact]
    public async Task Inquisitor_Virtue_preserves_Arcane_Mights_wand_ratio_in_supported_attack_hits()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        EquipFamilyGem(game, FamilyGem("KineticBlast"));
        game.Items.Add(PlayerInventories.Weapon1, 0, 0, ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!));
        InquisitorSet(game.State, "base_intelligence", 1000);
        InquisitorSet(game.State, "base_dexterity", 1000);
        InquisitorSet(game.State, "attack_minimum_added_fire_damage", 100);
        InquisitorSet(game.State, "attack_maximum_added_fire_damage", 100);
        InquisitorSet(game.State, "spell_damage_+%", 0);
        InquisitorSet(game.State, "elemental_damage_+%", 0);
        game.State.Pools = Program.PoolsFor(game.State, 60);
        game.State.Clock.Stop();
        ushort skill = Program.GemSkillIdPool.Value[0];
        double Fire() => ProgramValue<DamagePacket>("SupportedHit", game.State, skill)[DamageType.Fire].Min;
        double baseline = Fire();
        Assert.True(baseline > 0);
        InquisitorSet(game.State, ArcaneMight.Wand, 200);
        InquisitorSet(game.State, "spell_damage_+%", 100);
        Assert.Equal(baseline * 3, Fire(), 8);
        Assert.Null((await game.Change(InquisitorPath(19417), true)).Error);
        Program.InquisitorSkillUsed(game.State, attack: false, spell: true);
        Assert.Equal(baseline * 3 * 1.1, Fire(), 8);
        game.State.PlayerStats.Remove(InquisitorKey(ArcaneMight.Wand));
        Assert.Equal(baseline * 2.5 * 1.1, Fire(), 8); // Kinetic Blast's own 150% grant remains.
        Assert.Null((await game.Change([(ushort)19417], false, refundPoints: true)).Error);
        Assert.Equal(baseline * 2.5, Fire(), 8);
    }

    [Fact]
    public async Task Inquisitor_318_Pious_copies_life_regeneration_before_life_recovery_and_only_ES_recovery_scales_it()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        Assert.Null((await game.Change(InquisitorPath(32816), true)).Error);
        Program.CharacterPlayerStats(game.State);
        var inputs = new Dictionary<string, int>
        {
            ["base_life_regeneration_rate_per_minute"] = 6000,
            ["life_regeneration_rate_+%"] = 100,
            ["life_recovery_rate_+%"] = 200,
            ["energy_shield_regeneration_rate_+%"] = 100,
            ["energy_shield_recovery_rate_+%"] = 50,
        };
        int Get(string name) => inputs.GetValueOrDefault(name);
        Assert.Equal(18000, ProgramValue<double>("InquisitorMirroredPerMinute", game.State, (Func<string, int>)Get));
        inputs["cannot_recover_life"] = 1;
        inputs["no_energy_shield_recharge_or_regeneration"] = 1;
        Assert.Equal(18000, ProgramValue<double>("InquisitorMirroredPerMinute", game.State, (Func<string, int>)Get));
        inputs["cannot_recover_energy_shield"] = 1;
        Assert.Equal(0, ProgramValue<double>("InquisitorMirroredPerMinute", game.State, (Func<string, int>)Get));
        inputs.Remove("cannot_recover_energy_shield");
        inputs["no_life_regeneration"] = 1;
        Assert.Equal(0, ProgramValue<double>("InquisitorMirroredPerMinute", game.State, (Func<string, int>)Get));
    }
}
