using GameServer.Core.World;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.Projectiles;

using League = GameServer.Core.Accounts.League;

public sealed partial class WhispererRuntimeTests
{
    internal static void DeadeyeGameplayTransactions()
    {
        var definition = Assert.Single(new AscendancyCatalog().Definitions,
            d => d.Identity.Family == AscendancyFamily.Normal && d.Name == "Deadeye");
        using var h = new Harness(definition); var c = h.Conn;
        c.MainAscendancy = new(definition.Identity, [24848, 23169, 45313, 44482], []);
        c.LeagueModule = new(new League("Standard", "Standard", false, false), new HashSet<string>(["Game"]));
        c.StatSourcesCacheState = null;
        Assert.Equal(8, Game.OrdinaryTailwindSpeed(c));
        for (int i = 1; i <= 10; i++) Game.OrdinaryAcceptedUse(c, i);
        Game.OrdinaryAcceptedUse(c, 10); // Same admitted action cannot grant twice.
        Assert.Equal(16, Game.OrdinaryTailwindSpeed(c));
        var player = Game.CharacterPlayerStats(c);
        Assert.Equal(.6, Game.OrdinaryIncomingHit(c, player, 0), 8);
        Assert.Equal(8, Game.OrdinaryTailwindSpeed(c));
        Game.OrdinaryAcceptedUse(c, 11);
        var native = Game.OrdinaryPresentation(c);
        Assert.NotEmpty(native); Assert.NotEqual((ushort)0, c.OrdinaryTailwindBuff);
        Assert.Empty(Game.OrdinaryPresentation(c));
        OrdinaryAdapterTransactions(h);
        var gem = h.Fireball;
        var session = Game.ProjectilesFor("ordinary", h.Wire, h.Ciphers, c);
        var released = Game.CaptureGemProjectile(c, session, gem.Id, gem, (170, 100), Game.CaptureSkillCriticalRoll(c, gem.Id));
        var offense = Assert.IsType<ProjectileOffense>(released.Payload);
        Assert.Equal(1.3, offense.OrdinaryDistance.HitMultiplier(70, false), 8);
        c.MainAscendancy = new(definition.Identity, [], []); c.StatSourcesCacheState = null;
        Assert.Equal(0, Game.OrdinaryTailwindSpeed(c));
        Assert.NotEmpty(Game.OrdinaryPresentation(c)); Assert.Equal((ushort)0, c.OrdinaryTailwindBuff);
        Assert.Equal(1.3, offense.OrdinaryDistance.HitMultiplier(70, false), 8); // Release survives refund.
        Assert.Equal(1, Game.CaptureGemProjectile(c, session, gem.Id, gem, (170, 100),
            Game.CaptureSkillCriticalRoll(c, gem.Id)).Payload is ProjectileOffense next
                ? next.OrdinaryDistance.HitMultiplier(70, false) : 0);
        MarkGameplayTransactions(h);
        OrdinaryUtilityTransactions(h);
        MirageGameplayTransactions(h);
    }

    static void MirageGameplayTransactions(Harness h)
    {
        var c = h.Conn;
        c.MainAscendancy = new(h.Definition.Identity, [], []); c.StatSourcesCacheState = null;
        c.Monsters.Clear(); c.PlayerLife = c.PlayerMaxLife; c.PlayerMana = c.Pools.Mana;
        foreach (var old in h.Items.Items(PlayerInventories.Weapon1).ToArray()) h.Items.Remove(PlayerInventories.Weapon1, old.Id);
        var arrow = ItemCatalog.Create(Program.Items.Find("Tornado Shot")!);
        var support = ItemCatalog.Create(Program.Items.Find("Mirage Archer Support")!);
        var bow = h.Items.Add(PlayerInventories.Weapon1, 0, 0, ItemCatalog.Create(Program.Items.Find("Crude Bow")!) with
            { Sockets = [new(SocketColour.White, arrow), new(SocketColour.White, support)], Links = [2] });
        var gem = Assert.Single(Game.GemSkillEffects(c), g => Game.ArrowsFor(c, g.Effect));
        var session = Game.ProjectilesFor("Mirage", h.Wire, h.Ciphers, c);
        double now = session.Runtime.Now / 120000d;
        c.Walk.Teleport(100, 100, now);
        var target = new SpawnedMonster("Mirage target", new(110, 100, 0), 0, 0, 100000) { Hostile = true };
        c.Monsters[99] = target;
        int action = ++c.ActionCount;
        while (Game.OrdinaryOf(c, _ => 0).Accepted(c.ProjectileEpoch, action)) action = ++c.ActionCount;
        string? signature = Game.MirageSignature(c, gem.Id);
        var supported = Game.SupportedSkill(c, gem.Id);
        Assert.True(signature is not null, $"Mirage source: requirements={supported.RequirementsMet}, gem={supported.SourceGem is not null}, arrows={Game.ArrowsFor(c, gem.Effect)}, disabled={Game.GemSkillInstanceDisabled(c, gem.Effect, gem.Id)}, supports={string.Join(',', supported.Supports.Select(s => s.Effect.Id))}");
        var receipt = new Game.MirageReceipt(c.ProjectileEpoch, action, gem.Id, signature);
        Assert.Null(Game.MirageOwnerHit(session, receipt, target));
        Assert.True(Game.PayMana(c, gem.Id, out uint paid, out _));
        Game.OrdinaryAcceptedUse(c, action);
        Game.MirageBody body;
        using (Game.ProjectileMutation(c, advance: false))
        {
            var released = Game.PaidMirageRelease(c, action, gem.Id,
                Game.CaptureGemProjectile(c, session, gem.Id, gem, (110, 100), Game.CaptureSkillCriticalRoll(c, gem.Id)));
            Assert.NotNull(Assert.IsType<ProjectileOffense>(released.Payload).MirageRelease);
            body = Assert.IsType<Game.MirageBody>(Game.MirageOwnerHit(session, receipt, target));
            Assert.Null(Game.MirageOwnerHit(session, receipt, target));
        }
        void Flush()
        {
            var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            session.Enqueue(() => { sent.SetResult(); return Task.CompletedTask; });
            sent.Task.GetAwaiter().GetResult();
        }
        Flush(); Assert.True(body.Created);
        Assert.Single(c.Minions.All, m => m.Kind == "MirageArcherProxy");
        Assert.False(body.Monster.Hostile); Assert.Equal(Program.PlayerId, body.Monster.OwnerId);
        uint mana = c.PlayerMana, life = target.Life;
        double lastOwnerHit = c.LastEnemyHitAt;
        using (Game.ProjectileMutation(c, advance: false)) Game.MirageStep(session, now);
        session.AdvanceTo(ProjectileRuntime.Time(now + 2)); Flush();
        Assert.True(target.Life < life); Assert.Equal(mana, c.PlayerMana);
        Assert.Equal(lastOwnerHit, c.LastEnemyHitAt);
        Assert.Equal(double.NegativeInfinity, target.PlayerHitAt);
        Assert.Single(c.Minions.All, m => m.Kind == "MirageArcherProxy");
        h.Items.Remove(PlayerInventories.Weapon1, bow.Id);
        using (Game.ProjectileMutation(c, advance: false)) Game.MirageStep(session, now + 2);
        Flush(); Assert.DoesNotContain(c.Minions.All, m => m.Kind == "MirageArcherProxy");
        Assert.False(Game.MirageCurrent(session, body));
        Assert.Null(Game.MirageOwnerHit(session, receipt, target));
        Assert.True(paid > 0);
        var mirrored = new SpawnedMonster("Mirrored", new(110, 100, 0), 0, 0, 100000)
        { Mods = new([], [], new(new Dictionary<string, int> { ["reflect_non_damaging_ailments_%_chance"] = 100 })) };
        var cold = DamagePacket.Of(DamageType.Cold, 100000, 100000);
        var stats = Game.CharacterPlayerStats(c);
        Game.AilmentsOnHitLocked(c, mirrored, cold, false, true, 150, stats, null, now + 2,
            new Random(1), reflectToPlayer: false);
        Assert.True(mirrored.Ailments.Magnitude(MonsterElementalAilments.Kind.Freeze, now + 2) > 0);
        Assert.Equal(1, c.ElementalAilments.ActionFactor(now + 2));
        var ownerTarget = new SpawnedMonster("Mirrored owner control", new(110, 100, 0), 0, 0, 100000)
            { Mods = mirrored.Mods };
        Game.AilmentsOnHitLocked(c, ownerTarget, cold, false, true, 150, stats, null, now + 2, new Random(1));
        Assert.True(c.ElementalAilments.FreezeUntil(now + 2) > now + 2);
    }

    static void OrdinaryAdapterTransactions(Harness h)
    {
        var c = h.Conn;
        Assert.Single(c.OrdinaryGaleBuffs);
        double deadline = Assert.Single(c.OrdinaryGaleDeadlines);
        // A replacement assembly restores neutral clocks, while host presentation retains native instance ids.
        var table = typeof(Game).GetField("OrdinarySessions", System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
        table.GetType().GetMethod("Remove", [typeof(ClientState)])!.Invoke(table, [c]);
        Assert.Empty(Game.OrdinaryPresentation(c));
        Assert.Equal(deadline, Assert.Single(c.OrdinaryGaleDeadlines));
        Assert.Single(c.OrdinaryGaleBuffs);

        var peer = new ClientState(c.Layout, 2, c.Seed)
        { Instance = new(c.Instance!.Presence), PlayerLife = 1000, PlayerMana = 1000,
            LeagueModule = c.LeagueModule };
        peer.MainAscendancy = new(h.Definition.Identity, [], []);
        peer.PlayerSent = true; c.PlayerSent = true;
        peer.ProjectileRuntimeState = h.Time;
        peer.Instance!.PublishPlayer(new(c.PeerId, 0, null!, 100, 100, c.PlayerLife, c));
        Assert.Equal(8, Game.OrdinaryTailwindSpeed(peer)); // Owner Gale does not scale recipients.
        Assert.NotEmpty(Game.OrdinaryPresentation(peer));
        Assert.Equal(c.PeerId, peer.OrdinaryTailwindSource);
        peer.Instance.PublishPlayer(new(c.PeerId, 0, null!, 161, 100, c.PlayerLife, c));
        Assert.Equal(0, Game.OrdinaryTailwindSpeed(peer));
        Assert.NotEmpty(Game.OrdinaryPresentation(peer));
        peer.Instance.PublishPlayer(new(c.PeerId, 0, null!, 100, 100, c.PlayerLife, c));
        c.LeavingArea = true;
        Assert.Equal(0, Game.OrdinaryTailwindSpeed(peer)); c.LeavingArea = false;
        peer.Instance.RemovePlayer(c.PeerId);
        c.PlayerSent = false;

        foreach (bool ruthless in new[] { false, true })
        {
            c.LeagueModule = new(new League("ordinary", "Standard", false, false)
                { DataSet = ruthless ? "HardMode" : null }, new HashSet<string>(["Game"]));
            c.MainAscendancy = new(h.Definition.Identity, [44482], []); c.StatSourcesCacheState = null;
            var stats = Game.CharacterPlayerStats(c);
            int Get(string name) => stats.GetValueOrDefault(Key(name));
            int period = Get("gain_a_frenzy_charge_every_X_seconds_while_moving");
            Assert.True(period > 0);
            Game.ChargeSessions.Remove(c);
            var charges = Game.ChargesOf(c); charges.Die(); charges.Observe(Get);
            double start = ruthless ? 100 : 10;
            charges.Step(start, Get, () => Game.WeaponProfileFor(c), true, true, false, new Random(1));
            charges.Step(start + period * .75, Get, () => Game.WeaponProfileFor(c), true, true, false, new Random(1));
            Assert.Equal(0, charges.State.Held(ChargeType.Frenzy));
            Game.ChargeSessions.Remove(c);
            var replacement = Game.ChargesOf(c); replacement.Observe(Get);
            replacement.Step(start + period, Get, () => Game.WeaponProfileFor(c), true, true, false, new Random(1));
            Assert.Equal(1, replacement.State.Held(ChargeType.Frenzy));
            replacement.Die();
        }
        c.LeagueModule = new(new League("Standard", "Standard", false, false), new HashSet<string>(["Game"]));
        c.MainAscendancy = new(h.Definition.Identity, [24848, 23169, 45313, 44482], []); c.StatSourcesCacheState = null;
        Game.OrdinaryAcceptedUse(c, 12);
        var session = Game.ProjectilesFor("ordinary adapters", h.Wire, h.Ciphers, c);
        c.PlayerStats[Key("projectile_chain_from_terrain_chance_%")] = 100;
        c.PlayerStats[Key("no_barrage_projectile_spread")] = 1;
        c.PlayerStats[Key("projectiles_barrage")] = 1;
        var gem = h.Fireball;
        var release = Game.CaptureGemProjectile(c, session, gem.Id, gem, (170, 100),
            Game.CaptureSkillCriticalRoll(c, gem.Id));
        Assert.Equal(-1, release.Profile.NativeVolley!.FixedDegrees);
        var frozen = Assert.IsType<ProjectileOffense>(release.Payload);
        c.PlayerStats.Remove(Key("projectile_chain_from_terrain_chance_%"));
        c.PlayerStats.Remove(Key("no_barrage_projectile_spread"));
        c.PlayerStats.Remove(Key("projectiles_barrage"));
        var world = new OrdinaryTerrainWorld(session);
        var runtime = new ProjectileRuntime(world);
        runtime.Release(release with { Origin = new(0, 0), Aim = new(10, 0),
            Profile = new("ordinary terrain regression") { Speed = 100, Range = 20,
                Radius = .1, Chain = 1, Lifetime = ProjectileRuntime.Time(1) } });
        runtime.AdvanceTo(ProjectileRuntime.Time(.3));
        Assert.Equal(1, world.Redirects); Assert.Equal(1, world.Hits);
        Assert.Equal(100, frozen.OrdinaryResidual.TerrainChainChance);
    }

    sealed class OrdinaryTerrainWorld(Game.ProjectileSession production) : IProjectileWorld
    {
        public int Redirects, Hits;
        public IReadOnlyList<ProjectileActor> Actors(long from, long until) =>
            [new(new(42), 2, _ => new(0, 5), .1)];
        public TerrainContact? TraceTerrain(ProjectileVector from, ProjectileVector to) =>
            from.X < 5 && to.X >= 5 ? new((5 - from.X) / (to.X - from.X), new(-1, 0)) : null;
        public bool ChainFromTerrain(ProjectileEntity p) => production.ChainFromTerrain(p);
        public ProjectileHitResult Hit(ProjectileDamage damage) { Hits++; return ProjectileHitResult.Hit; }
        public void Publish(ProjectileEvent value) { if (value.Kind == ProjectileEventKind.Redirect) Redirects++; }
    }

    static void OrdinaryUtilityTransactions(Harness h)
    {
        var c = h.Conn;
        c.MainAscendancy = new(h.Definition.Identity, [24848], []); c.StatSourcesCacheState = null;
        foreach (var old in h.Items.Items(PlayerInventories.Weapon1).ToArray()) h.Items.Remove(PlayerInventories.Weapon1, old.Id);
        var auraGem = ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SkillGemClarity")!);
        h.Items.Add(PlayerInventories.Weapon1, 0, 0, ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!) with
            { Sockets = [new(SocketColour.White, auraGem)], Links = [1] });
        var skill = Game.GemSkillEffects(c).Single(g => Game.SkillsFor(c).EffectId(g.Effect) == "Clarity");
        c.Pools = c.Pools with { Mana = 1000 }; c.PlayerMana = 1000;
        var reservation = Game.SupportedReservation(c, skill.Id)!.Value;
        int Count() => Game.OrdinaryOf(c, n => Game.CharacterPlayerStats(c).GetValueOrDefault(Key(n))).Gale.Count;
        int before = Count();
        Assert.True(Game.ToggleAura("ordinary aura", h.Wire, h.Ciphers, c, skill.Id, skill.Effect,
            skill.Name, reservation, skill.Level, skill.Quality).GetAwaiter().GetResult());
        Assert.Equal(before + 1, Count());
        Assert.True(Game.ToggleAura("ordinary aura", h.Wire, h.Ciphers, c, skill.Id, skill.Effect,
            skill.Name, reservation, skill.Level, skill.Quality).GetAwaiter().GetResult());
        Assert.Equal(before + 1, Count()); // Deactivation is not a skill use.
    }

    static void MarkGameplayTransactions(Harness h)
    {
        var c = h.Conn;
        var production = Game.ProjectilesFor("marks", h.Wire, h.Ciphers, c);
        var runtime = production.Runtime;
        void Advance(double seconds)
        {
            h.Time.At = Math.Max(h.Time.At, Math.Max(c.Clock.Elapsed.TotalSeconds, runtime.Now / 120000d)) + seconds;
            runtime.AdvanceTo((long)(h.Time.At * 120000));
            h.Time.At = runtime.Now / 120000d;
        }
        c.PlayerStats[Key("virtual_dexterity_for_requirements")] = 1000;
        c.PlayerStats[Key("virtual_strength_for_requirements")] = 1000;
        var target = new SpawnedMonster("mark", new(105, 100, 0), 0, 0, 1000) { Hostile = true };
        var nearby = new SpawnedMonster("nearby", new(110, 100, 0), 0, 0, 1000) { Hostile = true };
        c.Monsters[42] = target; c.Monsters[43] = nearby;
        foreach (var metadata in Game.MarkCatalog.All)
        {
            c.PlayerStats.Remove(Key(DeadeyeMarkRules.NearbyTaken));
            c.PlayerStats.Remove(Key(DeadeyeMarkRules.MarkedTaken));
            var type = Program.Items.All.Single(t => Program.Items.Gem(t) is { IsSupport: false } g
                && g.Variants.Any(v => v.Effect == metadata.Effect));
            foreach (var old in h.Items.Items(PlayerInventories.Weapon1).ToArray()) h.Items.Remove(PlayerInventories.Weapon1, old.Id);
            h.Items.Add(PlayerInventories.Weapon1, 0, 0, ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!) with
                { Sockets = [new(SocketColour.White, ItemCatalog.Create(type))], Links = [1] });
            var skill = Game.GemSkillEffects(c).Single(g => g.Effect == metadata.Effect);
            bool Cast(uint id) => Game.CastGemSkill("marks", h.Wire, h.Ciphers, c, skill.Id,
                (100, 100), (105, 100), id).GetAwaiter().GetResult();
            c.PlayerMana = 1000; uint before = c.PlayerMana;
            Assert.False(Cast(0)); Assert.Equal(before, c.PlayerMana);
            c.Monsters[44] = new("friendly", new(105, 100, 0), 0, 0, 1000) { Hostile = false }; Assert.False(Cast(44));
            c.PlayerMana = 0; Assert.False(Cast(42)); Assert.Null(Game.MarksOf(c).Pending);
            c.PlayerMana = before;
            Assert.True(Cast(42)); uint paid = c.PlayerMana; Assert.True(paid < before);
            Assert.False(Cast(42)); Assert.Equal(paid, c.PlayerMana);
            Advance(2);
            if (metadata.Id == "SnipersMark")
            {
                Assert.NotNull(Game.MarksOf(c).Pending);
                var splitProbe = new ProjectileRuntime(new OrdinaryTerrainWorld(production));
                splitProbe.Release(new(new("mark split probe") { Speed = 1, Range = 10 }, new(Program.PlayerId),
                    new(Program.PlayerId), 1, new(100, 100), new(110, 100))
                    { Payload = new ProjectileOffense(DamagePacket.Empty, false, 100, 0, 100, "mark split probe") });
                Assert.True(production.TargetSplit(Assert.Single(splitProbe.Projectiles), production.ActorKey(42, target)) > 0);
                Assert.Null(Game.MarksOf(c).Pending);
            }
            var mark = Assert.IsType<Game.MarkTarget>(Game.CurrentMark(c));
            Assert.Equal(42u, mark.Id); Assert.True(double.IsPositiveInfinity(mark.Clock.Remaining));
            Assert.NotEmpty(Game.MarkPresentation(c)); Assert.Empty(Game.MarkPresentation(c));
            c.PlayerStats[Key("curse_effect_+%")] = 50;
            foreach (uint key in metadata.BuffKeys)
            {
                string name = Program.Stats.Name(key)!;
                int own = Game.SupportedSkill(c, skill.Id).Stats.GetValueOrDefault(key);
                Assert.Equal((int)Math.Truncate(own * 1.5), Game.MarkAmount(c, mark, name));
            }
            c.PlayerStats.Remove(Key("curse_effect_+%"));
            foreach (bool ruthless in new[] { false, true })
            {
                c.LeagueModule = new(new League("marks", "Standard", false, false)
                    { DataSet = ruthless ? "HardMode" : null }, new HashSet<string>(["Game"]));
                c.MainAscendancy = new(h.Definition.Identity, [5443], []); c.StatSourcesCacheState = null;
                var focal = Game.CharacterPlayerStats(c);
                Assert.Equal(ruthless ? 1.1 : .75, Game.MarkIncoming(c, 43, nearby, focal), 8);
                Assert.Equal(ruthless ? .8 : 1, Game.MarkIncoming(c, 42, target, focal), 8);
                Assert.Equal(ruthless ? 0 : 75, focal.GetValueOrDefault(Key("mark_skills_curse_effect_+%")));
                // The actual incoming writer consumes Focal before typed resource routing and block handling.
                c.Pools = c.Pools with { Life = 1000, Armour = 0, Evasion = 0, BlockChance = 0 };
                c.PlayerLife = 1000; c.Ward.SetMaximum(0); c.EnergyShield.SetMaximum(0, false);
                c.IncomingHitRandom = new HighDraw();
                var attack = new MonsterAttack(0x102, .4, 10, DamagePacket.Of(DamageType.Physical, 100, 100));
                var incoming = Game.CommitMonsterHitLocked("marks", c, 43, nearby, attack,
                    c.Clock.Elapsed.TotalSeconds, (100, 100), (110, 100), false)!;
                Assert.Equal(ruthless ? 890u : 925u, incoming.Life);
                c.MainAscendancy = new(h.Definition.Identity, [], []); c.StatSourcesCacheState = null;
                Assert.Equal(1, Game.MarkIncoming(c, 43, nearby, Game.CharacterPlayerStats(c)));
                Assert.Same(mark, Game.CurrentMark(c)); // Focal refund does not remove an independently paid mark.
            }
            c.LeagueModule = new(new League("Standard", "Standard", false, false), new HashSet<string>(["Game"]));
            c.StatSourcesCacheState = null;
            c.PlayerStats[Key(DeadeyeMarkRules.NearbyTaken)] = -25;
            c.PlayerStats[Key(DeadeyeMarkRules.MarkedTaken)] = -10;
            var stats = Game.CharacterPlayerStats(c);
            Assert.Equal(.75, Game.MarkIncoming(c, 43, nearby, stats), 8);
            Assert.Equal(1, Game.MarkIncoming(c, 42, target, stats), 8); // Standard excludes marked attacker.
            nearby.Walk.Teleport(200, 100, h.Time.At);
            Assert.Equal(1, Game.MarkIncoming(c, 43, nearby, stats));
            nearby.Walk.Teleport(110, 100, h.Time.At);
            c.PlayerStats[Key("your_marks_transfer_to_nearby_enemies_on_death_%_chance")] = 100;
            var clock = new OrdinaryEffectClock(10, h.Time.At);
            Game.MarksOf(c).Active = mark with { Clock = clock };
            Advance(2);
            uint afterDeathMana = (uint)Math.Min(Game.UnreservedMana(c), (long)paid
                + Math.Max(0, Game.MarkAmount(c, mark, "mana_granted_when_killed")));
            Assert.Equal(0u, Game.ApplyMonsterHitLocked(c, target, 2000)!.Value.Life);
            var transferred = Assert.IsType<Game.MarkTarget>(Game.CurrentMark(c));
            Assert.Equal(43u, transferred.Id); Assert.Same(clock, transferred.Clock);
            Assert.InRange(clock.Remaining, 8 - 1d / ProjectileRuntime.QuantaPerSecond,
                8 + 1d / ProjectileRuntime.QuantaPerSecond);
            Assert.Equal(afterDeathMana, c.PlayerMana);
            Assert.Null(Game.ApplyMonsterHitLocked(c, target, 2000)); Assert.Same(transferred, Game.CurrentMark(c));
            Advance(9); Assert.Null(Game.CurrentMark(c)); Assert.NotEmpty(Game.MarkPresentation(c));
            target = new("mark next", new(105, 100, 0), 0, 0, 1000) { Hostile = true }; c.Monsters[42] = target;
            Assert.True(Cast(42)); Advance(2); Assert.NotNull(Game.CurrentMark(c));
            // Same numeric id with a different object cannot inherit the old mark or its native removal.
            c.Monsters[42] = new("reused", new(105, 100, 0), 0, 0, 1000) { Hostile = true };
            Assert.Null(Game.CurrentMark(c)); Game.MarkPresentation(c); c.Monsters[42] = target;
            Assert.True(Cast(42)); Advance(2); Assert.NotNull(Game.CurrentMark(c));
            h.Items.Remove(PlayerInventories.Weapon1, h.Items.Items(PlayerInventories.Weapon1).Single().Id);
            Assert.Null(Game.CurrentMark(c)); Game.MarkPresentation(c);
            Assert.Equal(afterDeathMana - (before - paid) * 2, c.PlayerMana);
        }
    }

    internal static void OrdinaryGameplayTransactions()
    {
        var definition = Assert.Single(new AscendancyCatalog().Definitions,
            d => d.Identity.Family == AscendancyFamily.Normal && d.Name == "Slayer");
        using var h = new Harness(definition);
        var c = h.Conn;
        c.MainAscendancy = new(definition.Identity, [], []);
        // Ordinary admission does not depend on the optional Phrecia provider.
        c.LeagueModule = new(new League("Standard", "Standard", false, false), new HashSet<string>(["Game"]));
        c.StatSourcesCacheState = null;
        void Set(string name, int value) => c.PlayerStats[Key(name)] = value;
        foreach (bool ruthless in new[] { false, true })
        {
            c.LeagueModule = new(new League("ordinary", "Standard", false, false)
                { DataSet = ruthless ? "HardMode" : null }, new HashSet<string>(["Game"]));
            c.MainAscendancy = new(definition.Identity, [16306], []); c.StatSourcesCacheState = null;
            Set("virtual_maximum_frenzy_charges", 0);
            _ = Game.CharacterPlayerStats(c);
            Assert.Equal(0, Game.ChargesOf(c).Limits(ChargeType.Endurance).Maximum);
            Set("virtual_maximum_frenzy_charges", 7);
            _ = Game.CharacterPlayerStats(c);
            Assert.Equal(7, Game.ChargesOf(c).Limits(ChargeType.Frenzy).Maximum);
            Assert.Equal(7, Game.ChargesOf(c).Limits(ChargeType.Endurance).Maximum);
            Set("virtual_maximum_endurance_charges", 2);
            _ = Game.CharacterPlayerStats(c);
            Assert.Equal(2, Game.ChargesOf(c).Limits(ChargeType.Endurance).Maximum);
            c.PlayerStats.Remove(Key("virtual_maximum_endurance_charges"));
            c.PlayerStats.Remove(Key("virtual_maximum_frenzy_charges"));
        }
        c.LeagueModule = new(new League("Standard", "Standard", false, false), new HashSet<string>(["Game"]));
        c.MainAscendancy = new(definition.Identity, [], []); c.StatSourcesCacheState = null;
        Set(SlayerRules.KillMovementSpeed, 20); Set(SlayerRules.RareKillAttackSpeed, 20);
        Set(SlayerRules.RecentKillDamage, 10); Set(SlayerRules.UniqueDamage, 20);
        Set(SlayerRules.OverkillLeech, 20); Set("life_leech_does_not_stop_at_full_life", 1);
        var player = Game.CharacterPlayerStats(c);
        var target = new SpawnedMonster("ordinary lethal", new(100, 100, 0), 0, 0, 100)
            { Hostile = true, Rarity = MonsterRarity.Rare };
        c.Monsters[42] = target;
        var sources = new LeechSources(DamagePacket.Empty, DamagePacket.Empty, DamagePacket.Empty);
        Assert.Equal(0, Game.LeechOf(c).Life.Count);
        Game.LeechOnHitLocked(c, target, DamagePacket.Of(DamageType.Physical, 180, 180), 180, sources);
        Assert.Equal(1, Game.LeechOf(c).Life.Count);
        Assert.Equal(0u, Game.ApplyMonsterHitLocked(c, target, 180, player: player)!.Value.Life);
        Assert.Null(Game.ApplyMonsterHitLocked(c, target, 180, player: player));
        Assert.Equal(1, c.OrdinaryDeathSequence);
        Assert.Equal(1.1, Game.OrdinaryTargetMore(c, target, player), 8);
        var current = Game.CharacterPlayerStats(c);
        Assert.True(current[Key("attack_speed_+%")] >= 20);
        Assert.NotNull(c.OrdinaryResidualPayload);
        var phase = new SpawnedMonster("phase", new(100, 100, 0), 0, 0, 100) { StageRunning = true };
        Game.LeechOnHitLocked(c, phase, DamagePacket.Of(DamageType.Physical, 180, 180), 180, sources);
        Assert.Equal(1, Game.LeechOf(c).Life.Count);
        Assert.True(Game.ApplyMonsterHitLocked(c, phase, 180, player: player)!.Value.Life > 0);
        Assert.Equal(1, c.OrdinaryDeathSequence);
        Set(SlayerRules.KillMovementSpeed, 0); Set(SlayerRules.RareKillAttackSpeed, 0);
        _ = Game.CharacterPlayerStats(c);
        Set(SlayerRules.KillMovementSpeed, 20); Set(SlayerRules.RareKillAttackSpeed, 20);
        current = Game.CharacterPlayerStats(c);
        Assert.Equal(0, current.GetValueOrDefault(Key("attack_speed_+%")));
        // Accepted travel freezes remaining time and rebases the destination epoch; relog has no ticket grant.

        var nextVictim = new SpawnedMonster("carry", new(100, 100, 0), 0, 0, 100) { Rarity = MonsterRarity.Rare };
        Assert.Equal(0u, Game.ApplyMonsterHitLocked(c, nextVictim, 180, player: Game.CharacterPlayerStats(c))!.Value.Life);
        Game.CarryOrdinary(c, 987);
        var destination = h.NewConnection(c.Character!, ticket: 987, optional: false);
        destination.MainAscendancy = c.MainAscendancy;
        destination.ProjectileRuntimeState = new ManualRuntime { At = 100 };
        foreach (var (key, value) in c.PlayerStats) destination.PlayerStats[key] = value;
        Game.RestoreOrdinary(destination);
        int Get(string name) => destination.PlayerStats.GetValueOrDefault(Key(name));
        double arrived = Game.ProjectileWorldTime(destination);
        var carried = Game.OrdinaryOf(destination, Get).Slayer.Read(arrived, Get);
        Assert.Equal(2, carried.RecentKills); Assert.Equal(20, carried.AttackSpeedIncrease);
        Assert.Equal(0, Game.OrdinaryOf(destination, Get).Slayer.Read(arrived + 20, Get).AttackSpeedIncrease);
        Assert.False(OrdinaryResidualTransfers.Tickets.ContainsKey(987));
        Game.ClearOrdinary(c);
        Assert.Equal(1, Game.OrdinaryTargetMore(c, target, player));
        // Ordinary marks remain usable without Deadeye and still use the common requirements/payment backstop.
        var markGem = ItemCatalog.Create(Program.Items.All.Single(t => Program.Items.Gem(t) is { IsSupport: false } g
            && g.Variants.Any(v => v.Effect == Game.MarkCatalog.All.Single(m => m.Id == "AssassinsMark").Effect)));
        foreach (var old in h.Items.Items(PlayerInventories.Weapon1).ToArray()) h.Items.Remove(PlayerInventories.Weapon1, old.Id);
        h.Items.Add(PlayerInventories.Weapon1, 0, 0, ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!) with
            { Sockets = [new(SocketColour.White, markGem)], Links = [1] });
        var markSkill = Game.GemSkillEffects(c).Single(g => Game.SkillsFor(c).EffectId(g.Effect) == "AssassinsMark");
        c.PlayerStats[Key("virtual_dexterity_for_requirements")] = 1000;
        c.Monsters[45] = new("ordinary mark", new(105, 100, 0), 0, 0, 1000) { Hostile = true };
        bool Cast() => Game.CastGemSkill("ordinary marks", h.Wire, h.Ciphers, c, markSkill.Id,
            (100, 100), (105, 100), 45).GetAwaiter().GetResult();
        c.PlayerStats[Key("your_spells_are_disabled")] = 1;
        uint mana = c.PlayerMana; Assert.False(Cast()); Assert.Equal(mana, c.PlayerMana);
        c.PlayerStats.Remove(Key("your_spells_are_disabled"));
        Assert.True(Cast()); Assert.True(c.PlayerMana < mana);
        var pendingMark = Assert.IsType<Game.PendingMark>(Game.MarksOf(c).Pending);
        var runtime = Game.ProjectilesFor("ordinary marks", h.Wire, h.Ciphers, c).Runtime;
        runtime.AdvanceTo((long)((pendingMark.At + 1) * 120000));
        Assert.NotNull(Game.CurrentMark(c)); Assert.NotEmpty(Game.MarkPresentation(c));
        Game.ClearOrdinary(c); // Replacement/retirement loses Game-local identities, retaining only host cleanup receipts.
        Assert.Null(Game.CurrentMark(c)); Assert.NotEmpty(Game.MarkPresentation(c)); Assert.Empty(Game.MarkPresentation(c));
        Assert.True(Cast()); c.PlayerLife = 0;
        Assert.Null(Game.CurrentMark(c)); Assert.Null(Game.MarksOf(c).Pending);
    }
}
