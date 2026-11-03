/// <summary>
/// Scalar consumers of ordinary Slayer's pinned 3.29.3b sources. Callers supply resolved raw sources and actual
/// contact/leech context. These rules neither grant resources nor replace the common leech or charge engines.
/// </summary>
static class SlayerRules
{
    public const string OverkillLeech = "life_leech_on_overkill_damage_%";
    public const string NearbyCriticalMultiplier = "slayer_critical_strike_multiplier_+_per_nearby_enemy_up_to_100";
    public const string EnduranceEqualsFrenzy = "maximum_endurance_charges_is_equal_to_maximum_frenzy_charges";
    public const string RecentKillArea = "slayer_area_of_effect_+%_per_enemy_killed_recently_up_to_50%";
    public const string UniqueDamage = "slayer_damage_+%_final_against_unique_enemies";
    public const string RecentKillDamage = "damage_+%_if_enemy_killed_recently_final";
    public const string KillMovementSpeed = "gain_movement_speed_+%_for_20_seconds_on_kill";
    public const string RareKillAttackSpeed = "gain_attack_speed_+%_for_20_seconds_on_killing_rare_or_unique_enemy";

    /// <summary>
    /// Nominal Life leech from the excess of a lethal direct hit over the target's pre-hit Life.
    /// Pass post-mitigation direct damage before impale/reflection/cull; spells and attacks both qualify.
    /// Add to ordinary Life leech BEFORE one shared LeechSession.Hit call, so the instance cap, instant fraction,
    /// Ghost Reaver, target prohibitions and full-pool rules apply to their combined amount once.
    /// The caller excludes staged/invulnerable enemies and non-player damage.
    /// </summary>
    public static double OverkillLifeLeech(int directDamage, uint lifeBeforeHit, Func<string, int> get) =>
        lifeBeforeHit == 0 ? 0 : Math.Max(0L, (long)directDamage - lifeBeforeHit) * Math.Max(0, get(OverkillLeech)) / 100.0;

    /// <summary>Two independent MORE buckets. Unique-target damage must be resolved per actual target.</summary>
    public static double DamageMultiplier(Func<string, int> get, bool uniqueEnemy, bool killedRecently) =>
        More(uniqueEnemy ? get(UniqueDamage) : 0) * More(killedRecently ? get(RecentKillDamage) : 0);

    /// <summary>
    /// Bonus critical multiplier points, not increased critical chance or a damage multiplier.
    /// The caller counts actual nearby hostile, living enemies; this rule does not invent a nearby radius.
    /// </summary>
    public static int CriticalMultiplierBonus(Func<string, int> get, int nearbyEnemies) =>
        CappedProduct(get(NearbyCriticalMultiplier), nearbyEnemies, 100);

    public static int RecentKillAreaBonus(Func<string, int> get, int recentKills) =>
        CappedProduct(get(RecentKillArea), recentKills, 50);

    // Template 39, buff 1343: admission comes from the qualified passive buff template, not a similar stat.
    // Standard has this template; Ruthless does not. Apply to the enemy's source critical multiplier, once.
    public static int EnemyCriticalMultiplierDelta(bool hasTemplate39, bool livingHostile, double distance) =>
        hasTemplate39 && livingHostile && distance >= 0 && distance <= 60 ? -30 : 0;

    /// <summary>
    /// Apply AFTER completing the standard maxima and any explicit Frenzy replacement, BEFORE special mirrors,
    /// minimum clamping and state reconciliation. The flag replaces Endurance's own additive maximum.
    /// An explicit final Endurance replacement is the caller's last override, as in CharacterPlayerStats.
    /// </summary>
    public static int EnduranceMaximum(Func<string, int> get, int completedEndurance, int completedFrenzy) =>
        Math.Max(0, get(EnduranceEqualsFrenzy) != 0 ? completedFrenzy : completedEndurance);

    public readonly record struct LeechConditions(int AttackSpeedIncrease, int DamageTakenIncrease,
        bool CannotBeStunned, bool UnaffectedByBleeding);

    public static LeechConditions WhileLeeching(Func<string, int> get, LeechSession leech, double now) =>
        WhileLeeching(get, leech.Life.Active(now), leech.Mana.Active(now), leech.EnergyShield.Active(now));

    /// <summary>
    /// "While leeching" accepts any active Life/Mana/ES instance, including retained Life instances at full Life.
    /// DamageTakenIncrease joins the existing INCREASED damage-taken bucket; -10 is not a separate LESS multiplier.
    /// Bleeding remains present while unaffected; this is not ailment immunity, prevention or removal.
    /// </summary>
    public static LeechConditions WhileLeeching(Func<string, int> get, bool life, bool mana, bool energyShield) =>
        life || mana || energyShield
            ? new(get("attack_speed_+%_while_leeching"), get("damage_taken_+%_while_leeching"),
                get("cannot_be_stunned_while_leeching") != 0, get("unaffected_by_bleeding_while_leeching") != 0)
            : default;

    static double More(int value) => Math.Max(0, 100.0 + value) / 100;
    static int CappedProduct(int coefficient, int count, int cap) =>
        (int)Math.Clamp((long)coefficient * Math.Max(0, count), 0, cap);
}
