namespace GameServer.Gateway.Tests;

// Awakened support subsystems: Melee Splash's splash radius and damage multiplier (Multistrike's repeats are driven by
// the same skill stats through SupportedSkill).
public sealed class AwakenedSubsystemTests
{
    static Func<string, int> Get(params (string Name, int Value)[] stats) =>
        name => stats.Where(s => s.Name == name).Sum(s => s.Value);

    public void Melee_splash_radius_and_damage_follow_its_stats()
    {
        Assert.Null(MeleeSplash.Settings(Get()));
        var plain = MeleeSplash.Settings(Get(("support_melee_splash_damage_+%_final_for_splash", -40)));
        Assert.NotNull(plain);
        Assert.Equal(12, plain!.Value.Radius);
        Assert.Equal(0.6, plain.Value.More, 9);
        var grown = MeleeSplash.Settings(Get(("support_melee_splash_damage_+%_final_for_splash", -40),
            ("melee_splash_area_of_effect_+%_final", 100)));
        Assert.Equal(16, grown!.Value.Radius);
    }
}

public sealed class SupportPrerequisiteCases
{
    public static IEnumerable<object[]> SupportDamageAndAilmentCasesData()
    {
        yield return ["Crush_lowers_physical_damage_reduction_for_four_seconds"];
        yield return ["Poison_freeze_duration_and_chain_support_stats_apply"];
        yield return ["Maim_bloodlust_power_charges_and_volatility_apply"];
    }

    [Theory]
    [MemberData(nameof(SupportDamageAndAilmentCasesData))]
    public void SupportDamageAndAilmentCases(string caseId)
    {
        switch (caseId)
        {
            case "Crush_lowers_physical_damage_reduction_for_four_seconds":
                new AwakenedSupportTests().Crush_lowers_physical_damage_reduction_for_four_seconds();
                break;
            case "Poison_freeze_duration_and_chain_support_stats_apply":
                new GreenSupportMechanicsTests().Poison_freeze_duration_and_chain_support_stats_apply();
                break;
            case "Maim_bloodlust_power_charges_and_volatility_apply":
                new RedSupportMechanicsTests().Maim_bloodlust_power_charges_and_volatility_apply();
                break;
            default: throw new ArgumentOutOfRangeException(nameof(caseId), caseId, "Unknown support scenario");
        }
    }

    public static IEnumerable<object[]> SupportExecutionAndPresentationCasesData()
    {
        yield return ["Melee_splash_radius_and_damage_follow_its_stats"];
        yield return ["Final_repeat_double_damage_and_aura_support_effect_apply"];
        yield return ["Cull_the_weak_and_pacifism_cull_and_spare_by_rarity"];
    }

    [Theory]
    [MemberData(nameof(SupportExecutionAndPresentationCasesData))]
    public void SupportExecutionAndPresentationCases(string caseId)
    {
        switch (caseId)
        {
            case "Melee_splash_radius_and_damage_follow_its_stats":
                new AwakenedSubsystemTests().Melee_splash_radius_and_damage_follow_its_stats();
                break;
            case "Final_repeat_double_damage_and_aura_support_effect_apply":
                new AwakenedSupportTests().Final_repeat_double_damage_and_aura_support_effect_apply();
                break;
            case "Cull_the_weak_and_pacifism_cull_and_spare_by_rarity":
                new GreenSupportMechanicsTests().Cull_the_weak_and_pacifism_cull_and_spare_by_rarity();
                break;
            default: throw new ArgumentOutOfRangeException(nameof(caseId), caseId, "Unknown support scenario");
        }
    }

}
