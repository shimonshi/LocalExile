using GameServer.Core.World;

/// <summary>
/// The player's poisons on one monster. Multiple poisons can stack indefinitely; each deals chaos damage concurrently
/// and maintains its own duration (default 2 s). Guarded by the connection's Monsters lock.
/// </summary>
sealed class MonsterPoisons
{
    double expiryRate = 1;
    public void SetExpiryRate(double now, double rate)
    {
        rate = Math.Max(.25, rate);
        for (int i = 0; i < active.Count; i++)
            active[i] = active[i] with { Until = RecipientEffectTime.RetimedDeadline(active[i].Until, now, expiryRate, rate) };
        expiryRate = rate;
    }
    /// <param name="WeaponChance">The attacking weapon's own local_poison_on_hit_% ("X% chance to Poison on Hit" on
    /// it), which the panel's chance counts for that hand's attacks.</param>
    public readonly record struct Settings(int Chance, double Min, double Max, double CriticalMin, double CriticalMax,
        double Seconds, int WeaponChance = 0)
    {
        public static readonly Settings None = new(0, 0, 0, 0, 0, 0);

        /// <summary><paramref name="local"/>: the attacking weapon's local stats (none for a spell).</summary>
        public static Settings From(IEnumerable<(string Stat, int Value)> rows, string hand, Func<string, int> get,
            IReadOnlyDictionary<string, int>? local = null)
        {
            if (Prohibited(get)) return None;
            var r = rows.ToDictionary(x => x.Stat, x => x.Value);
            double faster = Faster(get);
            if (faster <= 0) return None;
            double Row(string bound, string kind) =>
                r.GetValueOrDefault($"{bound}_poison_damage_to_deal_per_minute_from_{kind}_{hand}_damage") / 60.0 * faster;
            long chance = (long)get("base_chance_to_poison_on_hit_%") + get("chaos_damage_chance_to_poison_%");
            double seconds = Ailments.PoisonDurationSeconds
                * Math.Max(0, 1 + ((double)get("poison_duration_+%") + get("base_all_ailment_duration_+%")) / 100) / faster
                * MonsterIgnites.DurationMultiplier(get);
            return new((int)Math.Clamp(chance, 0, 100), Row("minimum", "non_critical"), Row("maximum", "non_critical"),
                Row("minimum", "critical"), Row("maximum", "critical"), seconds,
                local?.GetValueOrDefault("local_poison_on_hit_%") ?? 0);
        }
    }

    /// <summary>Whether the attacker cannot poison: poison deals chaos damage, so a chaos prohibition stops it even from
    /// physical damage (PoB CalcOffence gates poison on <c>canDeal.Chaos</c>).</summary>
    public static bool Prohibited(Func<string, int> get) =>
        get("cannot_poison_enemies") != 0 || get("never_damaging_ailments") != 0
        || DamageStats.Prohibited(DamageType.Chaos, get);

    public static double Faster(Func<string, int> get) =>
        Math.Max(0, 1 + ((double)get("faster_poison_%") + get("damaging_ailments_deal_damage_+%_faster")) / 100.0);

    // Native combined_poison_duration_on_self resolvers (142808790 / 142808e20).
    // Read raw ingredients once: published combined aliases are not additional duration sources.
    public static double SelfDuration(Func<string, int> target)
    {
        double increased = (double)target("self_poison_duration_+%")
            + target("base_all_ailment_duration_on_self_+%") + target("self_damaging_ailment_duration_+%")
            + (double)target("self_damaging_ailment_duration_+%_per_barkskin_stack") * target("current_barkskin_stacks")
            + (double)target("duration_of_ailments_on_self_+%_per_fortification") * target("current_fortification");
        // Preserve binary32 percentage-unit aggregation and signed half rounding before applying the result.
        float more = Factor(target("self_poison_duration_+%_final")) * 100f;
        more *= Factor(target("pantheon_shakari_self_poison_duration_+%_final"));
        more *= target("keystone_lethe_shade") != 0 ? 2f : 1f;
        float percent = more - 100f;
        double rounded = Math.Truncate((double)(percent < 0 ? percent - .5f : percent + .5f));
        int morePercent = (int)Math.Clamp(rounded, int.MinValue, int.MaxValue);
        return Math.Max(0, 1 + increased / 100) * Math.Max(0, 1 + morePercent / 100d);

        static float Factor(int percent) => Math.Max(0, (100f + percent) / 100f);
    }

    readonly record struct Poison(double From, double Until, double DamagePerSecond, bool PlayerOwned = true);
    readonly List<Poison> active = [];
    double last, carry;

    /// <summary>The native <c>poison</c> buff instance on the monster (BuffDefinitions row 10).</summary>
    public ushort? Buff;

    public bool Poisoned => active.Count > 0;

    public bool Idle => active.Count == 0 && Buff is null;

    public double Until => active.Count == 0 ? 0 : active.Max(p => p.Until);

    public int Stacks(double now) => active.Count(p => p.From <= now && now < p.Until);

    public readonly record struct Snapshot(int Stacks, double StrongestDps, double Remaining);

    public Snapshot Capture(double now)
    {
        var live = active.Where(p => p.From <= now && now < p.Until).ToArray();
        if (live.Length == 0) return default;
        var strongest = live.OrderByDescending(p => p.DamagePerSecond).ThenByDescending(p => p.Until).First();
        return new(live.Length, strongest.DamagePerSecond, strongest.Until - now);
    }

    public double TotalDps(double now) =>
        active.Where(p => p.From <= now && now < p.Until).Sum(p => p.DamagePerSecond);

    public bool TryApply(Settings settings, bool critical, double now, int avoid, bool immune, Random random)
    {
        if (immune || settings.Seconds <= 0 || !RandomRolls.Chance(settings.Chance, 100, random)
            || avoid > 0 && RandomRolls.Chance(avoid, 100, random)) return false;
        var (min, max) = critical ? (settings.CriticalMin, settings.CriticalMax) : (settings.Min, settings.Max);
        double dps = min + (Math.Max(min, max) - min) * random.NextDouble();
        return Add(now, dps, settings.Seconds);
    }

    public bool Add(double now, double perSecond, double seconds, bool playerOwned = true)
    {
        if (!(perSecond > 0) || !(seconds > 0)) return false;
        if (active.Count == 0) { last = now; carry = 0; }
        active.Add(new(now, now + seconds / expiryRate, perSecond, playerOwned));
        return true;
    }

    public bool LastTickPlayerOwned { get; private set; } = true;

    /// <summary>Sums the concurrent chaos damage of all active poison stacks from last advance to <paramref name="until"/>,
    /// multiplied by the monster's chaos mitigation <paramref name="factor"/>.</summary>
    public int Advance(double until, double factor)
    {
        if (active.Count == 0 || !(until > last)) return 0;
        var cuts = active.SelectMany(p => new[] { p.From, p.Until }).Where(t => last < t && t < until)
            .Append(last).Append(until).Distinct().Order().ToArray();
        double damage = carry; LastTickPlayerOwned = false;
        for (int k = 1; k < cuts.Length; k++)
        {
            double mid = (cuts[k - 1] + cuts[k]) / 2;
            LastTickPlayerOwned |= active.Any(p => p.From <= mid && mid < p.Until && p.PlayerOwned);
            double sumDps = active.Where(p => p.From <= mid && mid < p.Until).Sum(p => p.DamagePerSecond);
            damage += sumDps * Math.Max(0, factor) * (cuts[k] - cuts[k - 1]);
        }
        last = until;
        active.RemoveAll(p => p.Until <= until);
        double finalDamage = Math.Max(0, damage);
        // At final expiry, endpoint subtraction and accumulated carry can leave a whole point just below its boundary.
        // Use the incoming ailments' subnanounit tolerance only here; live ticks retain their ordinary floor/carry.
        if (active.Count == 0 && Math.Ceiling(finalDamage) - finalDamage <= 1e-9)
            finalDamage = Math.Ceiling(finalDamage);
        int whole = (int)Math.Min(int.MaxValue, Math.Floor(finalDamage));
        carry = active.Count == 0 ? 0 : damage - whole;
        return whole;
    }

    public void Clear() { DiedPoisoned |= Poisoned; active.Clear(); carry = 0; }

    public bool DiedPoisoned { get; private set; }
}
