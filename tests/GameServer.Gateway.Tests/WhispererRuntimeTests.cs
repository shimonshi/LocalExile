using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Collections.Concurrent;
using League = GameServer.Core.Accounts.League;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.Projectiles;
using GameServer.Core.Protocol.Login;
using GameServer.Core.World;
using GameServer.Modding;

public sealed class WhispererRuntimeTests
{
    static readonly ushort[] A = [17123, 4174, 1116, 22720, 27553, 50932, 15390, 26415];
    static readonly ushort[] B = [30419, 53715, 21095, 2266, 540, 863, 9982, 5888];
    static readonly ushort[] C = [17123, 4174, 1116, 22720, 24726, 23799, 9982, 5888];
    static readonly ushort[] Seam = [17123, 4174, 15390, 26415, 540, 863, 9982, 5888];
    static uint Key(string name) => Program.Stats.Key(name)!.Value;
    static (FlaskEnchantments.Trigger Trigger, uint Item, byte Slot)[] FlaskSignals(ClientState conn)
    {
        var state = typeof(Game).GetMethod("Automation", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [conn])!;
        return ((ConcurrentQueue<(FlaskEnchantments.Trigger, uint, byte)>)state.GetType().GetField("Events")!.GetValue(state)!).ToArray();
    }
    static byte[] BlindStats(uint actor, int less) => InstanceEntities.StatsUpdate(actor,
        [(973u, less > 0 ? 1 : 0), (15023u, -less), (15024u, -less)]);
    static void Has(byte[] bytes, byte[] expected) => Assert.True(bytes.AsSpan().IndexOf(expected) >= 0,
        $"Missing packet {Convert.ToHexString(expected)}");

    [Fact]
    public async Task Actual_Whisperer_policy_sources_paid_resources_and_refunds_share_the_generic_engines()
    {
        var definition = Assert.Single(new AscendancyCatalog().Definitions,
            d => d.Identity.Family == AscendancyFamily.Phrecia && d.Name == "Whisperer");
        Assert.Equal(new(AscendancyFamily.Phrecia, "Deadeye", "3.29.3b"), definition.Identity);
        Assert.Equal((byte)2, definition.BaseClass); Assert.Equal(4, definition.TableRow);
        Assert.Equal((byte)2, definition.ClassNo); Assert.Equal((ushort)56856, definition.StartNode);
        Assert.Equal(19, definition.Nodes.Count);
        var absent = Harness.Connection("absent"); absent.MainAscendancy = new(definition.Identity);
        Assert.Throws<AscendancyRuleException>(() => AscendancyPolicies.RequirePlayable(absent, definition.Identity));
        Assert.True(AscendancyPolicies.Available(absent, definition.Identity with { Family = AscendancyFamily.Normal }));
        using var h = new Harness(definition); var c = h.Conn;
        var excluded = Harness.Connection("excluded"); excluded.MainAscendancy = new(definition.Identity);
        Assert.False(AscendancyPolicies.Available(excluded, definition.Identity));
        h.OptIn(false); Assert.False(h.Policy.Supports(definition.Identity));
        Assert.Throws<AscendancyRuleException>(() => AscendancyPolicies.RequirePlayable(c, definition.Identity));
        h.OptIn(true);
        Assert.False(h.Policy.Supports(definition.Identity with { Revision = "future" }));
        Assert.False(h.Policy.Supports(definition.Identity with { Id = "Whisperer" }));
        AscendancyPolicies.RequirePlayable(c, definition.Identity); // Real policy verifies the actual packed graph.
        Assert.Equal(1, AscendancyPolicies.AreaStats(c, definition.Identity)![21146]);
        Assert.Null(AscendancyPolicies.AreaStats(absent, definition.Identity with { Family = AscendancyFamily.Normal }));
        foreach (var build in new[] { A, B, C })
        {
            h.Build(build);
            var raw = Game.CharacterStatSources(c).Stats;
            foreach (ushort node in build)
                foreach (var (key, value) in AscendancyPolicies.NodeStats(c, definition, node, definition.Nodes[node].Stats))
                    Assert.NotEqual(0, raw.GetValueOrDefault(key));
        }
        h.Build(A); var skill = h.Fireball;
        var ranger = Game.SkillsFor(c).ClassFor(c.ClassRow);
        uint ManaForFinalDex() => (uint)decimal.Floor((ranger.BaseMaxMana + 6L * 68 + c.Pools.Dexterity) * 1.24m);
        Assert.Equal(ManaForFinalDex(), c.Pools.Mana); // Actual 4174 removes Intelligence and uses final Dexterity.
        c.PlayerStats[Key("dexterity_+%")] = 50; c.Pools = Game.PoolsFor(c, 68);
        Assert.Equal(ManaForFinalDex(), c.Pools.Mana);
        c.PlayerStats.Remove(Key("dexterity_+%")); c.Pools = Game.PoolsFor(c, 68);
        Assert.True(Game.PayMana(c, skill.Id, out uint doubled)); Assert.Equal(10u, doubled);
        Assert.Equal(10, h.SkillRecordCost(skill.Id));
        Assert.Null(await Game.ChangePassives("Whisperer", h.Wire, h.Ciphers, c, [22720], false, "paid refund", refundPoints: true));
        Assert.Equal((ushort)195, c.RefundPoints);
        Assert.DoesNotContain((ushort)22720, CharacterSaves.Load(c.Character!)!.MainAscendancy!.Allocated);
        Assert.True(Game.PayMana(c, skill.Id, out uint ordinary)); Assert.Equal(5u, ordinary);
        Assert.Equal(5, h.SkillRecordCost(skill.Id));
        Assert.Null(await Game.ChangePassives("Whisperer", h.Wire, h.Ciphers, c, [22720], true, "reallocate"));
        Assert.Equal(10, h.SkillRecordCost(skill.Id));

        h.Build(Seam);
        var state = Game.ResourceEffectsOf(c); state.Mana.Spent(199, 10);
        var poolsBeforeBlock = c.Pools;
        c.Pools = c.Pools with { Evasion = 0, BlockChance = 100, LifeGainedOnBlock = 0, ManaGainedOnBlock = 0 };
        c.IncomingHitRandom = new FixedDraw(0);
        var attack = new MonsterAttack(0x102, .4, 10, DamagePacket.Of(DamageType.Physical, 100, 100));
        var enemy = new SpawnedMonster("blocked", new(105, 100, 0), 0, 0, 1000) { Attack = attack };
        uint beforeBlock = c.PlayerMana;
        double blockAt = Game.ProjectileWorldTime(c);
        var blocked = Game.CommitMonsterHitLocked("Whisperer", c, 42, enemy, attack, blockAt, (100, 100), (105, 100), false)!;
        Assert.True(blocked.Blocked);
        await Game.PresentMonsterHit("Whisperer", h.Wire, h.Ciphers, c, 42, blocked);
        Assert.Single(FlaskSignals(c), s => s.Trigger == FlaskEnchantments.Trigger.Block);
        Assert.Equal(beforeBlock, c.PlayerMana); Assert.Equal(0, state.Mana.Take(blockAt + 4));
        Assert.Equal(199, state.Mana.SpentRemainder); c.Pools = poolsBeforeBlock;

        // Main's SavageHit is based on actual Life lost, after typed defensive Mana, not the raw hit size.
        c.Pools = c.Pools with { Life = 1000, Mana = 1000, Armour = 0, Evasion = 0, BlockChance = 0 };
        c.PlayerLife = c.PlayerMana = 1000; c.Ward.SetMaximum(0); c.EnergyShield.SetMaximum(0, false);
        c.IncomingHitRandom = new HighDraw();
        var heavy = attack with { Damage = DamagePacket.Of(DamageType.Physical, 200, 200) };
        double hitAt = Game.ProjectileWorldTime(c);
        var defended = Game.CommitMonsterHitLocked("Whisperer", c, 42, enemy, heavy, hitAt, (100, 100), (105, 100), false)!;
        Assert.False(defended.Blocked); Assert.Equal(1000u, defended.Life); Assert.Equal(800u, defended.Mana);
        Assert.DoesNotContain(FlaskSignals(c), s => s.Trigger == FlaskEnchantments.Trigger.SavageHit);
        Assert.Equal(199, state.Mana.SpentRemainder);
        c.PlayerMana = 0;
        var savage = Game.CommitMonsterHitLocked("Whisperer", c, 42, enemy, heavy, hitAt, (100, 100), (105, 100), false)!;
        Assert.Equal(800u, savage.Life); Assert.Equal(0u, savage.Mana);
        Assert.Single(FlaskSignals(c), s => s.Trigger == FlaskEnchantments.Trigger.SavageHit);
        c.Pools = poolsBeforeBlock;
        state.Mana.Clear(); state.Mana.Spent(199, 10);
        c.PlayerMana = 60; c.Ward.SetMaximum(50); c.EnergyShield.SetMaximum(30, true);
        var player = Game.CharacterPlayerStats(c);
        Assert.Equal(20, Game.DamagePlayerHitTyped(c, 100, 10, 10, 10, player));
        Assert.Equal(40u, c.PlayerMana); Assert.Equal(0u, c.EnergyShield.Current);
        Assert.Equal(199, state.Mana.SpentRemainder); // Defensive Mana is not a cost.
        Assert.Equal(14, state.Mana.Take(14)); // Post-Ward 70 includes the ES/Mana portions.
        state.Mana.Clear(); state.Mana.Spent(199, 10);
        state.Mana.Hit(20, 1, 20); Assert.Equal(0, state.Mana.Take(24));
        c.PlayerMana = c.Pools.Mana; Game.SettleFullManaLocked(c, 24);
        Assert.Equal(199, state.Mana.SpentRemainder);
        state.Mana.Hit(25, 4, 20); Assert.Equal(0, state.Mana.Take(29)); // Discarded .2 cannot combine with .8.
        state.Mana.Clear(); state.Mana.Spent(199, 10); c.Rage.Clear(); c.PlayerMana = 100;
        Assert.True(Game.PayMana(c, skill.Id, out uint paid)); Assert.Equal(5u, paid);
        Assert.Equal(4, state.Mana.SpentRemainder);
        Assert.Equal(10, c.Rage.Read(0, new(NoInherentLoss: true)).Current);
        c.PlayerMana = 0; Assert.False(Game.PayMana(c, skill.Id, out _)); Assert.Equal(4, state.Mana.SpentRemainder);
        Assert.Null(await Game.ChangePassives("Whisperer", h.Wire, h.Ciphers, c, [863], false, "spend source refund", refundPoints: true));
        Assert.Equal(0, state.Mana.SpentRemainder);

        h.Build(B); c.PlayerMana = c.Pools.Mana; c.PlayerLife -= 100;
        var manaType = Program.Items.Find("Small Mana Flask")!;
        foreach (var old in h.Items.Items(PlayerInventories.Flask).Where(i => i.X == 0).ToArray()) h.Items.Remove(PlayerInventories.Flask, old.Id);
        h.Items.Add(PlayerInventories.Flask, 0, 0, ItemCatalog.Create(manaType) with { Charges = 24 });
        var gate = Program.SendGates.GetValue(h.Ciphers, _ => new(1, 1));
        await gate.WaitAsync();
        var use = Game.UseFlask("Whisperer", h.Wire, h.Ciphers, c, 0);
        Assert.False(use.IsCompleted);
        // The accepted use keeps the old recipient snapshot while a real paid node refund is queued behind it.
        var refund = Game.ChangePassives("Whisperer", h.Wire, h.Ciphers, c, [53715], false, "flask source refund", refundPoints: true);
        Assert.DoesNotContain((ushort)53715, c.MainAscendancy.Allocated);
        gate.Release(); await Task.WhenAll(use, refund);
        var acceptedMana = h.Items.Items(PlayerInventories.Flask).Single(i => i.X == 0);
        Assert.Contains((FlaskEnchantments.Trigger.Adjacent, acceptedMana.Id, (byte)0), FlaskSignals(c));
        Assert.DoesNotContain(FlaskSignals(c), s => s.Trigger == FlaskEnchantments.Trigger.LifeFlask);
        var effect = Assert.Single(c.FlaskEffects.End(_ => true));
        Assert.True(effect.ManaPerSecond > 0, effect.ToString()); Assert.Equal(0, effect.Queue); Assert.True(effect.ManaStays);
        c.FlaskEffects.Start(effect); // Reuse the actually accepted effect and its captured extra recipient.
        var recovered = Game.AdvanceFlaskRecovery(c, effect.Started, effect.Ends);
        Assert.Equal(50, recovered.Mana, 7); Assert.Equal(50, recovered.Life, 7);
        foreach (var old in h.Items.Items(PlayerInventories.Flask).Where(i => i.X == 1).ToArray()) h.Items.Remove(PlayerInventories.Flask, old.Id);
        h.Items.Add(PlayerInventories.Flask, 1, 0, ItemCatalog.Create(Program.Items.Find("Small Life Flask")!) with { Charges = 21 });
        await Game.UseFlask("Whisperer", h.Wire, h.Ciphers, c, 1);
        var acceptedLife = h.Items.Items(PlayerInventories.Flask).Single(i => i.X == 1);
        Assert.Contains((FlaskEnchantments.Trigger.Adjacent, acceptedLife.Id, (byte)1), FlaskSignals(c));
        Assert.Contains((FlaskEnchantments.Trigger.LifeFlask, acceptedLife.Id, (byte)1), FlaskSignals(c));
        c.FlaskEffects.End(_ => true);
        var atEnd = effect with { Started = 0, Ends = 1, ManaPerSecond = 0, ManaAtEnd = 50 };
        Game.StartRecoveryEffectsLocked(c, Game.ResourceEffectsOf(c), [atEnd], copy: true);
        recovered = Game.AdvanceFlaskRecovery(c, 0, 1);
        Assert.Equal(50, recovered.Mana); Assert.Equal(0, recovered.Life);
        var queuedMana = effect with { Started = 10, Ends = 12, NoQueue = false, ManaStays = false };
        var queuedLife = effect with { Started = 10, Ends = 14, ManaPerSecond = 0, LifePerSecond = 20, NoQueue = false };
        Assert.Equal(2, queuedMana.Queue); Assert.Equal(1, queuedLife.Queue);
        Game.StartRecoveryEffectsLocked(c, Game.ResourceEffectsOf(c), [queuedLife, queuedMana], copy: true);
        recovered = Game.AdvanceFlaskRecovery(c, 10, 12);
        Assert.Equal(40 + recovered.Mana, recovered.Life, 7); Assert.Single(recovered.Ended);
    }

    [Fact]
    public async Task Shared_Blind_transfer_and_retirement_use_current_independent_state_without_wall_clock_waits()
    {
        var definition = Assert.Single(new AscendancyCatalog().Definitions,
            d => d.Identity.Family == AscendancyFamily.Phrecia && d.Name == "Whisperer");
        using var h = new Harness(definition); var c = h.Conn; h.Build(B);
        Assert.False(Game.InflictPlayerBlind(c, 4)); // Actual 5888 immunity.
        var target = new SpawnedMonster("Blind", new(105, 100, 0), 0, 0, 1000) { Level = 60, RampageBlindUntil = 6 };
        c.Monsters[42] = target;
        var source = Game.CharacterPlayerStats(c);
        Assert.Equal((100, 50, 4d), Game.BlindInputs(source));
        Assert.True(Game.InflictMonsterBlind(c, 42, target, 0, (100, 0, 4)));
        c.FlaskGrounds.SetBlinded(42, true, () => c.NextBuff++);
        Has(Game.BlindPresentation(c)!, BlindStats(42, 30));
        var state = Game.ResourceEffectsOf(c); ushort firstBuff = state.TargetBlind(42, target).Buff;
        h.Time.At = 1;
        Assert.True(Game.InflictMonsterBlind(c, 42, target, 1, (100, 0, 4)));
        var renewed = Game.BlindPresentation(c)!;
        Has(renewed, InstanceEntities.RemoveBuff(42, firstBuff));
        Assert.NotEqual(firstBuff, state.TargetBlind(42, target).Buff);
        Assert.Equal((20, 5d), state.TargetBlind(42, target).SentOwn);
        Assert.Equal((30, 0d), state.TargetBlind(42, target).Sent); // Merged value unchanged, own native timer renewed.
        int reads = 0;
        using (GameEvents.PlayerStatSources.Add("Game", (_, stats) => { reads++; return stats; }))
        {
            Game.BlindPresentation(c); int one = reads; reads = 0;
            for (uint id = 50; id < 82; id++)
            { c.Monsters[id] = new("smoke", new(105, 100, 0), 0, 0, 100); c.FlaskGrounds.SetBlinded(id, true, () => c.NextBuff++); }
            Game.BlindPresentation(c); Assert.Equal(one, reads);
        }
        int evasion = Game.Monsters.BaseEvasion(60)!.Value;
        int chance = DamageModel.AccuracyHitChance(evasion, 500);
        Assert.True(Game.RampageAttackHits(c, 42, target, 500, 1, new FixedDraw(chance), source));
        c.FlaskGrounds.SetBlinded(42, false, () => 0);
        state.TargetBlind(42, target).Read(5); target.RampageBlindUntil = 6;
        h.Time.At = 5; Has(Game.BlindPresentation(c)!, BlindStats(42, 20));
        h.Time.At = 6; Has(Game.BlindPresentation(c)!, BlindStats(42, 0));
        var removed = new SpawnedMonster("retired actor", new(105, 100, 0), 0, 0, 100);
        c.Monsters[90] = removed;
        Assert.True(Game.InflictMonsterBlind(c, 90, removed, 6, (100, 50, 4)));
        Game.BlindPresentation(c); ushort removedBuff = state.TargetBlind(90, removed).Buff;
        c.Monsters[90] = new("replacement actor", new(105, 100, 0), 0, 0, 100);
        var replaced = Game.BlindPresentation(c)!;
        Has(replaced, InstanceEntities.RemoveBuff(90, removedBuff)); Has(replaced, BlindStats(90, 0));
        Assert.Equal(0, state.TargetBlind(90, c.Monsters[90]).Read(6).Less);

        c.FlaskGrounds.SetBlinded(42, true, () => c.NextBuff++);
        Game.BlindPresentation(c); h.Wire.Drain(h.Receive);
        var gate = Program.SendGates.GetValue(h.Ciphers, _ => new(1, 1));
        await gate.WaitAsync();
        var queued = Game.SendBlind("Whisperer", h.Wire, h.Ciphers, c);
        var refund = Game.ChangePassives("Whisperer", h.Wire, h.Ciphers, c, [5888], false, "Blind source refund", refundPoints: true);
        Assert.DoesNotContain((ushort)5888, c.MainAscendancy.Allocated);
        gate.Release(); await Task.WhenAll(queued, refund);
        var current = h.Wire.Drain(h.Receive);
        Has(current, BlindStats(42, 20)); Assert.True(current.AsSpan().IndexOf(BlindStats(42, 30)) < 0);

        // Capture real gem releases, refund their actual source, then land the admitted old snapshots.
        foreach (var (effectId, weaponName) in new[] { ("BurningArrow", "Crude Bow"), ("Fireball", "Driftwood Wand") })
        {
            h.Build(C);
            foreach (var item in h.Items.Items(PlayerInventories.Weapon1).ToArray()) h.Items.Remove(PlayerInventories.Weapon1, item.Id);
            var gemItem = ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SkillGem" + effectId)!);
            h.Items.Add(PlayerInventories.Weapon1, 0, 0, ItemCatalog.Create(Program.Items.Find(weaponName)!) with
                { Sockets = [new(SocketColour.White, gemItem)], Links = [1] });
            c.StatSourcesCacheState = null; c.Pools = Game.PoolsFor(c, 68) with { Accuracy = 500 };
            c.PlayerStats[Key("global_always_hit")] = 1; c.PlayerStats[Key("base_chance_to_ignite_%")] = 100;
            c.PlayerStats.Remove(Key("never_ignite"));
            foreach (var id in c.Monsters.Keys.Where(id => id != 42).ToArray()) c.Monsters.Remove(id);
            c.FlaskGrounds.SetBlinded(42, false, () => 0); target.RampageBlindUntil = 0;
            target.Walk.Teleport(130, 100, 0);
            var session = new Game.ProjectileSession("Whisperer", h.Wire, h.Ciphers, c); c.ProjectileRuntime = session;
            var gem = Game.GemSkillEffects(c).Single(g => Game.SkillsFor(c).EffectId(g.Effect) == effectId);
            ProjectileUse Capture() => (ProjectileUse)typeof(Game).GetMethod("CaptureGemProjectile", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [c, session, gem.Id, gem, (180, 100), new SkillCriticalRoll(0)])!;
            try
            {
                var use = Capture(); var shot = Assert.IsType<ProjectileOffense>(use.Payload);
                Assert.Equal(effectId == "Fireball", shot.ClientPredicted);
                Assert.Equal((100, 50, 4d), shot.Blind); Assert.Equal(100, shot.Ignite.Chance); Assert.True(shot.Ignite.Min > 0);
                Assert.Null(await Game.ChangePassives("Whisperer", h.Wire, h.Ciphers, c, [5888], false, "release source refund", refundPoints: true));
                c.PlayerStats[Key("never_ignite")] = 1;
                var later = Assert.IsType<ProjectileOffense>(Capture().Payload);
                Assert.Equal(0, later.Blind.Chance); Assert.Equal(MonsterIgnites.Settings.None, later.Ignite);
                session.Schedule(session.Runtime.Now, () => true, () => use with { Profile = use.Profile with { Speed = 100 } });
                session.AdvanceTo(ProjectileRuntime.Time(1));
                Assert.Equal(30, Game.ResourceEffectsOf(c).TargetBlind(42, target).Read(1).Less);
                Assert.True(target.Ignites.Burning);
            }
            finally { session.Invalidate(); await session.OutputCompleted; c.ProjectileRuntimeState = h.Time; }
        }
        h.Build(A); h.Time.At = 10; state = Game.ResourceEffectsOf(c);
        foreach (var item in h.Items.Items(PlayerInventories.Offhand1).ToArray()) h.Items.Remove(PlayerInventories.Offhand1, item.Id);
        h.Items.Add(PlayerInventories.Offhand1, 0, 0, ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!));
        c.Pools = c.Pools with { Accuracy = 1000, AccuracyBase = 997, AccuracyPct = 7 };
        Dictionary<string, int> mainLocal = new() { ["local_accuracy_rating"] = 12 };
        Dictionary<string, int> offLocal = new() { ["local_accuracy_rating"] = 82 };
        Dictionary<uint, int> own = new() { [Key("accuracy_rating")] = 53, [Key("accuracy_rating_+%")] = 17 };
        int OffAccuracy() => Game.OffHandPanelStats(c, Game.CharacterPlayerStats(c), mainLocal, offLocal, 0, own)!
            .Single(s => s.Stat == "off_hand_accuracy_rating").Value;
        int offBefore = OffAccuracy(), selectedBefore = Game.SupportedAccuracy(c, h.Fireball.Id).Rating;
        Assert.True(Game.InflictPlayerBlind(c, 4));
        Assert.Equal(offBefore * 4 / 5, OffAccuracy());
        Assert.Equal(selectedBefore * 4 / 5, Game.SupportedAccuracy(c, h.Fireball.Id).Rating);
        state.Mana.Hit(10, 100, 20); state.Mana.Spent(199, 10);
        var transfer = new PendingTransfer(1, 1, false, AtSpawn: true);
        Assert.True(c.TryBeginAreaChange(transfer, out _)); Assert.Equal(10, c.RampageTransferAt);
        await Game.Transfer("Whisperer", h.Wire, h.Ciphers, c, transfer, _ =>
        {
            // Simulate five seconds of Auth loading through production discard, without sleeping or advancing a Stopwatch.
            Game.DiscardResourceLoadingRecovery(c, 15); state.PlayerBlind.Read(15);
            return Task.FromResult(new ControlResponse { Ok = true, Ticket = 123, Area = 1, Instance = 1 });
        });
        var wrong = h.NewConnection("wrong", 123); Game.RestoreResourceEffects(wrong);
        Assert.Null(wrong.ResourceEffectsState);
        var arriving = h.NewConnection(c.Character!, 123); Game.RestoreResourceEffects(arriving);
        var carried = Game.ResourceEffectsOf(arriving);
        Assert.True(carried.Loading); Assert.Equal(20, carried.PlayerBlind.Read(0).Less);
        Assert.Equal(199, carried.Mana.SpentRemainder);
        Game.ActivateResourceEffects(arriving, 100); Assert.Equal(20, carried.PlayerBlind.Read(103).Less);
        Assert.Equal(5, carried.Mana.Take(101)); Assert.Equal(15, carried.Mana.Take(104));
        Assert.Equal(0, carried.PlayerBlind.Read(104).Less);
        Game.RestoreResourceEffects(arriving); Assert.Equal(0, carried.Mana.Take(104));
        var failed = h.NewConnection("failed"); var failedState = Game.ResourceEffectsOf(failed);
        failed.ProjectileRuntimeState = new ManualRuntime { At = 10 };
        failedState.Mana.Hit(10, 100, 20); failedState.Mana.Spent(199, 10);
        var refused = new PendingTransfer(1, 1, false);
        Assert.True(failed.TryBeginAreaChange(refused, out _));
        await Game.Transfer("Whisperer", h.Wire, h.Ciphers, failed, refused, _ =>
        {
            Game.DiscardResourceLoadingRecovery(failed, 11);
            return Task.FromResult(new ControlResponse { Ok = false, Error = "controlled refusal" });
        });
        Assert.False(failed.LeavingArea); Assert.Null(failedState.TransferCheckpoint);
        Assert.Equal(0, failedState.Mana.Take(11)); Assert.Equal(199, failedState.Mana.SpentRemainder);
        Assert.Equal(5, failedState.Mana.Take(12));

        var live = h.NewConnection("death"); var effects = Game.ResourceEffectsOf(live);
        var world = live.RampageWorld;
        Assert.Same(effects, live.ResourceEffectsState); Assert.Same(world, live.OwnedCombatState);
        live.RetireResourceEffects(); Assert.Same(world, live.OwnedCombatState);
        effects = Game.ResourceEffectsOf(live); Assert.NotNull(live.ResourceEffectsState);
        effects.Mana.Hit(0, 1000, 100); effects.Mana.Spent(199, 10);
        effects.PlayerBlind.Inflict(0, 4, 0, false);
        live.PlayerLife = 1; live.PlayerMana = 0; live.EnergyShield.SetMaximum(0, false);
        var fire = DamagePacket.Of(DamageType.Fire, 100, 100);
        live.ElementalAilments.Apply(fire, fire, true, live.PlayerMaxLife, 0, _ => 0, new FixedDraw(0));
        live.ProjectileRuntimeState = new ManualRuntime();
        // Present an owned buff before death, then hold publication while the lethal frame retires its owner.
        Game.BlindPresentation(live); ushort dyingBuff = effects.PlayerBlind.Buff;
        await gate.WaitAsync();
        double carry = .5;
        Assert.False(Game.CompleteResourceRecoveryLocked(live, 0, 1, Game.CharacterPlayerStats(live), 100, 0, live.Pools.Mana, ref carry));
        Assert.Equal(0u, live.PlayerLife); Assert.Equal(0u, live.PlayerMana); Assert.Equal(0, carry);
        Assert.Null(live.ResourceEffectsState); Assert.Null(live.OwnedCombatState); Assert.Equal(0, effects.Mana.SpentRemainder);
        Assert.Equal(0, effects.PlayerBlind.Read(0).Less);
        var deathOutput = Game.SendBlind("Whisperer", h.Wire, h.Ciphers, live);
        Assert.False(deathOutput.IsCompleted); gate.Release(); await deathOutput;
        Has(h.Wire.Drain(h.Receive), InstanceEntities.RemoveBuff(Program.PlayerId, dyingBuff));

        var ordinary = h.NewConnection("ordinary", optional: false); ordinary.MainAscendancy = new();
        var normalState = Game.ResourceEffectsOf(ordinary); normalState.Mana.Spent(199, 10);
        h.Provider.Stop(ex => throw ex);
        Assert.False(ordinary.Closed); Assert.Same(normalState, ordinary.ResourceEffectsState);
        Assert.Equal(199, normalState.Mana.SpentRemainder); Assert.True(c.Closed);
        h.Rules.Stop(ex => throw ex); Assert.Null(ordinary.ResourceEffectsState);
        Assert.False(Game.ResourceEffectsCurrent(ordinary, normalState));
        var replacementRules = new ModuleLifetime("Game"); var replacementSource = new ModuleLifetime("PhreciaAscendancies");
        Game.CombatLifetime = replacementRules;
        using (var replacement = AscendancyPolicies.Register(replacementSource, h.Policy))
        {
            var currentState = Game.ResourceEffectsOf(ordinary); currentState.Mana.Spent(7, 10);
            ordinary.ResourceEffectsLease.Complete(h.Rules, null, normalState, normalState.Generation);
            h.Rules.Stop(ex => throw ex);
            Assert.True(Game.ResourceEffectsCurrent(ordinary, currentState)); Assert.Equal(7, currentState.Mana.SpentRemainder);
            var reentered = h.NewConnection("replacement"); var nextState = Game.ResourceEffectsOf(reentered);
            Assert.Same(replacementSource, nextState.Source);
            Assert.False(Game.ResourceEffectsCurrent(reentered, effects));
            reentered.MainAscendancy = new(definition.Identity, B, AscendancyRules.AwardKeys);
            reentered.Pools = Game.PoolsFor(reentered, 68);
            reentered.PlayerLife = reentered.PlayerMaxLife - 100; reentered.PlayerMana = reentered.Pools.Mana;
            var nextItems = Game.ItemsOf(reentered);
            foreach (var old in nextItems.Items(PlayerInventories.Flask).Where(i => i.X == 0).ToArray()) nextItems.Remove(PlayerInventories.Flask, old.Id);
            nextItems.Add(PlayerInventories.Flask, 0, 0, ItemCatalog.Create(Program.Items.Find("Small Mana Flask")!) with { Charges = 24 });
            await reentered.FlaskBarGate.WaitAsync();
            var staleUse = Game.UseFlask("Whisperer", h.Wire, h.Ciphers, reentered, 0);
            Assert.False(staleUse.IsCompleted);
            replacementSource.Stop(ex => throw ex); Assert.Null(reentered.ResourceEffectsState);
            reentered.FlaskBarGate.Release(); await staleUse;
            Assert.Equal(24, nextItems.Items(PlayerInventories.Flask).Single(i => i.X == 0).Item.Charges);
            Assert.Empty(reentered.FlaskEffects.End(_ => true)); Assert.Empty(FlaskSignals(reentered));
        }
        replacementRules.Stop(ex => throw ex); Assert.Null(ordinary.ResourceEffectsState);
        Assert.Equal(definition.Identity, CharacterSaves.Load(c.Character!)!.MainAscendancy!.Selection);
    }

    sealed class FixedDraw(int value) : Random { public override int Next(int maxValue) => value; public override double NextDouble() => 0; }
    sealed class HighDraw : Random { public override int Next(int maxValue) => maxValue - 1; public override double NextDouble() => 0; }
    sealed class ManualRuntime : IProjectileRuntime
    {
        public double At; public bool Active { get; private set; } = true;
        public double NowTicks => At * 120000; public void Invalidate() => Active = false;
    }
    sealed class CaptureStream : NetworkStream
    {
        readonly MemoryStream bytes = new(); readonly Socket peer;
        static (Socket Writer, Socket Peer) Pair()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            var writer = new Socket(SocketType.Stream, ProtocolType.Tcp); writer.Connect(listener.LocalEndpoint);
            return (writer, listener.AcceptSocket());
        }
        public CaptureStream() : this(Pair()) { }
        CaptureStream((Socket Writer, Socket Peer) pair) : base(pair.Writer, ownsSocket: true) => peer = pair.Peer;
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        { bytes.Write(buffer.Span); return ValueTask.CompletedTask; }
        public byte[] Drain(ConnectionCiphers receive) { var result = bytes.ToArray(); bytes.SetLength(0); receive.Receive.Process(result); return result; }
        protected override void Dispose(bool disposing) { if (disposing) { peer.Dispose(); bytes.Dispose(); } base.Dispose(disposing); }
    }
    sealed class Harness : IDisposable
    {
        const string Flag = "Game_VERIFIED_WHISPERER";
        readonly string? oldFlag = Environment.GetEnvironmentVariable(Flag);
        readonly string path = Path.Combine(Path.GetTempPath(), "whisperer-" + Guid.NewGuid().ToString("N") + ".db");
        readonly IGameLogic? oldLogic = Program.GameLogicOrNull;
        readonly IModuleLifetime? oldRules = Game.CombatLifetime;
        readonly IDisposable registration;
        readonly List<ClientState> connections = [];
        internal readonly ModuleLifetime Rules = new("Game"), Provider = new("PhreciaAscendancies");
        internal readonly IAscendancyFamilyPolicy Policy;
        internal readonly AscendancyCatalog.Definition Definition;
        internal readonly CaptureStream Wire = new();
        internal readonly ConnectionCiphers Ciphers = new(new byte[64], false), Receive = new(new byte[64], true);
        internal readonly ManualRuntime Time = new();
        internal readonly ClientState Conn;
        internal PlayerInventories Items => Game.ItemsOf(Conn);
        internal (ushort Id, int Effect, string Name, int Level, int Quality, uint Gem) Fireball =>
            Game.GemSkillEffects(Conn).Single(g => Game.SkillsFor(Conn).EffectId(g.Effect) == "Fireball");
        internal Harness(AscendancyCatalog.Definition definition)
        {
            Saves.Open(path); Definition = definition;
            Game.CombatLifetime = Rules; Program.SetGameLogic(new GameServer.Modules.Game.GameLogic());
            var assembly = typeof(GameServer.Modules.PhreciaAscendancies.PhreciaAscendanciesModule).Assembly;
            Policy = (IAscendancyFamilyPolicy)Activator.CreateInstance(assembly.GetType("GameServer.Modules.PhreciaAscendancies.PhreciaFamilyPolicy")!, true)!;
            OptIn(true); registration = AscendancyPolicies.Register(Provider, Policy);
            Conn = NewConnection("Whisperer" + Guid.NewGuid().ToString("N")); Conn.ProjectileRuntimeState = Time;
            var gem = ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SkillGemFireball")!);
            Items.Add(PlayerInventories.Weapon1, 0, 0, ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!) with
                { Sockets = [new(SocketColour.White, gem)], Links = [1] });
            Conn.PlayerStats[Key("virtual_intelligence_for_requirements")] = 1000;
            Conn.PlayerStats[Key("no_inherent_rage_loss")] = 1;
        }
        internal void OptIn(bool enabled) => Environment.SetEnvironmentVariable(Flag, enabled ? "1" : "0");
        internal static ClientState Connection(string name, bool optional = false, uint ticket = 1) =>
            new(new(1, "Whisperer", 300, 300, 100, 100, null, []), ticket, 1)
            {
                Character = name, ClassRow = 3, PlayerLife = 1000, PlayerMana = 1000, Instance = new(),
                Experience = CharacterStats.ExperienceFor(68), Pools = new(68, 1000, 1000, 100, 0),
                LeagueModule = new(new League("WhispererTest", "Standard", false, false),
                    new HashSet<string>(optional ? ["Game", "PhreciaAscendancies"] : ["Game"])),
                Keyring = new InstanceKeyring(new byte[32]),
            };
        internal ClientState NewConnection(string name, uint ticket = 1, bool optional = true)
        {
            var c = Connection(name, optional, ticket); c.MainAscendancy = new(Definition.Identity, A, AscendancyRules.AwardKeys);
            connections.Add(c); if (optional) AscendancyPolicies.SelectionChanged(c); return c;
        }
        internal void Build(ushort[] nodes)
        {
            Conn.MainAscendancy = new(Definition.Identity, nodes, AscendancyRules.AwardKeys);
            Conn.Passives = []; Conn.RefundPoints = 200; Conn.StatSourcesCacheState = null;
            CharacterSaves.SetMainAscendancy(Conn.Character!, Conn.MainAscendancy, [], new Dictionary<ushort, ushort>(), 200);
            Conn.SavedPassiveProgress = new(Conn.MainAscendancy, [], new Dictionary<ushort, ushort>(), 200);
            AscendancyPolicies.SelectionChanged(Conn);
            Conn.Pools = Game.PoolsFor(Conn, 68); Conn.PlayerLife = Conn.PlayerMaxLife; Conn.PlayerMana = Conn.Pools.Mana;
        }
        internal int SkillRecordCost(ushort skill) => Game.PlayerSkills(Conn).Single(s => s.Id == skill).Stats!.Single(s => s.Key == Key("mana_cost")).Value;
        public void Dispose()
        {
            foreach (var c in connections) { c.Closed = true; c.RetireResourceEffects(); c.RetireOwnedCombat(); PlayerInventories.Forget(c.Character!); }
            registration.Dispose(); Provider.Stop(_ => { }); Rules.Stop(_ => { }); Wire.Dispose();
            Game.CombatLifetime = oldRules; Program.SetGameLogic(oldLogic!); Environment.SetEnvironmentVariable(Flag, oldFlag);
            Saves.Db.Dispose(); foreach (string suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }
    }
}
