using System.Collections.Immutable;

/// <summary>Pinned ordinary sources only. Callers supply mode-resolved stats, never normal plus Ruthless rows.</summary>
static class OrdinaryPathfinderRules
{
    internal const string Surgeon = "pathfinder_flask_life_to_recover_+%_final";
    internal const string Preserve = "flasks_%_chance_to_not_consume_charges";
    internal const string Dispel = "dispel_status_ailments_on_flask_use";
    internal const string DoublePoison = "pathfinder_poison_damage_+100%_final_chance_during_flask_effect";
    internal const string SpreadDuringFlask = "unique_spread_poison_to_nearby_enemies_during_flask_effect";
    internal const string SpreadAlways = "spread_poison_to_nearby_enemies_on_kill";
    internal const string DistillCost = "pathfinder_skills_consume_x_charges_from_a_bismuth_diamond_or_amethyst_flask";

    // Resolve an otherwise inert base source into the virtual duration bucket once, before final overrides.
    internal static (string Stat, int Value) PoisonDuration(Func<string, int> get) =>
        ("poison_duration_+%", (int)Math.Clamp((long)get("poison_duration_+%")
            + get("base_poison_duration_+%"), int.MinValue, int.MaxValue));

    internal static FlaskRecovery.Use Recovery(FlaskRecovery.Use recovery, Func<string, int> get)
    {
        double factor = Math.Max(0, 1 + get(Surgeon) / 100d);
        return recovery with { Life = recovery.Life * factor, InstantLife = recovery.InstantLife * factor,
            ExtraLife = recovery.ExtraLife * factor };
    }

    // Admission still requires the full ordinary use cost. Preservation does not make an empty flask usable.
    // Roll only once, inside the admitted transaction, never on a refused request or a Distiller payment.
    internal static int? UseCost(int held, int cost, Func<string, int> get, Func<int, int> roll)
    {
        cost = Math.Max(0, cost);
        if (held < cost) return null;
        int chance = Math.Clamp(get(Preserve), 0, 100);
        return cost > 0 && chance > 0 && roll(100) < chance ? 0 : cost;
    }

    internal static double PoisonDps(double rolledDps, bool flaskActive, Func<string, int> get, Func<int, int> roll)
    {
        int chance = Math.Clamp(get(DoublePoison), 0, 100);
        return rolledDps * (rolledDps > 0 && flaskActive && chance > 0 && roll(100) < chance ? 2 : 1);
    }

    internal static bool CanSpread(bool flaskActive, Func<string, int> get) =>
        get(SpreadAlways) > 0 || flaskActive && get(SpreadDuringFlask) > 0;

    internal enum Kind { Other, Bismuth, Diamond, Amethyst }
    internal readonly record struct ChargeSource(uint Id, byte Slot, Kind Kind, int Charges);
    internal readonly record struct PaymentWitness(uint Id, byte Slot, uint BaseHash, int Before, int After);
    internal sealed record Distillation(IReadOnlyList<(uint Id, int Cost)> Payments, IReadOnlyList<(string Stat, int Value)> Stats)
    {
        internal ImmutableArray<PaymentWitness> Witnesses { get; init; } = [];
        internal int Action { get; init; }
        internal int Epoch { get; init; }
        internal uint Mana { get; init; }
        internal uint Life { get; init; }
    }

    // Explicit server model: one eligible flask of EACH kind in bar order; caller resolves supported use admission.
    // The entire returned plan must commit atomically with skill admission, once per use, never once per target/repeat.
    internal static Distillation Distill(IEnumerable<ChargeSource> bar, bool eligibleSkill, Func<string, int> get)
    {
        List<(uint, int)> payments = [];
        List<(string, int)> stats = [];
        int cost = get(DistillCost);
        if (!eligibleSkill || cost <= 0) return new(payments.ToImmutableArray(), stats.ToImmutableArray());
        foreach (var group in bar.Where(f => f.Kind != Kind.Other && f.Charges >= cost).OrderBy(f => f.Slot).GroupBy(f => f.Kind))
        {
            var flask = group.First();
            payments.Add((flask.Id, cost));
            switch (flask.Kind)
            {
                case Kind.Bismuth:
                    int penetration = get("pathfinder_skills_penetrate_elemental_resistances_%_if_charges_consumed_from_bismuth_flask");
                    foreach (string element in new[] { "fire", "cold", "lightning" }) stats.Add(($"base_reduce_enemy_{element}_resistance_%", penetration));
                    break;
                case Kind.Diamond:
                    stats.Add(("critical_strike_chance_+%", get("pathfinder_skills_critical_strike_chance_+%_if_charges_consumed_from_diamond_flask")));
                    break;
                case Kind.Amethyst:
                    stats.Add(("physical_damage_%_to_add_as_chaos", get("pathfinder_skills_physical_damage_%_to_add_as_chaos_if_charges_consumed_from_amethyst_flask")));
                    break;
            }
        }
        return new(payments.ToImmutableArray(), stats.ToImmutableArray());
    }

    // Snapshot must come from the existing poison engine BEFORE lethal paths clear it. No summed-stack proliferation.
    internal readonly record struct PoisonSnapshot(double Dps, double Remaining);
    internal static bool Spread(MonsterPoisons recipient, PoisonSnapshot poison, double now, bool flaskActive,
        Func<string, int> get, bool immune, int avoid, Func<int, int> roll)
    {
        if (!CanSpread(flaskActive, get) || immune || !(poison.Dps > 0) || !(poison.Remaining > 0)) return false;
        if (avoid > 0 && roll(100) < Math.Clamp(avoid, 0, 100)) return false;
        // Already rolled and scaled at original application: no second Toxicist roll or attacker scaling.
        return recipient.Add(now, poison.Dps, poison.Remaining);
    }
}
