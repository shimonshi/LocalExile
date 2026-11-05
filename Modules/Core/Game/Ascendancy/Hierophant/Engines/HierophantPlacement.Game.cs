using System.Net.Sockets;
using GameServer.Core.Protocol.Login;
using GameServer.Core.Protocol.Instance;

static partial class Game
{
    /// <summary>Accepted offensive totem placement. Reuses native totem Life, cap, collision and creation contracts.</summary>
    internal static async Task<uint?> PlaceHierophantOffensiveTotem(string remote, NetworkStream stream,
        ConnectionCiphers ciphers, ProjectileSession session, ushort skill, int acceptedAction, (int X, int Y) at)
    {
        var c = session.Conn; int epoch = c.ProjectileEpoch;
        string? signature; double until; MonsterCatalog.Variety? variety;
        lock (c.Monsters)
        {
            signature = HierophantEngineSignature(c, skill); variety = HierophantOffensiveTotemVariety(c, skill);
            if (signature is null || variety is null || !HierophantEngineLive(session) || c.Paused
                || !OrdinaryOf(c, _ => 0).Accepted(epoch, acceptedAction)) return null;
            var s = SupportedSkill(c, skill); var stats = SupportedStats(c, skill);
            var support = s.Supports.FirstOrDefault(x => x.Effect.Id == "SupportSpellTotem");
            int summonMs = support is not null ? SkillsFor(c).CastTimeMs(support.Effect.Row) : SkillsFor(c).CastTimeMs(s.Effect);
            double seconds = summonMs / 1000d / Math.Max(.01, 1 + MarkStat(stats, "summon_totem_cast_speed_+%") / 100d)
                / Math.Max(.01, PlayerActionFactor(c));
            until = ResourceEffectsTime(c) + seconds;
        }
        while (true)
        {
            lock (c.Monsters)
            {
                if (!HierophantEngineLive(session) || c.ProjectileEpoch != epoch || !PlayerActionCurrent(c, acceptedAction)
                    || HierophantEngineSignature(c, skill) != signature) return null;
                if (!c.Paused && ResourceEffectsTime(c) >= until) break;
            }
            await Task.Delay(16); // Wakeup only; game progress is exclusively ResourceEffectsTime.
        }
        uint? created = null;
        // This guarded send-gate variant keeps cancellation, source identity, placement and native creation atomic.
        // The existing Rejuvenation creator remains unchanged, including its aura behavior.
        await Send(remote, stream, ciphers, [], null, atGate: () =>
        {
            lock (LevelUpdatesFor(c).Gate)
            lock (c.Monsters)
            {
                if (!HierophantEngineLive(session) || c.Paused || c.ProjectileEpoch != epoch
                    || !PlayerActionCurrent(c, acceptedAction) || HierophantEngineSignature(c, skill) != signature
                    || !OrdinaryOf(c, _ => 0).Accepted(epoch, acceptedAction)
                    || acceptedAction <= HierophantEngineWorlds.GetValue(session, _ => new()).LastUse) return null;
                var gem = SupportedSkill(c, skill); var stats = SupportedStats(c, skill);
                int Get(string n) => MarkStat(stats, n);
                int limit = BaseTotemsAllowed + Get("number_of_additional_totems_allowed");
                byte level = (byte)Math.Clamp(SkillsFor(c).PlayerLevelReq(gem.Effect, gem.Level), 1, 100);
                if (limit <= 0 || Monsters.BaseLife(variety, level) is not int baseLife) return null;
                uint life = (uint)Math.Clamp((long)baseLife * Math.Max(0L, 100L + Get("totem_life_+%")) / 100, 1, int.MaxValue);
                double duration = Get("base_totem_duration") / 1000d * Math.Max(0, 1 + Get("totem_duration_+%") / 100d);
                if (duration <= 0) return null;
                uint id = c.NextObjectId++; int size = Math.Clamp(variety.ObjectSize - 1, -1, 127);
                var place = (X: Math.Clamp(at.X, 0, Math.Max(0, c.Layout.GridW - 1)),
                    Y: Math.Clamp(at.Y, 0, Math.Max(0, c.Layout.GridH - 1)));
                if (size >= 0)
                {
                    bool Floor(int x, int y) => c.Layout.WalkableFor(x, y, size) && !c.Barrier.Contains((x, y));
                    // Preserve the existing Rejuvenation policy when the area has no captured walk map.
                    if (CollisionEscape.Find(place, c.Layout.GridW, c.Layout.GridH, Floor, c.MonsterBlockers(id), moverSize: size)
                        is { } clear) place = clear;
                }
                double now = ResourceEffectsTime(c);
                var body = new TotemState.Totem(id, skill, place, life, now + duration, null);
                var replaced = c.Totems.Place(body, limit, now);
                if (replaced.Count > 0) TotemsDiedLocked(c, now, CharacterPlayerStats(c));
                if (!HierophantEngineAdmit(session, skill, acceptedAction, place, body))
                    throw new InvalidOperationException("Validated totem admission changed under the combat gate");
                if (size >= 0) c.SetBlocker(id, place.X, place.Y, size);
                var bytes = new List<byte>();
                if (c.MonsterTypes.Add((variety.Row, level)))
                    bytes.AddRange(InstanceEntities.MonsterTypes([((ushort)variety.Row, level)]));
                var blob = new MonsterObject(place.X, place.Y, life, variety.ComponentsOrBase,
                    StateMachine: variety.StateMachine, TransitionState: variety.TransitionStates, Team: 0,
                    Stats: [(StatKeys.MaximumLife, (int)life)]) { LifeData = variety.LifeData };
                bytes.AddRange(InstanceEntities.CreateObject(id,
                    InstanceEntities.ObjectTypeHash(MonsterObject.TypePath(variety.Id, level)), blob.Blob()));
                bytes.AddRange(InstanceProtocol.EnterWakeZone(id));
                created = id; return bytes.ToArray();
            }
        });
        return created;
    }
}
