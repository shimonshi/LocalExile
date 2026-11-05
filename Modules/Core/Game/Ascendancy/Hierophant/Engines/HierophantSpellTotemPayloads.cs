static partial class Game
{
    static SupportedHitInputs? HierophantTotemHitInputs(ClientState c, HierophantEngineActor actor, bool area)
    {
        var inputs = SupportedHitInputsFor(c, actor.Skill, area);
        if (inputs is null || actor.Totem is null) return inputs;
        var get = inputs.Get;
        // PrimarySupportSpellTotem's multiplier is not consumed by GemSupportDamage.
        // Scale the complete base/added packet once, including the damaging-ailment projection.
        // Totem increases join the increased bucket before conversion and more modifiers.
        return inputs with
        {
            Raw = inputs.Raw.Scale(Math.Max(0, 1 + get("support_totem_damage_+%_final") / 100d)),
            Get = n => n == "damage_+%" ? get(n) + get("totem_damage_+%") : get(n),
        };
    }
}
