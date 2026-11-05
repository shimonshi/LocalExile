using GameServer.Core.World;

static partial class Game
{
    internal static void IncomingMotion(ClientState conn, double at, bool after)
    {
        if (after || conn.ResourceEffectsState is not ResourceEffectsState owner || !ResourceEffectsCurrent(conn, owner)) return;
        var state = owner.Incoming;
        if (!state.Settling && double.IsFinite(state.SettledAt) && at > state.SettledAt)
        {
            state.Settling = true;
            try { AdvanceIncomingPhysicalAilmentsLocked(conn, state, state.SettledAt, at, CharacterPlayerStats(conn)); }
            finally { state.Settling = false; }
        }
    }
    // effectRow is the ACTUAL executing GrantedEffects row, never the variety's unrelated first effect.
    // Attack.Damage already has generic/type damage baked in by MonsterModEffects.Hit.
    internal static Func<string,int> IncomingMonsterAilmentSources(ClientState conn, SpawnedMonster monster, int effectRow)
    {
        var skill = SkillsFor(conn).EffectStats(effectRow, monster.Level).ToDictionary(x => x.Item1,x => x.Item2);
        return name => (name is "damage_+%" or "physical_damage_+%" or "chaos_damage_+%" or "elemental_damage_+%"
            or "fire_damage_+%" or "cold_damage_+%" or "lightning_damage_+%" ? 0
            : monster.Mods.Effects[name]) + (Stats.Key(name) is uint key ? skill.GetValueOrDefault(key) : 0);
    }

    internal static bool AssassinIncomingHitAdmitted(ClientState conn, double at, IReadOnlyDictionary<uint,int> stats)
    {
        lock (conn.Monsters)
        {
            var owner = ResourceEffectsOf(conn);
            return CurrentGameWork && ResourceEffectsCurrent(conn, owner) && !conn.Closed && !conn.LeavingArea
                && !conn.Paused && conn.PlayerLife > 0 && !InGracePeriod(conn)
                && !OrdinaryAssassinAvoidHit(conn, stats, at, conn.IncomingHitRandom);
        }
    }
    /// <summary>Single admission seam for attack AND spell hits. Insert before all hit side effects.
    /// The caller retains its existing block/suppression/mitigation/recoup executor in commit.</summary>
    internal static T? CommitAssassinIncomingHit<T>(ClientState conn, double at,
        IReadOnlyDictionary<uint,int> stats, Func<T?> commit) where T : class
    {
        lock (conn.Monsters)
        {
            if (!AssassinIncomingHitAdmitted(conn, at, stats)) return null;
            return commit();
        }
    }

    /// <summary>baseHit is the separate pre-critical, pre-defence ailment base after source conversion.
    /// sourceStats merges pinned monster base/skill/mod stats; hit-only modifiers must not enter this input.</summary>
    internal static (bool Poison, bool Bleed) ApplyIncomingPhysicalAilmentsLocked(ClientState conn,
        AssassinIncomingAilments state, DamagePacket baseHit, bool spell, bool critical, bool blocked,
        double now, IReadOnlyDictionary<uint,int> player, Func<string,int> sourceStats, uint source)
    {
        lock (conn.Monsters)
        {
            if (!state.Current(conn) || conn.Closed || conn.LeavingArea || conn.Paused || conn.PlayerLife == 0
                || blocked || InGracePeriod(conn)) return default;
            int Target(string n) => Stats.Key(n) is uint k ? player.GetValueOrDefault(k) : 0;
            int Source(string n) => n == "critical_ailment_dot_multiplier_+" ? 30 + sourceStats(n) : sourceStats(n);
            // Reuse the existing conversion/ailment buckets, never the mitigated or critical hit amount.
            var rows = AilmentPanelStats.Compute(new(spell ? "spell" : "main_hand", spell), baseHit, new(), Source);
            var poison = MonsterPoisons.Settings.From(rows, spell ? "spell" : "main_hand", Source);
            var bleed = spell ? MonsterIgnites.Settings.None : BleedSettings(rows, "main_hand", Source, null, false, false);
            int poisonChance = (int)Math.Clamp((long)Source("base_chance_to_poison_on_hit_%")
                + Source(spell ? "spells_chance_to_poison_on_hit_%" : "chance_to_poison_on_hit_with_attacks_%")
                + (baseHit[DamageType.Chaos].Max > 0 ? Source("chaos_damage_chance_to_poison_%") : 0),0,100);
            bool p = false, b = false;
            if (Target("immune_to_poison") == 0 && !MonsterPoisons.Prohibited(Source)
                && RandomRolls.Chance(poisonChance, 100, conn.IncomingHitRandom)
                && !RandomRolls.Chance(Target("avoid_poison_%"), 100, conn.IncomingHitRandom))
            {
                double min = critical ? poison.CriticalMin : poison.Min, max = critical ? poison.CriticalMax : poison.Max;
                p = state.Add(AssassinIncomingAilments.Kind.Poison, now, min + Math.Max(0,max-min) * conn.IncomingHitRandom.NextDouble(),
                    poison.Seconds * MonsterPoisons.SelfDuration(Target), source);
            }
            if (!spell && Target("immune_to_bleeding") == 0
                && RandomRolls.Chance(critical ? bleed.CriticalChance : bleed.Chance, 100, conn.IncomingHitRandom)
                && !RandomRolls.Chance(Target("avoid_bleed_%"), 100, conn.IncomingHitRandom))
            {
                double min = critical ? bleed.CriticalMin : bleed.Min, max = critical ? bleed.CriticalMax : bleed.Max;
                b = state.Add(AssassinIncomingAilments.Kind.Bleed, now, min + Math.Max(0,max-min) * conn.IncomingHitRandom.NextDouble(),
                    bleed.Seconds * Math.Max(0, 1 + Target("self_bleed_duration_+%") / 100d)
                        * Math.Max(0, 1 + Target("self_bleed_duration_+%_final") / 100d), source);
            }
            return (p,b);
        }
    }

    internal static int AdvanceIncomingPhysicalAilmentsLocked(ClientState conn, AssassinIncomingAilments state,
        double from, double until, IReadOnlyDictionary<uint,int> player)
    {
        lock (conn.Monsters)
        {
            if (!state.Current(conn) || conn.Closed || conn.PlayerLife == 0) { state.Clear(); return 0; }
            if (conn.Paused || conn.LeavingArea || until <= from) return 0;
            int Get(string n) => Stats.Key(n) is uint k ? player.GetValueOrDefault(k) : 0;
            double Factor(AssassinIncomingAilments.Effect e, double at)
            {
                var type = e.Kind == AssassinIncomingAilments.Kind.Poison ? DamageType.Chaos : DamageType.Physical;
                if (PlayerImmunities(conn).Contains(type)) return 0;
                double mitigation = type == DamageType.Chaos
                    ? DamageModel.Mitigate(1d, Resistance(conn,type), MaxResistance(conn,type))
                    : Math.Max(0, 1 - PlayerDamageReduction(conn,player).Physical / 100d);
                return mitigation * conn.ElementalAilments.DamageTakenFactor(at,
                    DamageTakenFromBlindedPct(conn,e.Source,player) + OrdinaryAssassinIncomingIncreased(conn,player,at)
                    + Get("damage_taken_+%") + Get(type == DamageType.Chaos ? "chaos_damage_taken_+%" : "physical_damage_taken_+%"));
            }
            // Split the natural arrival boundary; movement mutations must settle the previous interval before retiming.
            int total = 0; uint lifeBefore = conn.PlayerLife;
            foreach (double end in new[] { conn.Walk.ArrivesAt, until }.Where(t => from < t && t <= until).Distinct().Order())
            {
                var tick = state.Integrate(from,end,Get,Factor,conn.Walk.Moving);
                total = (int)Math.Min(int.MaxValue,(long)total + state.ApplyResources(conn,tick.Poison,tick.Bleed,end,Get));
                from = end;
            }
            // Motion/projectile settlement can precede the recovery worker's Life baseline.
            // Use the existing out-of-band resource notification also used by hit/kill recovery.
            if (conn.PlayerLife != lifeBefore) LeechOf(conn).InstantLifeChanged = true;
            if (conn.PlayerLife == 0) { state.Clear(); conn.InvalidateIncomingHits(); RecordHardcoreDeath(conn); }
            // Deliberately no RecoupHitLocked, Mana.Hit, when-hit charges, avoidance or owner ailment procs.
            return total;
        }
    }
}
