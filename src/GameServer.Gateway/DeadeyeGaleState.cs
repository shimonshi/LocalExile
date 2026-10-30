/// <summary>Owner-local Gale Force. All calls run under the host combat lock on pausable world seconds.</summary>
sealed class DeadeyeGaleState
{
    public const int Maximum = 10, GaleCategory = 18;
    public const double Duration = 4;
    public const string EffectPerStack = "tailwind_effect_on_self_+%_per_gale_force";
    public const string TakenPerStack = "damage_taken_+%_final_per_gale_force";
    public const string LoseOnHit = "lose_all_gale_force_when_hit";
    readonly List<EffectClock> stacks = [];
    bool source, suspended, retired;
    double rate = 1;
    int effect, taken;
    bool lose;
    public long Revision { get; private set; }
    public bool Tailwind => source && !suspended && !retired;
    public int Count => Tailwind ? stacks.Count : 0;
    public int TailwindEffect => Count * effect;
    public double DamageTakenFactor => Math.Max(0, 1 + (long)Count * taken / 100d);
    public IReadOnlyList<EffectClock> Stacks => stacks;

    public void Observe(double now, bool gatheringWinds, Func<string, int> get, bool alive = true)
    {
        Advance(now);
        if (retired) return;
        int oldEffect = TailwindEffect; double oldTaken = DamageTakenFactor, oldRate = rate;
        bool oldSource = source;
        source = gatheringWinds && alive;
        effect = get(EffectPerStack); taken = get(TakenPerStack); lose = get(LoseOnHit) != 0;
        rate = EffectClock.RecipientRate(GaleCategory, get);
        foreach (var stack in stacks) stack.ChangeRate(now, suspended ? 0 : rate);
        if (!source) stacks.Clear();
        if (oldSource != source || oldEffect != TailwindEffect || oldTaken != DamageTakenFactor
            || oldRate != rate && stacks.Count > 0) Revision++;
    }

    public void Advance(double now)
    {
        foreach (var stack in stacks) stack.Advance(now);
        if (stacks.RemoveAll(s => s.Remaining <= 0 || now >= s.Deadline) > 0) Revision++;
    }

    // Call exactly once at successful owner skill admission, after payment/cooldown validation.
    // Attacks/casts/instant skills count, repeats/triggered/owned actors and rejected requests do not.
    public bool AdmitUse(double now, bool accepted, bool ownerUse = true)
    {
        Advance(now);
        if (!accepted || !ownerUse || !Tailwind) return false;
        if (stacks.Count == Maximum) stacks.RemoveAt(0); // Explicit policy: newest ten independent grants survive.
        stacks.Add(new(Duration, now, rate)); Revision++;
        return true;
    }

    // Capture/mitigate with this factor before committing loss. Blocked hits still count; evasion never does.
    public double IncomingHit(double now, bool accepted)
    {
        Advance(now);
        double factor = DamageTakenFactor;
        if (accepted && lose && Count > 0) { stacks.Clear(); Revision++; }
        return factor;
    }

    // A transfer freezes remaining effect time during loading. Failure resumes the same owner state.
    // Destination restores only after ticket acceptance; relog starts with a new empty state.
    public double[] Suspend(double now)
    {
        Advance(now);
        if (retired) return [];
        suspended = true;
        foreach (var stack in stacks) stack.ChangeRate(now, 0);
        Revision++;
        return stacks.Select(s => s.Remaining).ToArray();
    }

    public void Resume(double now)
    {
        if (retired || !suspended) return;
        suspended = false;
        foreach (var stack in stacks) stack.ChangeRate(now, rate);
        Revision++;
    }

    public void Restore(double now, IEnumerable<double> remaining)
    {
        if (!Tailwind) return;
        stacks.Clear();
        foreach (double duration in remaining.Where(s => double.IsFinite(s) && s > 0).TakeLast(Maximum))
            stacks.Add(new(Math.Min(Duration, duration), now, rate));
        Revision++;
    }

    public void Clear(bool retire = false)
    {
        stacks.Clear(); source = false; suspended = false; retired |= retire; Revision++;
    }
}
