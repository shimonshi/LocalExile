/// <summary>
/// Ordinary Slayer's owner-local kill windows, measured on the caller's monotonic pausable game clock.
/// Record only a synchronous, once-only credited player kill. All access requires the owner's combat lock.
/// Persistence, transfer policy and stat publication remain with the Gateway lifecycle.
/// </summary>
sealed class SlayerKillState
{
    public const double RecentlySeconds = 4;
    public const double HeadsmanSeconds = 20;
    readonly Queue<double> kills = new();
    EffectClock? movement, attack;
    double rate = 1;
    long nextGrant, movementGrant, attackGrant;

    // Immutable presentation data, read under the same owner combat lock as RecordKill/Read.
    // Identity changes on refresh/restore even if the +10 stat and recipient rate did not change.
    public readonly record struct BuffGrant(long Identity, int Value, double Remaining, double Deadline, double Rate);
    public readonly record struct BuffSnapshot(BuffGrant? Movement, BuffGrant? Attack);

    public BuffSnapshot ReadBuffs(double now, Func<string, int> get)
    {
        ValidateTime(now);
        Trim(now, get);
        BuffGrant? Grant(EffectClock? clock, long identity, string source) => Remaining(clock, now) > 0
            ? new(identity, get(source), clock!.Remaining, clock.Deadline, clock.Rate) : null;
        return new(Grant(movement, movementGrant, SlayerRules.KillMovementSpeed),
            Grant(attack, attackGrant, SlayerRules.RareKillAttackSpeed));
    }

    public sealed record Carried(double[] Kills, double Movement, double Attack, double Rate);

    public Carried Carry(double now) => new(kills.Select(t => Math.Max(0, t - now)).Where(t => t > 0).ToArray(),
        Remaining(movement, now), Remaining(attack, now), rate);

    static double Remaining(EffectClock? clock, double now) { clock?.Advance(now); return clock?.Remaining ?? 0; }

    public void Restore(Carried value, double now)
    {
        Clear();
        rate = value.Rate;
        foreach (double seconds in value.Kills) kills.Enqueue(now + seconds);
        movement = new(value.Movement, now, rate);
        attack = new(value.Attack, now, rate);
        movementGrant = ++nextGrant;
        attackGrant = ++nextGrant;
    }

    // BuffDefinitions rows 1341/1342 use category 18, not category 1.
    // Recent kill history is a world-time condition, not a recipient buff.
    void ObserveRate(double now, Func<string, int> get)
    {
        rate = EffectClock.RecipientRate(18, get);
        movement?.ChangeRate(now, rate);
        attack?.ChangeRate(now, rate);
    }

    public readonly record struct Snapshot(int RecentKills, int MovementSpeedIncrease, int AttackSpeedIncrease,
        int AreaIncrease)
    {
        public bool KilledRecently => RecentKills > 0;
    }

    public void RecordKill(double now, bool rareOrUnique, Func<string, int> get)
    {
        ValidateTime(now);
        Trim(now, get);
        kills.Enqueue(now + RecentlySeconds);
        // These are buffs gained while the source is held, not a retrospective test of "killed recently".
        if (get(SlayerRules.KillMovementSpeed) != 0)
        { movement = new(HeadsmanSeconds, now, rate); movementGrant = ++nextGrant; }
        if (rareOrUnique && get(SlayerRules.RareKillAttackSpeed) != 0)
        { attack = new(HeadsmanSeconds, now, rate); attackGrant = ++nextGrant; }
    }

    public Snapshot Read(double now, Func<string, int> get)
    {
        ValidateTime(now);
        Trim(now, get);
        return new(kills.Count, Remaining(movement, now) > 0 ? get(SlayerRules.KillMovementSpeed) : 0,
            Remaining(attack, now) > 0 ? get(SlayerRules.RareKillAttackSpeed) : 0,
            SlayerRules.RecentKillAreaBonus(get, kills.Count));
    }

    public void Clear()
    {
        kills.Clear();
        movement = attack = null;
        rate = 1;
    }

    void Trim(double now, Func<string, int> get)
    {
        ObserveRate(now, get);
        while (kills.TryPeek(out double expires) && expires <= now) kills.Dequeue();
        // A paid refund clears the buff even if the same node is reallocated before its old deadline.
        if (get(SlayerRules.KillMovementSpeed) == 0) movement = null;
        if (get(SlayerRules.RareKillAttackSpeed) == 0) attack = null;
    }

    static void ValidateTime(double now)
    {
        if (!double.IsFinite(now)) throw new ArgumentOutOfRangeException(nameof(now));
    }
}
