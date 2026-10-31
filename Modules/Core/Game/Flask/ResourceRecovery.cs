static partial class Game
{
    internal static bool ManaFlaskCopiesLife(FlaskRecovery.Use recovery, IReadOnlyDictionary<uint, int> player) =>
        recovery.Mana > 0 && (recovery.ManaSeconds ?? recovery.Seconds) > 0
        && Stats.Key("non_instant_mana_recovery_from_flasks_also_recovers_life") is uint key && player.GetValueOrDefault(key) != 0;

    internal static void StartRecoveryEffectsLocked(ClientState conn, ResourceEffectsState owner,
        IEnumerable<Flasks.Effect> effects, bool copy)
    {
        foreach (var effect in effects)
        {
            conn.FlaskEffects.Start(effect);
            if (copy && effect.ManaPerSecond > 0) owner.LifeCopies.Add(effect);
        }
    }
    internal static (double Life, double Mana, List<Flasks.Effect> Ended) AdvanceFlaskRecovery(ClientState conn, double from, double to)
    {
        var owner = ResourceEffectsOf(conn);
        double copy = 0;
        // A synchronous observer; the queue retains no rule delegate. Only continuous earned Mana is eligible.
        var result = conn.FlaskEffects.Advance(from, to, (effect, seconds) =>
        {
            if (ResourceEffectsCurrent(conn, owner) && owner.LifeCopies.Contains(effect)) copy += effect.ManaPerSecond * seconds;
        });
        foreach (var ended in result.Ended) owner.LifeCopies.Remove(ended);
        return (result.Life + copy, result.Mana, result.Ended);
    }
}
