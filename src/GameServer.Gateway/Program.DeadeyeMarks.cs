using System.Runtime.CompilerServices;
using GameServer.Core.Projectiles;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

partial class Program
{
    internal static readonly DeadeyeMarkCatalog DeadeyeMarks = new();
    internal sealed record DeadeyeMarkSource(ushort Skill, int Effect, int Level, int Quality, uint Gem);
    internal sealed class DeadeyeMarkState
    {
        public int Generation;
        public DeadeyeMarkInstance? Active;
        public double BusyUntil;
    }
    internal sealed record DeadeyeMarkInstance(DeadeyeMarkSource Source, uint Caster, uint Target,
        SpawnedMonster Monster, ushort Buff, int Generation, EffectClock Clock)
    {
        public uint[]? PublishedValues;
    }
    static readonly ConditionalWeakTable<ClientState, DeadeyeMarkState> DeadeyeMarkStates = new();
    internal static DeadeyeMarkState DeadeyeMarkOf(ClientState conn) => DeadeyeMarkStates.GetValue(conn, _ => new());
    internal static int DeadeyeValue(IReadOnlyDictionary<uint, int> stats, string name) =>
        Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
    static bool DeadeyeActive(ClientState conn) => !conn.Closed && !conn.LeavingArea && !conn.RageRetired
        && conn.PlayerLife > 0;
    static bool DeadeyeMarkSourceCurrent(ClientState conn, DeadeyeMarkSource source) =>
        GemSkillEffects(conn).Any(g => g.Id == source.Skill && g.Effect == source.Effect && g.Gem == source.Gem)
        && GemSkillInstanceDisabled(conn, source.Effect, source.Skill) is null;

    // Mark spells have native 500 ms casts. This entry owns payment; call before the generic gem payment path.
    internal static bool TryDeadeyeMarkCast(ProjectileSession session, ushort skill, uint targetId)
    {
        var conn = session.Conn;
        lock (conn.Monsters)
        {
            double now = session.Runtime.Now / (double)ProjectileRuntime.QuantaPerSecond;
            var gem = GemSkillEffects(conn).FirstOrDefault(g => g.Id == skill);
            if (DeadeyeMarks.Find(gem.Effect) is null || gem.Name is null || !DeadeyeActive(conn) || conn.Paused
                || conn.ElementalAilments.ActionFactor(now) <= 0 || conn.HitRecovery.Active(now)
                || InTown(conn) || !conn.Monsters.TryGetValue(targetId, out var target)
                || !target.Hostile || target.Dead || target.Life == 0) return false;
            var source = new DeadeyeMarkSource(skill, gem.Effect, gem.Level, gem.Quality, gem.Gem);
            if (!DeadeyeMarkSourceCurrent(conn, source)) return false;
            var state = DeadeyeMarkOf(conn);
            if (now < state.BusyUntil) return false;
            var stats = SupportedStats(conn, skill);
            int Get(string name) => DeadeyeValue(stats, name);
            var timing = SpellCastTiming.Calculate(SkillsFor(conn).CastMs(gem.Effect),
                CastSpeedPct(CharacterPlayerStats(conn)) + DeadeyeValue(SupportedSkill(conn, skill).Stats, "base_cast_speed_+%")
                + Get("mark_skill_cast_speed_+%"));
            if (timing.DurationMilliseconds is not int ms || PlayerActionFactor(conn) <= 0) return false;
            double duration = ms / 1000d / PlayerActionFactor(conn);
            if (Dist(conn.Walk.Position(now), target.Walk.Position(now)) > 120) return false;
            uint cost = (uint)Math.Max(0, Get("mana_cost"));
            if (conn.PlayerMana < cost) return false;
            if (CooldownFor(conn, gem.Effect, gem.Level, gem.Quality) is { } cooldown)
            {
                if (!TryBeginSkillCooldownUse(conn, skill, gem.Effect, cooldown,
                    false, () => { }, out _, requestedAt: now)) return false;
                now = Math.Max(now, session.Runtime.Now / (double)ProjectileRuntime.QuantaPerSecond);
            }
            if (!PayMana(conn, skill, out _)) return false;
            DeadeyeGaleAdmitUse(conn, true);
            int generation = ++state.Generation;
            int action = ++conn.ActionCount;
            state.BusyUntil = now + duration;
            var from = conn.Walk.Position(now); var to = target.Walk.Position(now);
            DeadeyeMarkPacket(session, () => InstanceProtocol.ActionStarted(PlayerId, from.X, from.Y, to.X, to.Y,
                skill, 0xffff, 0x0408, targetObjectId: targetId), () => state.Generation == generation);
            session.Packet(InstanceProtocol.LifeUpdate(PlayerId, conn.PlayerLife, conn.PlayerMana));
            session.Runtime.Schedule(ProjectileRuntime.Time(state.BusyUntil), () =>
            {
                if (!DeadeyeActive(conn) || conn.Paused || state.Generation != generation || conn.ActionCount != action
                    || conn.ElementalAilments.ActionFactor(state.BusyUntil) <= 0 || conn.HitRecovery.Active(state.BusyUntil)
                    || !DeadeyeMarkSourceCurrent(conn, source) || !conn.Monsters.TryGetValue(targetId, out var current)
                    || !ReferenceEquals(current, target) || target.Dead || target.Life == 0 || !target.Hostile) return;
                ApplyDeadeyeMark(session, source, targetId, target);
                DeadeyeMarkPacket(session, () => InstanceProtocol.ActionEnded(PlayerId), () => conn.ActionCount == action);
            });
            return true;
        }
    }

    internal static void DeadeyeMarkPacket(ProjectileSession session, Func<byte[]> packet, Func<bool> current)
    {
        session.Enqueue(() => Send(session.remote, session.stream, session.ciphers, [], null, atGate: () =>
        {
            lock (session.Conn.Monsters)
                return session.Active && !session.Conn.Paused && current() ? packet() : null;
        }));
    }

    static void ApplyDeadeyeMark(ProjectileSession session, DeadeyeMarkSource source, uint target, SpawnedMonster monster)
    {
        var conn = session.Conn; var state = DeadeyeMarkOf(conn);
        RemoveDeadeyeMark(session);
        double now = session.Runtime.Now / (double)ProjectileRuntime.QuantaPerSecond;
        var own = SupportedSkill(conn, source.Skill).Stats;
        int Get(string name) => DeadeyeValue(own, name);
        // Ordinary marks have no duration. A future sourced duration uses the target category-14 clock.
        double seconds = Get("base_skill_effect_duration") > 0 ? Get("base_skill_effect_duration") / 1000d
            * Math.Max(0, 1 + DeadeyeValue(SupportedStats(conn, source.Skill), "skill_effect_duration_+%") / 100d)
            : double.PositiveInfinity;
        var mark = new DeadeyeMarkInstance(source, PlayerId, target, monster, conn.NextBuff++, ++state.Generation,
            new(seconds, now, DeadeyeMarkRecipientRate(conn, target, monster, now)));
        state.Active = mark;
        PublishDeadeyeMark(session, mark);
    }

    static void PublishDeadeyeMark(ProjectileSession session, DeadeyeMarkInstance mark)
    {
        var conn = session.Conn; var state = DeadeyeMarkOf(conn);
        DeadeyeMarkPacket(session, () =>
        {
            var metadata = DeadeyeMarks.Find(mark.Source.Effect)!;
            uint[] values = [.. metadata.BuffKeys.Select(key => unchecked((uint)DeadeyeMarkAmount(conn, mark, Stats.Name(key) ?? "")))];
            if (mark.PublishedValues?.SequenceEqual(values) == true) return [];
            byte[] remove = mark.PublishedValues is null ? [] : InstanceEntities.RemoveBuff(mark.Target, mark.Buff);
            mark.PublishedValues = values;
            return [.. remove, .. InstanceEntities.AddBuff(mark.Target, mark.Buff, metadata.Buff, mark.Caster, mark.Source.Skill)];
        }, () => ReferenceEquals(state.Active, mark) && DeadeyeMarkCurrent(session, mark));
    }

    internal static void RemoveDeadeyeMark(ProjectileSession session)
    {
        var state = DeadeyeMarkOf(session.Conn);
        if (state.Active is { } mark)
        {
            state.Active = null;
            DeadeyeMarkPacket(session, () => InstanceEntities.RemoveBuff(mark.Target, mark.Buff),
                () => session.Conn.Monsters.TryGetValue(mark.Target, out var target) && ReferenceEquals(target, mark.Monster));
        }
    }

    static bool DeadeyeMarkCurrent(ProjectileSession session, DeadeyeMarkInstance mark)
    {
        var conn = session.Conn;
        double now = session.Runtime.Now / (double)ProjectileRuntime.QuantaPerSecond;
        mark.Clock.ChangeRate(now, DeadeyeMarkRecipientRate(conn, mark.Target, mark.Monster, now));
        return DeadeyeActive(conn) && mark.Clock.Remaining > 0 && DeadeyeMarkSourceCurrent(conn, mark.Source)
            && conn.Monsters.TryGetValue(mark.Target, out var monster) && ReferenceEquals(monster, mark.Monster);
    }

    static double DeadeyeMarkRecipientRate(ClientState conn, uint target, SpawnedMonster monster, double now) =>
        EffectClock.RecipientRate(14, name => monster.Mods.Effects[name]
            + (name == "buff_time_passed_+%_other_than_temporal_chains"
                ? ActiveMonsterTemporalChains(conn, target, now)?.OtherEffects ?? 0 : 0));

    internal static void RefreshDeadeyeMarks(ProjectileSession session)
    {
        var state = DeadeyeMarkOf(session.Conn);
        if (state.Active is { } mark && (!DeadeyeMarkCurrent(session, mark) || mark.Monster.Dead || mark.Monster.Life == 0))
            RemoveDeadeyeMark(session);
        else if (state.Active is { } active) PublishDeadeyeMark(session, active);
    }

    internal static void RetireDeadeyeMarks(ProjectileSession session)
    {
        var state = DeadeyeMarkOf(session.Conn); ++state.Generation; state.BusyUntil = 0;
        RemoveDeadeyeMark(session);
    }

    internal static int DeadeyeMarkAmount(ClientState conn, DeadeyeMarkInstance mark, string name)
    {
        var own = SupportedSkill(conn, mark.Source.Skill).Stats;
        int value = DeadeyeValue(own, name);
        var caster = CharacterPlayerStats(conn);
        double effect = Math.Max(0, 1 + (DeadeyeValue(caster, "curse_effect_+%")
            + DeadeyeValue(caster, "mark_skills_curse_effect_+%") + DeadeyeValue(own, "mark_skills_curse_effect_+%")) / 100d);
        return (int)Math.Clamp(Math.Truncate(value * effect), int.MinValue, int.MaxValue);
    }

    internal static DeadeyeMarkInstance? DeadeyeMarkOn(ProjectileSession session, uint id, SpawnedMonster target)
    {
        var mark = DeadeyeMarkOf(session.Conn).Active;
        return mark is not null && mark.Target == id && ReferenceEquals(mark.Monster, target) && target.Hostile
            && DeadeyeMarkCurrent(session, mark) ? mark : null;
    }

    internal static DeadeyeMarkHitSource? CaptureDeadeyeMarkHitSource(ClientState conn, ushort skill, bool area)
    {
        if (SupportedHitInputsFor(conn, skill, area) is not { } inputs) return null;
        var gem = SupportedSkill(conn, skill);
        double effectiveness = AddedEffectiveness(conn, gem.Effect, gem.Level);
        SupportedHitInputs WithFlat(DamageRange flat) => inputs with
        { Raw = inputs.Raw.Add(DamageType.Physical, flat.Scale(effectiveness)) };
        string hand = inputs.Attack ? "main_hand" : "spell";
        // Pinned PoB16de4b82 CalcOffence base damage includes enemy-added base before both hit and ailment conversion.
        // This is model evidence, not a recovered native server formula; retain the identical frozen hitter inputs.
        return new(flat => SupportedHitFrom(WithFlat(flat), suppress: false),
            flat => MonsterIgnites.Settings.From(SupportedAilmentRowsFrom(WithFlat(flat), hand), hand, inputs.Get));
    }

    internal static DeadeyeMarkHitSource CaptureDeadeyeOrdinaryMarkHitSource(ClientState conn, bool melee)
    {
        var player = new Dictionary<uint, int>(CharacterPlayerStats(conn));
        var weapon = WeaponFor(conn);
        int Get(string name) => DeadeyeValue(player, name);
        var raw = SupportedAttackDamage.Raw(weapon, Get);
        var conversion = DamageStats.Conversion(player, Stats, attack: true);
        return new(flat => RageStats.ApplyAttack(PlayerHitDamage.Resolve(raw.Add(DamageType.Physical, flat), Get,
            conversion, spell: false, melee: melee, projectile: !melee, wieldingWand: weapon.WieldingWand), Get)
            .Scale(melee ? GamblerFor(player).DamageFactor : 1),
            flat => MonsterIgnites.Settings.From(AilmentPanelStats.Compute(
                new("main_hand", Spell: false, Melee: melee, Projectile: !melee),
                raw.Add(DamageType.Physical, flat), conversion, Get), "main_hand", Get));
    }

    internal static DamagePacket DeadeyeMarkedPacket(ProjectileSession session, uint id, SpawnedMonster target,
        DamagePacket packet, bool attack, bool projectile, DeadeyeMarkHitSource? source = null)
    {
        if (DeadeyeMarkOn(session, id, target) is not { } mark) return packet;
        int Get(string name) => DeadeyeMarkAmount(session.Conn, mark, name);
        var flat = new DamageRange(Get("minimum_added_physical_damage_taken"), Get("maximum_added_physical_damage_taken"));
        if (source is not null && !flat.IsZero) packet = source.Resolve(flat);
        // Sniper's target increased HIT damage taken belongs after mitigation; never scales the ailment basis.
        return packet;
    }

    internal static MonsterIgnites.Settings DeadeyeMarkedIgnite(ProjectileSession session, uint id,
        SpawnedMonster target, MonsterIgnites.Settings settings, DeadeyeMarkHitSource? source)
    {
        if (source?.ResolveIgnite is not { } resolve || DeadeyeMarkOn(session, id, target) is not { } mark) return settings;
        var flat = new DamageRange(DeadeyeMarkAmount(session.Conn, mark, "minimum_added_physical_damage_taken"),
            DeadeyeMarkAmount(session.Conn, mark, "maximum_added_physical_damage_taken"));
        return flat.IsZero ? settings : resolve(flat);
    }

    internal static (int Chance, int Multiplier) DeadeyeMarkedCritical(ProjectileSession session, uint id,
        SpawnedMonster target, int chance, int multiplier)
    {
        if (DeadeyeMarkOn(session, id, target) is not { } mark) return (chance, multiplier);
        return (Math.Clamp(chance + DeadeyeMarkAmount(session.Conn, mark, "enemy_additional_critical_strike_chance_against_self"), 0, 10000),
            multiplier + DeadeyeMarkAmount(session.Conn, mark, "enemy_additional_critical_strike_multiplier_against_self"));
    }

    internal static double DeadeyeMarkedPhysicalReduction(ProjectileSession session, uint id, SpawnedMonster target) =>
        DeadeyeMarkOn(session, id, target) is { } mark
            ? DeadeyeMarkAmount(session.Conn, mark, "base_additional_physical_damage_reduction_%") : 0;

    internal static int DeadeyeMarkSplit(ProjectileSession session, uint id, SpawnedMonster target)
    {
        // Speculative Core query: do not advance clocks, transfer, publish or grant any resources.
        var conn = session.Conn; var mark = DeadeyeMarkOf(conn).Active;
        if (mark is null || mark.Target != id || !ReferenceEquals(mark.Monster, target) || !target.Hostile
            || target.Dead || target.Life == 0 || !DeadeyeActive(conn) || !DeadeyeMarkSourceCurrent(conn, mark.Source)
            || mark.Clock.Deadline <= session.Runtime.Now / (double)ProjectileRuntime.QuantaPerSecond) return 0;
        return Math.Clamp(DeadeyeMarkAmount(conn, mark, "projectiles_hitting_self_split_into_x"), 0, 256);
    }

    internal static (int Increased, double Physical) DeadeyeMarkTargetModifiers(ProjectileSession session,
        uint id, SpawnedMonster target, bool projectile)
    {
        var mark = DeadeyeMarkOn(session, id, target);
        return (projectile && mark is not null
            ? DeadeyeMarkAmount(session.Conn, mark, "projectile_damage_taken_+%") : 0,
            mark is not null ? DeadeyeMarkAmount(session.Conn, mark, "base_additional_physical_damage_reduction_%") : 0);
    }

    // Compatibility entry for engine fixtures; production and proxies use the same MonsterDamage target inputs.
    internal static (int Damage, DamagePacket Taken) DeadeyeMarkedDamage(ProjectileSession session, uint id,
        SpawnedMonster target, DamagePacket rolled, bool critical, bool attack, HitResistances.Mods mods,
        Penetration.PerType penetration, bool invertElemental = false, bool projectile = true)
    {
        var modifiers = DeadeyeMarkTargetModifiers(session, id, target, projectile);
        var (damage, _) = MonsterDamage(target, rolled, critical, attack, mods, penetration, out var taken,
            invertElemental, modifiers.Increased, modifiers.Physical);
        return (damage, taken);
    }

    internal static DamagePacket DeadeyeMarkTakenComponents(ProjectileSession session, uint id, SpawnedMonster target,
        DamagePacket mitigated, bool spell, bool projectile)
    {
        var modifiers = DeadeyeMarkTargetModifiers(session, id, target, projectile);
        return MonsterTakenComponents(target, mitigated, spell, modifiers.Increased, modifiers.Physical);
    }

    internal static HitAnimation? DeadeyeMarkFlinch(ProjectileSession session, uint id, SpawnedMonster target,
        int damage, double now, Random random, bool grantOwnerEffects = true)
    {
        var result = HitReaction.TryFlinch(target, damage, now, random);
        if (result is not { } animation) return null;
        double duration = DeadeyeMarkStun(session, id, target, animation.Seconds, random, grantOwnerEffects);
        if (duration > animation.Seconds)
        {
            target.FlinchUntil += duration - animation.Seconds;
            target.BusyUntil = Math.Max(target.BusyUntil, target.FlinchUntil);
        }
        return animation with { Seconds = duration };
    }

    internal static LeechSources DeadeyeMarkedLeech(ProjectileSession session, uint id, SpawnedMonster target,
        LeechSources source, bool attack)
    {
        if (!attack || DeadeyeMarkOn(session, id, target) is not { } mark) return source;
        DamagePacket Coeff(string name) => new DamagePacketBuilder().Coefficients(DeadeyeMarkAmount(session.Conn, mark, name));
        return source with
        {
            Life = source.Life.Add(Coeff("life_leech_on_any_damage_when_hit_by_attack_permyriad")),
            Mana = source.Mana.Add(Coeff("mana_leech_on_any_damage_when_hit_by_attack_permyriad"))
        };
    }

    internal static void DeadeyeMarkHit(ProjectileSession session, uint id, SpawnedMonster target, bool attack,
        bool grantOwnerEffects = true, Random? random = null)
    {
        if (!grantOwnerEffects || DeadeyeMarkOn(session, id, target) is not { } mark) return;
        if (attack) DeadeyeMarkRecovery(session.Conn, DeadeyeMarkAmount(session.Conn, mark, "life_granted_when_hit_by_attacks"),
            DeadeyeMarkAmount(session.Conn, mark, "mana_granted_when_hit_by_attacks"));
        foreach (var (type, name) in new[] { (ChargeType.Power, "power"), (ChargeType.Frenzy, "frenzy"), (ChargeType.Endurance, "endurance") })
        {
            int chance = DeadeyeMarkAmount(session.Conn, mark, $"grant_attacker_{name}_charge_when_hit_%_chance");
            if (chance > 0 && (chance >= 100 || (random ?? Random.Shared).Next(100) < chance))
                ChargesOf(session.Conn).Enqueue(new ChargeEvent.Grant(type, 1, "marked enemy quality hit"));
        }
    }

    static void DeadeyeMarkRecovery(ClientState conn, int life, int mana)
    {
        if (conn.PlayerLife == 0) return;
        conn.PlayerLife = (uint)Math.Min(conn.PlayerMaxLife, (long)conn.PlayerLife + Math.Max(0, life));
        conn.PlayerMana = (uint)Math.Min(UnreservedMana(conn), (long)conn.PlayerMana + Math.Max(0, mana));
        TrimLeechLocked(conn);
    }

    internal static double DeadeyeMarkStun(ProjectileSession session, uint id, SpawnedMonster target, double duration,
        Random random, bool grantOwnerEffects = true)
    {
        if (DeadeyeMarkOn(session, id, target) is not { } mark) return duration;
        int Get(string name) => DeadeyeMarkAmount(session.Conn, mark, name);
        if (grantOwnerEffects && Get("enemy_grant_X_rage_on_stun") > 0)
        {
            var stats = CharacterPlayerStats(session.Conn);
            session.Conn.Rage.Gain(Get("enemy_grant_X_rage_on_stun"), ProjectileWorldTime(session.Conn),
                RageStats.Inputs(name => DeadeyeValue(stats, name)), DeadeyeValue(stats, "has_soul_gain_prevention") != 0);
        }
        return duration * (random.Next(100) < Get("enemy_chance_to_double_stun_duration_%_vs_self") ? 2 : 1);
    }

    internal static void DeadeyeMarkDeath(ProjectileSession session, uint id, SpawnedMonster dead, Random random,
        bool ownerKill = true)
    {
        if (DeadeyeMarkOn(session, id, dead) is not { } mark) return;
        var conn = session.Conn;
        int Get(string name) => DeadeyeMarkAmount(conn, mark, name);
        if (ownerKill)
        {
            DeadeyeMarkRecovery(conn, Get("life_granted_when_killed"), Get("mana_granted_when_killed"));
            foreach (var (type, name) in new[] { (ChargeType.Power, "power"), (ChargeType.Frenzy, "frenzy"), (ChargeType.Endurance, "endurance") })
                if (random.Next(100) < Get($"chance_to_grant_{name}_charge_on_death_%"))
                    ChargesOf(conn).Enqueue(new ChargeEvent.Grant(type, 1, "marked enemy death"));
        }
        var stats = CharacterPlayerStats(conn);
        bool transfer = random.Next(100) < DeadeyeValue(stats, "your_marks_transfer_to_nearby_enemies_on_death_%_chance");
        double now = session.Runtime.Now / (double)ProjectileRuntime.QuantaPerSecond;
        var origin = dead.Walk.Position(now);
        var next = conn.Monsters.Where(p => p.Key != id && p.Value.Hostile && !p.Value.Dead && p.Value.Life > 0)
            .Select(p => (p.Key, Monster: p.Value, Distance: Dist(origin, p.Value.Walk.Position(now))))
            .Where(p => p.Distance <= 60).OrderBy(p => p.Distance).ThenBy(p => p.Key).FirstOrDefault();
        RemoveDeadeyeMark(session);
        if (transfer && next.Monster is not null) ApplyDeadeyeMark(session, mark.Source, next.Key, next.Monster);
    }

    internal static double DeadeyeMarkIncoming(ProjectileSession session, uint attacker, SpawnedMonster enemy)
    {
        if (!enemy.Hostile || DeadeyeMarkOf(session.Conn).Active is not { } mark || attacker == mark.Target
            || !DeadeyeMarkCurrent(session, mark) || mark.Monster.Dead || mark.Monster.Life == 0) return 1;
        double now = session.Runtime.Now / (double)ProjectileRuntime.QuantaPerSecond;
        if (Dist(enemy.Walk.Position(now), mark.Monster.Walk.Position(now)) > 60) return 1;
        return Math.Max(0, 1 + DeadeyeValue(CharacterPlayerStats(session.Conn),
            "damage_taken_+%_final_from_enemies_near_marked_enemy") / 100d);
    }

    // Kept private to this adapter; this builds the existing per-type LeechSources representation.
    sealed class DamagePacketBuilder
    {
        public DamagePacket Coefficients(int value)
        {
            var result = DamagePacket.Empty;
            foreach (var type in DamageTypes.ConversionOrder) result = result.Add(DamagePacket.Of(type, value, value));
            return result;
        }
    }
}
