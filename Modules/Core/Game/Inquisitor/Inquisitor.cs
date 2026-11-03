using GameServer.Core.World;

static partial class Game
{
    // Pure projection from an already-qualified final pool snapshot. No EquipmentStatSources publication,
    // lazy connection session, clock advancement or optional Phrecia module dependency.
    internal static int InquisitorProvidence(Func<string, int> sources, int finalStrength, int finalIntelligence) =>
        InquisitorOffense.RighteousProvidenceIncrease(sources, finalStrength, finalIntelligence);

    // Independent attack projection for consumers that do not already store Providence in their pooled increase.
    internal static int InquisitorAttackCritical(bool armed, int localCritical, long increased,
        int finalStrength, int finalIntelligence, Func<string, int> player, Func<string, int>? skill = null) =>
        AttackBaseCriticalStrike.Chance(armed, localCritical,
            increased + InquisitorProvidence(player, finalStrength, finalIntelligence), player, skill);

    internal static int InquisitorSpellCritical(int? intrinsic, int? mainHandLocalCritical,
        int finalStrength, int finalIntelligence, Func<string, int> player, Func<string, int>? skill = null,
        long? increased = null) => SpellCriticalStrike.Chance(
            InquisitorOffense.SpellBase(intrinsic, mainHandLocalCritical, player), player, skill,
            (increased ?? ((long)player("critical_strike_chance_+%") + player("spell_critical_strike_chance_+%")))
                + InquisitorProvidence(player, finalStrength, finalIntelligence));

    internal static DamagePacket InquisitorSpellPacket(DamagePacket intrinsic, SkillCatalog.Unarmed? mainHand,
        Func<string, int> sources, double effectiveness, DamageConversion? conversion = null) =>
        InquisitorOffense.SpellPacket(intrinsic, mainHand, sources, effectiveness, conversion);
}
