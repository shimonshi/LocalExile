using GameServer.Core.World;

/// <summary>PR #198's shared raw-character attack packet, adapted to typed derived damage, Rage and melee context.
/// The gem contributes effectiveness and quality-aware conversion before critical strikes and target mitigation.</summary>
static class AttackSkillHit
{
    public static DamagePacket BaseHit(SkillCatalog.Unarmed weapon, IReadOnlyDictionary<uint, int> character,
        StatCatalog catalog, bool melee)
    {
        int Get(string name) => catalog.Key(name) is uint key ? character.GetValueOrDefault(key) : 0;
        var derived = DerivedDamageIncreases.From(Get);
        return DamageStats.Added(weapon.HitDamage(character, melee, derived.AttackPercent), Get, spell: false, melee,
                additionalIncreasedPct: derived.AttackPercent)
            .Scale(Math.Max(0, 1 + Get("attack_damage_+%_final_from_rage") / 100.0));
    }

    public static DamagePacket Resolve(SkillCatalog.Unarmed weapon, IReadOnlyDictionary<uint, int> character,
        IEnumerable<(uint, int)> levelConversionInputs, StatCatalog catalog, int effectiveness, bool melee,
        IEnumerable<(uint, int)>? qualityConversionInputs = null)
    {
        // This bounded raw-character attack packet supports explicit skill effectiveness/conversion below;
        // skill-owned generic damage modifiers or flat additions must not become panel-only effects.
        var hit = BaseHit(weapon, character, catalog, melee);
        // Match quality-aware ConversionFor: raw player keys win over level inputs, then quality adds once.
        var conversionInputs = new Dictionary<uint, int>(character);
        foreach (var (key, value) in levelConversionInputs) conversionInputs.TryAdd(key, value);
        if (qualityConversionInputs is not null)
            foreach (var (key, value) in qualityConversionInputs)
                conversionInputs[key] = conversionInputs.GetValueOrDefault(key) + value;
        return SpellDamage.WithEffectiveness(hit, effectiveness)
            .Convert(DamageStats.Conversion(conversionInputs, catalog, attack: true));
    }
}
