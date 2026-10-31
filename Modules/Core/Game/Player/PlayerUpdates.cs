using GameServer.Modding;
using GameServer.Core.Diagnostics;

// Scheduling is separate from game time; deterministic drivers use the same loops without wall-clock waits.
interface IPlayerUpdateSchedule
{
    double Now(ClientState conn);
    double ResourceNow(ClientState conn) => Now(conn);
    Task Wait(int milliseconds, CancellationToken stopping);
}

sealed class PlayerUpdatesState(IModuleLifetime rules, IPlayerUpdateSchedule schedule) : ITransientCombatState
{
    internal readonly IModuleLifetime Rules = rules;
    internal readonly IPlayerUpdateSchedule Schedule = schedule;
    internal readonly CancellationTokenSource Stop = new();
    internal long Generation;
    internal Task Completion = Task.CompletedTask;
    public void Clear() => Stop.Cancel();
}

static partial class Game
{
    sealed class PlayerUpdateSchedule : IPlayerUpdateSchedule
    {
        public double Now(ClientState conn) => conn.Clock.Elapsed.TotalSeconds;
        public double ResourceNow(ClientState conn) => ProjectileWorldTime(conn);
        public Task Wait(int milliseconds, CancellationToken stopping) => Task.Delay(milliseconds, stopping);
    }
    internal static IPlayerUpdateSchedule PlayerUpdatesSchedule = new PlayerUpdateSchedule();

    internal static bool PlayerUpdatesCurrent(ClientState conn, PlayerUpdatesState state) => CurrentGameRules
        && !conn.Closed && !conn.RageRetired && !state.Stop.IsCancellationRequested
        && ReferenceEquals(conn.PlayerUpdatesState, state) && ReferenceEquals(CombatLifetime, state.Rules)
        && conn.PlayerUpdatesWorker.Current(state.Rules, null, state, state.Generation);

    internal static void EnsurePlayerUpdates(ClientState conn)
    {
        lock (conn.Monsters)
        {
            if (!CurrentGameRules || conn.Closed || conn.RageRetired || !conn.PlayerSent
                || conn.Wire.Stream is null || conn.Wire.Ciphers is null) return;
            if (conn.PlayerUpdatesState is not PlayerUpdatesState state || !PlayerUpdatesCurrent(conn, state))
            {
                conn.RetirePlayerUpdates();
                state = new(CombatLifetime!, PlayerUpdatesSchedule); conn.PlayerUpdatesState = state;
            }
            if (!conn.PlayerUpdatesWorker.TryStart(conn, state.Rules, null, state, out long generation)) return;
            state.Generation = generation;
            // Do not inherit an old projectile/worker's AsyncLocal output predicate into its replacement.
            if (ExecutionContext.IsFlowSuppressed()) state.Completion = Task.Run(() => RunPlayerUpdates(conn, state));
            else
                using (ExecutionContext.SuppressFlow()) state.Completion = Task.Run(() => RunPlayerUpdates(conn, state));
        }
    }

    static async Task RunPlayerUpdates(ClientState conn, PlayerUpdatesState state)
    {
        var previous = Program.ProjectileOutputGuard.Value;
        Program.ProjectileOutputGuard.Value = () => PlayerUpdatesCurrent(conn, state);
        try
        {
            var (remote, stream, ciphers) = conn.Wire;
            await Task.WhenAll(Drive(() => RunPlayerTick(remote, stream, ciphers, conn, state)),
                Drive(() => RunResourceRecovery(remote, stream, ciphers, conn, state)));
        }
        catch (OperationCanceledException) when (state.Stop.IsCancellationRequested) { }
        catch (Exception ex) { Console.WriteLine($"[gateway] {conn.Wire.Remote} player updates failed: {ServerLog.Describe(ex)}"); throw; }
        finally
        {
            Program.ProjectileOutputGuard.Value = previous;
            lock (conn.Monsters) conn.PlayerUpdatesWorker.Complete(state.Rules, null, state, state.Generation);
        }
        async Task Drive(Func<Task> loop)
        {
            try { await loop(); }
            finally { state.Stop.Cancel(); }
        }
    }
}
