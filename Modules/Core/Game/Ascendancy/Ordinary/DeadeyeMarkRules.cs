/// <summary>One owner-local mark with target identity and generation. Values are qualified gem/caster inputs.</summary>
sealed class DeadeyeMarkRules
{
    public const string NearbyTaken = "damage_taken_+%_final_from_enemies_near_marked_enemy";
    public const string MarkedTaken = "deadeye_damage_taken_+%_final_from_marked_enemy";
    public sealed record Mark(uint Target, long TargetGeneration, string SkillSignature,
        OrdinaryEffectClock Clock, long Revision);
    public Mark? Active { get; private set; }
    long revision;

    public Mark Apply(double now, uint target, long targetGeneration, string signature,
        double duration, Func<string, int> recipient)
    {
        if (double.IsNaN(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(duration));
        return Active = new(target, targetGeneration, signature,
            new(duration, now, OrdinaryEffectClock.RecipientRate(14, recipient)), ++revision);
    }

    public Mark? Current(double now, uint target, long targetGeneration, bool sourceCurrent,
        Func<string, int> recipient)
    {
        if (Active is not { } mark) return null;
        mark.Clock.ChangeRate(now, OrdinaryEffectClock.RecipientRate(14, recipient));
        if (!sourceCurrent || mark.Clock.Remaining <= 0) { Clear(); return null; }
        return mark.Target == target && mark.TargetGeneration == targetGeneration ? mark : null;
    }

    // Standard excludes the marked attacker. Ruthless's different raw stat is consumed independently;
    // neither mode inherits the other's values. Caller has already validated a living hostile mark recipient.
    public static double Incoming(Func<string, int> qualified, bool attackerIsMarked,
        double distanceFromMark, bool ruthless) =>
        Math.Max(0, 1 + (!attackerIsMarked && distanceFromMark <= 60 ? qualified(NearbyTaken) : 0) / 100d)
        * Math.Max(0, 1 + (ruthless && attackerIsMarked ? qualified(MarkedTaken) : 0) / 100d);

    public static int Amount(int gemValue, Func<string, int> caster, Func<string, int> skill) =>
        (int)Math.Clamp(Math.Truncate(gemValue * Math.Max(0, 1 + ((long)caster("curse_effect_+%")
            + caster("mark_skills_curse_effect_+%") + skill("mark_skills_curse_effect_+%")) / 100d)),
            int.MinValue, int.MaxValue);

    // Transfer preserves the original effect clock and does not repeat payment or reset a finite duration.
    public bool Transfer(uint dead, long deadGeneration, uint next, long nextGeneration)
    {
        if (Active is not { } mark || mark.Target != dead || mark.TargetGeneration != deadGeneration) return false;
        Active = mark with { Target = next, TargetGeneration = nextGeneration, Revision = ++revision };
        return true;
    }
    public void Clear() { Active = null; ++revision; }
}
