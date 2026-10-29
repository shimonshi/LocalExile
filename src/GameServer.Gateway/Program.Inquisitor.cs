using System.Runtime.CompilerServices;
using System.Net.Sockets;
using System.Collections.Concurrent;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.Protocol.Login;
using GameServer.Core.World;

partial class Program
{
    sealed class InquisitorSession
    {
        public InquisitorGround Ground { get; } = new();
        public InquisitorCombatState Combat { get; } = new();
        public ushort GroundBuff = 0, FanaticBuff = 0, ChargeBuff = 0;
        public uint? GroundObject;
        public (int X, int Y, int Radius) GroundArt;
        public (bool Ground, bool Fanatic, int Charges, int AttackMore, bool Flask, double Until) Sent = default;
        public Dictionary<uint, ushort> EnemyBuffs { get; } = [];
        public Dictionary<uint, ushort> GroundEnemyBuffs { get; } = [];
    }

    static readonly ConditionalWeakTable<ClientState, InquisitorSession> Inquisitors = new();
    static InquisitorSession InquisitorOf(ClientState conn) => Inquisitors.GetValue(conn, _ => new());

    // Raw active equipment/passive sources and developer replacements, before the final Inquisitor projection.
    static Dictionary<uint, int> InquisitorInputs(ClientState conn)
    {
        var sources = CharacterStatSources(conn).Stats;
        foreach (var (key, value) in conn.PlayerStats) sources[key] = value;
        return sources;
    }

    static InquisitorCombatState.Snapshot ObserveInquisitor(ClientState conn, IReadOnlyDictionary<uint, int> sources)
    {
        int Get(string name) => Stats.Key(name) is uint key ? sources.GetValueOrDefault(key) : 0;
        double now = ProjectileWorldTime(conn);
        var state = InquisitorOf(conn);
        bool alive = conn.PlayerLife > 0 && !conn.Closed && !conn.RageRetired;
        ObserveInquisitorGround(conn, state.Ground, conn.Walk, now, alive, InquisitorGround.Sources.From(Get));
        var inputs = InquisitorCombatState.Inputs.From(Get, fanaticismSeconds: 5);
        if (!alive) state.Combat.Clear();
        return state.Combat.Read(now, inputs);
    }

    static void ObserveInquisitorGround(ClientState conn, InquisitorGround ground, PlayerWalk walk, double now,
        bool alive, InquisitorGround.Sources sources) => ground.Observe(now, walk.Position(now), now < walk.ArrivesAt,
        alive, sources, conn.FlaskGrounds.Covers(FlaskGrounds.Kind.ConsecratedGround, walk.Position(now)),
        stationarySince: walk.ArrivesAt);

    // Both sides matter: the old walk may have arrived since its last tick, then this order starts a new one.
    internal static void InquisitorMotionChanged(ClientState conn, PlayerWalk walk, double now)
    {
        lock (conn.Monsters)
        {
            var sources = InquisitorInputs(conn);
            int Get(string name) => Stats.Key(name) is uint key ? sources.GetValueOrDefault(key) : 0;
            ObserveInquisitorGround(conn, InquisitorOf(conn).Ground, walk, now,
                conn.PlayerLife > 0 && !conn.Closed && !conn.RageRetired, InquisitorGround.Sources.From(Get));
        }
    }

    internal static void InquisitorSkillUsed(ClientState conn, bool attack, bool spell, bool instant = false)
    {
        lock (conn.Monsters)
        {
            if (conn.PlayerLife == 0 || conn.Closed || conn.LeavingArea || conn.RageRetired || conn.Paused) return;
            var sources = InquisitorInputs(conn);
            int Get(string name) => Stats.Key(name) is uint key ? sources.GetValueOrDefault(key) : 0;
            var state = InquisitorOf(conn).Combat;
            var inputs = InquisitorCombatState.Inputs.From(Get, 5);
            double now = ProjectileWorldTime(conn);
            if (attack) state.Attack(now, inputs);
            else if (spell) state.Spell(now, inputs, instant);
        }
    }

    static void WithInquisitorStats(ClientState conn, Dictionary<uint, int> stats)
    {
        lock (conn.Monsters)
        {
            var value = ObserveInquisitor(conn, stats);
            var ground = InquisitorOf(conn).Ground;
            int Get(string name) => Stats.Key(name) is uint key ? stats.GetValueOrDefault(key) : 0;
            void Add(string name, int amount)
            {
                if (Stats.Key(name) is uint key) stats[key] = (int)Math.Clamp((long)stats.GetValueOrDefault(key) + amount, int.MinValue, int.MaxValue);
            }
            if (ground.OwnerAffected(ProjectileWorldTime(conn)) && conn.FlaskGrounds.PlayerStats().Count == 0)
                foreach (var (name, amount) in FlaskGrounds.ConsecratedStats) Add(name, amount);
            Add("critical_strike_chance_+%", InquisitorOffense.RighteousProvidenceIncrease(Get, conn.Pools.Strength, conn.Pools.Intelligence));
            if (Stats.Key("on_consecrated_ground") is uint marker) stats[marker] = ground.OwnerAffected(ProjectileWorldTime(conn)) ? 1 : 0;
            if (Stats.Key("current_fanaticism_charges") is uint charges) stats[charges] = value.FanaticCharges;
        }
    }

    static InquisitorOffense.FanaticismModifiers InquisitorFanaticism(ClientState conn, int effect)
    {
        lock (conn.Monsters)
        {
            var sources = InquisitorInputs(conn);
            var value = ObserveInquisitor(conn, sources);
            return InquisitorOffense.Fanaticism(value.Fanaticism(ProjectileWorldTime(conn)),
                SkillsFor(conn).IsSpell(effect) && !SkillsFor(conn).IsAttack(effect),
                Stats.Key("buff_effect_on_self_+%") is uint buff ? sources.GetValueOrDefault(buff) : 0);
        }
    }

    static double InquisitorAttackFactor(ClientState conn)
    {
        lock (conn.Monsters) return ObserveInquisitor(conn, InquisitorInputs(conn)).AttackDamageFactor;
    }

    static DamagePacket InquisitorBattlemage(ClientState conn, Func<string, int> get)
    {
        var weapon = WeaponIn(conn, ActiveMainHand(conn));
        return InquisitorOffense.BattlemageDamage(weapon is null ? null
            : weapon.LocalDamage ?? DamagePacket.Of(DamageType.Physical, weapon.MinDamage, weapon.MaxDamage).Add(weapon.LocalAdded), get);
    }

    static double InquisitorElementalTaken(ClientState conn, SpawnedMonster target)
    {
        // Augury is a passive attachment, not a raw stat on the notable. Respect its Ruthless template.
        if (!conn.MainAscendancy.Allocated.Contains((ushort)40059) || !target.Hostile || target.Dead || target.Life == 0 || conn.PlayerLife == 0) return 1;
        double now = ProjectileWorldTime(conn);
        return Dist(conn.Walk.Position(now), target.Walk.Position(now)) <= 60 ? 1 + (conn.Ruthless ? 8 : 16) / 100d : 1;
    }

    static double InquisitorMirroredPerMinute(ClientState conn, Func<string, int> get)
    {
        if (!InquisitorOf(conn).Ground.OwnerMirrorsLifeRegeneration(ProjectileWorldTime(conn))
            || get("cannot_recover_energy_shield") != 0 || get("no_life_regeneration") != 0) return 0;
        // Pious is additional ES recovery from life regeneration, not ES regeneration.
        // PR318's normal life calculation includes life-only recovery; copy its preceding regeneration stage.
        return InquisitorLifeRegenerationBeforeRecovery(conn, get)
            * Math.Max(0, 1 + get("energy_shield_recovery_rate_+%") / 100d);
    }

    static double InquisitorLifeRegenerationBeforeRecovery(ClientState conn, Func<string, int> get) =>
        get("no_life_regeneration") != 0 ? 0 :
        Math.Max(0, get("base_life_regeneration_rate_per_minute")
            + conn.Pools.Life * get("life_regeneration_rate_per_minute_%") / 100d)
        * Math.Max(0, 1 + get("life_regeneration_rate_+%") / 100d);

    static double InquisitorMirroredRecovery(ClientState conn, double from, double to, Func<string, int> get)
    {
        if (get("cannot_recover_energy_shield") != 0 || get("no_life_regeneration") != 0) return 0;
        ObserveInquisitor(conn, InquisitorInputs(conn));
        return InquisitorOf(conn).Ground.MirroredLifeRegeneration(from, to,
            InquisitorLifeRegenerationBeforeRecovery(conn, get) / 60)
            * Math.Max(0, 1 + get("energy_shield_recovery_rate_+%") / 100d);
    }

    static int InquisitorCastSpeed(ClientState conn, int effect, int increased) =>
        (int)Math.Clamp(Math.Round((100d + increased) * InquisitorFanaticism(conn, effect).CastSpeedFactor - 100), int.MinValue, int.MaxValue);

    static int InquisitorGroundTaken(ClientState conn, SpawnedMonster target)
    {
        lock (conn.Monsters)
        {
            ObserveInquisitor(conn, InquisitorInputs(conn));
            double now = ProjectileWorldTime(conn);
            var at = target.Walk.Position(now);
            // Monster identity is its live object key; retired/dead recipients never retain an effect.
            uint id = conn.Monsters.FirstOrDefault(kv => ReferenceEquals(kv.Value, target)).Key;
            return InquisitorOf(conn).Ground.EnemyDamageTaken(now, id, at, target.Hostile && !target.Dead && target.Life > 0,
                conn.FlaskGrounds.Covers(FlaskGrounds.Kind.ConsecratedGround, at));
        }
    }

    internal static DamagePacket InquisitorTargetDamage(ClientState conn, SpawnedMonster target, DamagePacket hit)
    {
        double ground = InquisitorGroundTaken(conn, target) / 100d;
        double elemental = InquisitorElementalTaken(conn, target) - 1;
        // Both are increased damage taken; additive together, before separate more/less multipliers.
        return hit.Select((type, range) => range.Scale(Math.Max(0, 1 + target.Mods.Effects["base_damage_taken_+%"] / 100d + ground +
            (type is DamageType.Fire or DamageType.Cold or DamageType.Lightning ? elemental : 0))));
    }

    static double InquisitorFireTaken(ClientState conn, SpawnedMonster target) =>
        Math.Max(0, InquisitorElementalTaken(conn, target) + target.Mods.Effects["base_damage_taken_+%"] / 100d + InquisitorGroundTaken(conn, target) / 100d);

    internal static DamagePacket InquisitorEnemyDamage(ClientState conn, SpawnedMonster target, DamagePacket hit)
    {
        if (InquisitorElementalTaken(conn, target) == 1) return hit;
        return hit.Select((type, range) => range.Scale(type is DamageType.Fire or DamageType.Cold or DamageType.Lightning ? .92 : 1));
    }

    static async Task SendInquisitor(string remote, NetworkStream stream, ConnectionCiphers ciphers, ClientState conn)
    {
        await Send(remote, stream, ciphers, [], "Inquisitor effects", atGate: () =>
        {
            lock (conn.Monsters)
            {
                if (conn.Closed || conn.LeavingArea || conn.RageRetired) return null;
                double now = ProjectileWorldTime(conn);
                var state = InquisitorOf(conn);
                var value = ObserveInquisitor(conn, InquisitorInputs(conn));
                var current = (Ground: state.Ground.OwnerAffected(now), Fanatic: value.Fanaticism(now), Charges: value.FanaticCharges, AttackMore: value.AttackMorePct,
                    Flask: conn.FlaskGrounds.PlayerStats().Count > 0, Until: value.Fanaticism(now) ? value.FanaticismUntil : 0);
                if (current == default && state.Sent == default && state.EnemyBuffs.Count == 0
                    && state.GroundEnemyBuffs.Count == 0 && state.Ground.Radius == 0 && state.GroundObject is null
                    && !conn.MainAscendancy.Allocated.Contains((ushort)40059)) return null;
                var bytes = new List<byte>();
                foreach (var gone in state.EnemyBuffs.Keys.Except(conn.Monsters.Keys).ToArray()) state.EnemyBuffs.Remove(gone);
                foreach (var gone in state.GroundEnemyBuffs.Keys.Except(conn.Monsters.Keys).ToArray()) state.GroundEnemyBuffs.Remove(gone);
                var artAt = state.Ground.Position;
                var wantedArt = (artAt.X, artAt.Y, state.Ground.Radius);
                if (state.GroundArt != wantedArt || state.GroundObject is null && state.Ground.HasStationaryGround)
                {
                    if (state.GroundObject is uint oldObject) bytes.AddRange(InstanceProtocol.RemoveObject(oldObject));
                    state.GroundObject = null;
                    state.GroundArt = wantedArt;
                    if (state.Ground.HasStationaryGround && FlaskGroundArt.Value.TryGetValue(FlaskGrounds.Kind.ConsecratedGround, out var art))
                    {
                        uint obj = conn.NextObjectId++;
                        state.GroundObject = obj;
                        var effect = new EffectObject(artAt.X, artAt.Y, art.Ao) { Flags0 = EffectObject.FixedRotation,
                            Scale = (float)(state.Ground.Radius * (250d / 23) / Math.Max(1, art.ArtRadius)) };
                        bytes.AddRange(InstanceEntities.CreateObject(obj, InstanceEntities.ObjectTypeHash(EffectObject.TypePath), effect.Blob()));
                        bytes.AddRange(InstanceProtocol.EnterWakeZone(obj));
                    }
                }
                foreach (var (id, monster) in conn.Monsters)
                {
                    bool ground = state.Ground.EnemyDamageTaken(now, id, monster.Walk.Position(now),
                        monster.Hostile && !monster.Dead && monster.Life > 0,
                        conn.FlaskGrounds.Covers(FlaskGrounds.Kind.ConsecratedGround, monster.Walk.Position(now))) != 0;
                    if (ground && !state.GroundEnemyBuffs.ContainsKey(id))
                    {
                        state.GroundEnemyBuffs[id] = conn.NextBuff++;
                        bytes.AddRange(InstanceEntities.AddBuff(id, state.GroundEnemyBuffs[id], 1265, PlayerId));
                    }
                    else if (!ground && state.GroundEnemyBuffs.Remove(id, out ushort groundGone))
                        bytes.AddRange(InstanceEntities.RemoveBuff(id, groundGone));
                    bool affected = InquisitorElementalTaken(conn, monster) != 1;
                    if (affected && !state.EnemyBuffs.ContainsKey(id))
                    {
                        state.EnemyBuffs[id] = conn.NextBuff++;
                        bytes.AddRange(InstanceEntities.AddBuff(id, state.EnemyBuffs[id], 582, PlayerId));
                    }
                    else if (!affected && state.EnemyBuffs.Remove(id, out ushort gone))
                        bytes.AddRange(InstanceEntities.RemoveBuff(id, gone));
                }
                if (current != state.Sent)
                {
                    foreach (ushort old in new[] { state.GroundBuff, state.FanaticBuff, state.ChargeBuff })
                        if (old != 0) bytes.AddRange(InstanceEntities.RemoveBuff(PlayerId, old));
                    state.GroundBuff = state.FanaticBuff = state.ChargeBuff = 0;
                    if (current.Ground && !current.Flask) bytes.AddRange(InstanceEntities.AddBuff(PlayerId, state.GroundBuff = conn.NextBuff++, FlaskGrounds.ConsecratedBuffRow, PlayerId));
                    if (current.Fanatic) bytes.AddRange(InstanceEntities.AddBuff(PlayerId, state.FanaticBuff = conn.NextBuff++, 1719, PlayerId,
                        u68: BitConverter.SingleToUInt32Bits((float)Math.Max(0, value.FanaticismUntil - now))));
                    if (current.Charges > 0) bytes.AddRange(InstanceEntities.AddBuff(PlayerId, state.ChargeBuff = conn.NextBuff++, 1700, PlayerId,
                        stacks: (ushort)current.Charges));
                    state.Sent = current;
                    bytes.AddRange(PlayerStatsUpdateAtGate(conn, PlayerStatsWithCleared(conn)));
                    bytes.AddRange(InstanceEntities.PlayerSkillsChange(PlayerId, PlayerSkills(conn), []));
                }
                return bytes.Count == 0 ? null : [.. bytes];
            }
        });
    }

    sealed record InquisitorTransfer(string Character, DateTime At, InquisitorCombatState.Carried Combat);
    static readonly ConcurrentDictionary<uint, InquisitorTransfer> CarriedInquisitors = new();
    static void CarryInquisitor(ClientState conn, uint ticket)
    {
        lock (conn.Monsters)
        {
            var raw = InquisitorInputs(conn);
            int Get(string name) => Stats.Key(name) is uint key ? raw.GetValueOrDefault(key) : 0;
            if (conn.PlayerLife == 0) InquisitorOf(conn).Combat.Clear();
            foreach (var (old, entry) in CarriedInquisitors)
                if (DateTime.UtcNow - entry.At > TimeSpan.FromMinutes(5)) CarriedInquisitors.TryRemove(old, out _);
            CarriedInquisitors.TryAdd(ticket, new(CharacterName(conn), DateTime.UtcNow,
                InquisitorOf(conn).Combat.Carry(ProjectileWorldTime(conn), InquisitorCombatState.Inputs.From(Get, 5))));
        }
    }

    static void RestoreInquisitor(ClientState conn)
    {
        lock (conn.Monsters)
        {
            if (!CarriedInquisitors.TryGetValue(conn.Ticket, out var entry) || entry.Character != CharacterName(conn)
                || !CarriedInquisitors.TryRemove(new KeyValuePair<uint, InquisitorTransfer>(conn.Ticket, entry))) return;
            var raw = InquisitorInputs(conn);
            int Get(string name) => Stats.Key(name) is uint key ? raw.GetValueOrDefault(key) : 0;
            if (conn.PlayerLife > 0 && DateTime.UtcNow - entry.At <= TimeSpan.FromMinutes(5))
                InquisitorOf(conn).Combat.Restore(entry.Combat, ProjectileWorldTime(conn), InquisitorCombatState.Inputs.From(Get, 5));
        }
    }
}
