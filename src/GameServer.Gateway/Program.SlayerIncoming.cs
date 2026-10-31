using GameServer.Core.World;

partial class Program
{
    internal static void BleedOnMonsterHitLocked(ClientState conn, SpawnedMonster monster, MonsterAttack attack,
        bool critical, bool blocked, double now, IReadOnlyDictionary<uint, int> stats, uint sourceId = 0)
    {
        if (blocked || conn.PlayerLife == 0 || conn.Closed || conn.LeavingArea || InGracePeriod(conn)) return;
        int Get(string name) => Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
        var effects = monster.Mods.Effects;
        int chance = effects["global_bleed_on_hit"] != 0 ? 100 : effects["bleed_on_hit_with_attacks_%"]
            + effects[attack.Ranged ? "bleed_on_bow_attack_chance_%" : "bleed_on_melee_attack_chance_%"];
        chance += Get("receive_bleeding_chance_%_when_hit_by_attack");
        if (chance <= 0 || Get("immune_to_bleeding") != 0 || Get("cannot_gain_bleeding") != 0
            || !RandomRolls.Chance(chance, 100, conn.IncomingHitRandom)
            || RandomRolls.Chance(Get("avoid_bleed_%") + Get("base_avoid_bleed_%"), 100, conn.IncomingHitRandom)) return;
        double physical = attack.Damage[DamageType.Physical].Roll(conn.IncomingHitRandom)
            * (critical ? PlayerElementalAilments.MonsterCriticalAilmentMultiplier : 1);
        double duration = 5 * Math.Max(0, 1 + Get("self_bleed_duration_+%_final") / 100.0);
        if (conn.Bleeding.Apply(physical, now, duration, Get,
            () => IncomingMarkDamageFactor(conn, sourceId, monster)))
            Console.WriteLine($"[gateway] bleeding admitted from {monster.Label}: base {physical:0.###}, {duration:0.###} s");
    }

    internal static int AdvancePlayerBleedingLocked(ClientState conn, double from, double until,
        IReadOnlyDictionary<uint, int> stats)
    {
        lock (conn.Monsters) return AdvancePlayerBleedingCore(conn, from, until, stats);
    }

    static int AdvancePlayerBleedingCore(ClientState conn, double from, double until,
        IReadOnlyDictionary<uint, int> stats)
    {
        if (conn.Closed || conn.LeavingArea || conn.RageRetired || conn.PlayerLife == 0)
        { conn.Bleeding.Clear(); conn.BleedBypassCarry = 0; return 0; }
        int Get(string name) => Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
        var leech = LeechContext(conn);
        bool unaffected = Get("base_unaffected_by_bleeding") != 0 || SlayerRules.WhileLeeching(Get,
            leech.Life, leech.Mana, leech.EnergyShield).UnaffectedByBleeding;
        bool moving = conn.Walk.ArrivesAt > from;
        int damage = conn.Bleeding.Advance(from, until, Get, unaffected, moving,
            PlayerDamageReduction(conn, stats).Physical, PlayerDamageTakenFactor(conn, until, stats)
                * Math.Max(0, 1 + Get("harbinger_channel_buff_damage_taken_+%_final") / 100.0));
        if (PlayerImmunities(conn).Contains(DamageType.Physical)) damage = 0;
        int life = HarbingerDefences.DamageOverTime(conn.EnergyShield, damage,
            Get("base_non_chaos_damage_bypass_energy_shield_%"), until, ref conn.BleedBypassCarry);
        conn.PlayerLife = (uint)Math.Max(0, (long)conn.PlayerLife - life);
        if (conn.PlayerLife == 0) { conn.InvalidateIncomingHits(); RecordHardcoreDeath(conn); }
        return damage;
    }

    // Reflected damage is neither an enemy skill hit nor player damage: no leech, ailment, Rage, charge or recursion.
    // Flat thorns and percentage reflection read the actual enemy raw modifier sources at accepted contact.
    internal static int ReflectOwnerHitLocked(ClientState conn, SpawnedMonster target, OwnerSkillHit hit)
    {
        var stats = CharacterPlayerStats(conn);
        int Get(string name) => Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
        if (conn.Closed || conn.LeavingArea || conn.RageRetired || conn.PlayerLife == 0 || InGracePeriod(conn)
            || Get("immune_to_reflected_damage") != 0 || Get("damage_cannot_be_reflected") != 0
            || hit.DirectDamage <= 0 || !hit.Attribution.GrantOwnerLeech) return 0;
        int total = 0, chaos = 0;
        foreach (var type in Enum.GetValues<DamageType>())
        {
            string name = type.ToString().ToLowerInvariant();
            if (PlayerImmunities(conn).Contains(type)
                || type == DamageType.Physical && Get("cannot_take_reflected_physical_damage") != 0
                || DamageTypes.IsElemental(type) && Get("cannot_take_reflected_elemental_damage") != 0) continue;
            var e = target.Mods.Effects;
            double amount = Math.Max(0, e[name + "_damage_to_return_when_hit"])
                + (hit.Melee ? Math.Max(0, e[name + "_damage_to_return_to_melee_attacker"]) : 0);
            foreach (string source in hit.Melee ? new[] { "when_hit", "to_melee_attacker" } : new[] { "when_hit" })
            {
                int min = Math.Max(0, e["minimum_" + name + "_damage_to_return_" + source]);
                int max = Math.Max(min, e["maximum_" + name + "_damage_to_return_" + source]);
                if (max > 0) amount += min + conn.IncomingHitRandom.NextDouble() * ((long)max - min);
            }
            // Hiltless-style raw item source is reflected damage, never a new enemy attack.
            if (type == DamageType.Physical && hit.Attack)
            {
                int min = Math.Max(0, Get("minimum_physical_damage_to_reflect_to_self_on_attack"));
                int max = Math.Max(min, Get("maximum_physical_damage_to_reflect_to_self_on_attack"));
                if (max > 0) amount += min + conn.IncomingHitRandom.NextDouble() * ((long)max - min);
            }
            int pct = type == DamageType.Physical ? e["map_monsters_reflect_%_physical_damage"]
                : DamageTypes.IsElemental(type) ? e["map_monsters_reflect_%_elemental_damage"] : 0;
            // Taken is the direct typed hit before critical scaling; preserve its proportions, never use Impale/cull.
            double basis = hit.Taken.Components.Sum(c => c.Range.Min);
            if (basis > 0) amount += hit.DirectDamage * Math.Max(0, hit.Taken[type].Min) / basis * Math.Max(0, pct) / 100;
            if (amount <= 0) continue;
            // Reflection-specific and generic increased/reduced taken share one bucket AFTER mitigation.
            // Armour sees the unreduced reflected packet; Shock and conditional leech taken join the same bucket.
            int reflectedIncrease = Get("reflect_damage_taken_+%") + (type == DamageType.Physical
                ? Get("physical_reflect_damage_taken_+%") : DamageTypes.IsElemental(type)
                    ? Get("elemental_reflect_damage_taken_+%") : 0);
            var reduction = PlayerDamageReduction(conn, stats);
            int raw = (int)Math.Min(int.MaxValue, amount);
            int resisted = type == DamageType.Physical ? raw : DamageModel.Mitigate(raw,
                Resistance(conn, type), MaxResistance(conn, type));
            int mitigated = PlayerHitMitigation.AfterResistance(conn.Pools, type, resisted,
                reduction.Physical, reduction.Elemental, conn.Pools.Armour);
            double taken = PlayerDamageTakenFactor(conn, ProjectileWorldTime(conn), stats, reflectedIncrease);
            int damage = FortificationDamageTaken(conn, (int)Math.Min(int.MaxValue, Math.Round(mitigated * taken)));
            total = (int)Math.Min(int.MaxValue, (long)total + damage);
            if (type == DamageType.Chaos) chaos = damage;
        }
        if (total <= 0) return 0;
        conn.PlayerLife = (uint)Math.Max(0, (long)conn.PlayerLife - DamagePlayerHit(conn, total - chaos, chaos,
            ProjectileWorldTime(conn), stats));
        conn.ReflectionDirty = true;
        TrimLeechLocked(conn);
        if (conn.PlayerLife == 0) { conn.InvalidateIncomingHits(); RecordHardcoreDeath(conn); }
        return total;
    }
}
