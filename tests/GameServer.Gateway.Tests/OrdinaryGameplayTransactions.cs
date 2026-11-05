using GameServer.Core.World;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.Protocol.Login;
using GameServer.Core.Projectiles;
using GameServer.Modding;
using System.Net;
using System.Net.Sockets;

using League = GameServer.Core.Accounts.League;
using MonsterRarity = GameServer.Core.Protocol.Instance.MonsterRarity;

namespace GameServer.Gateway.Tests;

static class OrdinaryGameplayChecks
{
    internal static async Task TornadoNova()
    {
        using var h = new Harness(new AscendancyCatalog().Definitions.Single(d => d.Identity.Family == AscendancyFamily.Normal && d.Name == "Deadeye"));
        var c = h.Conn;
        foreach (string effect in new[] { "TornadoShot", "TornadoShotAltX", "TornadoShotAltY" })
        {
            var stats = new Dictionary<string,int> { ["base_number_of_projectiles"]=1, ["number_of_additional_projectiles"]=2,
                ["number_of_additional_arrows"]=1, ["projectiles_rain"]=1, ["tornado_shot_num_of_secondary_projectiles"]=3 };
            int Get(string n) => stats.GetValueOrDefault(n);
            var p = GatewayProjectileSkillProfiles.Resolve(effect,"Metadata/Projectiles/TornadoShotArrow",Get,100,attack:true,arrows:true);
            Assert.Equal(1,p.Count); Assert.False(p.DirectDamage); Assert.True(p.Lobbed);
            var volley = Assert.IsType<ProjectileEmissionRecipe>(p.EndEmission).Child;
            Assert.Equal(4,volley.Count); Assert.True(volley.DirectDamage); Assert.False(volley.Lobbed);
            var secondary = Assert.IsType<ProjectileEmissionRecipe>(volley.EndEmission).Child;
            Assert.Equal(3,secondary.Count); Assert.True(secondary.DirectDamage);
            var panel = stats.ToDictionary(kv => Key(kv.Key), kv => kv.Value);
            var raw = panel.ToDictionary(kv => kv.Key, kv => kv.Value);
            Game.ApplyProjectilePanelStats(Program.Skills.EffectRow(effect), panel);
            foreach (string count in new[] { "total_number_of_projectiles_to_fire", "virtual_total_number_of_projectiles",
                "total_number_of_arrows_to_fire", "virtual_total_number_of_arrows" })
                Assert.Equal(volley.Count, panel[Key(count)]);
            foreach (var input in raw) Assert.Equal(input.Value, panel[input.Key]);
            stats["projectiles_rain"]=0;
            var control = GatewayProjectileSkillProfiles.Resolve(effect,"Metadata/Projectiles/TornadoShotArrow",Get,100,attack:true,arrows:true);
            Assert.Equal(4,control.Count); Assert.True(control.DirectDamage); Assert.Equal(3,control.EndEmission!.Child.Count);
            Assert.True(control.EndEmission.Child.DirectDamage);
            var session = new Game.ProjectileSession("Tornado Nova",h.Wire,h.Ciphers,c); c.ProjectileRuntimeState=session;
            try
            {
                c.Walk.Teleport(98,100,0);
                var offense = new ProjectileOffense(DamagePacket.Of(DamageType.Physical,100,100),true,0,0,150,"frozen paid stage")
                {
                    CriticalRoll=new(0), Presentation=new() { SpawnAtCaster=true,
                        EventEffect=e=>e.Projectile.ParentId==0&&e.Kind==ProjectileEventKind.Spawn
                            ? "Metadata/Effects/Spells/supports/arrow_rain/rain_rig.ao" : null }
                };
                // Keep both emissions away from the fixture's world boundary; count, damage and offense stay resolved.
                volley = volley with { Range=4 };
                var flight = p with { Range=1, EndEmission=p.EndEmission! with { Child=volley } };
                uint effectId=c.NextObjectId;
                session.Runtime.Release(new(flight,new(Program.PlayerId,c.ProjectileEpoch),new(Program.PlayerId,c.ProjectileEpoch),1,
                    new(100,100),new(101,100)) { Payload=offense });
                var flush=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                session.Enqueue(()=> { flush.SetResult(); return Task.CompletedTask; }); await flush.Task;
                var expectedEffect=InstanceEntities.CreateObject(effectId,InstanceEntities.ObjectTypeHash(EffectObject.TypePath),
                    new EffectObject(98,100,"Metadata/Effects/Spells/supports/arrow_rain/rain_rig.ao").Blob());
                string prefix=Convert.ToHexString(expectedEffect.AsSpan(0,PacketHistory.KeptBytes));
                Assert.Contains(Program.HistoryFor(h.Ciphers).Lines(),line=>line.Contains(prefix,StringComparison.Ordinal));
                long landing=ProjectileRuntime.Time(.01); session.AdvanceTo(landing);
                long volleyAt=landing+p.EndEmission!.Delay;
                Assert.True(volleyAt>landing);
                session.AdvanceTo(volleyAt-1); Assert.Empty(session.Runtime.Projectiles);
                session.AdvanceTo(volleyAt);
                var arrows=session.Runtime.Projectiles.ToArray(); Assert.Equal(4,arrows.Length);
                Assert.All(arrows,a=> { Assert.Same(offense,a.Use.Payload); Assert.Equal(volley,a.Profile); });
                long terminal=volleyAt+(long)Math.Ceiling(volley.Range/volley.Speed*ProjectileRuntime.QuantaPerSecond);
                session.AdvanceTo(terminal);
                var children=session.Runtime.Projectiles.ToArray(); Assert.Equal(12,children.Length);
                Assert.All(children,a=> { Assert.Same(offense,a.Use.Payload); Assert.True(a.Profile.DirectDamage); });
                var shot=children[0]; var point=shot.Position+shot.Direction*2;
                var target=new SpawnedMonster("secondary contact",new((int)Math.Round(point.X),(int)Math.Round(point.Y),0),0,0,100000);
                c.Monsters[909]=target;
                session.AdvanceTo(terminal+ProjectileRuntime.Time(.04));
                Assert.True(target.Life<target.MaxLife);
                c.Monsters.Clear();
            }
            finally { session.Invalidate(); await session.OutputCompleted; }
        }
    }
    static uint Key(string name) => Program.Stats.Key(name)!.Value;
    internal static void DeadeyeGameplayTransactions()
    {
        var definition = Assert.Single(new AscendancyCatalog().Definitions,
            d => d.Identity.Family == AscendancyFamily.Normal && d.Name == "Deadeye");
        using var h = new Harness(definition); var c = h.Conn;
        c.MainAscendancy = new(definition.Identity, [24848, 23169, 45313, 44482], []);
        c.LeagueModule = new(new League("Standard", "Standard", false, false), new HashSet<string>(["Game"]));
        c.StatSourcesCacheState = null;
        Assert.Equal(8, Game.OrdinaryTailwindSpeed(c));
        for (int i = 1; i <= 10; i++) Game.OrdinaryAcceptedUse(c, ++c.ActionCount);
        Game.OrdinaryAcceptedUse(c, c.ActionCount); // Same admitted action cannot grant twice.
        Assert.Equal(16, Game.OrdinaryTailwindSpeed(c));
        var player = Game.CharacterPlayerStats(c);
        c.Pools = c.Pools with { Life = 1000, Armour = 0, Evasion = 0, BlockChance = 0 };
        c.PlayerLife = 1000; c.Ward.SetMaximum(0); c.EnergyShield.SetMaximum(0, false);
        c.IncomingHitRandom = new HighDraw();
        var mixed = new MonsterAttack(0x102, .4, 10,
            DamagePacket.Of(DamageType.Physical, 100, 100).Add(DamagePacket.Of(DamageType.Fire, 100, 100)));
        var enemy = new SpawnedMonster("mixed hit", new(110, 100, 0), 0, 0, 1000) { Hostile = true };
        var incoming = Game.CommitMonsterHitLocked("mixed Gale", c, 42, enemy, mixed,
            c.Clock.Elapsed.TotalSeconds, (100, 100), (110, 100), false)!;
        Assert.Equal(880u, incoming.Life); // Both types receive the one hit's 40% less, before Gale is cleared.
        Assert.Equal(8, Game.OrdinaryTailwindSpeed(c));
        Game.OrdinaryAcceptedUse(c, ++c.ActionCount);
        var native = Game.OrdinaryPresentation(c);
        Assert.NotEmpty(native); Assert.NotEqual((ushort)0, c.OrdinaryTailwindBuff);
        // Tailwind is not an IsRecovery row: a stat value would become an unchecked native list count.
        Assert.True(native.AsSpan().IndexOf(InstanceEntities.AddBuff(Program.PlayerId,
            c.OrdinaryTailwindBuff, 1094, Program.PlayerId)) >= 0);
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
        var nova = ItemCatalog.Create(Program.Items.Find("Arrow Nova Support")!);
        c.PlayerStats[Key("mirage_archers_do_not_attach")]=1;
        var bow = h.Items.Add(PlayerInventories.Weapon1, 0, 0, ItemCatalog.Create(Program.Items.Find("Crude Bow")!) with
            { Sockets = [new(SocketColour.White, arrow), new(SocketColour.White, support), new(SocketColour.White,nova)], Links = [3] });
        var gem = Assert.Single(Game.GemSkillEffects(c), g => Game.ArrowsFor(c, g.Effect));
        var session = Game.ProjectilesFor("Mirage", h.Wire, h.Ciphers, c);
        double now = session.Runtime.Now / 120000d;
        c.Walk.Teleport(100, 100, now);
        var target = new SpawnedMonster("Mirage target", new(110, 100, 0), 0, 0, 100000) { Hostile = true };
        c.Monsters[99] = target;
        int action = ++c.ActionCount;
        string? signature = Game.MirageSignature(c, gem.Id);
        var supported = Game.SupportedSkill(c, gem.Id);
        Assert.True(signature is not null, $"Mirage source: requirements={supported.RequirementsMet}, gem={supported.SourceGem is not null}, arrows={Game.ArrowsFor(c, gem.Effect)}, disabled={Game.GemSkillInstanceDisabled(c, gem.Effect, gem.Id)}, supports={string.Join(',', supported.Supports.Select(s => s.Effect.Id))}");
        Assert.Null(Game.PaidMirageAdmission(c, action));
        Assert.True(Game.PayMana(c, gem.Id, out uint paid, out _));
        Game.OrdinaryAcceptedUse(c, action);
        var admission = Assert.IsType<DeadeyeMirageLifecycle.Release>(Game.PaidMirageAdmission(c, action));
        // A paid windup and its later hit survive newer accepted actions without retaining area-wide history.
        Game.OrdinaryAcceptedUse(c, ++c.ActionCount);
        Assert.False(Game.OrdinaryOf(c, _ => 0).Accepted(c.ProjectileEpoch, action));
        Game.MirageReceipt receipt;
        Game.MirageBody body;
        using (Game.ProjectileMutation(c, advance: false))
        {
            var released = Game.PaidMirageRelease(c, admission, gem.Id,
                Game.CaptureGemProjectile(c, session, gem.Id, gem, (110, 100), Game.CaptureSkillCriticalRoll(c, gem.Id)));
            receipt = Assert.IsType<Game.MirageReceipt>(Assert.IsType<ProjectileOffense>(released.Payload).MirageRelease);
            Game.OrdinaryAcceptedUse(c, ++c.ActionCount);
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
        Assert.True(body.Actor.Detached);
        c.Walk.Teleport(160,100,now);
        var detached=Game.CaptureMirage(session,body,(110,100));
        Assert.Equal(body.Actor.Position,detached.Origin);
        Assert.Equal(10,detached.Profile.Range,8);
        Assert.Equal(.415,detached.Profile.Range/detached.Profile.Speed,8);
        detached=Game.CaptureMirage(session,body,(110,100),releaseDelay:.2);
        Assert.Equal(.215,detached.Profile.Range/detached.Profile.Speed,8);
        uint effectId=c.NextObjectId;
        session.Runtime.Release(detached); Flush();
        var expectedEffect=InstanceEntities.CreateObject(effectId,InstanceEntities.ObjectTypeHash(EffectObject.TypePath),
            new EffectObject(100,100,"Metadata/Effects/Spells/supports/arrow_rain/rain_rig.ao")
                { Flags0=4,AnimationWord=22,AnimationValue=-172,AnimatedFlags=9,AnimationId=6 }.Blob());
        string prefix=Convert.ToHexString(expectedEffect.AsSpan(0,PacketHistory.KeptBytes));
        Assert.Contains(Program.HistoryFor(h.Ciphers).Lines(),line=>line.Contains(prefix,StringComparison.Ordinal));
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
        Game.OrdinaryAcceptedUse(c, ++c.ActionCount);
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
        foreach (var old in h.Items.Items(PlayerInventories.Weapon1).ToArray()) h.Items.Remove(PlayerInventories.Weapon1, old.Id);
        var blood = ItemCatalog.Create(Program.Items.Find("Petrified Blood")!);
        h.Items.Add(PlayerInventories.Weapon1, 0, 0, ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!) with
            { Sockets = [new(SocketColour.White, blood)], Links = [1] });
        c.PlayerStats[Key("virtual_strength_for_requirements")] = 1000;
        c.PlayerStats[Key("virtual_dexterity_for_requirements")] = 1000;
        var pb = Game.GemSkillEffects(c).Single(g => Game.SkillsFor(c).EffectId(g.Effect) == PetrifiedBlood.EffectId);
        var pbReservation = Game.SupportedReservation(c, pb.Id)!.Value;
        int beforeBlood = Count();
        bool Toggle((int Flat, int Percent) cost) => Game.TogglePetrifiedBlood("ordinary blood", h.Wire, h.Ciphers,
            c, pb.Id, pb.Effect, pb.Name, cost, pb.Level, pb.Quality).GetAwaiter().GetResult();
        Assert.True(Toggle(pbReservation));
        Assert.True(c.PetrifiedBlood.Active);
        Assert.Equal(beforeBlood + 1, Count());
        Assert.True(Toggle(pbReservation));
        Assert.False(c.PetrifiedBlood.Active);
        Assert.Equal(beforeBlood + 1, Count());
        c.Auras[skill.Id] = new(skill.Id, skill.Effect, "Clarity", skill.Name, 500, AuraCatalog.BuffRow("Clarity"),
            (int)c.Pools.Mana, 0);
        Assert.False(Toggle(pbReservation));
        c.Auras.Remove(skill.Id);
        Assert.Equal(beforeBlood + 1, Count());
        CharacterSaves.SetPetrifiedBlood(c.Character!, true);
        Game.RestoreRememberedPetrifiedBlood(c);
        Assert.True(c.PetrifiedBlood.Active);
        Assert.Equal(beforeBlood + 1, Count()); // Restored intent is not a new manual use.
        c.PetrifiedBlood.Suspend();
        CharacterSaves.SetPetrifiedBlood(c.Character!, false);
    }

    static void MarkGameplayTransactions(Harness h)
    {
        var c = h.Conn;
        c.ProjectileRuntime?.Invalidate();
        // Explicit advances own this fixture's world time; no timer can finish a cast between adjacent assertions.
        var production = new Game.ProjectileSession("marks", h.Wire, h.Ciphers, c);
        c.ProjectileRuntime = production;
        var runtime = production.Runtime;
        runtime.AdvanceTo(ProjectileRuntime.Time(Math.Max(h.Time.At, c.Clock.Elapsed.TotalSeconds) + 60));
        h.Time.At = runtime.Now / (double)ProjectileRuntime.QuantaPerSecond;
        void Advance(double seconds)
        {
            h.Time.At = Math.Max(h.Time.At, Math.Max(c.Clock.Elapsed.TotalSeconds, runtime.Now / 120000d)) + seconds;
            runtime.AdvanceTo((long)(h.Time.At * 120000));
            h.Time.At = runtime.Now / 120000d;
        }
        c.PlayerStats[Key("virtual_dexterity_for_requirements")] = 1000;
        c.PlayerStats[Key("virtual_strength_for_requirements")] = 1000;
        var target = new SpawnedMonster("mark", new(105, 100, 0), 0, 0, 1000) { Hostile = true,
            Mods = new([], [], new(new Dictionary<string, int> { ["projectile_damage_taken_+%"] = 11 })) };
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
            var shown = Game.MarkPresentation(c);
            Assert.True(shown.AsSpan().IndexOf(InstanceEntities.AddBuff(42, c.OrdinaryMarkBuff,
                metadata.Buff, Program.PlayerId, skill.Id, f6c: (float)mark.Clock.Rate)) >= 0);
            Assert.True(shown.AsSpan().IndexOf(InstanceEntities.StatsUpdate(42, metadata.BuffKeys.Select(k =>
                (k, target.Mods.Effects[Program.Stats.Name(k)!] + Game.MarkAmount(c, mark, Program.Stats.Name(k)!))).ToArray())) >= 0);
            Assert.Empty(Game.MarkPresentation(c));
            c.PlayerStats[Key("curse_effect_+%")] = 50;
            foreach (uint key in metadata.BuffKeys)
            {
                string name = Program.Stats.Name(key)!;
                int own = Game.SupportedSkill(c, skill.Id).Stats.GetValueOrDefault(key);
                Assert.Equal((int)Math.Truncate(own * 1.5), Game.MarkAmount(c, mark, name));
            }
            var revalued = Game.MarkPresentation(c);
            Assert.True(revalued.AsSpan().IndexOf(InstanceEntities.StatsUpdate(42, metadata.BuffKeys
                .Select(k => (k, target.Mods.Effects[Program.Stats.Name(k)!])).ToArray())) >= 0);
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

    internal static void SlayerGameplayTransactions()
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
            Set("gain_X_endurance_charges_per_second", -1);
            _ = Game.CharacterPlayerStats(c);
            var charges = Game.ChargesOf(c);
            charges.Die();
            charges.Step(0, _ => 0, () => Game.WeaponProfileFor(c), true, false, false, new Random(1));
            charges.Enqueue(new ChargeEvent.Grant(ChargeType.Endurance, 7, "Masterful gain"));
            charges.Step(.1, _ => 0, () => Game.WeaponProfileFor(c), true, false, false, new Random(1));
            Assert.Equal(7, charges.State.Held(ChargeType.Endurance));
            Assert.Equal(7, charges.Limits(ChargeType.Endurance).Maximum);
            charges.Step(1.1, _ => 0, () => Game.WeaponProfileFor(c), true, false, false, new Random(1));
            Assert.Equal(6, charges.State.Held(ChargeType.Endurance));
            Assert.Equal(7, charges.Limits(ChargeType.Endurance).Maximum);
            Set("virtual_maximum_endurance_charges", 2);
            _ = Game.CharacterPlayerStats(c);
            Assert.Equal(2, Game.ChargesOf(c).Limits(ChargeType.Endurance).Maximum);
            charges.Enqueue(new ChargeEvent.Grant(ChargeType.Endurance, 10, "Explicit maximum gain"));
            charges.Step(1.2, _ => 0, () => Game.WeaponProfileFor(c), true, false, true, new Random(1));
            Assert.Equal(2, charges.State.Held(ChargeType.Endurance));
            Assert.Equal(2, charges.Limits(ChargeType.Endurance).Maximum);
            typeof(Game).GetMethod("CommitAcceptedChargeBatchLocked", System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.NonPublic)!.Invoke(null, [c,
                new PlayerMinions.AcceptedCharges[] { new(false, false, MonsterRarity.Normal, null,
                    new Dictionary<uint, int>(), false, 1.2) }, Game.CharacterPlayerStats(c)]);
            Assert.Equal(7, charges.Limits(ChargeType.Frenzy).Maximum);
            Assert.Equal(2, charges.Limits(ChargeType.Endurance).Maximum);
            Assert.Equal(2, charges.State.Held(ChargeType.Endurance));
            c.PlayerStats.Remove(Key("virtual_maximum_endurance_charges"));
            c.PlayerStats.Remove(Key("virtual_maximum_frenzy_charges"));
            c.PlayerStats.Remove(Key("gain_X_endurance_charges_per_second"));
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
        var destination = h.NewConnection(c.Character!, ticket: 987);
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

    internal static void TransferComposition()
    {
        var definition = Assert.Single(new AscendancyCatalog().Definitions,
            d => d.Identity.Family == AscendancyFamily.Normal && d.Name == "Slayer");
        using var h = new Harness(definition);
        var c = h.Conn;
        c.PlayerStats[Key(SlayerRules.RareKillAttackSpeed)] = 20;
        var victim = new SpawnedMonster("transfer kill", new(100, 100, 0), 0, 0, 100)
            { Hostile = true, Rarity = MonsterRarity.Rare };
        Assert.Equal(0u, Game.ApplyMonsterHitLocked(c, victim, 180, player: Game.CharacterPlayerStats(c))!.Value.Life);
        int resolved = 0;
        using var hook = GameEvents.TransferResolved.Add("Game", (conn, _, response) =>
        {
            Assert.Same(c, conn);
            Assert.True(response.Ok);
            Assert.False(conn.RageRetired);
            int Get(string name) => conn.PlayerStats.GetValueOrDefault(Key(name));
            Assert.Equal(1, Game.OrdinaryOf(conn, Get).Slayer.Read(Game.ProjectileWorldTime(conn), Get).RecentKills);
            resolved++;
            return Task.CompletedTask;
        });
        var transfer = new PendingTransfer(1, 0, false, AtSpawn: true);
        Assert.True(c.TryBeginAreaChange(transfer, out _));
        Game.Transfer("ordinary transfer", h.Wire, h.Ciphers, c, transfer,
            _ => Task.FromResult(new ControlResponse
                { Ok = true, Ticket = 988, Area = 1 })).GetAwaiter().GetResult();
        Assert.Equal(1, resolved);
        Assert.True(c.RageRetired);
        var arrival = h.NewConnection(c.Character!, 988);
        var arrivalWorld = new Game.ProjectileSession("ordinary arrival", h.Wire, h.Ciphers, arrival);
        arrival.ProjectileRuntime = arrivalWorld;
        arrivalWorld.AdvanceTo(ProjectileRuntime.Time(100));
        Assert.Equal(100, Game.ProjectileWorldTime(arrival));
        arrival.PlayerStats[Key(SlayerRules.RareKillAttackSpeed)] = 20;
        Game.RestoreOrdinary(arrival);
        var state = Game.OrdinaryOf(arrival, n => arrival.PlayerStats.GetValueOrDefault(Key(n)))
            .Slayer.Read(100, n => arrival.PlayerStats.GetValueOrDefault(Key(n)));
        Assert.Equal(1, state.RecentKills);
        Assert.Equal(20, state.AttackSpeedIncrease);
        Assert.False(OrdinaryResidualTransfers.Tickets.ContainsKey(988));
    }

    sealed class HighDraw : Random
    {
        public override int Next(int maxValue) => maxValue - 1;
        public override double NextDouble() => 0;
    }

    sealed class ManualRuntime : IProjectileRuntime
    {
        internal double At;
        public bool Active => true;
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
        internal Sink() : this(Pair()) { }
        Sink((Socket Writer, Socket Peer) pair) : base(pair.Writer, ownsSocket: true) => peer = pair.Peer;
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
        protected override void Dispose(bool disposing) { if (disposing) peer.Dispose(); base.Dispose(disposing); }
    }

    sealed class Harness : IDisposable
    {
        readonly string path = Path.Combine(Path.GetTempPath(), "ordinary-" + Guid.NewGuid().ToString("N") + ".db");
        readonly IGameLogic? oldLogic = Program.GameLogicOrNull;
        readonly IModuleLifetime? oldRules = Game.CombatLifetime;
        readonly ModuleLifetime rules = new("Game");
        readonly List<ClientState> connections = [];
        internal readonly AscendancyCatalog.Definition Definition;
        internal readonly Sink Wire = new();
        internal readonly ConnectionCiphers Ciphers = new(new byte[64], false);
        internal readonly ManualRuntime Time = new();
        internal readonly ClientState Conn;
        internal PlayerInventories Items => Game.ItemsOf(Conn);
        internal (ushort Id, int Effect, string Name, int Level, int Quality, uint Gem) Fireball =>
            Game.GemSkillEffects(Conn).Single(g => Game.SkillsFor(Conn).EffectId(g.Effect) == "Fireball");
        internal Harness(AscendancyCatalog.Definition definition)
        {
            Saves.Open(path); Definition = definition;
            Game.CombatLifetime = rules; Program.SetGameLogic(new GameServer.Modules.Game.GameLogic());
            Conn = NewConnection("Ordinary" + Guid.NewGuid().ToString("N"));
            Conn.ProjectileRuntimeState = Time;
            var gem = ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SkillGemFireball")!);
            Items.Add(PlayerInventories.Weapon1, 0, 0, ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!) with
                { Sockets = [new(SocketColour.White, gem)], Links = [1] });
            Conn.PlayerStats[Key("virtual_intelligence_for_requirements")] = 1000;
            Conn.PlayerStats[Key("no_inherent_rage_loss")] = 1;
        }
        internal ClientState NewConnection(string name, uint ticket = 1)
        {
            byte row = (byte)Enumerable.Range(0, 7).First(r => Program.Skills.ClassFor((byte)r).IntegerId == Definition.BaseClass);
            var c = new ClientState(new(1, "Ordinary", 300, 300, 100, 100, null, []), ticket, 1)
            {
                Character = name, ClassRow = row, PlayerLife = 1000, PlayerMana = 1000, Instance = new(),
                Experience = CharacterStats.ExperienceFor(68), Pools = new(68, 1000, 1000, 100, 0),
                LeagueModule = new(new League("OrdinaryTest", "Standard", false, false), new HashSet<string>(["Game"])),
                Keyring = new InstanceKeyring(new byte[32]), MainAscendancy = new(Definition.Identity, [], AscendancyRules.AwardKeys),
            };
            connections.Add(c); return c;
        }
        public void Dispose()
        {
            foreach (var c in connections)
            {
                c.Closed = true; c.ProjectileRuntime?.Invalidate(); c.RetirePlayerUpdates();
                c.RetireResourceEffects(); c.RetireOwnedCombat(); PlayerInventories.Forget(c.Character!);
            }
            rules.Stop(_ => { }); Wire.Dispose();
            Game.CombatLifetime = oldRules; Program.SetGameLogic(oldLogic!);
            Saves.Db.Dispose(); foreach (string suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }
    }
}
