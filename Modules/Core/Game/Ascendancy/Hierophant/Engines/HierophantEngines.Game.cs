using System.Runtime.CompilerServices;
using GameServer.Core.Projectiles;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

static partial class Game
{
    internal sealed record HierophantBrandUse(ResourceEffectsState Owner, int Epoch, int Action,
        int Interruption, ushort Skill, ushort Sequence, string Signature, long ContactAt, BrandPresentationIdentity? Presentation = null);

    // Capture inside accepted payment, before output can await and replace the source or advance the clock.
    internal static HierophantBrandUse? CaptureHierophantBrandUse(ClientState c, ushort skill, int action, ushort sequence)
    {
        var s = SupportedSkill(c, skill);
        if (SkillsFor(c).EffectId(s.Effect) is not ("StormBrand" or "ArmageddonBrand")
            || HierophantEngineSignature(c, skill) is not { } signature || CastMs(c, s.Effect, skill) is not int cast)
            return null;
        var owner = ResourceEffectsOf(c);
        if (!ResourceEffectsCurrent(c, owner) || owner.Loading) return null;
        double delay = Math.Max(0, cast) / 1000d * Animations.SpellAreaRelease(SkillsFor(c).ClassFor(c.ClassRow).AnimatedObject);
        return new(owner, c.ProjectileEpoch, action, c.ActionInterruptionGeneration, skill, sequence, signature,
            ProjectileRuntime.Time(ResourceEffectsTime(c) + delay), CaptureBrandPresentation(c, skill, sequence));
    }

    internal static void ScheduleHierophantBrand(ProjectileSession session, HierophantBrandUse use,
        (int X, int Y) at, Func<bool> current)
    {
        // Advance and sample under the same mutation gate. A new session starts at zero, while
        // its pump may already have committed the current connection time before this cast schedules.
        var c = session.Conn;
        bool Current() => session.Active && current() && c.ProjectileEpoch == use.Epoch
            && c.ActionCount == use.Action && c.ActionInterruptionGeneration == use.Interruption
            && ResourceEffectsCurrent(c, use.Owner) && !use.Owner.Loading
            && HierophantEngineSignature(c, use.Skill) == use.Signature;
        using (ProjectileMutation(c))
            if (Current())
                session.Runtime.Schedule(Math.Max(session.Runtime.Now, use.ContactAt), () =>
                {
                    if (Current()) HierophantEngineAdmit(session, use.Skill, use.Action, at,
                        placement: use);
                });
    }

    internal sealed class HierophantEngineActor(uint id, ushort skill, string signature, (int X, int Y) at)
    {
        internal readonly uint Id = id;
        internal readonly ushort Skill = skill;
        internal readonly string Signature = signature;
        internal (int X, int Y) At = at;
        internal TotemState.Totem? Totem;
        internal HierophantBrandClock? Brand;
        internal HierophantBrandUse? Use;
        internal BrandPresentationState? Presentation;
        internal uint Target;
        internal SpawnedMonster? Attached;
        internal double Next, FractionalLife;
        internal int RecallSearch;
    }
    sealed class HierophantEngineWorld
    {
        internal readonly List<HierophantEngineActor> Actors = [];
        internal int LastUse = -1;
        internal ResourceEffectsState? Owner;
        internal double Last;
    }
    static readonly ConditionalWeakTable<ProjectileSession, HierophantEngineWorld> HierophantEngineWorlds = new();
    internal static IReadOnlyList<HierophantEngineActor> HierophantEngineActors(ProjectileSession session) =>
        HierophantEngineWorlds.GetValue(session, _ => new()).Actors;
    internal static bool HierophantSpellTotemPayload(ClientState c, int effect) =>
        SkillsFor(c).EffectId(effect) == "IceNova"
        || SkillsFor(c).EffectId(effect) is ("Fireball" or "FreezingPulse")
            && ResolveProjectileDefinition(effect) is { RequiresTerrain: false };
    internal static string? HierophantEngineSignature(ClientState c, ushort skill)
    {
        var s = SupportedSkill(c, skill);
        if (!s.RequirementsMet || s.SourceGem is null || SkillsFor(c).IsAttack(s.Effect)
            || GemSkillInstanceDisabled(c, s.Effect, skill) is not null) return null;
        string? id = SkillsFor(c).EffectId(s.Effect);
        bool supportedTotem = s.Supports.Any(x => x.Effect.Id == "SupportSpellTotem")
            && HierophantSpellTotemPayload(c, s.Effect);
        if (id is not ("StormBrand" or "ArmageddonBrand" or "ShockwaveTotem") && !supportedTotem) return null;
        // Source identity only. Live offense and node refunds are read afresh, not frozen in a global proxy stat bag.
        return $"{s.SourceGem.RuntimeIdentity}:{s.Effect}:{s.Level}:{s.Quality}:"
            + string.Join(',', s.Supports.Select(x => $"{x.Effect.Row}/{x.Gem.Level}/{x.Gem.Quality}"));
    }
    internal static MonsterCatalog.Variety? HierophantOffensiveTotemVariety(ClientState c, ushort skill)
    {
        if (HierophantEngineSignature(c, skill) is null) return null;
        var s = SupportedSkill(c, skill);
        // Native spell gems use sentinel 22 (no variation row). Explicit local skin policy for that support:
        // the pinned IntelligenceTotem; do not claim the native attribute/skin selection algorithm is recovered.
        if (s.Supports.Any(x => x.Effect.Id == "SupportSpellTotem"))
            return Monsters.Find("Metadata/Monsters/Totems/IntelligenceTotem") is { Supported: true } skin ? skin : null;
        return TotemVarieties.Value.TryGetValue(s.Effect, out int row)
            && Monsters.Find(row.ToString()) is { Supported: true } v ? v : null;
    }
    static bool HierophantEngineLive(ProjectileSession session) => session.Active && !session.Conn.Closed
        && !session.Conn.LeavingArea && !session.Conn.RageRetired && session.Conn.PlayerLife > 0;
    internal static bool HierophantEngineCurrent(ProjectileSession session, HierophantEngineActor actor) =>
        HierophantEngineLive(session) && !session.Conn.Paused
        && HierophantEngineWorlds.TryGetValue(session, out var world) && world.Owner is { } owner
        && ResourceEffectsCurrent(session.Conn, owner) && !owner.Loading
        && HierophantEngineActors(session).Contains(actor)
        && HierophantEngineSignature(session.Conn, actor.Skill) == actor.Signature
        && (actor.Totem?.Alive(ResourceEffectsTime(session.Conn)) ?? actor.Brand?.Alive == true);

    /// <summary>Call once at accepted placement release, under XP gate then Monsters; payment belongs to admission.</summary>
    internal static bool HierophantEngineAdmit(ProjectileSession session, ushort skill, int acceptedAction,
        (int X, int Y) at, TotemState.Totem? createdTotem = null, HierophantBrandUse? placement = null)
    {
        var c = session.Conn;
        if (!HierophantEngineLive(session) || c.Paused || InTown(c)
            || !OrdinaryOf(c, _ => 0).Accepted(c.ProjectileEpoch, acceptedAction)
            || HierophantEngineSignature(c, skill) is not { } signature) return false;
        var world = HierophantEngineWorlds.GetValue(session, _ => new());
        if (acceptedAction <= world.LastUse) return false;
        double now = ResourceEffectsTime(c);
        var s = SupportedSkill(c, skill); string? id = SkillsFor(c).EffectId(s.Effect);
        var stats = SupportedStats(c, skill); int Get(string n) => MarkStat(stats, n);
        bool brand = id is "StormBrand" or "ArmageddonBrand";
        if (!brand && (createdTotem is null || createdTotem.SkillId != skill || !createdTotem.Alive(now)
            || !c.Totems.All.Contains(createdTotem))) return false;
        int limit = Math.Max(0, Get("base_total_number_of_sigils_allowed"));
        if (brand && (limit == 0 || Get("base_sigil_repeat_frequency_ms") <= 0)) return false;
        world.LastUse = acceptedAction;
        var duration = brand ? BrandDurationFor(c, FinalPlayerStats(c).ToDictionary(x => x.Item1, x => x.Item2), s.Stats) : default;
        if (brand && !duration.Alive) return false; // A paid zero-duration use cannot later replay into a live actor.
        HierophantBrandClock? clock = brand ? new(Math.Min(now, placement is null ? now
            : placement.ContactAt / (double)ProjectileRuntime.QuantaPerSecond),
            duration.AttachedMilliseconds / 1000d, duration.DetachedMilliseconds / 1000d) : null;
        if (clock is not null)
        {
            // Network output can delay scheduling past contact. Do not restart an expired prediction's budget.
            clock.Advance(now, false, 0, 0, 0);
            if (!clock.Alive) return false;
        }
        var owner = ResourceEffectsOf(c);
        if (!ReferenceEquals(world.Owner, owner)) { RetireHierophantEngines(session); world.Owner = owner; }
        var actor = new HierophantEngineActor(createdTotem?.Id ?? c.NextObjectId++, skill, signature, at)
            { Totem = createdTotem, Next = now, Use = placement,
                Presentation = placement?.Presentation is { } identity ? new(identity) : null };
        if (brand)
        {
            actor.Brand = clock;
            while (world.Actors.Count(x => x.Brand is not null) >= limit)
                RemoveHierophantActor(session, world, world.Actors.First(x => x.Brand is not null), now);
        }
        if (world.Actors.Count == 0) world.Last = now;
        world.Actors.Add(actor);
        return true;
    }

    static void RemoveHierophantActor(ProjectileSession session, HierophantEngineWorld world,
        HierophantEngineActor actor, double now)
    {
        RetireHierophantActor(session, actor, now); world.Actors.Remove(actor);
    }
    internal static void RetireHierophantEngines(ProjectileSession session)
    {
        if (HierophantEngineWorlds.TryGetValue(session, out var world))
        {
            foreach (var actor in world.Actors) RetireHierophantActor(session, actor,Math.Max(world.Last,ResourceEffectsTime(session.Conn)));
            world.Actors.Clear(); world.Owner = null;
        } // Keep use watermark: a stale accepted receipt cannot revive actors.
    }
    static void RetireHierophantActor(ProjectileSession session, HierophantEngineActor actor,double now)
    { RetireBrandPresentation(session, actor); if (actor.Totem is { Life: > 0 } body) { body.Life=0; body.DiedAt=now; } }

    /// <summary>Called on the existing world pump, under its XP -> Monsters locks. Never start another timer.</summary>
    internal static void HierophantEngineStep(ProjectileSession session)
    {
        var c = session.Conn; var world = HierophantEngineWorlds.GetValue(session, _ => new());
        if (!HierophantEngineLive(session)) { RetireHierophantEngines(session); return; }
        if (c.Paused) return;
        double now = ResourceEffectsTime(c), dt = Math.Max(0, now - world.Last); world.Last = now;
        // Regenerate each totem's own Life. The player's projected regeneration must never be copied to it.
        var player = CharacterPlayerStats(c);
        int living = c.Totems.Living(now).Count();
        foreach (var actor in world.Actors.ToArray())
        {
            if (!HierophantEngineCurrent(session, actor)) { RetireHierophantActor(session, actor,now); world.Actors.Remove(actor); continue; }
            var stats = SupportedStats(c, actor.Skill); int Get(string n) => MarkStat(stats, n);
            if (actor.Brand is not null)
            {
                int cap = Math.Max(0, Get("base_total_number_of_sigils_allowed"));
                while (world.Actors.Count(x => x.Brand is not null) > cap)
                    RemoveHierophantActor(session, world, world.Actors.First(x => x.Brand is not null), now);
                if (!world.Actors.Contains(actor)) continue;
                int attachedCap = Math.Max(0, Get("base_number_of_sigils_allowed_per_target"));
                if (actor.Target != 0 && !world.Actors.Where(x => x.Brand is not null && x.Target == actor.Target)
                    .Take(attachedCap).Contains(actor)) { actor.Target = 0; actor.Attached = null; }
            }
            if (actor.Totem is { } totem)
            {
                double regen = Math.Max(0, Get("totem_life_regeneration_per_minute_%")
                    + living * MarkStat(player, "you_and_totem_life_regeneration_rate_per_minute_%_per_active_totem"));
                actor.FractionalLife += totem.MaxLife * regen * dt / 6000;
                uint gain = (uint)Math.Min(uint.MaxValue, Math.Floor(actor.FractionalLife));
                actor.FractionalLife -= gain; totem.Life = (uint)Math.Min(totem.MaxLife, (long)totem.Life + gain);
                if (totem.Life == totem.MaxLife) actor.FractionalLife = 0;
            }
            bool attached = actor.Attached is { Dead: false, Life: > 0 } old
                && c.Monsters.GetValueOrDefault(actor.Target) == old;
            if (attached) actor.At = actor.Attached!.Walk.Position(now);
            if (actor.Brand is { } clock)
            {
                double rate = Math.Max(0, 1 + Get("sigil_repeat_frequency_+%") / 100d)
                    * Math.Max(0, 1 + Get("cast_speed_+%") / 100d);
                int pulses = clock.Advance(now, attached, Get("base_sigil_repeat_frequency_ms") / 1000d, rate,
                    Get("brand_activation_rate_+%_final_during_last_20%_of_active_duration"));
                if (!clock.Alive) { RemoveHierophantActor(session, world, actor, now); continue; }
                if (!attached)
                {
                    actor.Target = 0; actor.Attached = null;
                    var target = HierophantEngineTargets(c, actor.At, 30 * Math.Max(0,
                        1 + (Get("sigil_target_search_range_+%") + actor.RecallSearch) / 100d), now)
                        .FirstOrDefault(x => world.Actors.Count(a => a.Brand is not null && a.Target == x.Id)
                            < Math.Max(0, Get("base_number_of_sigils_allowed_per_target")));
                    if (target.Monster is not null)
                    { actor.Target = target.Id; actor.Attached = target.Monster; actor.RecallSearch = 0; }
                }
                PresentBrandAttachment(session, actor);
                for (int i = 0; i < Math.Min(32, pulses); i++)
                    if (actor.Attached is { Dead: false, Life: > 0 }) HierophantEngineActivate(session, actor);
            }
            else if (now >= actor.Next)
            {
                double range = Math.Max(0, Get("base_totem_range"));
                var target = HierophantEngineTargets(c, actor.At, range, now).FirstOrDefault();
                if (target.Monster is null) continue;
                // Totems use spell cast speed, never the placement speed or Fanaticism's hand-cast multiplier.
                double interval = SkillsFor(c).CastTimeMs(SupportedSkill(c, actor.Skill).Effect) / 1000d
                    / Math.Max(.01, 1 + Get("cast_speed_+%") / 100d)
                    / Math.Max(.01, 1 + Get("support_spell_totem_cast_speed_+%_final") / 100d);
                actor.Next = now + Math.Max(TickSeconds, interval);
                HierophantEngineActivate(session, actor, target.Monster.Walk.Position(now));
            }
        }
    }
    static IEnumerable<(uint Id, SpawnedMonster Monster)> HierophantEngineTargets(ClientState c,
        (int X, int Y) at, double range, double now) => c.Monsters
        .Where(x => x.Value.Hostile && !x.Value.Dead && x.Value.Life > 0 && !x.Value.Invulnerable
            && x.Value.OwnerId is null && Dist(x.Value.Walk.Position(now), at) <= range)
        .OrderBy(x => Dist(x.Value.Walk.Position(now), at)).ThenBy(x => x.Key).Select(x => (x.Key, x.Value));

    internal static DamagePacket HierophantEngineHit(ClientState c, HierophantEngineActor actor, bool area)
    {
        var inputs = HierophantTotemHitInputs(c, actor, area);
        if (inputs is null) return DamagePacket.Empty;
        var get = inputs.Get;
        // Brand damage joins the increased bucket BEFORE conversion/more, rather than multiplying the final hit.
        if (actor.Brand is not null)
            inputs = inputs with { Get = n => n == "damage_+%" ? get(n) + get("sigil_damage_+%") : get(n) };
        return SupportedHitFrom(inputs, suppress: false);
    }
    internal static void HierophantEngineActivate(ProjectileSession session, HierophantEngineActor actor,
        (int X, int Y)? aim = null, Random? random = null)
    {
        if (!HierophantEngineCurrent(session, actor)) return;
        // An autonomous activation is not a paid use. Clear any ambient admission contribution;
        // the original placement's Distiller contribution deliberately does not apply to later activations.
        using var autonomous = BeginOrdinaryUse(session.Conn);
        CaptureOrdinarySkillUse(session.Conn);
        var c = session.Conn; var gem = GemSkillEffects(c).Single(x => x.Id == actor.Skill);
        double now = ResourceEffectsTime(c); var stats = SupportedStats(c, actor.Skill);
        var player = HierophantOffenseStats(c);
        var own = new Dictionary<uint, int>(SupportedSkill(c, actor.Skill).Stats);
        var critical = CaptureSkillCriticalRoll(c, actor.Skill);
        var chance = SupportedReleaseCriticalChance(c, actor.Skill);
        var blindedChance = SupportedCriticalChanceVsBlinded(c, actor.Skill, release: true);
        var profaneChance = SupportedCriticalChanceVsProfane(c, actor.Skill, release: true);
        int multiplier = SupportedCriticalMultiplier(c, actor.Skill);
        var poison = SupportedPoison(c, actor.Skill, true, player);
        var ignite = SupportedIgnite(c, actor.Skill, true, player);
        if (actor.Totem is not null && HierophantTotemHitInputs(c, actor, false) is { } ailmentInputs)
        {
            var rows = SupportedAilmentRowsFrom(ailmentInputs, "spell");
            poison = MonsterPoisons.Settings.From(rows, "spell", ailmentInputs.Get);
            ignite = MonsterIgnites.Settings.From(rows, "spell", ailmentInputs.Get);
        }
        if (actor.Totem is not null && ResolveProjectileDefinition(gem.Effect) is not null)
        {
            var use = CaptureGemProjectile(c, session, gem.Id, gem, aim ?? actor.At, critical,
                casterOrigin: ProjectileSession.Vector(actor.At));
            var shot = (ProjectileOffense)use.Payload!;
            shot = shot with { Hit = HierophantEngineHit(c, actor, false),
                AreaHit = HierophantEngineHit(c, actor, true), Poison = poison, Ignite = ignite };
            // ObjectProjectileOffense is an existing generic contact executor. It avoids all owner hit/leech callbacks.
            session.Runtime.Release(use with { Emitter = new(actor.Id, c.ProjectileEpoch), Origin = ProjectileSession.Vector(actor.At),
                ReturnAnchor = null, ProjectilePayloads = null,
                Payload = new ObjectProjectileOffense("Owned spell totem", (host, contact) =>
                {
                    if (!HierophantEngineCurrent(host, actor) || !c.Monsters.TryGetValue(contact.Target.Id, out var m)
                        || host.ActorKey(contact.Target.Id, m) != contact.Target) return ProjectileHitResult.Miss;
                    return HierophantEngineContact(host, actor, contact.Target.Id, m,
                        (contact.IsArea ? shot.AreaHit ?? shot.Hit : shot.Hit).Scale(contact.Multiplier),
                        shot.Penetration, shot.ResistanceMods, contact.Projectile.RandomSource,
                        player, shot.SkillStats, CriticalChanceOn(c,contact.Target.Id,shot.CriticalChance,
                            shot.VsBlindedCriticalChance,shot.VsProfaneCriticalChance), shot.CriticalMultiplier, shot.CriticalRoll,
                        shot.Poison, shot.Ignite, shot.Bleed,
                        shot.ImpaleSettings,shot.CriticalImpaleSettings);
                }) { Presentation = shot.Presentation } });
            return;
        }
        int Get(string n) => MarkStat(stats, n);
        var center = actor.Brand is not null ? actor.Attached?.Walk.Position(now) ?? actor.At : actor.At;
        double radius = SkillArea.Radius(Math.Max(1, Get("active_skill_base_area_of_effect_radius")), Get, spell: true);
        bool storm = SkillsFor(c).EffectId(gem.Effect) == "StormBrand";
        var targets = HierophantEngineTargets(c, center, storm ? 30 : radius, now)
            .OrderBy(x => x.Id == actor.Target ? 0 : 1).ToArray();
        int count = storm ? Math.Max(1, Get("conduit_sigil_number_of_beam_targets")) : int.MaxValue;
        var raw = player;
        var activation = HierophantEngineHit(c, actor, true);
        foreach (var (id, m) in targets.Take(count))
        {
            double attachedMore = actor.Brand is not null && id == actor.Target
                ? Math.Max(0, 1 + Get("sigil_attached_target_hit_damage_+%_final") / 100d) : 1;
            HierophantEngineContact(session, actor, id, m, activation.Scale(attachedMore),
                Penetration.Of(raw, Stats, false, own), HitResistMods(raw, own), random ?? Random.Shared,
                player, own, CriticalChanceOn(c, id, chance, blindedChance, profaneChance),
                multiplier, critical, poison, ignite);
        }
    }

    internal static ProjectileHitResult HierophantEngineContact(ProjectileSession session, HierophantEngineActor actor,
        uint id, SpawnedMonster target, DamagePacket packet, Penetration.PerType penetration, HitResistances.Mods mods, Random random,
        IReadOnlyDictionary<uint, int>? capturedPlayer = null, IReadOnlyDictionary<uint, int>? capturedSkill = null,
        int? capturedChance = null, int? capturedMultiplier = null, SkillCriticalRoll? rolls = null,
        MonsterPoisons.Settings poison = default,
        MonsterIgnites.Settings ignite = default, MonsterIgnites.Settings bleed = default,
        ImpaleStats.Settings? impale = null, ImpaleStats.Settings? criticalImpale = null)
    {
        var c = session.Conn;
        if (!HierophantEngineCurrent(session, actor) || !target.Hostile || target.Dead || target.Life == 0
            || target.Invulnerable || c.Monsters.GetValueOrDefault(id) != target) return ProjectileHitResult.Miss;
        if (MonsterBlocks(target, random, true)) return ProjectileHitResult.Blocked;
        var player = capturedPlayer ?? CharacterPlayerStats(c); var own = capturedSkill ?? SupportedSkill(c, actor.Skill).Stats;
        double now = ResourceEffectsTime(c);
        var gambler = GamblerFor(player);
        packet = new SkillDamageContext(SkillHitKind.Spell, player).Hit(packet)
            .Scale(OrdinaryTargetMore(c, target, player));
        var rolled = WithConditionalSupportDamageLocked(c, own, false, target,
            GameEvents.WithOutgoingDamage(c, player, target, (gambler with { DamageMorePct = 0 })
                .ForSkill(n => MarkStat(own, n), true).RollOutgoingHit(packet, random)), now);
        bool critical = RollCriticalStrike(c, capturedChance ?? SupportedReleaseCriticalChance(c, actor.Skill), own,
            attack: false, skillRoll: rolls ?? CaptureSkillCriticalRoll(c, actor.Skill), player: player);
        int sourceMultiplier = (capturedMultiplier ?? SupportedCriticalMultiplier(c, actor.Skill))
            + OrdinaryCriticalBonus(c, player) + MarkOn(c, target, "enemy_additional_critical_strike_multiplier_against_self");
        int multiplier = CriticalMultiplierOn(target, sourceMultiplier);
        var (damage, _, mitigated) = MonsterHitDamage(c, target, rolled, critical, false, now, mods, penetration,
            gambler.InvertsResistance(random));
        if (critical) damage = DamageModel.CriticalDamage(damage, multiplier);
        if (actor.Brand is not null)
            LeechOnHitLocked(c, target, MonsterTakenComponents(target, mitigated, true), damage,
                HitLeechSources(player, false, own));
        var impaleSettings = critical ? criticalImpale ?? ImpaleStats.From(player,Stats,false,own,critical:true)
            : impale ?? ImpaleStats.From(player,Stats,false,own);
        double physical = rolled[DamageType.Physical].Min * (critical ? Math.Max(0,sourceMultiplier)/100d : 1);
        var (reflected, _) = target.Impale.OnHit(physical,now,impaleSettings,random,target.Mods.Effects["avoid_impale_%"]);
        damage = ImpaleStats.AddReflected(damage,MonsterImpaleDamage(target,reflected));
        // Totem hits are ally hits; Brands belong to the caster but never masquerade as a hand-cast use.
        var hit = ApplyMonsterHitLocked(c, target, damage, allyAt: actor.Totem is null ? null : actor.At);
        if (hit is null) return ProjectileHitResult.Miss;
        OrdinaryHierophantSpellHit(c, player, true, true, true);
        AilmentsOnHitLocked(c, target, mitigated, true, critical, multiplier, player, own, now, random,
            poison: poison,
            reflectToPlayer: actor.Totem is null, provenance: actor.Totem is null ? HitProvenance.Owner : HitProvenance.Proxy);
        IgniteOnHitLocked(session, id, target, ignite with { Proxy=actor.Totem is not null }, critical, now);
        BleedOnHitLocked(session, id, target, bleed with { Proxy=actor.Totem is not null }, critical, now);
        if (actor.Brand is not null)
        {
            var source = SupportedSkill(c, actor.Skill);
            ChargesOnHit(c, target, critical, spell: true, source.Effect, source.Level, source.Quality);
            if (critical) CommitProjectileCritFlasks(session, now, random);
        }
        session.Packet(InstanceProtocol.LifeUpdate(id, hit.Value.Life), FormattableString.Invariant(
            $"event=hierophant-contact epoch={c.ProjectileEpoch} time={now:R} actor={actor.Id} body={actor.Totem?.Id} origin={actor.At.X},{actor.At.Y} skill={actor.Skill} source={actor.Signature} target={id} outcome=hit life={hit.Value.Life}"));
        if (hit.Value.StageStarts) CommitProjectileBossStage(session, id, target, ProjectileRuntime.Time(now));
        if (hit.Value.Life == 0) CommitProjectileKill(session, id, target, ProjectileRuntime.Time(now), random,
            playerKill: actor.Totem is null);
        if (actor.Brand is not null) CommitOwnerHitCharges(session, now, random);
        return ProjectileHitResult.Hit;
    }
}
