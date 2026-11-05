/// <summary>Candidate rules for the pinned ordinary tree. Inputs must be original sources, never projected output.</summary>
static class OrdinaryHierophantRules
{
    public const string HitSurge = "gain_arcane_surge_on_spell_hit_by_you_or_your_totems";
    public const string ManaSurge = "hierophant_gain_arcane_surge_on_mana_use_threshold";
    public const string SurgeMore = "arcane_surge_on_you_spell_damage_+%_final_from_hierophant";
    public const string TotemMore = "hierophant_passive_damage_+%_final_per_totem";
    public const string CostMore = "hierophant_mana_cost_+%_final";
    public const string SelfAilment = "non_damaging_ailment_effect_+%_on_self_while_you_have_arcane_surge";
    public static bool Owns(AscendancyIdentity? identity) => identity is
        { Family: AscendancyFamily.Normal, Id: "Hierophant", Revision: AscendancyCatalog.Revision };

    // Pinned SupportArcaneSurge stat set 8451: 4000 ms, 20 cast speed, 30 mana regeneration.
    // Reusing this unlevelled buff is an explicit server model, not proof of native ascendancy parity.
    public const double SurgeSeconds = 4;
    public static int Scale(int value, int effect) => Clamp(decimal.Truncate(value * Math.Max(0m, 100m + effect) / 100));
    public static int Clamp(decimal value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);
    public static int ManaDamage(Func<string, int> get) => get("additive_mana_modifiers_apply_to_damage_at_30%_value") == 0
        ? 0 : Clamp(decimal.Truncate(get("maximum_mana_+%") * .3m));
    public static int Area(Func<string, int> get, uint unreservedMaximum) =>
        get("hierophant_area_of_effect_+%_per_50_unreserved_mana_up_to_100%") == 0 ? 0
        : (int)Math.Min(100, unreservedMaximum / 50);
    public static uint ManaCost(uint ordinaryCost, Func<string, int> get) =>
        (uint)Math.Clamp(decimal.Floor(ordinaryCost * Math.Max(0m, 100m + get(CostMore)) / 100), 0, int.MaxValue);
    public static double DamageFactor(Func<string, int> get, int livingTotems, bool spell, bool surge) =>
        Math.Max(0, 1 + get(TotemMore) * (double)Math.Max(0, livingTotems) / 100)
        * (spell && surge ? Math.Max(0, 1 + Scale(get(SurgeMore), get("arcane_surge_effect_+%")) / 100.0) : 1);
    public static double AilmentEffectFactor(Func<string, int> get, bool surge) =>
        surge ? Math.Max(0, 1 + get(SelfAilment) / 100.0) : 1;

    public static IReadOnlyList<(string Stat, int Value)> Sources(Func<string, int> get, uint unreservedMaximum,
        int livingTotems, bool surge)
    {
        int count = Math.Max(0, livingTotems);
        var result = new List<(string, int)>
        {
            ("damage_+%", ManaDamage(get)), ("skill_area_of_effect_+%", Area(get, unreservedMaximum)),
            ("life_regeneration_rate_per_minute_%", Clamp((decimal)count * get("you_and_totem_life_regeneration_rate_per_minute_%_per_active_totem"))),
            ("mana_regeneration_rate_per_minute_%", Clamp((decimal)count * get("mana_regeneration_rate_per_minute_%_per_active_totem")))
        };
        if (surge)
        {
            result.Add(("has_arcane_surge", 1));
            result.Add(("cast_speed_+%", Scale(20, get("arcane_surge_effect_+%"))));
            result.Add(("mana_regeneration_rate_+%", Scale(30, get("arcane_surge_effect_+%"))));
            result.Add(("skill_area_of_effect_+%", get("area_of_effect_+%_while_you_have_arcane_surge")));
            result.Add(("base_life_leech_from_spell_damage_permyriad", get("life_leech_from_spell_damage_permyriad_while_you_have_arcane_surge")));
        }
        return result;
    }
}

/// <summary>Game-time state. Caller serializes it under Monsters. Refund, identity/mode changes and death clear it.</summary>
sealed class OrdinaryHierophantState
{
    bool enabled, hardMode;
    int hitSource, threshold;
    double until;
    public int ManaRemainder { get; private set; }
    public void Observe(bool ordinaryHierophant, bool ruthless, Func<string, int> get)
    {
        int hit = ordinaryHierophant && !ruthless ? get(OrdinaryHierophantRules.HitSurge) : 0;
        int mana = ordinaryHierophant && ruthless ? Math.Max(0, get(OrdinaryHierophantRules.ManaSurge)) : 0;
        if (enabled != ordinaryHierophant || hardMode != ruthless || hitSource != hit || threshold != mana) Clear();
        (enabled, hardMode, hitSource, threshold) = (ordinaryHierophant, ruthless, hit, mana);
    }
    public double Until => until;
    public bool Active(double now) => enabled && now < until;
    public bool SpellHit(double now, bool landed, bool spell, bool playerOrOwnedTotem)
    {
        if (!enabled || hardMode || hitSource == 0 || !landed || !spell || !playerOrOwnedTotem) return false;
        until = now + OrdinaryHierophantRules.SurgeSeconds; return true;
    }
    public bool Spend(double now, uint actualManaSpent)
    {
        if (!enabled || !hardMode || threshold <= 0 || actualManaSpent == 0) return false;
        long total = ManaRemainder + (long)actualManaSpent;
        ManaRemainder = (int)(total % threshold);
        if (total < threshold) return false;
        until = now + OrdinaryHierophantRules.SurgeSeconds; return true;
    }
    public void Clear() { until = double.NegativeInfinity; ManaRemainder = 0; }
    public sealed record Carried(double Remaining, int ManaRemainder, bool HardMode, int HitSource, int Threshold);
    // The local model freezes duration through loading; totems are deliberately not carried.
    public Carried Carry(double now)
    {
        var carried = Capture(now);
        Clear(); return carried;
    }
    public Carried Capture(double now) => new(Math.Max(0, until - now), ManaRemainder, hardMode, hitSource, threshold);
    internal void RestoreOwned(Carried value, double now)
    { enabled = true; hardMode = value.HardMode; hitSource = value.HitSource; threshold = value.Threshold; Restore(value, now); }
    public void Restore(Carried value, double now)
    {
        Clear();
        if (!enabled || hardMode != value.HardMode || hitSource != value.HitSource || threshold != value.Threshold) return;
        if (value.Remaining > 0) until = now + Math.Min(OrdinaryHierophantRules.SurgeSeconds, value.Remaining);
        ManaRemainder = threshold > 0 ? Math.Clamp(value.ManaRemainder, 0, threshold - 1) : 0;
    }
}
