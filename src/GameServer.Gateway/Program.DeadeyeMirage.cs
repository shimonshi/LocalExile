using System.Runtime.CompilerServices;
using GameServer.Core.Projectiles;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

partial class Program
{
    internal sealed class DeadeyeMirageState
    {
        public int Generation;
        public bool Ticking;
        public readonly Dictionary<uint, DeadeyeMirageActor> Actors = [];
    }
    static readonly ConditionalWeakTable<ClientState, DeadeyeMirageState> DeadeyeMirageStates = new();
    internal static DeadeyeMirageState DeadeyeMirageOf(ClientState conn) => DeadeyeMirageStates.GetValue(conn, _ => new());

    static string? DeadeyeMirageSignature(ClientState conn, ushort skill)
    {
        var selected = SupportedSkill(conn, skill);
        if (!selected.RequirementsMet || selected.SourceGem is null || !SkillsFor(conn).IsAttack(selected.Effect)
            || GemSkillInstanceDisabled(conn, selected.Effect, skill) is not null
            || ActiveMainHand(conn)?.Type?.Class != "Bow"
            || !selected.Supports.Any(s => s.Effect.Id == "SupportMirageArcher")) return null;
        var weapon = ActiveMainHand(conn)!;
        var stats = CharacterPlayerStats(conn);
        // A changed link/support, weapon, active set or ascendancy ownership invalidates queued actor work.
        return $"{selected.Effect}:{selected.Level}:{selected.Quality}:{selected.SourceGem.GetHashCode()}:"
            + $"{weapon.Value.Item.GetHashCode()}:" + string.Join(',', selected.Supports.Select(s => $"{s.Effect.Row}/{s.Gem.Level}/{s.Gem.Quality}"))
            + $":{DeadeyeValue(stats, "mirage_archers_do_not_attach")}:{DeadeyeValue(stats, "additional_max_mirage_archers")}:"
            + $"{DeadeyeValue(stats, "mirage_archer_duration_+%")}";
    }

    internal static DeadeyeMirageActor? DeadeyeMirageBowHit(ProjectileSession session, ushort skill,
        SpawnedMonster target, bool directPlayer = true)
    {
        var conn = session.Conn;
        lock (conn.Monsters)
        {
            if (!directPlayer || !target.Hostile || !DeadeyeActive(conn) || conn.Paused || InTown(conn)) return null;
            string? signature = DeadeyeMirageSignature(conn, skill);
            if (signature is null) return null;
            var gem = GemSkillEffects(conn).FirstOrDefault(g => g.Id == skill);
            if (gem.Name is null || ResolveProjectileDefinition(gem.Effect) is null) return null;
            DeadeyeMirageStep(session);
            double now = session.Runtime.Now / (double)ProjectileRuntime.QuantaPerSecond;
            var stats = SupportedStats(conn, skill);
            int Get(string name) => DeadeyeValue(stats, name);
            bool detached = Get("mirage_archers_do_not_attach") != 0;
            var state = DeadeyeMirageOf(conn);
            var at = ProjectileSession.Vector(conn.Walk.Position(now));
            // Inclusive no-summon boundary, owner-local, centre to centre. Never refresh while this gate fails.
            int radius = Math.Max(0, Get("cannot_summon_mirage_archer_if_near_mirage_archer_radius"));
            if (detached && state.Actors.Values.Any(a => (a.Position - at).Length <= radius)) return null;
            double seconds = Math.Max(0, Get("support_mirage_archer_base_duration") / 1000d
                * Math.Max(0, 1 + (Get("mirage_archer_duration_+%") + Get("skill_effect_duration_+%")) / 100d));
            if (seconds <= 0) return null;
            var old = !detached ? state.Actors.Values.SingleOrDefault() : null;
            // Repeated owner hits extend an attached archer without interrupting its slower attack or its flights.
            // Step already retired invalid sources/signatures; a different supported skill still replaces the actor.
            if (old is not null && old.Signature == signature && old.Source.Skill == skill)
            {
                old.RefreshLifetime(seconds, now, EffectClock.RecipientRate(18, Get));
                return old;
            }
            if (old is not null) RetireDeadeyeMirage(session, old);
            int limit = Math.Clamp(1 + Get("additional_max_mirage_archers"), 1, 256);
            while (state.Actors.Count >= limit) RetireDeadeyeMirage(session,
                state.Actors.Values.OrderBy(a => a.Generation).First());
            var source = new DeadeyeMarkSource(skill, gem.Effect, gem.Level, gem.Quality, gem.Gem);
            var actor = new DeadeyeMirageActor(conn.NextObjectId++, ++state.Generation, source, signature,
                detached, at, new(seconds, now, EffectClock.RecipientRate(18, Get))) { NextAttack = now };
            state.Actors.Add(actor.Id, actor);
            CreateDeadeyeMirage(session, actor);
            StartDeadeyeMirageTicker(session);
            return actor;
        }
    }

    static bool DeadeyeMirageCurrent(ProjectileSession session, DeadeyeMirageActor actor)
    {
        actor.Clock.Advance(session.Runtime.Now / (double)ProjectileRuntime.QuantaPerSecond);
        return session.Active && DeadeyeActive(session.Conn) && !actor.Retired && actor.Clock.Remaining > 1e-9
            && DeadeyeMirageOf(session.Conn).Actors.GetValueOrDefault(actor.Id) == actor
            && DeadeyeMirageSignature(session.Conn, actor.Source.Skill) == actor.Signature;
    }

    static void CreateDeadeyeMirage(ProjectileSession session, DeadeyeMirageActor actor)
    {
        var conn = session.Conn;
        var variety = Monsters.Find("Metadata/Monsters/Mirage/RangerMirage")
            ?? throw new InvalidOperationException("Native RangerMirage variety is required.");
        if (!variety.Supported) throw new InvalidOperationException("Native RangerMirage components are not serializable.");
        DeadeyeMarkPacket(session, () =>
        {
            byte level = (byte)Math.Clamp(conn.Pools.Level, 1, 100);
            var skill = SkillsFor(conn).FindByRow(actor.Source.Skill, actor.Source.Effect, actor.Source.Level,
                SupportedSkill(conn, actor.Source.Skill).Stats.Select(s => (s.Key, s.Value)).ToArray());
            var model = new MonsterObject((int)actor.Position.X, (int)actor.Position.Y, 1, variety.ComponentsOrBase,
                variety.AnimatedObject, Name: "Mirage Archer", Team: 0, Skills: skill is null ? null : [skill],
                Stats: [(StatKeys.MaximumLife, 1)], StateMachine: variety.StateMachine);
            var packets = new List<byte>();
            if (conn.MonsterTypes.Add((variety.Row, level))) packets.AddRange(InstanceEntities.MonsterTypes([((ushort)variety.Row, level)]));
            packets.AddRange(InstanceEntities.CreateObject(actor.Id,
                InstanceEntities.ObjectTypeHash(MonsterObject.TypePath(variety.Id, level)), model.Blob()));
            packets.AddRange(InstanceProtocol.EnterWakeZone(actor.Id));
            actor.Created = true;
            return packets.ToArray();
        }, () => DeadeyeMirageCurrent(session, actor));
    }

    static void StartDeadeyeMirageTicker(ProjectileSession session)
    {
        var state = DeadeyeMirageOf(session.Conn);
        if (state.Ticking) return;
        state.Ticking = true;
        void Tick()
        {
            state.Ticking = false;
            if (!session.Active) return;
            DeadeyeMirageStep(session);
            if (state.Actors.Count == 0) return;
            state.Ticking = true;
            session.Runtime.Schedule(session.Runtime.Now + ProjectileRuntime.Time(.05), Tick);
        }
        session.Runtime.Schedule(session.Runtime.Now, Tick);
    }

    internal static void DeadeyeMirageStep(ProjectileSession session)
    {
        var conn = session.Conn;
        double now = session.Runtime.Now / (double)ProjectileRuntime.QuantaPerSecond;
        foreach (var actor in DeadeyeMirageOf(conn).Actors.Values.ToArray())
        {
            if (!DeadeyeMirageCurrent(session, actor)) { RetireDeadeyeMirage(session, actor); continue; }
            if (conn.Paused) continue; // Normal runtime pump itself freezes; explicit test advances do not attack.
            var stats = SupportedStats(conn, actor.Source.Skill);
            actor.Clock.ChangeRate(now, EffectClock.RecipientRate(18, name => DeadeyeValue(stats, name)));
            if (actor.Clock.Remaining <= 1e-9) { RetireDeadeyeMirage(session, actor); continue; }
            if (!actor.Detached)
            {
                var at = ProjectileSession.Vector(conn.Walk.Position(now));
                if ((at - actor.Position).Length > .1)
                {
                    actor.Position = at;
                    DeadeyeMarkPacket(session, () => InstanceProtocol.Teleport(actor.Id, (int)at.X, (int)at.Y),
                        () => actor.Created && DeadeyeMirageCurrent(session, actor));
                }
            }
            if (now + 1e-9 < actor.NextAttack || InTown(conn)) continue;
            var target = conn.Monsters.Where(p => p.Value.Hostile && !p.Value.Dead && p.Value.Life > 0)
                .Select(p => (p.Key, Monster: p.Value, Position: ProjectileSession.Vector(p.Value.Walk.Position(now))))
                .Where(p => (p.Position - actor.Position).Length <= 120)
                .OrderBy(p => (p.Position - actor.Position).Length).ThenBy(p => p.Key).FirstOrDefault();
            if (target.Monster is null) { actor.NextAttack = now + .1; continue; }
            int Get(string name) => DeadeyeValue(stats, name);
            double factor = 1 + Get("support_mirage_archer_attack_speed_+%_final") / 100d;
            if (factor <= 0) { actor.NextAttack = now + .1; continue; }
            // Undo the owner's recipient action clock, retaining the skill/weapon's attack-speed calculation.
            // A proxy receives the ally Tailwind base, never the owner's Gale-scaled self effect or chill clock.
            var tailwindSource = DeadeyeTailwindSource(conn);
            var tailwind = DeadeyeGaleAura.ForRecipient([tailwindSource],
                new(actor.Id, actor.Generation), 1, actor.Position, world: tailwindSource.World);
            double actionFactor = tailwind?.ActionFactor(_ => 0) ?? 1;
            double seconds = SupportedAttackSeconds(conn, actor.Source.Skill) * PlayerActionFactor(conn) / factor / actionFactor;
            actor.NextAttack = now + Math.Max(.05, seconds);
            int attack = ++actor.AttackGeneration;
            var origin = actor.Position;
            DeadeyeMarkPacket(session, () => InstanceProtocol.ActionStarted(actor.Id, (int)origin.X, (int)origin.Y,
                (int)target.Position.X, (int)target.Position.Y, actor.Source.Skill, 0xffff, 0x0408, targetObjectId: target.Key),
                () => actor.Created && DeadeyeMirageCurrent(session, actor) && actor.AttackGeneration == attack);
            session.Runtime.Schedule(session.Runtime.Now + ProjectileRuntime.Time(seconds * .5), () =>
            {
                if (!DeadeyeMirageCurrent(session, actor) || actor.AttackGeneration != attack || conn.Paused
                    || target.Monster.Dead || target.Monster.Life == 0) return;
                var use = CaptureDeadeyeMirage(session, actor, target.Monster.Walk.Position(
                    session.Runtime.Now / (double)ProjectileRuntime.QuantaPerSecond), attack, seconds);
                session.Runtime.Release(use);
            });
        }
    }

    internal static ProjectileUse CaptureDeadeyeMirage(ProjectileSession session, DeadeyeMirageActor actor,
        (int X, int Y) target, int attack, double? attackSeconds = null)
    {
        var conn = session.Conn;
        var gem = GemSkillEffects(conn).Single(g => g.Id == actor.Source.Skill);
        var use = CaptureGemProjectile(conn, session, gem.Id, gem, target, new SkillCriticalRoll(0));
        var original = (ProjectileOffense)use.Payload!;
        double factor = Math.Max(0, 1 + DeadeyeValue(SupportedSkill(conn, gem.Id).Stats,
            "support_mirage_archer_damage_+%_final") / 100d);
        // Skill/weapon/support stats come from the owner at each attack, never minion damage, Rage grants or leech.
        var shot = original with
        {
            Hit = original.Hit.Scale(factor), AreaHit = original.AreaHit?.Scale(factor), ClientPredicted = false,
            Leech = new(DamagePacket.Empty, DamagePacket.Empty, DamagePacket.Empty), HitPacket = false,
            Ignite = ScaleDeadeyeMirageIgnite(original.Ignite, factor), Label = "Mirage Archer: " + original.Label
        };
        var profile = use.Profile;
        if (profile.Barrage)
        {
            var tailwindSource = DeadeyeTailwindSource(conn);
            var tailwind = DeadeyeGaleAura.ForRecipient([tailwindSource], new(actor.Id, actor.Generation), 1,
                actor.Position, world: tailwindSource.World);
            double proxySeconds = attackSeconds ?? SupportedAttackSeconds(conn, gem.Id) * PlayerActionFactor(conn)
                / Math.Max(.01, 1 + DeadeyeValue(SupportedStats(conn, gem.Id), "support_mirage_archer_attack_speed_+%_final") / 100d)
                / (tailwind?.ActionFactor(_ => 0) ?? 1);
            // Every arrow belongs to the already admitted proxy attack, whose duration is frozen at its start.
            profile = profile with { SequentialDelay = Math.Max(1, ProjectileRuntime.Time(proxySeconds * .5 / profile.Count)) };
        }
        return use with
        {
            Profile = profile,
            Owner = new(PlayerId, conn.ProjectileEpoch), Emitter = new(actor.Id, actor.Generation),
            Origin = actor.Position, Aim = ProjectileSession.Vector(target),
            Payload = new DeadeyeMirageOffense(shot, actor, attack)
            {
                DirectMarkSource = CaptureDeadeyeMarkHitSource(conn, gem.Id, false) is { } direct
                    ? new(flat => direct.Resolve(flat).Scale(factor), direct.ResolveIgnite is { } directIgnite
                        ? flat => ScaleDeadeyeMirageIgnite(directIgnite(flat), factor) : null) : null,
                AreaMarkSource = CaptureDeadeyeMarkHitSource(conn, gem.Id, true) is { } area
                    ? new(flat => area.Resolve(flat).Scale(factor), area.ResolveIgnite is { } areaIgnite
                        ? flat => ScaleDeadeyeMirageIgnite(areaIgnite(flat), factor) : null) : null
            }, ProjectilePayloads = null,
            ReturnAnchor = new(PlayerId, conn.ProjectileEpoch)
        };
    }

    static MonsterIgnites.Settings ScaleDeadeyeMirageIgnite(MonsterIgnites.Settings input, double factor) =>
        input with { Min = input.Min * factor, Max = input.Max * factor,
            CriticalMin = input.CriticalMin * factor, CriticalMax = input.CriticalMax * factor };

    internal static void RetireDeadeyeMirage(ProjectileSession session, DeadeyeMirageActor actor)
    {
        actor.Retired = true; ++actor.AttackGeneration;
        DeadeyeMirageOf(session.Conn).Actors.Remove(actor.Id);
        DeadeyeMarkPacket(session, () => InstanceProtocol.RemoveObject(actor.Id), () => actor.Created);
    }

    internal static void RetireDeadeyeMirages(ProjectileSession session)
    {
        var state = DeadeyeMirageOf(session.Conn); ++state.Generation;
        foreach (var actor in state.Actors.Values.ToArray()) RetireDeadeyeMirage(session, actor);
    }

    // Shared ProjectileSession.Hit routes this explicit payload here before the ordinary player branch.
    internal static ProjectileHitResult DeadeyeMirageHit(ProjectileSession session, ProjectileDamage contact,
        DeadeyeMirageOffense proxy)
    {
        if (!DeadeyeMirageCurrent(session, proxy.Actor) || session.Conn.Paused) return ProjectileHitResult.Miss;
        return session.HitOwner(contact, proxy.Shot with
        {
            MarkDirectBasis = proxy.DirectMarkSource,
            MarkAreaBasis = proxy.AreaMarkSource
        }, SkillDamageAttribution.Mirage);
    }
}
