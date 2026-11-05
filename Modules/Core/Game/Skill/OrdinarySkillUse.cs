static partial class Game
{
    // Flow-local paid offense, never a mutation of the player's global stats. Each channel stage gets a new scope.
    internal sealed class OrdinarySkillUse(ClientState conn) : IDisposable
    {
        readonly OrdinarySkillUse? previous = OrdinaryUseScope.Value;
        internal readonly ClientState Conn = conn;
        internal bool? SurgeActive;
        internal OrdinaryPathfinderRules.Distillation? Distillation;
        public void Dispose() => OrdinaryUseScope.Value = previous;
    }
    static readonly AsyncLocal<OrdinarySkillUse?> OrdinaryUseScope = new();
    // Scheduled releases run on an unrelated pump. Capture only this use's availability, not its mutable scope.
    internal static Func<T> CaptureOrdinaryRelease<T>(ClientState conn, Func<T> release)
    {
        bool? active = OrdinaryUseScope.Value is { } use && ReferenceEquals(use.Conn, conn) ? use.SurgeActive : null;
        var paid = OrdinaryUseScope.Value is { } source && ReferenceEquals(source.Conn, conn) ? source.Distillation : null;
        return () =>
        {
            using var offense = new DistillerOffense();
            using var scope = BeginOrdinaryUse(conn); scope.SurgeActive = active; scope.Distillation = paid;
            var previous = HierophantOffenseRead.Value;
            HierophantOffenseRead.Value = active is bool value ? (conn, value) : null;
            try { return release(); }
            finally { HierophantOffenseRead.Value = previous; }
        };
    }
    internal static void StartProjectilePump(Func<Task> start)
    {
        var use = OrdinaryUseScope.Value; var read = HierophantOffenseRead.Value;
        OrdinaryUseScope.Value = null; HierophantOffenseRead.Value = null;
        try { _ = start(); }
        finally { OrdinaryUseScope.Value = use; HierophantOffenseRead.Value = read; }
    }
    internal static OrdinarySkillUse BeginOrdinaryUse(ClientState conn)
    {
        var scope = new OrdinarySkillUse(conn); OrdinaryUseScope.Value = scope; return scope;
    }
    internal static void CaptureOrdinarySkillUse(ClientState conn)
    {
        if (IsOrdinaryHierophant(conn) && OrdinaryUseScope.Value is { } scope && ReferenceEquals(scope.Conn, conn))
            scope.SurgeActive ??= HierophantOf(conn).Active(ResourceEffectsTime(conn));
    }
    static readonly AsyncLocal<(ClientState Conn, bool Active)?> HierophantOffenseRead = new();
    internal static Dictionary<uint, int> HierophantOffenseStats(ClientState conn)
    {
        var previous = HierophantOffenseRead.Value;
        if (OrdinaryUseScope.Value is { SurgeActive: bool active } scope && ReferenceEquals(scope.Conn, conn))
            HierophantOffenseRead.Value = (conn, active);
        try { return CharacterPlayerStats(conn); }
        finally { HierophantOffenseRead.Value = previous; }
    }
    internal static Dictionary<uint, int> HierophantFinalOffense(ClientState conn) =>
        FinalPlayerStatsFrom(conn, HierophantOffenseStats(conn)).ToDictionary(kv => kv.Item1, kv => kv.Item2);
}
