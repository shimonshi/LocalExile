using GameServer.Core.World;
using GameServer.Core.Protocol.Instance;

/// <summary>Recipient poison/bleed state owned by ResourceEffectsState, never a connection-global ledger.
/// All calls require Monsters. Times use ResourceEffectsTime, not wall time.</summary>
sealed class AssassinIncomingAilments
{
    internal enum Kind { Poison, Bleed }
    internal sealed record Effect(Kind Kind, double From, double Until, double Dps, uint Source);
    internal sealed record Carry(Effect[] Effects);
    readonly List<Effect> effects = [];
    readonly ResourceEffectsState owner;
    readonly long generation;
    double poisonCarry, bleedCarry, takenPoisonCarry, takenBleedCarry, bypassCarry;
    double advanced = double.NegativeInfinity;
    internal double SettledAt => advanced;
    internal bool Settling;
    internal bool NeedsPresentation => poisonBuff != 0 || bleedBuff != 0 || effects.Count != 0 || RetiredBuffs.Count != 0;
    ushort poisonBuff, bleedBuff;
    internal readonly HashSet<(uint Actor, ushort Buff)> RetiredBuffs = [];
    internal AssassinIncomingAilments(ResourceEffectsState owner) { this.owner = owner; generation = owner.Generation; }
    internal bool Current(ClientState conn) => ReferenceEquals(conn.ResourceEffectsState, owner)
        && generation == owner.Generation && Game.ResourceEffectsCurrent(conn, owner);
    internal int Poisons(double at) => effects.Count(e => e.Kind == Kind.Poison && e.From <= at && at < e.Until);
    internal bool Bleeding(double at) => effects.Any(e => e.Kind == Kind.Bleed && e.From <= at && at < e.Until);
    internal void Clear()
    {
        if (poisonBuff != 0) RetiredBuffs.Add((Program.PlayerId,poisonBuff));
        if (bleedBuff != 0) RetiredBuffs.Add((Program.PlayerId,bleedBuff));
        poisonBuff = bleedBuff = 0;
        effects.Clear(); poisonCarry = bleedCarry = takenPoisonCarry = takenBleedCarry = bypassCarry = 0;
        advanced = double.NegativeInfinity;
    }
    internal Carry Capture(double at) => new(effects.Where(e => e.Until > at)
        .Select(e => e with { From = Math.Max(0, e.From - at), Until = e.Until - at }).ToArray());
    internal void Restore(Carry carry, double at)
    {
        Clear(); effects.AddRange(carry.Effects.Select(e => e with { From = at + e.From, Until = at + e.Until }));
        advanced = at;
    }
    internal bool Add(Kind kind, double now, double dps, double seconds, uint source)
    {
        if (!double.IsFinite(dps) || !double.IsFinite(seconds) || dps <= 0 || seconds <= 0) return false;
        if (!double.IsFinite(advanced)) advanced = now;
        effects.Add(new(kind, now, now + seconds, dps, source)); return true;
    }
    internal byte[] Presentation(ClientState conn, double at)
    {
        lock (conn.Monsters)
        {
            if (!Current(conn)) Clear();
            var bytes = new List<byte>();
            foreach (var entry in RetiredBuffs) bytes.AddRange(InstanceEntities.RemoveBuff(entry.Actor,entry.Buff));
            RetiredBuffs.Clear();
            void Show(bool present, ref ushort instance, ushort row)
            {
                if (present == (instance != 0)) return;
                if (instance != 0) bytes.AddRange(InstanceEntities.RemoveBuff(Program.PlayerId,instance));
                instance = 0;
                if (present) bytes.AddRange(InstanceEntities.AddBuff(Program.PlayerId,instance=conn.NextBuff++,row,Program.PlayerId));
            }
            Show(Poisons(at)>0,ref poisonBuff,10); Show(Bleeding(at),ref bleedBuff,140);
            return [..bytes];
        }
    }
    internal (int Poison, int Bleed) Integrate(double from, double until, Func<string,int> get,
        Func<Effect,double,double> factor, Func<double,bool> moving)
    {
        from = Math.Max(from, advanced);
        if (until <= from) return default;
        // Immunity removes presence; unaffected only removes this interval's damage and fractional earnings.
        effects.RemoveAll(e => get(e.Kind == Kind.Poison ? "immune_to_poison" : "immune_to_bleeding") != 0);
        bool unaffected = get("unaffected_by_damaging_ailments") != 0;
        double poison = unaffected ? 0 : poisonCarry, bleed = unaffected ? 0 : bleedCarry;
        var cuts = effects.SelectMany(e => new[] { e.From, e.Until }).Where(t => from < t && t < until)
            .Append(from).Append(until).Distinct().Order().ToArray();
        for (int i = 1; i < cuts.Length && !unaffected; i++)
        {
            double at = (cuts[i-1] + cuts[i]) / 2, dt = cuts[i] - cuts[i-1];
            var active = effects.Where(e => e.From <= at && at < e.Until).ToArray();
            poison += active.Where(e => e.Kind == Kind.Poison).Sum(e => e.Dps * factor(e, at)) * dt;
            // Keep weaker bleeds so they can resume after the strongest expires.
            bleed += active.Where(e => e.Kind == Kind.Bleed).Select(e => e.Dps * factor(e, at))
                .DefaultIfEmpty().Max() * (moving(at) ? 3 : 1) * dt;
        }
        effects.RemoveAll(e => e.Until <= until); advanced = until;
        int p = Whole(poison), b = Whole(bleed);
        poisonCarry = effects.Any(e => e.Kind == Kind.Poison) ? poison - p : 0;
        bleedCarry = effects.Any(e => e.Kind == Kind.Bleed) ? bleed - b : 0;
        if (unaffected) takenPoisonCarry = takenBleedCarry = bypassCarry = 0;
        return (p,b);
    }
    static int Whole(double value) => (int)Math.Clamp(Math.Floor(value + 1e-9), 0, int.MaxValue);
    internal int ApplyResources(ClientState conn, int poison, int bleed, double at, Func<string,int> get)
    {
        poison = IncomingResourceDefences.TakenOverTime(poison, get, ref takenPoisonCarry);
        bleed = IncomingResourceDefences.TakenOverTime(bleed, get, ref takenBleedCarry);
        if (conn.Immune) return 0;
        int loss = (int)Math.Min(int.MaxValue, (long)conn.EnergyShield.Damage(poison, true, at)
            + IncomingResourceDefences.DamageOverTime(conn.EnergyShield, bleed,
                get(IncomingResourceDefences.BypassStat), at, ref bypassCarry));
        conn.PlayerLife = (uint)Math.Max(0, (long)conn.PlayerLife - loss);
        return loss;
    }
}
