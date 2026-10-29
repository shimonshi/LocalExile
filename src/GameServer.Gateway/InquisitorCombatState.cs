/// <summary>Owner-local Inquisitor cast history and periodic Fanatic charges; callers hold their combat lock.</summary>
sealed class InquisitorCombatState
{
    public readonly record struct Inputs(int AttackMorePerSpell, int MaximumFanaticCharges,
        int FanaticChargeGainPerSecond, bool GainFanaticismAtMaximum, bool ConsumeFanaticCharges,
        double FanaticismSeconds)
    {
        public bool Zeal => MaximumFanaticCharges > 0 && FanaticChargeGainPerSecond > 0 && GainFanaticismAtMaximum;

        public static Inputs From(Func<string, int> get, double fanaticismSeconds) => new(
            Math.Max(0, get(InquisitorOffense.Virtue)), Math.Max(0, get("maximum_fanaticism_charges")),
            Math.Max(0, get("gain_x_fanatic_charges_every_second_if_have_attacked_in_past_second")),
            get("gain_fanaticism_for_4_seconds_on_reaching_maximum_fanatic_charges") != 0,
            get("lose_all_fanatic_charges_on_reaching_maximum_fanatic_charges") != 0,
            double.IsFinite(fanaticismSeconds) ? Math.Max(0, fanaticismSeconds) : 0);
    }

    public readonly record struct Snapshot(int AttackMorePct, int FanaticCharges, double FanaticismUntil)
    {
        public double AttackDamageFactor => 1 + AttackMorePct / 100.0;
        public bool Fanaticism(double now) => now < FanaticismUntil;
    }

    public sealed record Carried(double[] SpellAges, double? AttackAge, int FanaticCharges,
        double NextChargeDelay, double FanaticismRemaining);

    // The minimum positive integer source is 1%; thirty casts suffice even for a developer-replaced source.
    readonly Queue<double> spells = new();
    Inputs inputs;
    double time = double.NegativeInfinity, lastAttack = double.NegativeInfinity;
    double nextCharge = double.PositiveInfinity, fanaticismUntil = double.NegativeInfinity;
    int charges;
    bool retired;

    public void Configure(double now, Inputs value)
    {
        if (retired) return;
        now = Advance(now);
        if (value.AttackMorePerSpell <= 0) spells.Clear();
        if (!value.Zeal)
        {
            charges = 0;
            fanaticismUntil = lastAttack = double.NegativeInfinity;
            nextCharge = double.PositiveInfinity;
        }
        else if (!inputs.Zeal)
            nextCharge = now + 1;
        charges = Math.Min(charges, Math.Max(0, value.MaximumFanaticCharges));
        inputs = value;
    }

    public void Attack(double now, Inputs value)
    {
        Configure(now, value);
        if (!retired && inputs.Zeal) lastAttack = time;
    }

    // Call once for an accepted manual spell use. Instant spells and triggered spells do not count as casts.
    public void Spell(double now, Inputs value, bool instant, bool triggered = false)
    {
        Configure(now, value);
        if (retired || instant || triggered || inputs.AttackMorePerSpell <= 0) return;
        spells.Enqueue(time);
        while (spells.Count > 30) spells.Dequeue();
    }

    public Snapshot Read(double now, Inputs value)
    {
        Configure(now, value);
        if (retired) return default;
        int more = (int)Math.Min(30L, (long)inputs.AttackMorePerSpell * spells.Count);
        return new(more, charges, fanaticismUntil);
    }

    double Advance(double now)
    {
        if (!double.IsFinite(now)) throw new ArgumentOutOfRangeException(nameof(now));
        now = Math.Max(now, time);
        while (spells.TryPeek(out double cast) && cast <= now - 8) spells.Dequeue();
        if (inputs.Zeal && now >= nextCharge)
        {
            // Advance before recording the next attack, so a late read cannot award its attack to earlier ticks.
            // There can be at most one eligible one-second tick after the most recent observed attack.
            if (nextCharge <= lastAttack + 1)
            {
                charges = (int)Math.Min(inputs.MaximumFanaticCharges, (long)charges + inputs.FanaticChargeGainPerSecond);
                if (charges >= inputs.MaximumFanaticCharges)
                {
                    fanaticismUntil = nextCharge + inputs.FanaticismSeconds;
                    if (inputs.ConsumeFanaticCharges) charges = 0;
                }
            }
            nextCharge += Math.Floor(now - nextCharge) + 1;
        }
        time = now;
        return now;
    }

    public void Clear()
    {
        spells.Clear(); charges = 0;
        lastAttack = fanaticismUntil = time = double.NegativeInfinity;
        nextCharge = double.PositiveInfinity;
        inputs = default;
    }

    // Loading time is frozen. Retiring the source prevents a second ticket from copying the same transient state.
    public Carried Carry(double now, Inputs value)
    {
        Configure(now, value);
        if (retired) return new([], null, 0, 1, 0);
        var carried = new Carried(spells.Select(cast => time - cast).ToArray(),
            double.IsFinite(lastAttack) ? time - lastAttack : null, charges,
            double.IsFinite(nextCharge) ? Math.Max(0, nextCharge - time) : 1,
            Math.Max(0, fanaticismUntil - time));
        Clear(); retired = true;
        return carried;
    }

    public void Restore(Carried carried, double now, Inputs value)
    {
        Clear(); retired = false;
        Configure(now, value);
        if (value.AttackMorePerSpell > 0)
            foreach (double age in carried.SpellAges.Where(a => double.IsFinite(a) && a >= 0 && a < 8).TakeLast(30))
                spells.Enqueue(now - age);
        if (!value.Zeal) return;
        charges = Math.Clamp(carried.FanaticCharges, 0, value.MaximumFanaticCharges);
        lastAttack = carried.AttackAge is double attackAge && double.IsFinite(attackAge) && attackAge >= 0
            ? now - attackAge : double.NegativeInfinity;
        nextCharge = now + (double.IsFinite(carried.NextChargeDelay) ? Math.Clamp(carried.NextChargeDelay, 0, 1) : 1);
        fanaticismUntil = now + (double.IsFinite(carried.FanaticismRemaining)
            ? Math.Clamp(carried.FanaticismRemaining, 0, value.FanaticismSeconds) : 0);
    }
}
