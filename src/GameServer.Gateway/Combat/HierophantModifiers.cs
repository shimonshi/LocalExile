/// <summary>Pinned ordinary source rows, frozen by the Game snapshot before hit/ailment construction.</summary>
static class HierophantModifiers
{
    public static double Damage(Func<string, int> get, bool spell, bool ailment = false) =>
        Math.Max(0, 1 + get("hierophant_passive_damage_+%_final_per_totem")
            * (double)Math.Max(0, get("number_of_active_totems")) / 100)
        * (spell && !ailment && get("has_arcane_surge") != 0
            ? Math.Max(0, 1 + Math.Truncate(get("arcane_surge_on_you_spell_damage_+%_final_from_hierophant")
                * Math.Max(0, 100d + get("arcane_surge_effect_+%")) / 100) / 100) : 1);
}
