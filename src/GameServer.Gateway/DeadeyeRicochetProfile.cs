using GameServer.Core.Projectiles;

/// <summary>Frozen release inputs. Never re-read owner sources during an in-flight contact.</summary>
static class DeadeyeRicochetProfile
{
    public const string TerrainChance = "projectile_chain_from_terrain_chance_%";
    public const string BarrageFlag = "projectiles_barrage";
    public const string NoSpread = "no_barrage_projectile_spread";

    public static ProjectileProfile Terrain(ProjectileProfile profile, Func<string, int> get) => profile with
        { TerrainChainChancePercent = Math.Clamp(get(TerrainChance), 0, 100) };

    public static bool IsBarrage(string effect, bool attack, Func<string, int> get) =>
        attack && (effect == "Barrage" || get(BarrageFlag) != 0);

    // Call after the ordinary count resolver, once. It preserves non-primary consumers and prohibits an
    // unsupported competing recipe rather than turning secondary clusters into a sequential player barrage.
    public static ProjectileProfile Barrage(ProjectileProfile profile, string effect, bool attack,
        Func<string, int> get, double attackSeconds)
    {
        if (!IsBarrage(effect, attack, get)) return profile;
        if (profile.CountConsumer != ProjectileCountConsumer.Primary || profile.Lobbed)
            throw new InvalidOperationException("Barrage requires a primary, non-lobbed projectile recipe.");
        if (!double.IsFinite(attackSeconds) || attackSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(attackSeconds));
        // Pinned data owns quantity/damage/time-per-projectile; unexposed animation split/spread use the
        // documented LocalExile policy: the existing contact delay then a half-action sequential span, 5 degree cone.
        double spread = get(NoSpread) != 0 ? 0 : Math.PI / 36
            * Math.Max(0, 1 + get("barrage_support_projectile_spread_+%") / 100d);
        return profile with { Barrage = true, RandomSpread = true, SpreadRadians = spread,
            AimSpreadDistance = 0, CircularSpread = false,
            SequentialDelay = Math.Max(1, ProjectileRuntime.Time(AttackSeconds(attackSeconds,
                profile with { Barrage = true }, get) * .5 / profile.Count)) };
    }

    public static double AttackSeconds(double ordinarySeconds, ProjectileProfile profile, Func<string, int> get) =>
        profile.Barrage ? ordinarySeconds * Math.Max(.01,
            1 + (long)get("support_barrage_attack_time_+%_per_projectile_fired") * profile.Count / 100d) : ordinarySeconds;
}
