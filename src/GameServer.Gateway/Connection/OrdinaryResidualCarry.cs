sealed partial class ClientState
{
    internal string? OrdinaryResidualPayload;
    internal long OrdinaryDeathSequence;
    internal long OrdinaryUtilitySequence;
    internal ushort OrdinaryMovementBuff, OrdinaryAttackBuff, OrdinaryTailwindBuff;
    internal volatile bool OrdinarySharesTailwind;
    internal uint OrdinaryTailwindSource;
    internal int OrdinaryTailwindAmount;
    internal int OrdinaryTailwindObserved;
    internal ushort[] OrdinaryGaleBuffs = [];
    internal double[] OrdinaryGaleDeadlines = [];
    internal double OrdinaryGaleRate;
    internal double OrdinaryMovementDeadline, OrdinaryAttackDeadline;
    internal (int Movement, int Attack, int Area, int Taken, bool Stun, bool Bleed) OrdinaryStats;
    internal bool OrdinaryStatsDirty;
    internal ushort OrdinaryMarkBuff;
    internal ushort OrdinaryMarkSkill;
    internal uint OrdinaryMarkGem;
    internal uint OrdinaryMarkTarget;
    internal SpawnedMonster? OrdinaryMarkMonster;
    internal double OrdinaryMarkDeadline;
    internal uint[] OrdinaryMarkValues = [];
    internal uint[] OrdinaryMarkKeys = [];
}

static class OrdinaryResidualTransfers
{
    internal sealed record Carry(string Character, string Payload, double At, DateTime Created);
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<uint, Carry> Tickets = new();
}
