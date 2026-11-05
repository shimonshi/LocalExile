static partial class Game
{
    // Nested flow-local payment receipts are restored at disposal; background pumps start without ambient payment.
    internal sealed class OrdinarySkillUse(ClientState conn) : IDisposable
    {
        readonly OrdinarySkillUse? previous = OrdinaryUseScope.Value;
        internal readonly ClientState Conn = conn;
        internal OrdinaryPathfinderRules.Distillation? Distillation;
        public void Dispose() => OrdinaryUseScope.Value = previous;
    }
    static readonly AsyncLocal<OrdinarySkillUse?> OrdinaryUseScope = new();
    internal static OrdinarySkillUse BeginOrdinaryUse(ClientState conn)
    {
        var scope = new OrdinarySkillUse(conn); OrdinaryUseScope.Value = scope; return scope;
    }
    internal static Func<T> CaptureOrdinaryRelease<T>(ClientState conn, Func<T> release)
    {
        var paid = OrdinaryUseScope.Value is { } source && ReferenceEquals(source.Conn, conn) ? source.Distillation : null;
        return () =>
        {
            using var offense = new DistillerOffense();
            using var scope = BeginOrdinaryUse(conn); scope.Distillation = paid;
            return release();
        };
    }
    internal static void StartProjectilePump(Func<Task> start)
    {
        var use = OrdinaryUseScope.Value; OrdinaryUseScope.Value = null;
        try { _ = start(); }
        finally { OrdinaryUseScope.Value = use; }
    }
}
