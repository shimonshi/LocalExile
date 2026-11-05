using GameServer.Core.World;

/// <summary>Pinned Default Assassin inputs. Call with the selected mode's raw sources, never display aliases.</summary>
static class OrdinaryAssassinRules
{
    internal const string NotLowChance = "assassin_critical_strike_chance_+%_final_vs_enemies_not_on_low_life";
    internal const string LowChance = "assassinate_passive_critical_strike_chance_vs_enemies_on_low_life_+%_final";
    internal const string LowMultiplier = "assassin_critical_strike_multiplier_+_vs_enemies_on_low_life";
    internal const string NotLowMultiplier = "assassin_critical_strike_multiplier_+_vs_enemies_not_on_low_life";
    internal const string SpellChance = "assassin_ascendancy_spell_critical_strike_chance_+%_final";
    internal const string NearbyDamage = "damage_+%_final_if_there_is_at_most_1_rare_or_unique_enemy_nearby";
    internal const string NearbyTaken = "damage_taken_+%_if_there_are_at_least_2_rare_or_unique_enemies_nearby";
    internal const string CriticalPoison = "poison_from_critical_strikes_damage_+%_final";
    internal const string RecentPoisonDuration = "poison_duration_+%_per_poison_applied_recently";
    internal const string PoisonKillRecovery = "recover_permyriad_maximum_life_per_poison_on_enemy_on_kill";

    internal static bool LowLife(uint life, uint maximum) => maximum > 0 && (long)life * 2 <= maximum;

    // Conditional MORE/LESS must precede the final cap. Supply the ordinary chance without its 100% cap.
    // A previously capped 100% chance cannot correctly compose Mystical Infusion's LESS modifier.
    internal static int CriticalChance(double uncapped, bool spell, uint life, uint maximum, Func<string, int> get,
        bool prohibited = false)
    {
        if (prohibited) return 0;
        double value = Math.Max(0, uncapped) * More(get(LowLife(life, maximum) ? LowChance : NotLowChance));
        if (spell) value *= More(get(SpellChance));
        return (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 10_000);
    }

    internal static int CriticalMultiplier(int ordinary, uint life, uint maximum, Func<string, int> get,
        bool noExtraDamage = false) => noExtraDamage ? 100 : (int)Math.Clamp((long)ordinary
            + get(LowLife(life, maximum) ? LowMultiplier : NotLowMultiplier), 0, int.MaxValue);

    internal static double More(int percent) => Math.Max(0, 1 + percent / 100d);
    internal static double OutgoingMore(int nearbyRareOrUnique, Func<string, int> get) =>
        nearbyRareOrUnique <= 1 ? More(get(NearbyDamage)) : 1;
    // An increased/reduced damage-taken contribution, NOT its own LESS multiplier. Applies to hits and DoT.
    internal static int IncomingIncreased(int nearbyRareOrUnique, Func<string, int> get) =>
        nearbyRareOrUnique >= 2 ? get(NearbyTaken) : 0;

    internal static MonsterPoisons.Settings Poison(MonsterPoisons.Settings ordinary, int recentPoisons,
        Func<string, int> get)
    {
        double existing = 100d + get("poison_duration_+%") + get("base_all_ailment_duration_+%");
        double added = (double)Math.Max(0, recentPoisons) * get(RecentPoisonDuration);
        // Reconstruct duration even if a negative ordinary duration had reduced it to zero.
        double faster = MonsterPoisons.Faster(get);
        double seconds = faster > 0 ? Ailments.PoisonDurationSeconds * Math.Max(0, existing + added) / 100 / faster : 0;
        return ordinary with { CriticalMin = ordinary.CriticalMin * More(get(CriticalPoison)),
            CriticalMax = ordinary.CriticalMax * More(get(CriticalPoison)), Seconds = seconds };
    }

    internal static double PoisonKillLife(uint maximumLife, int liveStacksAtDeath, Func<string, int> get) =>
        get("cannot_recover_life") != 0 ? 0 : maximumLife * (double)Math.Max(0, liveStacksAtDeath)
            * Math.Max(0, get(PoisonKillRecovery)) / 10_000;

    internal static bool PoisonEligible(DamagePacket landed, Func<string, int> get) => !MonsterPoisons.Prohibited(get)
        && (get("all_damage_can_poison") != 0 ? landed.MinTotal > 0
            : landed[DamageType.Physical].Min + landed[DamageType.Chaos].Min > 0);

    internal static int MarkEffect(int maximumPowerCharges, Func<string, int> get) => (int)Math.Clamp(
        (long)Math.Max(0, maximumPowerCharges) * get("mark_skills_curse_effect_+%_per_maximum_power_charge"), int.MinValue, int.MaxValue);
    internal static int InstantLeech(int maximumPowerCharges, Func<string, int> get) => (int)Math.Clamp(
        (long)Math.Max(0, maximumPowerCharges) * get("leech_%_is_instant_per_maximum_power_charge"), 0, 100);
}
