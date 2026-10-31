using System.Net.Sockets;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.Protocol.Login;

static partial class Game
{
    internal static readonly Lazy<Dictionary<FlaskGrounds.Kind, (string Ao, int ArtRadius)>> FlaskGroundArt = new(() => FlaskGrounds.LoadArt("data"));

    // Called in the accepted charge transaction, under Monsters. The accepted position/deadline and
    // output receipts survive replacement without retaining a Game closure or creating another timer.
    static void MakeFlaskGroundsLocked(ClientState conn, IReadOnlyDictionary<string, int> local, double seconds, double now)
    {
        var at = conn.Walk.Position(now);
        var packets = Automation(conn).Packets;
        foreach (var (kind, radius) in FlaskGrounds.OnUse(local))
        {
            FlaskGrounds.Ground ground;
            if (kind == FlaskGrounds.Kind.Taunt)
            {
                ground = new(kind, at.X, at.Y, radius, now + FlaskGrounds.TauntSeconds);
                foreach (var (id, m) in conn.Monsters)
                    if (!m.Dead && m.Life > 0 && m.Hostile && ground.Covers(m.Walk.Position(now))) ground.Taunted[id] = conn.NextBuff++;
                uint taunt = BitConverter.SingleToUInt32Bits((float)FlaskGrounds.TauntSeconds);
                foreach (var (id, buff) in ground.Taunted)
                    packets.Add(InstanceEntities.AddBuff(id, buff, FlaskGrounds.TauntedBuffRow, PlayerId, u68: taunt));
            }
            else
            {
                uint? obj = null;
                if (FlaskGroundArt.Value.TryGetValue(kind, out var art))
                {
                    obj = conn.NextObjectId++;
                    float scale = (float)(radius * (250.0 / 23) / Math.Max(1, art.ArtRadius));
                    var effect = new EffectObject(at.X, at.Y, art.Ao) { Flags0 = EffectObject.FixedRotation, Scale = scale };
                    packets.Add(InstanceEntities.CreateObject(obj.Value, InstanceEntities.ObjectTypeHash(EffectObject.TypePath), effect.Blob()));
                    packets.Add(InstanceProtocol.EnterWakeZone(obj.Value));
                }
                ground = new(kind, at.X, at.Y, radius, now + seconds, obj);
            }
            conn.FlaskGrounds.Add(ground);
        }
    }

    // The existing area claim owns this 100 ms role too: smoke/consecration/taunt bookkeeping and
    // pending removals must move together, rather than an old timer dropping the current stat refresh.
    internal static void AdvanceFlaskGrounds(ClientState conn, PlayerUpdatesState updates)
    {
        lock (conn.Monsters)
        {
            if (!PlayerUpdatesCurrent(conn, updates) || conn.LeavingArea || conn.Paused || !conn.FlaskGrounds.Any) return;
            double now = updates.Schedule.Now(conn);
            var state = conn.FlaskGrounds;
            var pending = Automation(conn);
            foreach (var ground in state.Expire(now))
            {
                if (ground.Object is uint obj) pending.Packets.Add(InstanceProtocol.RemoveObject(obj));
                foreach (var (id, buff) in ground.Taunted)
                    if (conn.Monsters.ContainsKey(id)) pending.Packets.Add(InstanceEntities.RemoveBuff(id, buff));
            }
            foreach (uint gone in state.BlindedMonsters().Except(conn.Monsters.Keys)) state.SetBlinded(gone, false, () => 0);
            foreach (var (id, monster) in conn.Monsters)
            {
                bool inside = !monster.Dead && monster.Life > 0 && monster.Mods.Effects["cannot_be_blinded"] == 0
                    && state.Covers(FlaskGrounds.Kind.SmokeCloud, monster.Walk.Position(now));
                if (state.SetBlinded(id, inside, () => conn.NextBuff++) is { } change)
                    pending.Packets.Add(change.Added
                        ? InstanceEntities.AddBuff(id, change.Buff, FlaskGrounds.BlindedBuffRow, PlayerId)
                        : InstanceEntities.RemoveBuff(id, change.Buff));
            }
            bool on = conn.PlayerLife > 0 && state.Covers(FlaskGrounds.Kind.ConsecratedGround, conn.Walk.Position(now));
            if (state.SetConsecrated(on, () => conn.NextBuff++) is { } consecrated)
            {
                pending.Packets.Add(consecrated.Added
                    ? InstanceEntities.AddBuff(PlayerId, consecrated.Buff, FlaskGrounds.ConsecratedBuffRow, PlayerId)
                    : InstanceEntities.RemoveBuff(PlayerId, consecrated.Buff));
                pending.StatRevision++;
            }
            state.StopTickerIfIdle();
        }
    }

    internal static async Task RunFlaskGrounds(string remote, NetworkStream stream, ConnectionCiphers ciphers,
        ClientState conn, PlayerUpdatesState updates)
    {
        while (PlayerUpdatesCurrent(conn, updates))
        {
            await updates.Schedule.Wait(PlayerUpdateRole.FlaskGrounds, 100, updates.Stop.Token);
            AdvanceFlaskGrounds(conn, updates);
            if (!PlayerUpdatesCurrent(conn, updates)) return;
            await PublishFlaskChanges(remote, stream, ciphers, conn);
        }
    }
}
