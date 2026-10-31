using System.Numerics;

/// <summary>Avidity is a MORE bucket after unrounded flat/increased inputs and before the only floor/replacement.</summary>
static class DeadeyeAvidityAccuracy
{
    public const string Stat = "deadeye_accuracy_rating_+%_final_per_frenzy_charge";

    public static int Calculate(long basis, long increased, int perFrenzy, int standardFrenzy, int? replacement = null)
    {
        if (replacement is int final) return Math.Max(0, final);
        BigInteger more = BigInteger.Max(0, 100 + (BigInteger)perFrenzy * Math.Max(0, standardFrenzy));
        return (int)BigInteger.Min(int.MaxValue, BigInteger.Max(0, basis)
            * BigInteger.Max(0, 100 + (BigInteger)increased) * more / 10000);
    }

    public static int StandardFrenzy(ChargeSession charges) =>
        charges.Converted(ChargeType.Frenzy) ? 0 : charges.Effective(ChargeType.Frenzy);

    public static int Selected(CharacterStats.Pools pools, WeaponProfile weapon, Func<string, long> own,
        int perFrenzy, int standardFrenzy, int? replacement = null)
    {
        if (replacement is int final) return Math.Max(0, final);
        long flat = AccuracyRating.Flat(weapon, own, includeLocal: false);
        long increased = AccuracyRating.Increased(weapon, own);
        // The character pool already contains Avidity. Recompute its unrounded basis only for changed skill inputs.
        return flat == 0 && increased == 0 ? pools.Accuracy
            : Calculate(pools.AccuracyBase + flat, pools.AccuracyPct + increased, perFrenzy, standardFrenzy);
    }
}
