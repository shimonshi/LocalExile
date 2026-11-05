using GameServer.Core.Protocol.Instance;
using System.Collections.Immutable;

static partial class Game
{
    static readonly AsyncLocal<bool> DistillerReading = new();
    internal sealed class DistillerOffense : IDisposable
    {
        readonly bool previous = DistillerReading.Value;
        internal DistillerOffense(bool enabled = true) => DistillerReading.Value = enabled;
        public void Dispose() => DistillerReading.Value = previous;
    }
    internal static int PaidDistillerCritical(ClientState conn) => DistillerInputs(conn, new Dictionary<uint, int>())
        .GetValueOrDefault(Stats.Key("critical_strike_chance_+%") ?? 0);

    // Pure composition into private executor inputs, never the character snapshot or panel builder.
    internal static Dictionary<uint, int> DistillerInputs(ClientState conn, IReadOnlyDictionary<uint, int> source)
    {
        var result = new Dictionary<uint, int>(source);
        if (DistillerReading.Value && OrdinaryUseScope.Value is { Distillation: { } paid } scope && ReferenceEquals(scope.Conn, conn))
            foreach (var (name, value) in paid.Stats)
                if (Stats.Key(name) is uint key) result[key] = result.GetValueOrDefault(key) + value;
        return result;
    }

    // Caller holds FlaskBarGate, then LevelUpdates -> Monsters. The inventory lock bridges pickups
    // which intentionally do not acquire FlaskBarGate. Resource validation/debit runs once inside it.
    internal static bool PayDistillerUseLocked(ClientState conn, bool eligible, Func<bool> debit)
    {
        var items = ItemsOf(conn);
        var bar = items.Items(PlayerInventories.Flask);
        var plan = PathfinderDistillationLocked(conn, eligible, CharacterPlayerStats(conn), bar);
        if (plan.Payments.Any(p => !bar.Any(f => f.Id == p.Id))) return false;
        var payments = plan.Payments.Select(p => (bar.Single(f => f.Id == p.Id), p.Cost)).ToArray();
        uint mana = conn.PlayerMana, life = conn.PlayerLife;
        if (!items.TryPaySkillFlasks(payments, debit, out var changed, out var revision)) return false;
        plan = plan with { Action = conn.ActionCount, Epoch = conn.ProjectileEpoch,
            Mana = mana - conn.PlayerMana, Life = life - conn.PlayerLife,
            Witnesses = payments.Select(p => new OrdinaryPathfinderRules.PaymentWitness(p.Item1.Id, p.Item1.X,
                p.Item1.Item.TypeHash, p.Item1.Item.Charges, p.Item1.Item.Charges - p.Cost)).ToImmutableArray() };
        if (OrdinaryUseScope.Value is { } scope && ReferenceEquals(scope.Conn, conn)) scope.Distillation = plan;
        if (changed.Count > 0)
            Automation(conn).Packets.Add(new(InstanceItems.InventoryChange(PlayerInventories.InventoryId(PlayerInventories.Flask),
                revision, changed.Select(f => f.Id).ToArray(), changed), "Master Distiller paid skill charges"));
        return true;
    }

    internal static bool DistillerEligible(ClientState conn, int effect, int level, int quality) =>
        Supports.Of(effect)?.Types.Contains(47) != true
        && SkillHitPlan.ForSkill(SkillsFor(conn), effect, level, quality).CanHit;
}
