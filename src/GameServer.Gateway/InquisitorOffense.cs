using GameServer.Core.World;

/// <summary>Inquisitor source rules before generic spell increases, effectiveness and conversion.</summary>
static class InquisitorOffense
{
    public const string Providence = "increase_crit_chance_by_lowest_of_str_or_int";
    public const string Justice = "base_spell_critical_chance_equal_to_the_critical_strike_chance_of_main_weapon";
    public const string Battlemage = "keystone_battlemage";
    public const string Virtue = "inquisitor_attack_damage_+%_final_per_non_instant_spell_cast_in_8_seconds_max_30%";

    // Final attributes are supplied by the same pool calculation that consumes the flat +50 Str/Int node stats.
    // The flag is boolean: allocating a second source does not multiply the attribute-derived increase.
    public static int RighteousProvidenceIncrease(Func<string, int> get, int strength, int intelligence) =>
        get(Providence) == 0 ? 0 : Math.Max(0, Math.Min(strength, intelligence));

    // A weapon's local critical modifiers belong in mainHandCritical. Global attack critical increases, off-hand
    // weapons and an unarmed base grant do not. Global cannot-crit is still enforced by SpellCriticalStrike.Chance.
    public static int? SpellBase(int? spellBase, int? mainHandCritical, Func<string, int> get) =>
        get(Justice) == 0 ? spellBase : Math.Max(0, mainHandCritical ?? 0);

    // Only the equipped weapon's local damage enters here, never its globally increased attack HitDamage.
    // Return raw added spell damage: its caller applies the spell's added effectiveness exactly once.
    public static DamagePacket BattlemageDamage(DamagePacket? mainHandWeapon, Func<string, int> get) =>
        get(Battlemage) == 0 || mainHandWeapon is null ? DamagePacket.Empty : mainHandWeapon;

    public static int Cost(int basis, double supportMultiplier, FanaticismModifiers fanaticism) =>
        (int)Math.Clamp(Math.Floor(Math.Max(0, basis) * Math.Max(0, supportMultiplier) * (double)fanaticism.CostFactor), 0, int.MaxValue);

    public readonly record struct FanaticismModifiers(int CastSpeedMorePct, int CostMorePct, int AreaIncreasePct)
    {
        public double CastSpeedFactor => Math.Max(0, 1 + CastSpeedMorePct / 100.0);
        public decimal CostFactor => Math.Max(0, 1 + CostMorePct / 100m);
    }

    // PoB's primary CalcPerform implementation treats these as Cast-scoped modifiers and scales their magnitude
    // by BuffEffectOnSelf. 3.29 cost is MORE (less), despite the legacy native stat name ending in "mana_cost_+%".
    public static FanaticismModifiers Fanaticism(bool active, bool selfCastSpell, int buffEffectPct = 0)
    {
        if (!active || !selfCastSpell) return default;
        int magnitude = (int)Math.Clamp(decimal.Floor(75m * Math.Max(0, 100m + buffEffectPct) / 100m), 0, int.MaxValue);
        return new(magnitude, -magnitude, magnitude);
    }
}
