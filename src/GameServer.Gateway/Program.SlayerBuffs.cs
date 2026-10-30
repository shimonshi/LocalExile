using System.Net.Sockets;
using System.Runtime.CompilerServices;
using GameServer.Core.Protocol.Login;

partial class Program
{
    static readonly ConditionalWeakTable<ClientState, SlayerBuffPresentation> SlayerBuffPresentations = new();

    static SlayerKillState.BuffSnapshot SlayerBuffStateLocked(ClientState conn)
    {
        if (conn.PlayerLife == 0) { conn.SlayerKills.Clear(); return default; }
        var stats = CharacterPlayerStats(conn);
        return conn.SlayerKills.ReadBuffs(conn.SlayerFrozenAt ?? ProjectileWorldTime(conn),
            name => Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0);
    }

    internal static byte[]? SlayerBuffPacketsAtGate(ClientState conn, int epoch)
    {
        lock (conn.Monsters)
        {
            // Failed transfer resumes the same presentation with rebased grants; successful transfer has a new
            // ClientState/presentation. Never consume a presentation or allocate native IDs for a stale lease.
            if (conn.ProjectileEpoch != epoch || !conn.PlayerSent || conn.Closed || conn.LeavingArea
                || conn.RageRetired) return null;
            var state = SlayerBuffStateLocked(conn);
            var packets = SlayerBuffPresentations.GetValue(conn, _ => new()).Build(state, PlayerId, () => conn.NextBuff++);
            return packets.Length == 0 ? null : packets;
        }
    }

    // Parent hook: await after the existing bleed/native-condition publication in RunPlayerTick.
    // No packets/IDs are captured before Send's gate. A refresh with unchanged +10 still has a new grant identity.
    internal static Task SendSlayerBuffs(string remote, NetworkStream stream, ConnectionCiphers ciphers, ClientState conn)
    {
        int epoch;
        lock (conn.Monsters)
        {
            if (!conn.PlayerSent || conn.Closed || conn.LeavingArea || conn.RageRetired) return Task.CompletedTask;
            epoch = conn.ProjectileEpoch;
            if (!SlayerBuffPresentations.GetValue(conn, _ => new()).NeedsPublication(SlayerBuffStateLocked(conn)))
                return Task.CompletedTask;
        }
        return Send(remote, stream, ciphers, [], "Slayer Headsman buffs",
            atGate: () => SlayerBuffPacketsAtGate(conn, epoch));
    }
}
