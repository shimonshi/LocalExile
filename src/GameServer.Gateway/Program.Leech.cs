using System.Runtime.CompilerServices;
using GameServer.Core.World;

partial class Program
{
    static readonly ConditionalWeakTable<ClientState, LeechSession> LeechSessions = new();
    internal static LeechSession LeechOf(ClientState conn) => LeechSessions.GetValue(conn, _ => new());

    static (bool Life, bool Mana, bool EnergyShield, bool FullEnergyShield) LeechContext(ClientState conn)
    {
        lock (conn.Monsters)
        {
            TrimLeechLocked(conn);
            if (conn.Closed || conn.LeavingArea || conn.RageRetired || conn.PlayerLife == 0) return default;
            var active = LeechOf(conn).Active(ProjectileWorldTime(conn));
            return (active.Life, active.Mana, active.EnergyShield, conn.EnergyShield.Full);
        }
    }

    static void WithLeechConditions(ClientState conn, Dictionary<uint, int> stats,
        IReadOnlySet<uint> overrides)
    {
        var state = LeechContext(conn);
        int Get(string name) => Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
        var conditions = SlayerRules.WhileLeeching(Get, state.Life, state.Mana, state.EnergyShield);
        if (conditions.AttackSpeedIncrease != 0)
        {
            var weapon = WeaponProfileFor(conn, activeSet: true);
            // Same increased-speed bucket and inherent MORE as PoolsFor; never feed back into raw sources.
            stats[StatKeys.MainHandAttackSpeedPct] += (int)Math.Round(conditions.AttackSpeedIncrease
                * (weapon.DualWield || weapon.Unarmed ? 1.1 : 1), MidpointRounding.AwayFromZero);
        }
        if (conditions.CannotBeStunned && Stats.Key("cannot_be_stunned") is uint stun && !overrides.Contains(stun))
            stats[stun] = 1;
        if (Stats.Key("base_damage_taken_+%") is uint taken)
            stats[taken] = (int)Math.Clamp((long)stats.GetValueOrDefault(taken) + conditions.DamageTakenIncrease,
                int.MinValue, int.MaxValue);
        foreach (var (name, active) in new[] { ("is_life_leeching", state.Life), ("is_mana_leeching", state.Mana),
            ("is_es_leeching", state.EnergyShield), ("is_leeching", state.Life || state.Mana || state.EnergyShield),
            ("on_full_energy_shield", state.FullEnergyShield) })
            if (Stats.Key(name) is uint key) stats[key] = active ? 1 : 0;
    }

    static void ConfigureLeech(ClientState conn, Func<string, int> get, IReadOnlyDictionary<uint, int> replacements,
        bool onLowLife)
    {
        int? Replacement(string name) => Stats.Key(name) is uint key && replacements.TryGetValue(key, out int value) ? value : null;
        lock (conn.Monsters)
        {
            int claws = (ActiveMainHand(conn)?.Type?.Class == "Claw" ? 1 : 0)
                + (ActiveOffHand(conn)?.Type?.Class == "Claw" ? 1 : 0);
            LeechOf(conn).Configure(ProjectileWorldTime(conn),
                LeechLimits.Resolve(get, conn.Pools.Life, conn.Pools.Mana, conn.Pools.EnergyShield, Replacement, onLowLife),
                get, Replacement, claws);
            TrimLeechLocked(conn);
        }
    }

    // Pool commits (including instant flasks/block gains) cannot leave ordinary leech alive until the next tick.
    internal static void TrimLeechLocked(ClientState conn)
    {
        double now = ProjectileWorldTime(conn);
        if (conn.PlayerLife == 0) LeechOf(conn).Clear(now);
        else LeechOf(conn).ClearFull(now, conn.PlayerLife >= conn.PlayerMaxLife,
            conn.PlayerMana >= UnreservedMana(conn), conn.EnergyShield.Current >= conn.EnergyShield.Maximum);
    }

    internal static LeechSources HitLeechSources(IReadOnlyDictionary<uint, int> player, bool attack,
        IReadOnlyDictionary<uint, int>? own = null, IReadOnlyDictionary<string, int>? local = null)
    {
        int Get(string name) => Stats.Key(name) is uint key
            ? (int)Math.Clamp((long)player.GetValueOrDefault(key) + (own?.GetValueOrDefault(key) ?? 0), int.MinValue, int.MaxValue) : 0;
        return LeechSources.From(Get, attack, name => local?.GetValueOrDefault(name) ?? 0);
    }

    // Invoke only inside an accepted direct-hit transaction, after admission, before assigning post-hit target Life.
    // The supplied direct inputs exclude separately triggered Impale and culling.
    internal static void LeechOnHitLocked(ClientState conn, SpawnedMonster target, DamagePacket taken,
        int directDamage, LeechSources sources, double overkillLife = 0)
    {
        if (!target.Hostile || target.Dead || target.Life == 0 || conn.Closed || conn.LeavingArea || conn.RageRetired || conn.Paused || conn.PlayerLife == 0) return;
        var amounts = sources.Amounts(taken, directDamage);
        amounts.Life += overkillLife;
        var session = LeechOf(conn);
        if (target.Mods.Effects["cannot_have_life_leeched_from"] != 0) amounts.Life = 0;
        if (target.Mods.Effects["cannot_have_energy_shield_leeched_from"] != 0)
        {
            amounts.EnergyShield = 0;
            if (session.ConvertsLifeToEnergyShield) amounts.Life = 0;
        }
        double now = ProjectileWorldTime(conn);
        var instant = session.Hit(now, amounts, conn.PlayerLife >= conn.PlayerMaxLife,
            conn.PlayerMana >= UnreservedMana(conn), conn.EnergyShield.Current >= conn.EnergyShield.Maximum, sources.Attack);
        uint life = conn.PlayerLife, mana = conn.PlayerMana;
        // Instant portions resolve in this hit transaction, so a later enemy hit sees the recovered resource.
        // Keep sub-point recovery between hits, and discard overflow at a full pool.
        conn.PlayerLife = RecoveryPool.Advance(life, conn.PlayerMaxLife, instant.Life, ref session.LifeInstantCarry, minimum: 1);
        conn.PlayerMana = RecoveryPool.Advance(mana, (uint)UnreservedMana(conn), instant.Mana, ref session.ManaInstantCarry);
        conn.EnergyShield.Gain(instant.EnergyShield);
        session.InstantLifeChanged |= life != conn.PlayerLife;
        session.InstantManaChanged |= mana != conn.PlayerMana;
        session.ClearFull(now, conn.PlayerLife >= conn.PlayerMaxLife, conn.PlayerMana >= UnreservedMana(conn),
            conn.EnergyShield.Current >= conn.EnergyShield.Maximum);
    }
}
