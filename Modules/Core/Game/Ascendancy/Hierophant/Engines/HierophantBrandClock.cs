/// <summary>Separate cumulative attached/detached budgets on the resource world's clock. No wall timers.</summary>
sealed class HierophantBrandClock(double now, double attached, double detached)
{
    public double AttachedLimit { get; } = attached;
    public double DetachedLimit { get; } = detached;
    public double AttachedUsed { get; private set; }
    public double DetachedUsed { get; private set; }
    public double Last { get; private set; } = now;
    public double Progress { get; private set; }
    public bool Alive => AttachedUsed < AttachedLimit && DetachedUsed < DetachedLimit;
    // Integrate through the last-20% boundary rather than changing the entire previous frame's rate.
    public int Advance(double now, bool attached, double interval, double rate, int lateMore)
    {
        if (!double.IsFinite(now) || now < Last) throw new ArgumentOutOfRangeException(nameof(now));
        double dt = now - Last; Last = now;
        if (!Alive) return 0;
        if (!attached) { DetachedUsed = Math.Min(DetachedLimit, DetachedUsed + dt); return 0; }
        dt = Math.Min(dt, AttachedLimit - AttachedUsed);
        double early = Math.Min(dt, Math.Max(0, AttachedLimit * .8 - AttachedUsed));
        double late = dt - early;
        AttachedUsed += dt;
        if (interval <= 0 || !double.IsFinite(interval) || rate <= 0) return 0;
        Progress += (early + late * Math.Max(0, 1 + lateMore / 100d)) * rate / interval;
        // Local policy: retire at the exact budget boundary, with no expiry-frame activation.
        int hits = (int)Math.Min(1024, Math.Floor(Progress + 1e-9));
        Progress -= hits;
        return Alive ? hits : 0;
    }
}
