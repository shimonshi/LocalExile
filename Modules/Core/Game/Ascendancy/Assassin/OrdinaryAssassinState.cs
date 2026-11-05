using GameServer.Core.World;

/// <summary>Owner-only transient state; caller serializes using Monsters and supplies world time (pause-aware).</summary>
sealed class OrdinaryAssassinState
{
    readonly Queue<double> poisons = new();
    internal string? SourceIdentity;
    double elusiveAt = double.NegativeInfinity;
    double elusiveScale;
    // Local Elusive model: 15% avoid hits / 30% movement, loses 20 effect percentage points per second.
    // These base constants and nearby radius require coordinator/native acceptance; they are not PSG fields.
    internal double ElusiveEffect(double now) => Math.Max(0, elusiveScale - Math.Max(0, now - elusiveAt) * .2);
    internal int RecentPoisons(double now)
    {
        while (poisons.TryPeek(out double at) && at <= now - 4) poisons.Dequeue();
        return poisons.Count;
    }
    internal void Observe(double now, Func<string, int> get, bool alive)
    {
        if (!alive) { Clear(); return; }
        if (get(OrdinaryAssassinRules.RecentPoisonDuration) == 0) poisons.Clear();
        if (get("gain_elusive_on_crit_%_chance") <= 0) { elusiveScale = 0; elusiveAt = double.NegativeInfinity; }
        _ = RecentPoisons(now);
    }
    // Only call after MonsterPoisons.Add returned true. Failed rolls, immunity and misses do not count.
    internal void AppliedPoison(double now, Func<string, int> get)
    {
        _ = RecentPoisons(now);
        if (get(OrdinaryAssassinRules.RecentPoisonDuration) != 0) poisons.Enqueue(now);
    }
    internal bool CriticalHit(double now, Func<string, int> get, Random random)
    {
        if (ElusiveEffect(now) > 0 || !RandomRolls.Chance(get("gain_elusive_on_crit_%_chance"), 100, random)) return false;
        elusiveAt = now;
        elusiveScale = Math.Max(0, 1 + get("elusive_effect_+%") / 100d);
        return elusiveScale > 0;
    }
    internal bool NoExtraCriticalDamage(double now, Func<string, int> get) => ElusiveEffect(now) > 0
        && get("self_take_no_extra_damage_from_critical_strikes_while_affected_by_elusive") != 0;
    internal void Clear() { poisons.Clear(); elusiveAt = double.NegativeInfinity; elusiveScale = 0; }

    internal readonly record struct Carry(double ElusiveEffect, double[] PoisonAges);
    internal Carry Export(double now) => new(ElusiveEffect(now),
        poisons.Where(at => now - 4 < at && at <= now).Select(at => now - at).ToArray());
    internal void RestoreOwned(Carry carry, double now) => Import(carry, now, 0, _ => 1, true);
    internal void Import(Carry carry, double now, double elapsed, Func<string, int> get, bool alive)
    {
        Clear();
        if (!alive) return;
        double delay = double.IsFinite(elapsed) ? Math.Max(0, elapsed) : 0;
        elusiveAt = now;
        elusiveScale = Math.Max(0, carry.ElusiveEffect - delay * .2);
        foreach (double age in carry.PoisonAges.OrderDescending())
            if (double.IsFinite(age) && age >= 0 && age + delay < 4) poisons.Enqueue(now - age - delay);
        Observe(now, get, alive);
    }
}
