using GameServer.Core.World;

static partial class Game
{
    static readonly DamageType[] ResourceElements = [DamageType.Lightning, DamageType.Cold, DamageType.Fire];
    static int ResourceAmount(double value) => (int)Math.Clamp(Math.Round(value), 0, int.MaxValue);
    static int ResourceElementTotal(int[] values) => (int)Math.Min(int.MaxValue,
        ResourceElements.Sum(t => (long)values[(int)t]));
    static DamagePacket ResourcePacket(int[] values) => DamageTypes.ConversionOrder.Aggregate(DamagePacket.Empty,
        (packet, type) => packet.Add(type, new(values[(int)type], values[(int)type])));

    // Uniform group scaling preserves the old aggregate rounding while retaining each element's identity.
    static void ScaleResourceElements(int[] values, int target)
    {
        long total = ResourceElements.Sum(t => (long)values[(int)t]);
        if (total == 0)
        {
            foreach (var type in ResourceElements) values[(int)type] = 0;
            values[(int)DamageType.Fire] = Math.Max(0, target);
            return;
        }
        var last = ResourceElements.Last(t => values[(int)t] > 0);
        int left = Math.Max(0, target);
        foreach (var type in ResourceElements)
        {
            int next = type == last ? left : (int)((long)values[(int)type] * target / total);
            values[(int)type] = next; left -= next;
            if (type == last) break;
        }
    }

    // Preserve the existing aggregate Fortification result and physical/chaos shares for the resource commit.
    internal static DamagePacket ResourceHitComponents(DamagePacket types, int physical, int other, int chaos)
    {
        var values = DamageTypes.ConversionOrder.Select(t => ResourceAmount(types[t].Min)).ToArray();
        values[(int)DamageType.Physical] = Math.Max(0, physical);
        values[(int)DamageType.Chaos] = Math.Max(0, chaos);
        ScaleResourceElements(values, Math.Max(0, other));
        return ResourcePacket(values);
    }

    // Legacy grouped inputs have no lightning/cold identity; real typed callers pass their complete packet below.
    internal static int DamagePlayerHitTyped(ClientState conn, int physical, int other, int chaos, double at,
        IReadOnlyDictionary<uint, int> player) => DamagePlayerHitTyped(conn,
            DamagePacket.Of(DamageType.Physical, physical, physical).Add(DamageType.Fire, new(other, other))
                .Add(DamageType.Chaos, new(chaos, chaos)), at, player);

    // The single resource writer: redirect, Focus, Ward once, ordered typed ES, actual residual Mana, then Life.
    internal static int DamagePlayerHitTyped(ClientState conn, DamagePacket components, double at,
        IReadOnlyDictionary<uint, int> player)
    {
        if (conn.Immune) return 0;
        var state = ResourceEffectsOf(conn);
        if (!ResourceEffectsCurrent(conn, state) || conn.LeavingArea) return 0;
        int Get(string name) => Stats.Key(name) is uint key ? player.GetValueOrDefault(key) : 0;
        var values = DamageTypes.ConversionOrder.Select(t => ResourceAmount(components[t].Min)).ToArray();
        int physical = values[(int)DamageType.Physical], other = ResourceElementTotal(values);
        ScaleResourceElements(values, other);
        int nonChaos = (int)Math.Min(int.MaxValue, (long)physical + other);
        var kept = TotemsTakeHitLocked(conn, nonChaos, values[(int)DamageType.Chaos], at, player);
        physical = nonChaos == 0 ? 0 : (int)((long)physical * kept.NonChaos / nonChaos);
        values[(int)DamageType.Physical] = IncomingResourceDefences.Taken(physical, Get);
        ScaleResourceElements(values, kept.NonChaos - physical);
        ScaleResourceElements(values, IncomingResourceDefences.Taken(ResourceElementTotal(values), Get));
        values[(int)DamageType.Chaos] = IncomingResourceDefences.Taken(kept.Chaos, Get);

        other = ResourceElementTotal(values);
        var ward = AbsorbPlayerWard(conn, values[(int)DamageType.Physical], other,
            values[(int)DamageType.Chaos], at, player);
        int absorbOther = other - ward.Other;
        int wardBypass = Math.Clamp(Get("base_damage_bypass_ward_%"), 0, 100);
        foreach (var type in ResourceElements)
        {
            int amount = values[(int)type];
            int eligible = amount - (int)((long)amount * wardBypass / 100);
            int absorbed = Math.Min(eligible, absorbOther);
            values[(int)type] -= absorbed; absorbOther -= absorbed;
        }
        values[(int)DamageType.Physical] = ward.Physical;
        values[(int)DamageType.Chaos] = ward.Chaos;
        int recoup = (int)Math.Min(int.MaxValue, values.Sum(v => (long)v));

        int bypass = Math.Clamp(Get(IncomingResourceDefences.BypassStat), 0, 100);
        values[(int)DamageType.Physical] = IncomingResourceDefences.Damage(conn.EnergyShield, ward.Physical, bypass, at);
        // Preserve the old aggregate non-physical bypass rounding, then absorb its actual eligible types in order.
        var bypassed = new int[5];
        int bypassBudget = (int)((long)ward.Other * bypass / 100);
        foreach (var type in ResourceElements)
        { bypassed[(int)type] = (int)((long)values[(int)type] * bypass / 100); bypassBudget -= bypassed[(int)type]; }
        foreach (var type in ResourceElements)
            if (bypassBudget > 0 && bypassed[(int)type] < values[(int)type])
            { bypassed[(int)type]++; bypassBudget--; }
        foreach (var type in ResourceElements)
            values[(int)type] = bypassed[(int)type]
                + conn.EnergyShield.Damage(values[(int)type] - bypassed[(int)type], false, at);
        values[(int)DamageType.Chaos] = conn.EnergyShield.Damage(ward.Chaos, true, at);
        var life = ResourcePacket(values);
        int mana = PlayerDamageTaken.FromMana(life, (int)Math.Min(int.MaxValue, conn.PlayerMana), Get);
        conn.PlayerMana -= (uint)mana;
        if (ResourceEffectsCurrent(conn, state)) state.Mana.Hit(at, recoup, Get("damage_taken_goes_to_mana_%"));
        return (int)Math.Min(int.MaxValue, life.MinTotal - mana);
    }
    internal static (int Physical, int Other, int Chaos) AbsorbPlayerWard(ClientState conn,
        int physical, int other, int chaos, double now, IReadOnlyDictionary<uint, int> stats)
    {
        int Get(string name) => Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
        int nonChaos = (int)Math.Min(int.MaxValue, (long)physical + other);
        int total = (int)Math.Min(int.MaxValue, (long)nonChaos + chaos);
        conn.Ward.Advance(now);
        bool wasBroken = conn.Ward.Broken;
        int remaining = conn.Ward.Damage(total, now, Get);
        int absorbed = total - remaining;
        // Ordered absorption keeps chaos bypassing ES. The supported shared Ward bypass is applied per component.
        int bypass = Math.Clamp(Get("base_damage_bypass_ward_%"), 0, 100);
        int nonChaosEligible = nonChaos - (int)((long)nonChaos * bypass / 100);
        int nonChaosAbsorbed = Math.Min(nonChaosEligible, absorbed);
        int physicalEligible = physical - (int)((long)physical * bypass / 100);
        int physicalAbsorbed = Math.Min(physicalEligible, nonChaosAbsorbed);
        if (!wasBroken && conn.Ward.Broken
            && conn.Antiquarian.GrantAdrenaline(now, Get(AntiquarianEffects.AdrenalineSource)))
        {
            conn.Pools = PoolsFor(conn, conn.Pools.Level);
            conn.ApplyMovementSpeed();
        }
        return (physical - physicalAbsorbed, Math.Max(0, other - (nonChaosAbsorbed - physicalAbsorbed)),
            Math.Max(0, chaos - (absorbed - nonChaosAbsorbed)));
    }

}
