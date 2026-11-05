/// <summary>One action's independent critical rolls, reusing the existing lazy luck/action-sharing contract.</summary>
sealed class OrdinaryAssassinCriticalRoll(int extraRolls, bool bifurcate)
{
    readonly SkillCriticalRoll first = new(extraRolls);
    readonly SkillCriticalRoll second = new(extraRolls);

    // 0/1/2 successes; caller must retain the count rather than collapse twice-critical into a boolean.
    // Production execution uses SkillCriticalRoll.Successes and CriticalStrikeResult for the researched bonus.
    internal int Check(int chance, bool prohibited, Random random)
    {
        if (prohibited) return 0;
        int count = first.Check(chance, random) ? 1 : 0;
        return count + (bifurcate && second.Check(chance, random) ? 1 : 0);
    }
}
