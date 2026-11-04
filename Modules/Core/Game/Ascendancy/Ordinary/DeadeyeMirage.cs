using System.Runtime.CompilerServices;
using GameServer.Core.Projectiles;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

static partial class Game
{
    internal sealed record MirageReceipt(DeadeyeMirageLifecycle.Release Admission, ushort Skill, string Signature)
    {
        internal int Epoch => (int)Admission.Area;
    }
    internal sealed class MirageBody(uint id, SpawnedMonster monster, DeadeyeMirageLifecycle.Actor actor)
    {
        internal readonly uint Id = id;
        internal readonly SpawnedMonster Monster = monster;
        internal readonly DeadeyeMirageLifecycle.Actor Actor = actor;
        internal bool Created;
    }
    sealed class MirageWorld
    {
        internal readonly DeadeyeMirageLifecycle Life = new();
        internal readonly Dictionary<long, MirageBody> Bodies = [];
    }
    static readonly ConditionalWeakTable<ProjectileSession, MirageWorld> MirageWorlds = new();
    const string MirageKind = "MirageArcherProxy";
    internal const string MirageVariety = "Metadata/Monsters/Mirage/RangerMirage";

    internal static string? MirageSignature(ClientState c, ushort skill)
    {
        var s = SupportedSkill(c, skill);
        if (!s.RequirementsMet || s.SourceGem is null || !ArrowsFor(c, s.Effect)
            || GemSkillInstanceDisabled(c, s.Effect, skill) is not null
            || !s.Supports.Any(x => x.Effect.Id == "SupportMirageArcher")
            || ResolveProjectileDefinition(s.Effect) is null) return null;
        var stats = SupportedStats(c, skill);
        // Immutable item identity includes the sockets/links. Refunds and source replacement retire pending attacks.
        return $"{s.SourceGem.GetHashCode()}:{ActiveMainHand(c)?.Item.GetHashCode()}:{s.Effect}:{s.Level}:{s.Quality}:"
            + string.Join(',', s.Supports.Select(x => $"{x.Effect.Row}/{x.Gem.Level}/{x.Gem.Quality}"))
            + $":{MarkStat(stats, "mirage_archers_do_not_attach")}:{MarkStat(stats, "additional_max_mirage_archers")}:"
            + MarkStat(stats, "cannot_summon_mirage_archer_if_near_mirage_archer_radius");
    }

    internal static DeadeyeMirageLifecycle.Release? PaidMirageAdmission(ClientState c, int action) =>
        OrdinaryOf(c, _ => 0).Accepted(c.ProjectileEpoch, action) ? new(c.ProjectileEpoch, action) : null;

    internal static ProjectileUse PaidMirageRelease(ClientState c, DeadeyeMirageLifecycle.Release? admission,
        ushort skill, ProjectileUse use)
    {
        if (admission is null || admission.Area != c.ProjectileEpoch || use.Payload is not ProjectileOffense shot
            || MirageSignature(c, skill) is not { } signature) return use;
        return use with { Payload = shot with { MirageRelease = new(admission, skill, signature) } };
    }

    internal static MirageBody? MirageOwnerHit(ProjectileSession session, MirageReceipt receipt, SpawnedMonster target)
    {
        var c = session.Conn;
        if (!session.Active || c.PlayerLife == 0 || c.Paused || InTown(c) || !target.Hostile
            || target.Invulnerable || receipt.Epoch != c.ProjectileEpoch
            || MirageSignature(c, receipt.Skill) != receipt.Signature) return null;
        var variety = Monsters.Find(MirageVariety);
        if (variety is not { Supported: true }) return null;
        double now = session.Runtime.Now / (double)ProjectileRuntime.QuantaPerSecond;
        MirageStep(session, now);
        var world = MirageWorlds.GetValue(session, _ => new());
        var stats = SupportedStats(c, receipt.Skill);
        int Get(string n) => MarkStat(stats, n);
        var actor = world.Life.OwnerHit(now, receipt.Admission, true, true, receipt.Epoch,
            receipt.Signature, receipt.Skill, ProjectileSession.Vector(c.Walk.Position(now)), Get);
        RetireMirageBodies(session, world);
        if (actor is null) return null;
        if (world.Bodies.TryGetValue(actor.Identity, out var same)) return same;
        uint id = c.NextObjectId++;
        var m = new SpawnedMonster("Mirage Archer", c.NewMonsterWalk(id, (int)actor.Position.X, (int)actor.Position.Y,
            0, -1, false), 0, 0, 1) { OwnerId = PlayerId, Hostile = false, Variety = variety,
            Level = (byte)Math.Clamp(c.Pools.Level, 1, 100) };
        var body = new MirageBody(id, m, actor);
        world.Bodies.Add(actor.Identity, body);
        c.Monsters.Add(id, m);
        // Generic owned roster, with no minion AI/aura/damage scaling. The projectile session owns its attacks.
        c.Minions.Add(new(id, m, MirageKind, MirageKind) { Until = double.PositiveInfinity });
        c.ProjectileObjects.Add(id);
        session.Packet(() => MirageCurrent(session, body) ? MirageCreatePacket(c, body) : null, "Mirage native actor");
        return body;
    }

    internal static byte[] MirageCreatePacket(ClientState c, MirageBody body)
    {
        var v = body.Monster.Variety!;
        var s = SupportedSkill(c, body.Actor.Skill);
        var skill = SkillsFor(c).FindByRow(body.Actor.Skill, s.Effect, s.Level, s.Stats.Select(x => (x.Key, x.Value)).ToArray());
        if (skill is null) return [];
        var bytes = new List<byte>();
        if (c.MonsterTypes.Add((v.Row, body.Monster.Level)))
            bytes.AddRange(InstanceEntities.MonsterTypes([((ushort)v.Row, body.Monster.Level)]));
        var blob = new MonsterObject((int)body.Actor.Position.X, (int)body.Actor.Position.Y, 1,
            v.ComponentsOrBase, v.AnimatedObject, Name: "Mirage Archer", Team: 0, Skills: [skill],
            Stats: [(StatKeys.MaximumLife, 1)], StateMachine: v.StateMachine);
        bytes.AddRange(InstanceEntities.CreateObject(body.Id,
            InstanceEntities.ObjectTypeHash(MonsterObject.TypePath(v.Id, body.Monster.Level)), blob.Blob()));
        // Both actor types have Inventories at component 8; reuse the native equipment writer and bow stance.
        bytes.AddRange(InstanceEntities.PlayerEquipmentUpdate(body.Id,
            Looks(c, ItemsOf(c)).Where(x => x.Slot is 0 or 1 or 7).ToArray()));
        bytes.AddRange(InstanceProtocol.EnterWakeZone(body.Id));
        body.Created = true;
        return [.. bytes];
    }

    internal static bool MirageCurrent(ProjectileSession session, MirageBody body) => session.Active
        && session.Conn.PlayerLife > 0 && !body.Monster.Dead
        && session.Conn.Monsters.GetValueOrDefault(body.Id) == body.Monster
        && MirageWorlds.GetValue(session, _ => new()).Life.Actors.Contains(body.Actor)
        && body.Actor.EquipmentSignature == MirageSignature(session.Conn, body.Actor.Skill)
        && body.Actor.Clock.Deadline > session.Runtime.Now / (double)ProjectileRuntime.QuantaPerSecond;

    static void RetireMirageBodies(ProjectileSession session, MirageWorld world)
    {
        var c = session.Conn;
        // Includes identities left by a replaced Game/session. Old attacks cannot pass its current cipher gate.
        foreach (var minion in c.Minions.All.Where(x => x.Kind == MirageKind).ToArray())
        {
            var body = world.Bodies.Values.FirstOrDefault(x => x.Id == minion.Id);
            if (body is not null && MirageCurrent(session, body)) continue;
            if (body is not null) world.Bodies.Remove(body.Actor.Identity);
            c.Minions.Remove(minion.Id);
            if (c.Monsters.GetValueOrDefault(minion.Id) == minion.Monster) c.Monsters.Remove(minion.Id);
            minion.Monster.Dead = true;
            session.Packet(() => { c.ProjectileObjects.Remove(minion.Id); return InstanceProtocol.RemoveObject(minion.Id); },
                "Mirage retired");
        }
    }

    internal static void MirageStep(ProjectileSession session, double now)
    {
        if (!session.Active) return;
        var c = session.Conn; var world = MirageWorlds.GetValue(session, _ => new());
        // Each actor validates its own exact source; multiple supported skills never invalidate one another's iteration.
        if (c.PlayerLife == 0) world.Life.Clear();
        foreach (var body in world.Bodies.Values.ToArray())
            if (MirageSignature(c, body.Actor.Skill) != body.Actor.EquipmentSignature)
                world.Life.Remove(body.Actor);
        world.Life.Advance(now);
        RetireMirageBodies(session, world);
        if (c.Paused || InTown(c) || c.PlayerLife == 0) return;
        foreach (var body in world.Bodies.Values.ToArray())
        {
            var actor = body.Actor;
            var stats = SupportedStats(c, actor.Skill);
            int Get(string n) => MarkStat(stats, n);
            actor.Clock.ChangeRate(now, OrdinaryEffectClock.RecipientRate(18, Get));
            if (!actor.Detached)
            {
                actor.Position = ProjectileSession.Vector(c.Walk.Position(now));
                body.Monster.Walk.Teleport((int)actor.Position.X, (int)actor.Position.Y, now);
                session.Packet(() => MirageCurrent(session, body)
                    ? InstanceProtocol.Teleport(body.Id, (int)actor.Position.X, (int)actor.Position.Y) : null);
            }
            var target = c.Monsters.Where(x => x.Value.Hostile && !x.Value.Dead && x.Value.Life > 0)
                .Select(x => (x.Key, Monster: x.Value, At: x.Value.Walk.Position(now)))
                .Where(x => Dist(x.At, ((int)actor.Position.X, (int)actor.Position.Y)) <= 120)
                .OrderBy(x => Dist(x.At, ((int)actor.Position.X, (int)actor.Position.Y))).ThenBy(x => x.Key).FirstOrDefault();
            double speed = Math.Max(0, 1 + Get("support_mirage_archer_attack_speed_+%_final") / 100d);
            if (target.Monster is null || speed <= 0) continue;
            double seconds = SupportedAttackSeconds(c, actor.Skill) * PlayerActionFactor(c) / speed;
            if (!world.Life.TryAttack(actor, now, c.ProjectileEpoch, actor.EquipmentSignature, seconds, out long attack)) continue;
            session.Packet(() => MirageCurrent(session, body) && actor.Attack == attack
                ? InstanceProtocol.ActionStarted(body.Id, (int)actor.Position.X, (int)actor.Position.Y,
                    target.At.X, target.At.Y, actor.Skill, 0xffff, 0x0408, targetObjectId: target.Key) : null);
            session.Schedule(ProjectileRuntime.Time(now + AttackReleaseDelay(AttackContactDelay(c, seconds, false))),
                () => MirageCurrent(session, body) && actor.Attack == attack && !c.Paused
                    && MarkTargetCurrent(c, target.Key, target.Monster),
                () => CaptureMirage(session, body, target.Monster.Walk.Position(
                    session.Runtime.Now / (double)ProjectileRuntime.QuantaPerSecond)));
        }
    }

    internal static ProjectileUse CaptureMirage(ProjectileSession session, MirageBody body, (int X, int Y) aim)
    {
        var c = session.Conn; var gem = GemSkillEffects(c).Single(x => x.Id == body.Actor.Skill);
        var use = CaptureGemProjectile(c, session, gem.Id, gem, aim, CaptureSkillCriticalRoll(c, gem.Id));
        var shot = (ProjectileOffense)use.Payload!;
        double factor = Math.Max(0, 1 + MarkStat(SupportedStats(c, gem.Id), "support_mirage_archer_damage_+%_final") / 100d);
        var ignite = shot.Ignite;
        return use with { Emitter = session.ActorKey(body.Id, body.Monster), Origin = body.Actor.Position,
            Aim = ProjectileSession.Vector(aim), ProjectilePayloads = null, Payload = shot with
            {
                Mirage = body, MirageRelease = null, Hit = shot.Hit.Scale(factor), AreaHit = shot.AreaHit?.Scale(factor),
                MarkSource = shot.MarkSource is { } mark ? new(flat => mark.Hit(flat).Scale(factor),
                    flat => ScaleMirageIgnite(mark.Ignite(flat), factor)) : null,
                MarkAreaSource = shot.MarkAreaSource is { } area ? new(flat => area.Hit(flat).Scale(factor),
                    flat => ScaleMirageIgnite(area.Ignite(flat), factor)) : null,
                Ignite = ignite with { Min = ignite.Min * factor, Max = ignite.Max * factor,
                    CriticalMin = ignite.CriticalMin * factor, CriticalMax = ignite.CriticalMax * factor },
                ClientPredicted = false, HitPacket = false, Leech = new(DamagePacket.Empty, DamagePacket.Empty, DamagePacket.Empty),
                Label = "Mirage Archer: " + shot.Label
            } };
    }

    static MonsterIgnites.Settings ScaleMirageIgnite(MonsterIgnites.Settings value, double factor) => value with
    {
        Min = value.Min * factor, Max = value.Max * factor,
        CriticalMin = value.CriticalMin * factor, CriticalMax = value.CriticalMax * factor,
    };

    static void CommitMirageHit(ProjectileSession session, ProjectileDamage contact, SpawnedMonster target, MonsterHit hit)
    {
        session.Packet(InstanceProtocol.LifeUpdate(contact.Target.Id, hit.Life), "Mirage projectile contact");
        if (hit.Life == 0)
        {
            session.Packet(InstanceEntities.StatsUpdate(contact.Target.Id, [(StatKeys.HideMiniLifeBar, 1)]));
            CommitProjectileKill(session, contact.Target.Id, target, contact.At, contact.Projectile.RewardRandomSource,
                playerKill: false);
        }
        if (hit.StageStarts) CommitProjectileBossStage(session, contact.Target.Id, target, contact.At);
    }
}
