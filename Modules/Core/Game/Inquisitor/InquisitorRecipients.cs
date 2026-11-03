using GameServer.Core.World;

/// <summary>Recipient effects only. FlaskGrounds remains the sole ground/owner-buff writer.</summary>
sealed class InquisitorRecipients
{
    public const string EnemyTaken = "consecrated_ground_enemy_damage_taken_+%";
    public const string Mirror = "consecrated_ground_allies_recover_es_as_well_as_life_from_life_regeneration";
    readonly Dictionary<uint, Recipient> enemies = [];
    readonly Recipient owner = new();

    sealed class Recipient
    {
        bool inside;
        double entered = double.PositiveInfinity, left = double.NegativeInfinity;
        public bool Active(double now, double linger) => inside || now < left + linger;
        public void Observe(double now, bool covered, double linger)
        {
            if (covered && !inside && !Active(now, linger)) entered = now;
            if (inside && !covered) left = now;
            inside = covered;
        }
        public double Seconds(double from, double to, double linger) =>
            Math.Max(0, Math.Min(to, inside ? double.PositiveInfinity : left + linger) - Math.Max(from, entered));
        public void Clear() { inside = false; entered = double.PositiveInfinity; left = double.NegativeInfinity; }
    }

    static double Linger(Func<string, int> get) => Math.Max(0, get(FlaskGrounds.LingerStat)) / 1000d;

    // Pass actual coverage from the existing ground, NOT OnConsecrated (which already includes linger).
    // Integrate the previous segment before each source/coverage/life transition. Death clears immediately.
    public void ObserveOwner(double now, bool alive, bool covered, Func<string, int> get)
    {
        if (!alive) { Clear(); return; }
        owner.Observe(now, covered, Linger(get));
    }

    public int ObserveEnemy(double now, uint id, bool livingHostile, bool covered, Func<string, int> get)
    {
        int taken = get(EnemyTaken);
        if (!livingHostile || taken == 0) { enemies.Remove(id); return 0; }
        double linger = Linger(get);
        if (!enemies.TryGetValue(id, out var recipient))
        {
            if (!covered) return 0;
            enemies[id] = recipient = new();
        }
        recipient.Observe(now, covered, linger);
        if (recipient.Active(now, linger)) return taken;
        enemies.Remove(id);
        return 0;
    }

    // Caller supplies the shared engine's actual Life regeneration before Life recovery-rate scaling,
    // including its exact temporary segments. Never feed Flask, Leech, Recoup or ES regeneration here.
    public double MirroredRecovery(double from, double to, double lifeRegenerationPerSecond, Func<string, int> get)
    {
        if (to <= from || get(Mirror) == 0 || get("no_life_regeneration") != 0
            || get("cannot_recover_energy_shield") != 0) return 0;
        return owner.Seconds(from, to, Linger(get)) * Math.Max(0, lifeRegenerationPerSecond)
            * Math.Max(0, 1 + get("energy_shield_recovery_rate_+%") / 100d);
    }

    public void Clear() { owner.Clear(); enemies.Clear(); }
}

static class InquisitorNearby
{
    // Passive attachment template, not a raw notable stat. Standard/Ruthless use different templates.
    public static int AuguryIncrease(bool allocated, bool ruthless, bool livingHostile, double distance) =>
        allocated && livingHostile && distance is >= 0 and <= 60 ? ruthless ? 8 : 16 : 0;

    // Return a bucket contribution, not a second multiplier. Hit and DoT consumers add it to existing INC taken.
    public static int TakenIncrease(DamageType type, int sanctuary, int augury) =>
        sanctuary + (DamageTypes.IsElemental(type) ? augury : 0);

    // This is a separate less multiplier, on typed incoming hit and supported DoT bases before critical ignite.
    public static double IncomingFactor(DamageType type, bool affected) =>
        affected && DamageTypes.IsElemental(type) ? .92 : 1;
}
