using System.Numerics;

/// <summary>Unrounded, uncapped permyriad chance. Project only after all contact conditions.</summary>
readonly record struct CriticalChanceValue(BigInteger Numerator, BigInteger Denominator)
{
    public int Project() => Denominator <= 0 ? 0 : (int)BigInteger.Clamp(
        (Numerator + Denominator / 2) / Denominator, 0, 10_000);
    public CriticalChanceValue More(int percent) => new(Numerator * BigInteger.Max(0, (BigInteger)100 + percent),
        Denominator * 100);
    public static implicit operator CriticalChanceValue(int value) => new(value, 1);
    public static implicit operator int(CriticalChanceValue value) => value.Project();
}

/// <summary>Critical status is independent of extra damage and is admitted once, even with two successes.</summary>
readonly record struct CriticalStrikeResult(int Successes)
{
    public bool Critical => Successes > 0;
    public int Multiplier(int ordinary, bool noExtra = false) => noExtra ? 100 : Successes < 2 ? ordinary
        : (int)Math.Clamp(100L + 2L * (ordinary - 100L), 0, int.MaxValue);
}
