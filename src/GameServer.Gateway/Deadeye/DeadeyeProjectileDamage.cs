/// <summary>
/// Release-frozen distance modifiers for projectile damage, evaluated at contact using cumulative travel.
/// This is the ordinary Deadeye stat, not the legacy player_far_shot attack-only modifier.
/// The host applies this only at contacts explicitly qualified by their damage recipe.
/// </summary>
readonly record struct DeadeyeProjectileDamage(int MaximumMorePercent, bool PointBlank)
{
    public const string FarShot = "deadeye_projectile_damage_+%_final_max_as_distance_travelled_increases";
    public const string PointBlankStat = "keystone_point_blank";

    public static DeadeyeProjectileDamage Capture(Func<string, int> get) =>
        new(Math.Max(0, get(FarShot)), get(PointBlankStat) != 0);

    // Pinned PoB 16de4b82d57f1c0de6eb40f37143c32d4da36a02 ModParser: {{35,0},{70,1}}.
    // Applies to projectile spell and attack hits and damaging ailments, with no close-range penalty.
    public double AilmentMultiplier(double distance) => 1 + MaximumMorePercent / 100d * Fraction(distance);

    // Point Blank applies only to projectile attack hits. Existing player_far_shot stays a separate multiplier.
    public double HitMultiplier(double distance, bool attack) =>
        AilmentMultiplier(distance) * (attack && PointBlank ? PointBlankMultiplier(distance) : 1);

    /// <summary>Scale the independent ailment basis once, never the already scaled/mitigated hit or its leech.</summary>
    public MonsterIgnites.Settings IgniteAt(MonsterIgnites.Settings release, double distance)
    {
        double more = AilmentMultiplier(distance);
        return release with
        {
            Min = release.Min * more, Max = release.Max * more,
            CriticalMin = release.CriticalMin * more, CriticalMax = release.CriticalMax * more
        };
    }

    static double Fraction(double distance)
    {
        Validate(distance);
        return Math.Clamp((distance - 35) / 35, 0, 1);
    }

    // Pinned PoB CalcOffence PointBlank DistanceRamp: {{10,1},{35,0},{120,-1}}, 30% more.
    static double PointBlankMultiplier(double distance) => distance <= 10 ? 1.3 : distance < 35
        ? 1 + .3 * (35 - distance) / 25 : distance < 120 ? 1 - .3 * (distance - 35) / 85 : .7;

    static void Validate(double distance)
    {
        if (!double.IsFinite(distance) || distance < 0) throw new ArgumentOutOfRangeException(nameof(distance));
    }
}
