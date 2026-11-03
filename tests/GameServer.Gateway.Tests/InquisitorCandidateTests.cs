using System.Net;
using System.Net.Sockets;
using GameServer.Core.Protocol.Login;
using GameServer.Core.Protocol.Instance;
using GameServer.Modding;
using League = GameServer.Core.Accounts.League;
using System.Text.Json;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed class InquisitorCandidateTests
{
    static Dictionary<string, int> Sources(params ushort[] nodes)
    {
        var definition = Assert.Single(new AscendancyCatalog().Definitions,
            d => d.Identity.Family == AscendancyFamily.Normal && d.Name == "Inquisitor");
        var values = new Dictionary<uint, int>();
        foreach (ushort node in nodes)
            foreach (var (key, value) in definition.Nodes[node].Stats)
                values[key] = values.GetValueOrDefault(key) + value;
        var result = new Dictionary<string, int>();
        foreach (var name in new[] { InquisitorOffense.Providence, InquisitorOffense.Justice,
            InquisitorOffense.Battlemage, InquisitorOffense.Virtue, "maximum_fanaticism_charges",
            "gain_x_fanatic_charges_every_second_if_have_attacked_in_past_second",
            "gain_fanaticism_for_4_seconds_on_reaching_maximum_fanatic_charges",
            "lose_all_fanatic_charges_on_reaching_maximum_fanatic_charges", InquisitorRecipients.EnemyTaken,
            InquisitorRecipients.Mirror, FlaskGrounds.LingerStat })
            result[name] = Program.Stats.Key(name) is uint key ? values.GetValueOrDefault(key) : 0;
        return result;
    }

    [Fact]
    public async Task Qualified_offense_composes_with_local_weapon_spell_and_critical_engines()
    {
        await WiredOffense();
        var sources = Sources(53884, 3154, 19417);
        int Get(string name) => sources.GetValueOrDefault(name);
        Assert.Equal(150, Game.InquisitorProvidence(Get, 200, 150));
        sources[InquisitorOffense.Providence] = 2;
        Assert.Equal(150, Game.InquisitorProvidence(Get, 200, 150)); // boolean, not twice
        var items = new ItemCatalog();
        var main = Assert.IsType<ItemCatalog.BaseType>(items.Find("Driftwood Wand"));
        using var rows = JsonDocument.Parse(File.ReadAllBytes("data/tables/WeaponTypes.json"));
        var row = Assert.Single(rows.RootElement.EnumerateArray(), r => r.GetProperty("BaseItemTypesKey").GetInt32() == main.Row);
        var weapon = ItemWeapons.Of(row.GetProperty("Speed").GetInt32(), row.GetProperty("DamageMin").GetInt32(),
            row.GetProperty("DamageMax").GetInt32(), row.GetProperty("RangeMax").GetInt32(), new Dictionary<string, int>(), 20);
        int critical = items.WeaponCritical(main);
        Assert.Equal(AttackBaseCriticalStrike.Chance(true, critical, 150, Get),
            Game.InquisitorAttackCritical(true, critical, 0, 200, 150, Get));
        int chance = Game.InquisitorSpellCritical(600, critical, 200, 150, Get);
        Assert.Equal(SpellCriticalStrike.Chance(critical, Get, playerIncrease: 150), chance);
        Assert.Equal(0, Game.InquisitorSpellCritical(600, null, 200, 150, Get));
        sources["global_cannot_crit"] = 1;
        Assert.Equal(0, Game.InquisitorSpellCritical(600, critical, 200, 150, Get));
        sources.Remove("global_cannot_crit");
        sources["attack_damage_+%"] = 900;
        sources["spell_damage_+%"] = 100;
        var intrinsic = DamagePacket.Of(DamageType.Fire, 10, 10);
        var packet = Game.InquisitorSpellPacket(intrinsic, weapon, Get, 3);
        Assert.Equal(weapon.LocalDamage![DamageType.Physical].Min * 6, packet[DamageType.Physical].Min, 8);
        Assert.Equal(20, packet[DamageType.Fire].Min, 8);
        sources[InquisitorOffense.Battlemage] = 0;
        Assert.Equal(0, Game.InquisitorSpellPacket(intrinsic, weapon, Get, 3)[DamageType.Physical].Min);
        Assert.Equal(0, Game.InquisitorSpellPacket(intrinsic, null, Get, 3)[DamageType.Physical].Min);
        sources[InquisitorOffense.Providence] = 0;
        sources[InquisitorOffense.Justice] = 0;
        Assert.Equal(600, Game.InquisitorSpellCritical(600, critical, 200, 150, Get));
    }

    [Fact]
    public async Task Accepted_uses_reload_refund_and_recipient_expiry_preserve_clock_and_recovery_boundaries()
    {
        await WiredTiming();
        var sources = Sources(19417, 13851, 39790, 32816);
        int Get(string name) => sources.GetValueOrDefault(name);
        var inputs = InquisitorCombatState.Inputs.From(Get, InquisitorCombatState.BaseFanaticismSeconds);
        Assert.True(inputs.Zeal);
        var state = new InquisitorCombatState();
        state.Used(0, inputs, new(false, true, false, false, false));
        state.Used(0, inputs, new(false, true, false, true, false));
        state.Used(0, inputs, new(false, true, false, false, true));
        state.Used(0, inputs, new(false, true, true, false, false));
        Assert.Equal(inputs.AttackMorePerSpell, state.Read(0, inputs).AttackMorePct);
        for (int i = 0; i < inputs.MaximumFanaticCharges; i++)
            state.Used(i + .1, inputs, new(true, false, false, false, false));
        double reached = inputs.MaximumFanaticCharges;
        Assert.True(state.Read(reached, inputs).Fanaticism(reached));
        Assert.Equal(0, state.Read(reached, inputs).FanaticCharges);
        var neutral = state.CaptureNeutral(reached + 1, inputs);
        Assert.Equal(default, state.Read(reached + 1, inputs));
        var restored = new InquisitorCombatState();
        restored.RestoreNeutral(neutral, 100, inputs);
        Assert.True(restored.Read(103.999, inputs).Fanaticism(103.999));
        Assert.False(restored.Read(104, inputs).Fanaticism(104));
        Assert.Equal(0, restored.Read(108, inputs).AttackMorePct);
        restored.Read(108, default); // refund
        Assert.False(restored.Read(108, inputs).Fanaticism(108));
        restored.Retire();
        restored.Used(109, inputs, new(false, true, false, false, false));
        Assert.Equal(default, restored.Read(109, inputs));

        var ground = new InquisitorRecipients();
        ground.ObserveOwner(0, true, true, Get);
        Assert.True(ground.ObserveEnemy(0, 42, true, true, Get) > 0);
        Assert.Equal(20, ground.MirroredRecovery(0, 2, 10, Get), 8);
        ground.ObserveOwner(2, true, false, Get);
        int sanctuary = ground.ObserveEnemy(2, 42, true, false, Get);
        sources["life_recovery_rate_+%"] = 900; // Life-only recovery must not scale the mirror
        sources["energy_shield_regeneration_rate_+%"] = 900;
        sources["energy_shield_recovery_rate_+%"] = 50;
        double expiry = 2 + Get(FlaskGrounds.LingerStat) / 1000d;
        Assert.Equal(15 * (expiry - 2), ground.MirroredRecovery(2, expiry + 1, 10, Get), 8);
        Assert.Equal(0, ground.ObserveEnemy(expiry, 42, true, false, Get));
        sources["cannot_recover_energy_shield"] = 1;
        Assert.Equal(0, ground.MirroredRecovery(2, expiry, 10, Get));
        sources.Remove("cannot_recover_energy_shield");
        sources["no_life_regeneration"] = 1;
        Assert.Equal(0, ground.MirroredRecovery(2, expiry, 10, Get));
        ground.ObserveOwner(expiry, false, false, Get);
        Assert.Equal(0, ground.MirroredRecovery(expiry, expiry + 1, 10, Get));
        Assert.Equal(sanctuary + 16, InquisitorNearby.TakenIncrease(DamageType.Fire, sanctuary,
            InquisitorNearby.AuguryIncrease(true, false, true, 60)));
        Assert.Equal(sanctuary, InquisitorNearby.TakenIncrease(DamageType.Chaos, sanctuary, 16));
        Assert.Equal(8, InquisitorNearby.AuguryIncrease(true, true, true, 60));
        Assert.Equal(0, InquisitorNearby.AuguryIncrease(true, false, true, 60.01));
        Assert.Equal(.92, InquisitorNearby.IncomingFactor(DamageType.Fire, true));
        Assert.Equal(1, InquisitorNearby.IncomingFactor(DamageType.Physical, true));
    }

    static uint K(string name) => Program.Stats.Key(name)!.Value;

    static async Task WiredOffense()
    {
        using var h = new OrdinaryHarness(); var c = h.Conn;
        h.Build(); var baseline = c.Pools;
        h.Build(53884);
        Assert.True(c.Pools.Strength >= baseline.Strength + 50);
        var raw = Game.CharacterPlayerStats(c);
        Assert.True(raw[K("critical_strike_chance_+%")] >= Math.Min(c.Pools.Strength, c.Pools.Intelligence));
        h.Build(3154, 19417);
        Assert.True(Game.SupportedHitInputsFor(c, h.Skill)!.Raw[DamageType.Physical].Min > 0);
        int withWeapon = Game.SupportedCriticalChance(c, h.Skill);
        Assert.True(withWeapon > 0);
        c.PlayerStats[K("global_cannot_crit")] = 1;
        Assert.Equal(0, Game.SupportedCriticalChance(c, h.Skill));
        c.PlayerStats.Remove(K("global_cannot_crit"));
        int refund = c.MainAscendancy.Allocated.Count * 5;
        Assert.Null(await Game.ChangePassives("Inquisitor", h.Stream, h.Ciphers, c, c.MainAscendancy.Allocated.ToArray(), false, "refund branch", refundPoints: true));
        Assert.Equal((ushort)(200 - refund), c.RefundPoints);
        Assert.Equal(0, Game.SupportedHitInputsFor(c, h.Skill)!.Raw[DamageType.Physical].Min);
    }

    static async Task WiredTiming()
    {
        using var h = new OrdinaryHarness(); var c = h.Conn;
        h.Build(19417, 13851);
        var unreachable = new SpawnedMonster("moving target", new(200, 200, 0), 0, 0, 1000);
        h.Time.At = .1;
        Assert.False(Game.AcceptDefaultSwingUse(c, unreachable, (100, 100), (200, 200), 10, 1));
        h.Time.At = 1;
        Assert.Equal(0, Game.InquisitorRead(c, Game.CharacterPlayerStats(c)).FanaticCharges);
        h.Time.At = 1.1;
        Assert.True(Game.AcceptDefaultSwingUse(c, unreachable, (200, 200), (200, 200), 10, 2));
        h.Time.At = 2;
        Assert.Equal(1, Game.InquisitorRead(c, Game.CharacterPlayerStats(c)).FanaticCharges);
        Game.ClearInquisitor(c);
        h.Time.At = .1;
        Assert.True(Game.TryBeginRangedAttack(c, .1, 1, SkillCatalog.FirstSkillId, 0, out _));
        h.Time.At = 1.1; // This fresh session's first full one-second period starts at .1.
        Assert.Equal(1, Game.InquisitorRead(c, Game.CharacterPlayerStats(c)).FanaticCharges);
        Game.ClearInquisitor(c);
        h.Time.At = 0;
        var beforeUse = Game.WeaponStats(c, Game.WeaponFor(c), Game.CharacterPlayerStats(c)).ToDictionary(p => p.Item1, p => p.Item2);
        Game.InquisitorAcceptedUse(c, Game.SupportedSkill(c, h.Skill).Effect);
        var afterUse = Game.WeaponStats(c, Game.WeaponFor(c), Game.CharacterPlayerStats(c)).ToDictionary(p => p.Item1, p => p.Item2);
        Assert.True(afterUse[StatKeys.DisplayHundredTimesDamagePerSkillUse] > beforeUse[StatKeys.DisplayHundredTimesDamagePerSkillUse]);
        Assert.Equal(10, Game.InquisitorRead(c, Game.CharacterPlayerStats(c)).AttackMorePct);
        var snapshot = c.InquisitorCombat.ToDictionary(p => p.Key, p => p.Value.ToArray());
        // A new Game helper instance reads only BCL payload on the durable connection.
        c.InquisitorCombat = snapshot;
        int max = InquisitorCombatState.Inputs.From(n => Game.CharacterPlayerStats(c).GetValueOrDefault(K(n)), 5).MaximumFanaticCharges;
        for (int i = 0; i < max; i++)
        {
            h.Time.At = i + .1;
            Game.InquisitorRead(c, Game.CharacterPlayerStats(c), new(true, false, false, false, false));
        }
        h.Time.At = max;
        Assert.True(Game.InquisitorRead(c, Game.CharacterPlayerStats(c)).Fanaticism(max));
        Assert.True(Game.PayMana(c, h.Skill, out uint cost, out _));
        Assert.Equal(1u, cost);
        var selfCast = Game.CharacterPlayerStats(c);
        Assert.Equal(.25m, Game.InquisitorModifiers(c, Game.SupportedSkill(c, h.Skill).Effect, selfCast).CostFactor);
        selfCast[K("is_totem")] = 1;
        Assert.Equal(1m, Game.InquisitorModifiers(c, Game.SupportedSkill(c, h.Skill).Effect, selfCast).CostFactor);
        Assert.Equal((int)cost, Game.PlayerSkills(c).Single(g => g.Id == h.Skill).Stats!.Single(p => p.Key == K("mana_cost")).Value);
        Assert.NotEmpty(Game.InquisitorPresentation(c));
        Game.CarryInquisitor(c, 907);
        var arrival = h.NewConnection(907, c.Character); arrival.MainAscendancy = c.MainAscendancy; h.Time.At = 100;
        Game.RestoreInquisitor(arrival);
        Game.ActivateInquisitor(arrival, 100);
        Assert.True(Game.InquisitorRead(arrival, Game.CharacterPlayerStats(arrival)).Fanaticism(100));
        var duplicate = h.NewConnection(907, c.Character); Game.RestoreInquisitor(duplicate);
        Assert.Empty(duplicate.InquisitorCombat);
        h.Build(40059, 39790, 32816);
        Game.CharacterPlayerStats(c); Game.InquisitorGroundStep(c, 100);
        Assert.NotEmpty(c.FlaskGrounds.PlayerStats());
        var enemy = new SpawnedMonster("Inquisitor target", new(105, 100, 0), 0, 0, 1000);
        c.Monsters[42] = enemy;
        Game.InquisitorGroundStep(c, 100);
        int sanctuary = c.InquisitorGroundSources[InquisitorRecipients.EnemyTaken];
        var fireHit = Game.MonsterHitDamage(c, enemy, DamagePacket.Of(DamageType.Fire, 100, 100), false, false, 100, default);
        Assert.Equal(116 + sanctuary, fireHit.Damage);
        Assert.Equal(1 + (sanctuary + 16) / 100d, Game.MonsterDotTaken(enemy, DamageType.Fire, 100), 8);
        Assert.Equal(100 + sanctuary, Game.MonsterHitDamage(c, enemy, DamagePacket.Of(DamageType.Chaos, 100, 100), false, false, 100, default).Damage);
        Assert.True(Game.InquisitorAugury(c, enemy, 100));
        var applied = Game.ApplyMonsterHitLocked(c, enemy, fireHit.Damage);
        Assert.NotNull(applied); Assert.Equal((uint)(1000 - fireHit.Damage), enemy.Life);
        var recovery = Game.CharacterPlayerStats(c);
        int Regen(string n) => recovery.GetValueOrDefault(K(n));
        double mirrored = Game.InquisitorMirrorRate(c, Regen);
        Assert.True(mirrored > 0);
        recovery[K("life_recovery_rate_+%")] = 900;
        recovery[K("energy_shield_regeneration_rate_+%")] = 900;
        Assert.Equal(mirrored, Game.InquisitorMirrorRate(c, Regen), 8);
        recovery[K("energy_shield_recovery_rate_+%")] = 50;
        Assert.Equal(mirrored * 1.5, Game.InquisitorMirrorRate(c, Regen), 8);
        c.Walk.Charge(250, 100, 300, 100);
        Assert.True(c.Walk.Moving(100), $"walk {c.Walk.Position(100)} -> {c.Walk.Destination}, arrives {c.Walk.ArrivesAt}");
        Assert.True(Game.CurrentGameWork);
        Assert.NotNull(c.Walk.MotionChanged);
        Assert.False(c.InquisitorOwnerInside);
        Assert.Equal(104, c.InquisitorOwnerUntil, 6);
        Game.InquisitorGroundStep(c, 104);
        Assert.Empty(c.FlaskGrounds.PlayerStats());
        // Late observation clips a finite own ground's linger to its actual deadline.
        h.Time.At = 104;
        c.PlayerStats[K("consecrated_ground_while_stationary_radius")] = 0;
        Game.CharacterPlayerStats(c);
        c.Walk.Teleport(100, 100, 104);
        c.FlaskGrounds.Add(new(FlaskGrounds.Kind.ConsecratedGround, 100, 100, 18, 105));
        Game.InquisitorGroundStep(c, 104);
        Assert.True(c.InquisitorOwnerInside);
        h.Time.At = 110;
        Game.InquisitorGroundStep(c, 110);
        Assert.Equal(109, c.InquisitorOwnerUntil, 8);
        Assert.Empty(c.FlaskGrounds.PlayerStats());
        Assert.Null(await Game.ChangePassives("Inquisitor", h.Stream, h.Ciphers, c, [(ushort)32816], false, "refund", refundPoints: true));
        c.PlayerLife = 0; Game.InquisitorRead(c, Game.CharacterPlayerStats(c));
        Assert.Empty(c.InquisitorCombat);
    }

    sealed class Clock : IProjectileRuntime
    {
        public double At; public bool Active => true;
        public double NowTicks => At * 120000;
        public void Invalidate() { }
    }

    sealed class Sink : NetworkStream
    {
        readonly Socket peer;
        static (Socket, Socket) Pair()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            var writer = new Socket(SocketType.Stream, ProtocolType.Tcp); writer.Connect(listener.LocalEndpoint);
            return (writer, listener.AcceptSocket());
        }
        public Sink() : this(Pair()) { }
        Sink((Socket Writer, Socket Peer) pair) : base(pair.Writer, ownsSocket: true) => peer = pair.Peer;
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        protected override void Dispose(bool disposing) { if (disposing) peer.Dispose(); base.Dispose(disposing); }
    }

    sealed class OrdinaryHarness : IDisposable
    {
        readonly string path = Path.Combine(Path.GetTempPath(), "inquisitor-" + Guid.NewGuid().ToString("N") + ".db");
        readonly IGameLogic? old = Program.GameLogicOrNull;
        readonly IModuleLifetime? oldRules = Game.CombatLifetime;
        readonly ModuleLifetime rules = new("Game");
        readonly List<ClientState> connections = [];
        internal readonly AscendancyCatalog.Definition Definition = new AscendancyCatalog().Definitions.Single(d => d.Name == "Inquisitor" && d.Identity.Family == AscendancyFamily.Normal);
        internal readonly Clock Time = new();
        internal readonly Sink Stream = new();
        internal readonly ConnectionCiphers Ciphers = new(new byte[64], false);
        internal readonly ClientState Conn;
        internal ushort Skill => Game.GemSkillEffects(Conn).Single(g => Game.SkillsFor(Conn).EffectId(g.Effect) == "Fireball").Id;
        public OrdinaryHarness()
        {
            Saves.Open(path); Game.CombatLifetime = rules; Program.SetGameLogic(new GameServer.Modules.Game.GameLogic());
            Conn = NewConnection(906);
            var gem = ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SkillGemFireball")!);
            Game.ItemsOf(Conn).Add(PlayerInventories.Weapon1, 0, 0, ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!) with
                { Sockets = [new(SocketColour.White, gem)], Links = [1] });
        }
        internal ClientState NewConnection(uint ticket, string? character = null)
        {
            var c = new ClientState(new(1, "Inquisitor", 300, 300, 100, 100, null, []), ticket, 1)
            {
                Character = character ?? "Inquisitor" + Guid.NewGuid().ToString("N"), ClassRow = 6, PlayerLife = 1000, PlayerMana = 1000,
                Instance = new(), Experience = CharacterStats.ExperienceFor(68), Pools = new(68, 1000, 1000, 100, 0),
                LeagueModule = new(new League("InquisitorTest", "Standard", false, false), new HashSet<string>(["Game"])),
                Keyring = new InstanceKeyring(new byte[32]), ProjectileRuntimeState = Time,
            };
            connections.Add(c); return c;
        }
        internal void Build(params ushort[] targets)
        {
            var nodes = new HashSet<ushort>();
            foreach (ushort target in targets)
            {
                var queue = new Queue<ushort>(); queue.Enqueue(Definition.StartNode);
                var parent = new Dictionary<ushort, ushort> { [Definition.StartNode] = Definition.StartNode };
                while (queue.TryDequeue(out ushort node) && node != target)
                    foreach (ushort next in Definition.Nodes[node].Adjacent.Where(Definition.Nodes.ContainsKey))
                        if (parent.TryAdd(next, node)) queue.Enqueue(next);
                for (ushort at = target; at != Definition.StartNode; at = parent[at]) nodes.Add(at);
            }
            Assert.True(nodes.Count <= 8);
            Conn.InquisitorRetired = false; Game.ClearInquisitor(Conn);
            Conn.MainAscendancy = new(Definition.Identity, nodes.ToArray(), AscendancyRules.AwardKeys);
            Game.AscendancyRules.Validate(Definition.BaseClass, Conn.MainAscendancy);
            Conn.Passives = []; Conn.RefundPoints = 200; Conn.StatSourcesCacheState = null;
            CharacterSaves.SetMainAscendancy(Conn.Character!, Conn.MainAscendancy, [], new Dictionary<ushort, ushort>(), 200);
            Conn.SavedPassiveProgress = new(Conn.MainAscendancy, [], new Dictionary<ushort, ushort>(), 200);
            Conn.Pools = Game.PoolsFor(Conn, 68); Conn.PlayerLife = Conn.PlayerMaxLife; Conn.PlayerMana = Conn.Pools.Mana;
        }
        public void Dispose()
        {
            foreach (var c in connections) { c.Closed = true; c.RetirePlayerUpdates(); c.RetireResourceEffects(); c.RetireOwnedCombat(); PlayerInventories.Forget(c.Character!); }
            Stream.Dispose(); rules.Stop(_ => { }); Game.CombatLifetime = oldRules; Program.SetGameLogic(old!);
            Saves.Db.Dispose(); foreach (string suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }
    }
}
