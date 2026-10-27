using GameServer.Core.World;

/// <summary>An ordinary bow's offensive inputs frozen at release. The actual target supplies evasion and impales
/// at contact. This preserves the ordinary attack's existing damage/crit/impale arithmetic.</summary>
sealed record OrdinaryBowShot(DamagePacket Hit, int Accuracy, int CriticalChance, int CriticalMultiplier, int ImpaleChance)
{
    public bool AlwaysHit { get; init; }
    public bool OnLowLife { get; init; }
    public int AccuracyMoreVsUniquePct { get; init; }
    public int AccuracyMoreInCloseRangePct { get; init; }
    public GamblerModifiers Rolls { get; init; }
    public IReadOnlyDictionary<uint, int> Character { get; init; } = new Dictionary<uint, int>();
    public IReadOnlyDictionary<uint, int> Skill { get; init; } = new Dictionary<uint, int>();
    public string? EffectId { get; init; }
    public Penetration.PerType Penetration { get; init; }
    public HitResistances.Mods ResistanceMods { get; init; }
    public static OrdinaryBowShot Capture(SkillCatalog.Unarmed weapon, IReadOnlyDictionary<uint, int> character,
        IEnumerable<(uint, int)> skillConversion, StatCatalog catalog, int accuracy, int criticalChance, int criticalMultiplier)
    {
        int Get(string name) => catalog.Key(name) is uint key ? character.GetValueOrDefault(key) : 0;
        var conversion = new Dictionary<uint, int>(character);
        var own = skillConversion.ToDictionary(s => s.Item1, s => s.Item2);
        foreach (var (key, value) in own) conversion.TryAdd(key, value);
        var hit = AttackSkillHit.BaseHit(weapon, character, catalog, melee: false)
            .Convert(DamageStats.Conversion(conversion, catalog, attack: true));
        return new(hit, accuracy, criticalChance, criticalMultiplier, Get("impale_on_hit_%_chance"))
        {
            Penetration = global::Penetration.Of(character, catalog, attack: true, own),
            ResistanceMods = new(Get("critical_strikes_ignore_elemental_resistances") | Own("critical_strikes_ignore_elemental_resistances"),
                Get("attack_critical_strikes_ignore_elemental_resistances") | Own("attack_critical_strikes_ignore_elemental_resistances"),
                Get("non_critical_strikes_penetrate_elemental_resistances_%") + Own("non_critical_strikes_penetrate_elemental_resistances_%"),
                Get("cannot_penetrate_or_ignore_elemental_resistances") | Own("cannot_penetrate_or_ignore_elemental_resistances")),
            AlwaysHit = Get("global_always_hit") != 0,
            Rolls = GamblerModifiers.From(Get),
            Character = new Dictionary<uint, int>(character),
            Skill = skillConversion.ToDictionary(s => s.Item1, s => s.Item2),
        };

        int Own(string name) => catalog.Key(name) is uint key ? own.GetValueOrDefault(key) : 0;
    }

    public int AccuracyAgainst(bool unique, double distance) => TargetAccuracy.AgainstTarget(Accuracy,
        AccuracyMoreVsUniquePct, AccuracyMoreInCloseRangePct, unique, TargetAccuracy.InCloseRange(distance));

    public bool Hits(int evasion, Random random, int? targetAccuracy = null) =>
        AlwaysHit || DamageModel.Hits(DamageModel.AccuracyHitChance(evasion, targetAccuracy ?? Accuracy), random.Next(100));

    public (int Damage, bool Critical) Roll(ImpaleState impale, Random random,
        Func<DamagePacket, bool, int>? mitigate = null, int? criticalMultiplier = null,
        int extraCriticalRolls = 0, Random? criticalRandom = null, bool cannotCrit = false)
    {
        if (mitigate is null)
        {
            var (hit, critical) = RollPacket(impale, random, criticalMultiplier,
                extraCriticalRolls != 0 || criticalRandom is not null || cannotCrit
                    ? chance => CriticalStrikePolicy.Roll(cannotCrit ? 0 : chance, criticalRandom ?? random, extraCriticalRolls) : null);
            return ((int)Math.Round(hit.MinTotal), critical);
        }
        // The explicit legacy mitigation callback receives the pre-critical roll, as on main.
        var packet = Hit;
        double physical = packet[DamageType.Physical].Max;
        double stored = impale.Bonus;
        if (stored > 0) packet = packet.Add(DamageType.Physical, new DamageRange(stored, stored));
        impale.AfterHit();
        if (ImpaleChance > 0 && random.Next(100) < ImpaleChance) impale.Apply(physical);
        var rolled = packet.Roll(random);
        bool crit = CriticalStrikePolicy.Roll(cannotCrit ? 0 : CriticalChance, criticalRandom ?? random, extraCriticalRolls);
        int damage = mitigate(rolled, crit);
        return (crit ? DamageModel.CriticalDamage(damage, criticalMultiplier ?? CriticalMultiplier) : damage, crit);
    }

    public (DamagePacket Hit, bool Critical) RollPacket(ImpaleState impale, Random random, int? multiplier = null, Func<int, bool>? criticalRoll = null)
    {
        var hit = Rolls.RollOutgoingHit(Hit, random);
        bool critical = criticalRoll?.Invoke(CriticalChance) ?? Rolls.IsCritical(CriticalChance, random, OnLowLife);
        if (critical) hit = hit.Critical(multiplier ?? CriticalMultiplier);
        // Match the ordinary melee path: reflection is added after outgoing/critical multipliers.
        double physical = hit[DamageType.Physical].Max;
        double stored = impale.Bonus;
        if (stored > 0) hit = hit.Add(DamageType.Physical, new DamageRange(stored, stored));
        impale.AfterHit();
        if (ImpaleChance > 0 && random.Next(100) < ImpaleChance) impale.Apply(physical);
        return (hit, critical);
    }
}
