static partial class Game
{
    internal static Func<string, int> CaptureAssassinCriticalSources(ClientState conn)
    {
        lock (conn.Monsters)
        {
            var raw = new Dictionary<uint, int>(CharacterPlayerStats(conn));
            bool selected = OrdinaryAssassinSelected(conn);
            return name => selected && Stats.Key(name) is uint k ? raw.GetValueOrDefault(k) : 0;
        }
    }

    // Guaranteed critical strikes bypass chance and bifurcation; prohibition wins first.
    // Reaching the ordinary chance cap by arithmetic is not a guaranteed-critical modifier.
    internal static CriticalStrikeResult ResolveOwnerCritical(ClientState conn, SpawnedMonster target,
        CriticalChanceValue chance, bool spell, IReadOnlyDictionary<uint, int>? own,
        SkillCriticalRoll? rolls, IReadOnlyDictionary<uint, int> player, Func<string, int>? captured = null)
    {
        if (CriticalProhibitedBy(player, !spell, own)) return new(0);
        if (AlwaysCritical(conn, n => StatOf(player, n))) return new(1);
        var get = captured ?? OrdinaryAssassinSources(conn, player);
        chance = chance.More(get(OrdinaryAssassinRules.LowLife(target.Life, target.MaxLife)
            ? OrdinaryAssassinRules.LowChance : OrdinaryAssassinRules.NotLowChance));
        if (spell) chance = chance.More(get(OrdinaryAssassinRules.SpellChance));
        rolls ??= new(CriticalExtraRollsFor(conn, player, own));
        return new(rolls.Successes(chance.Project(), conn.CriticalRandom,
            spell && get("spell_critical_strikes_bifurcate") != 0));
    }

    internal static int OwnerCriticalMultiplier(SpawnedMonster target, int ordinary,
        CriticalStrikeResult result, Func<string, int> get) => target.Mods.Effects.NoExtraCriticalDamage ? 100
            : OwnerSourceCriticalMultiplier(target, ordinary, result, get);

    internal static int OwnerSourceCriticalMultiplier(SpawnedMonster target, int ordinary,
        CriticalStrikeResult result, Func<string, int> get) => result.Multiplier(
            OrdinaryAssassinRules.CriticalMultiplier(ordinary, target.Life, target.MaxLife, get));

}
