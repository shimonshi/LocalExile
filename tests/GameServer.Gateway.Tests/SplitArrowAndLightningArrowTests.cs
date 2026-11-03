namespace GameServer.Gateway.Tests;

public sealed class SplitArrowAndLightningArrowTests
{
    [Fact]
    public void SplitArrowAndLightningArrowResolveTheirProjectileProfiles()
    {
        var skills = new SkillCatalog();

        int splitArrow = skills.EffectRow("SplitArrow");
        Assert.True(splitArrow >= 0);
        var splitArrowProj = skills.ProjectileFor(splitArrow);
        Assert.NotNull(splitArrowProj);
        Assert.Equal("Metadata/Projectiles/SplitArrowDefault", splitArrowProj!.Value.Id);
        Assert.Equal(3260, splitArrowProj.Value.Speed);

        int lightningArrow = skills.EffectRow("LightningArrow");
        Assert.True(lightningArrow >= 0);
        var lightningArrowProj = skills.ProjectileFor(lightningArrow);
        Assert.NotNull(lightningArrowProj);

        var lightningArrowProfile = GatewayProjectileSkillProfiles.Resolve("LightningArrow", lightningArrowProj!.Value.Id,
            name => name == "lightning_arrow_maximum_number_of_extra_targets" ? 3
                : name == "active_skill_base_area_of_effect_radius" ? 18 : 0, 3260, attack: true, arrows: true);
        Assert.NotNull(lightningArrowProfile.ContactTargets);
        Assert.Equal(3, lightningArrowProfile.ContactTargets!.Count);
        Assert.Equal(18, lightningArrowProfile.ContactTargets.Radius);

        foreach (string effect in new[] { "LightningArrow", "LightningArrowAltX", "LightningArrowRoyale" })
        foreach (var (baseRadius, increasedArea, flatRadius, moreRadius, expected) in new[]
        {
            (0, 0, 0, 0, 18),
            (0, 300, 0, 0, 36),
            (24, 300, 0, 0, 48),
            (0, 300, 2, 25, 50),
        })
        {
            int Get(string name) => name switch
            {
                "active_skill_base_area_of_effect_radius" => baseRadius,
                "base_skill_area_of_effect_+%" => increasedArea,
                "active_skill_base_radius_+" => flatRadius,
                "active_skill_area_of_effect_radius_+%_final" => moreRadius,
                _ => 0,
            };
            var scaled = GatewayProjectileSkillProfiles.Resolve(effect, lightningArrowProj.Value.Id,
                Get, 3260, attack: true, arrows: true);
            Assert.Equal(expected, scaled.ContactTargets!.Radius);
        }
    }
}
