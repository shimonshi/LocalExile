using System.Collections.Concurrent;

sealed partial class ClientState
{
    // Primitives only: Game replacement must not retain its collectible types or delegates.
    internal Dictionary<string, double[]> InquisitorCombat = [];
    internal double InquisitorObservedAt;
    internal bool InquisitorRetired;
    internal bool InquisitorLoading;
    internal ushort InquisitorFanaticBuff, InquisitorChargeBuff;
    internal double InquisitorShownUntil;
    internal int InquisitorShownCharges, InquisitorShownMore;
    internal readonly Dictionary<uint, double> InquisitorEnemyUntil = [];
    internal readonly HashSet<uint> InquisitorEnemyInside = [];
    internal readonly Dictionary<uint, double> InquisitorEnemyCoverageUntil = [];
    internal double InquisitorOwnerCoverageUntil = double.NegativeInfinity;
    internal double InquisitorOwnerUntil = double.NegativeInfinity;
    internal bool InquisitorOwnerInside;
    internal Dictionary<string, int> InquisitorGroundSources = [];
    internal double InquisitorGroundObservedAt = double.NegativeInfinity;
    internal bool InquisitorWasMoving;
    internal double InquisitorMirrorAt, InquisitorMirrorRate, InquisitorMirrorPending;
    internal double InquisitorMirrorUntil = double.NegativeInfinity;
    internal double InquisitorOwnerEntered = double.PositiveInfinity;
    internal List<double[]> InquisitorMirrorSegments = [];

    internal sealed record InquisitorTicket(string Character, DateTime Created, Dictionary<string, double[]> Combat);
    internal static readonly ConcurrentDictionary<uint, InquisitorTicket> InquisitorTickets = new();

    internal void ClearInquisitorState()
    {
        InquisitorCombat.Clear(); InquisitorEnemyInside.Clear(); InquisitorEnemyUntil.Clear(); InquisitorEnemyCoverageUntil.Clear();
        InquisitorLoading = false; InquisitorOwnerInside = false; InquisitorOwnerUntil = double.NegativeInfinity;
        InquisitorOwnerEntered = double.PositiveInfinity;
        InquisitorMirrorPending = InquisitorMirrorRate = 0; InquisitorMirrorSegments.Clear();
    }
}
