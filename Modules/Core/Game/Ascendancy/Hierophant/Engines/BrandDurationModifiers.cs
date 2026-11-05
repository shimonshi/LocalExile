/// <summary>Millisecond inputs shared by native Brand skill publication and the two authoritative budgets.</summary>
readonly record struct BrandDurations(int AttachedMilliseconds, int DetachedMilliseconds)
{
    public bool Alive => AttachedMilliseconds > 0 && DetachedMilliseconds > 0;
}

static class BrandDurationModifiers
{
    public const string Primary = "skill_effect_duration";
    public const string Secondary = "secondary_skill_effect_duration";
    static readonly string[] MoreSources =
    [
        "support_reduced_duration_skill_effect_duration_+%_final",
        "support_more_duration_skill_effect_duration_+%_final",
        "active_skill_quality_duration_+%_final",
        "support_swift_affliction_skill_effect_and_damaging_ailment_duration_+%_final",
        "passive_mastery_more_skill_effect_duration_+%_final",
        "passive_mastery_less_skill_effect_duration_+%_final",
        "unique_ebb_and_flow_support_duration_+%_final",
    ];

    // get already combines final player inputs and supported own inputs ONCE. Strength duration is already derived.
    // Native aggregates, when supplied, replace their constituent calculation; final replacements include explicit zero.
    public static BrandDurations Resolve(Func<string, int> get, Func<string, int?> aggregate, Func<string, int?> replacement)
    {
        float increased = aggregate("virtual_skill_effect_duration_+%")
            ?? Sum(get("skill_effect_duration_+%"), get("sigil_duration_+%"));
        float more = 100;
        foreach (string source in MoreSources)
        {
            float factor = Factor(get(source));
            if (factor == 0) { more = 0; break; }
            more *= factor;
        }
        int morePercent = aggregate("virtual_skill_effect_duration_pluspercent_final") ?? Round(more - 100f, signed: true);
        int additional = get("virtual_additional_skill_effect_duration");
        bool fixedDuration = get("fixed_skill_effect_duration") != 0;
        int Channel(string output, string basis, bool secondary)
        {
            if (replacement(output) is int replaced) return Math.Max(0, replaced);
            float value = Sum(get(basis), additional);
            if (!fixedDuration)
            {
                value *= Factor(increased + (secondary ? get("secondary_skill_effect_duration_+%") : 0));
                value *= Factor(morePercent);
                value *= Factor(get(secondary
                    ? "support_rapid_activation_brand_skill_only_secondary_duration_+%_final"
                    : "support_rapid_activation_brand_skill_only_primary_duration_+%_final"));
            }
            return Round(value);
        }
        return new(Channel(Primary, "base_skill_effect_duration", false),
            Channel(Secondary, "base_secondary_skill_effect_duration", true));
    }

    static float Factor(float percent) => Math.Max(0, (100f + percent) / 100f);
    internal static int Sum(int a, int b) => (int)Math.Clamp((long)a + b, int.MinValue, int.MaxValue);
    // Native secondary calculator uses binary32 and rounds half away from zero. Bound the supported integer output.
    static int Round(float value, bool signed = false) => (int)Math.Clamp(
        Math.Truncate((double)(value < 0 ? value - .5f : value + .5f)), signed ? int.MinValue : 0, int.MaxValue);
}

static partial class Game
{
    internal static bool OrdinaryBrand(int effect) => Skills.EffectId(effect) is "StormBrand" or "ArmageddonBrand";

    internal static BrandDurations BrandDurationFor(ClientState conn, IReadOnlyDictionary<uint, int> player,
        IReadOnlyDictionary<uint, int> own, PanelSourceSnapshot? panel = null)
    {
        Dictionary<uint, int>? overrides = null;
        if (panel is null) lock (conn.Monsters) overrides = new(conn.PlayerStats);
        int Get(string name) => Stats.Key(name) is uint key
            ? BrandDurationModifiers.Sum(player.GetValueOrDefault(key), own.GetValueOrDefault(key)) : 0;
        int? Aggregate(string name) => Stats.Key(name) is uint key && (player.ContainsKey(key) || own.ContainsKey(key))
            ? Get(name) : null;
        int? Replacement(string name) => Stats.Key(name) is uint key
            && (panel?.Overrides.Contains(key) ?? overrides!.ContainsKey(key))
                ? (panel?.Raw ?? overrides!).GetValueOrDefault(key) : null;
        return BrandDurationModifiers.Resolve(Get, Aggregate, Replacement);
    }
}
