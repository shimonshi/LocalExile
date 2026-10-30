/// <summary>Ordinary incoming attack bleeding. Strongest instance, 5 second base duration, 70% physical base/s.
/// Unaffected suppresses damage without preventing/removing the ailment; immunity prevents and removes it.</summary>
sealed class PlayerBleeding
{
    sealed record Bleed(double From, EffectClock Clock, double PerSecond, Func<double>? SourceTaken = null)
    { public double Until => Clock.Deadline; }
    readonly List<Bleed> effects = [];
    double carry;
    public bool Active(double now) { lock (effects) return effects.Any(e => e.From <= now && e.Until > now); }
    public bool Read(double now, Func<string, int> get)
    {
        lock (effects)
        {
            if (Immune(get)) Clear();
            foreach (var effect in effects) effect.Clock.ChangeRate(now, EffectClock.RecipientRate(2, get));
            return Active(now);
        }
    }
    public void Clear() { lock (effects) { effects.Clear(); carry = 0; } }
    public bool Apply(double physicalBase, double now, double seconds, Func<string, int> get, Func<double>? sourceTaken = null)
    {
        lock (effects) return ApplyLocked(physicalBase, now, seconds, get, sourceTaken);
    }
    bool ApplyLocked(double physicalBase, double now, double seconds, Func<string, int> get, Func<double>? sourceTaken)
    {
        if (Immune(get) || physicalBase <= 0 || seconds <= 0) return false;
        effects.Add(new(now, new EffectClock(seconds, now, EffectClock.RecipientRate(2, get)), physicalBase * .7, sourceTaken));
        return true;
    }
    static bool Immune(Func<string, int> get) => get("immune_to_bleeding") != 0 || get("cannot_gain_bleeding") != 0;
    public int Advance(double from, double until, Func<string, int> get, bool unaffected, bool moving,
        int physicalReduction, double takenFactor)
    {
        lock (effects) return AdvanceLocked(from, until, get, unaffected, moving, physicalReduction, takenFactor);
    }
    int AdvanceLocked(double from, double until, Func<string, int> get, bool unaffected, bool moving,
        int physicalReduction, double takenFactor)
    {
        if (Immune(get)) { Clear(); return 0; }
        if (until <= from) return 0;
        foreach (var effect in effects) effect.Clock.ChangeRate(from, EffectClock.RecipientRate(2, get));
        double damage = carry;
        var boundaries = effects.SelectMany(e => new[] { e.From, e.Until }).Where(t => t > from && t < until)
            .Append(from).Append(until).Distinct().Order().ToArray();
        for (int i = 1; i < boundaries.Length; i++)
        {
            double at = (boundaries[i - 1] + boundaries[i]) / 2;
            double strongest = effects.Where(e => e.From <= at && e.Until > at)
                .Select(e => e.PerSecond * Math.Max(0, e.SourceTaken?.Invoke() ?? 1)).DefaultIfEmpty().Max();
            if (!unaffected) damage += strongest * (boundaries[i] - boundaries[i - 1])
                * (moving && get("no_extra_bleed_damage_while_moving") == 0 ? 3 : 1)
                * (1 - Math.Clamp(physicalReduction, 0, 90) / 100.0) * Math.Max(0, takenFactor);
        }
        effects.RemoveAll(e => e.Until <= until);
        int whole = (int)Math.Min(int.MaxValue, Math.Floor(damage + 1e-9));
        carry = Math.Max(0, damage - whole);
        return whole;
    }
}
