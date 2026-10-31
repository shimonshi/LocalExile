using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using GameServer.Core.Projectiles;
using GameServer.Core.Protocol.Login;

partial class Program
{
    sealed class DeadeyeGaleSession
    {
        public DeadeyeGaleState State { get; } = new();
        public DeadeyeGalePresentation Presentation { get; } = new();
    }
    static readonly ConditionalWeakTable<ClientState, DeadeyeGaleSession> DeadeyeGales = new();
    static readonly ConcurrentDictionary<uint, (DateTime At, double[] Remaining)> DeadeyeGaleTransfers = new();
    static DeadeyeGaleSession GaleOf(ClientState conn) => DeadeyeGales.GetValue(conn, _ => new());

    internal static DeadeyeGaleAura.Source DeadeyeTailwindSource(ClientState conn, int team = 1,
        object? world = null, ProjectileActorKey? owner = null)
    {
        lock (conn.Monsters)
        {
            _ = WithDeadeyeGale(conn, CharacterStatSources(conn).Stats);
            var at = conn.Walk.Position(conn.Clock.Elapsed.TotalSeconds);
            return new(owner ?? new(PlayerId, conn.ProjectileEpoch), team, new(at.X, at.Y), GaleOf(conn).State)
                { World = world ?? conn };
        }
    }

    internal static bool HasGatheringWinds(ClientState conn) =>
        conn.MainAscendancy.Selection is { Family: AscendancyFamily.Normal } selected
        && Ascendancies.Find(selected)?.Name == "Deadeye" && conn.MainAscendancy.Allocated.Contains((ushort)24848);

    // Sources are the already assembled raw/charge/aura layer, never a recursive CharacterPlayerStats lookup.
    internal static Dictionary<uint, int> WithDeadeyeGale(ClientState conn, Dictionary<uint, int> sources)
    {
        lock (conn.Monsters)
        {
            var state = GaleOf(conn).State;
            int Get(string name) => Stats.Key(name) is uint key
                ? conn.PlayerStats.TryGetValue(key, out int set) ? set : sources.GetValueOrDefault(key) : 0;
            state.Observe(conn.Clock.Elapsed.TotalSeconds, HasGatheringWinds(conn), Get,
                conn.PlayerLife > 0 && !conn.Closed && !conn.RageRetired);
            var result = new Dictionary<uint, int>(sources);
            void Set(string name, int value) { if (Stats.Key(name) is uint key) result[key] = value; }
            Set("gale_force_stacks", state.Count);
            if (state.Tailwind)
            {
                // The base Tailwind buff does not stack with another Tailwind source.
                Set(ActionSpeed.Reduction, Get(ActionSpeed.Reduction) - (Get(ActionSpeed.HasTailwind) != 0 ? 0 : 8));
                Set(ActionSpeed.HasTailwind, 1);
                Set(ActionSpeed.TailwindEffect, Get(ActionSpeed.TailwindEffect) + state.TailwindEffect);
                Set("virtual_tailwind_effect_on_self_+%", Get(ActionSpeed.TailwindEffect) + state.TailwindEffect);
            }
            return result;
        }
    }

    internal static bool DeadeyeGaleAdmitUse(ClientState conn, bool accepted, bool ownerUse = true)
    {
        lock (conn.Monsters)
        {
            _ = WithDeadeyeGale(conn, CharacterStatSources(conn).Stats);
            return GaleOf(conn).State.AdmitUse(conn.Clock.Elapsed.TotalSeconds,
                accepted && !conn.LeavingArea && !conn.Closed && !conn.RageRetired && conn.PlayerLife > 0, ownerUse);
        }
    }

    // Mitigate using factor first, then call this with accepted=true at the synchronous accepted-hit commit.
    // DoT consumes the factor without clearing; evasion/source cancellation never commit loss. Blocked hits clear.
    internal static double DeadeyeGaleIncoming(ClientState conn, bool acceptedHit)
    {
        lock (conn.Monsters)
        {
            _ = WithDeadeyeGale(conn, CharacterStatSources(conn).Stats);
            return GaleOf(conn).State.IncomingHit(conn.Clock.Elapsed.TotalSeconds, acceptedHit);
        }
    }

    internal static void DeadeyeGaleClear(ClientState conn, bool retire = false)
    { lock (conn.Monsters) GaleOf(conn).State.Clear(retire); }

    internal static void DeadeyeGaleCarry(ClientState conn, uint ticket)
    {
        lock (conn.Monsters)
        {
            if (conn.PlayerLife == 0) { GaleOf(conn).State.Clear(); return; }
            DeadeyeGaleTransfers[ticket] = (DateTime.UtcNow, GaleOf(conn).State.Suspend(conn.Clock.Elapsed.TotalSeconds));
        }
        foreach (var (key, value) in DeadeyeGaleTransfers)
            if (DateTime.UtcNow - value.At > TimeSpan.FromMinutes(5)) DeadeyeGaleTransfers.TryRemove(key, out _);
    }

    internal static void DeadeyeGaleResumeFailedTransfer(ClientState conn, uint ticket)
    {
        DeadeyeGaleTransfers.TryRemove(ticket, out _);
        lock (conn.Monsters) GaleOf(conn).State.Resume(conn.Clock.Elapsed.TotalSeconds);
    }

    internal static void DeadeyeGaleReceive(ClientState conn, uint ticket)
    {
        lock (conn.Monsters)
        {
            _ = WithDeadeyeGale(conn, CharacterStatSources(conn).Stats);
            if (DeadeyeGaleTransfers.TryRemove(ticket, out var carry)
                && DateTime.UtcNow - carry.At <= TimeSpan.FromMinutes(5))
                GaleOf(conn).State.Restore(conn.Clock.Elapsed.TotalSeconds, carry.Remaining);
            GaleOf(conn).Presentation.Forget();
        }
    }

    internal static byte[] DeadeyeGalePacketsAtGate(ClientState conn)
    {
        lock (conn.Monsters)
        {
            if (conn.Closed || conn.LeavingArea || conn.RageRetired) return [];
            _ = WithDeadeyeGale(conn, CharacterStatSources(conn).Stats);
            return [.. GaleOf(conn).Presentation.Build(GaleOf(conn).State, conn.Clock.Elapsed.TotalSeconds,
                PlayerId, () => conn.NextBuff++).SelectMany(p => p)];
        }
    }

    static async Task RunDeadeyeGale(string remote, NetworkStream stream, ConnectionCiphers ciphers, ClientState conn)
    {
        long seen = -1;
        while (!conn.Closed && !conn.RageRetired)
        {
            await Task.Delay(50);
            if (conn.Paused || conn.LeavingArea || conn.Closed || conn.RageRetired) continue;
            bool changed;
            lock (conn.Monsters)
            {
                _ = WithDeadeyeGale(conn, CharacterStatSources(conn).Stats);
                long revision = GaleOf(conn).State.Revision;
                changed = revision != seen; seen = revision;
                if (changed) conn.ApplyMovementSpeed();
            }
            if (!changed) continue;
            await Send(remote, stream, ciphers, [], "Deadeye Tailwind/Gale", atGate: () => DeadeyeGalePacketsAtGate(conn));
            await SendChargeStats(remote, stream, ciphers, conn, "Deadeye action speed");
        }
    }
}
