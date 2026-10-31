using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Runtime.Loader;
using System.Collections;
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
        return conn.FlaskAutomation.Events.Select(s => (s.Trigger, s.Item, s.Slot)).ToArray();
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
        var (commands, replies) = h.CommandsFor(c);
        Assert.Contains(commands.Usages, command => command.Name == "blind");
        foreach (string invalid in new[] { "0", "61", "NaN" }) Assert.True(await commands.RunAsync("/blind " + invalid));
        Assert.All(replies, reply => Assert.StartsWith("Usage: /blind", reply)); replies.Clear();
        Assert.True(await commands.RunAsync("/blind")); Assert.Equal("Blind refused (immune or unavailable).", Assert.Single(replies));
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
        // Actual admission/replacement dispatch starts the same two production loops with manual scheduling.
        ordinary.PlayerStats[Key(ManaRecoveryRules.RagePer200)] = 10;
        ordinary.PlayerStats[Key("base_life_regeneration_rate_per_minute")] = 600;
        ordinary.PlayerStats[Key("base_mana_regeneration_rate_per_minute")] = 600;
        ordinary.PlayerSent = true; ordinary.Wire = ("Whisperer", h.Wire, h.Ciphers);
        ordinary.Pools = ordinary.Pools with { EnergyShield = 300 };
        ordinary.EnergyShield.SetMaximum(300, false); ordinary.PlayerLife = 500; ordinary.PlayerMana = 0;
        var runtime = new ManualRuntime(); ordinary.ProjectileRuntimeState = runtime;
        var oldSchedule = new ManualUpdates(runtime); Game.PlayerUpdatesSchedule = oldSchedule;
        var oldLogic = Program.Logic;
        Program.ActivatePlayerUpdates(ordinary);
        var oldUpdates = Assert.IsType<PlayerUpdatesState>(ordinary.PlayerUpdatesState);
        oldSchedule.Completion = oldUpdates.Completion;
        var oldFast = await oldSchedule.Next(Game.RecoveryStepMs);
        await oldSchedule.Next(Game.PlayerTickMs);
        await oldSchedule.OtherRoles();
        Program.ActivatePlayerUpdates(ordinary);
        Assert.Same(oldUpdates, ordinary.PlayerUpdatesState); Assert.Equal(1, oldSchedule.FastWaits);
        // The old writer commits while current, but its output remains queued across replacement.
        h.Wire.Drain(h.Receive); await gate.WaitAsync(); oldFast.Advance(1);
        await oldFast.Observed.Task;
        uint reloadLife, reloadMana, reloadEs;
        lock (ordinary.Monsters)
        {
            reloadLife = ordinary.PlayerLife; reloadMana = ordinary.PlayerMana; reloadEs = ordinary.EnergyShield.Current;
            Assert.True(reloadLife > 500); Assert.True(reloadMana > 0); Assert.True(reloadEs > 0);
        }
        h.Rules.Stop(ex => throw ex); Assert.Null(ordinary.ResourceEffectsState);
        Assert.False(Game.ResourceEffectsCurrent(ordinary, normalState));
        var replacementRules = new ModuleLifetime("Game"); var replacementSource = new ModuleLifetime("PhreciaAscendancies");
        var nextSchedule = new ManualUpdates(runtime) { At = 1 }; Game.PlayerUpdatesSchedule = nextSchedule;
        Game.CombatLifetime = replacementRules;
        Program.SetGameLogic(new GameServer.Modules.Game.GameLogic(replacementRules));
        var nextUpdates = Assert.IsType<PlayerUpdatesState>(ordinary.PlayerUpdatesState);
        nextSchedule.Completion = nextUpdates.Completion;
        var nextFast = await nextSchedule.Next(Game.RecoveryStepMs);
        var nextSlow = await nextSchedule.Next(Game.PlayerTickMs);
        oldLogic.ResumePlayerUpdates(ordinary); Program.ActivatePlayerUpdates(ordinary);
        Assert.Same(nextUpdates, ordinary.PlayerUpdatesState); Assert.Equal(1, nextSchedule.FastWaits);
        gate.Release(); await oldUpdates.Completion;
        await nextSchedule.OtherRoles();
        Assert.Empty(h.Wire.Drain(h.Receive)); Assert.False(ordinary.Closed);
        Assert.Equal(reloadLife, ordinary.PlayerLife); Assert.Equal(reloadMana, ordinary.PlayerMana);
        Assert.Equal(reloadEs, ordinary.EnergyShield.Current);
        ordinary.PlayerUpdatesWorker.Complete(h.Rules, null, oldUpdates, oldUpdates.Generation);
        Assert.True(Game.PlayerUpdatesCurrent(ordinary, nextUpdates));
        Assert.False(ordinary.PlayerUpdatesWorker.TryStart(ordinary, replacementRules, null, nextUpdates, out _));
        using (var replacement = AscendancyPolicies.Register(replacementSource, h.Policy))
        {
            var currentState = Game.ResourceEffectsOf(ordinary); currentState.Mana.Spent(7, 10);
            ordinary.ResourceEffectsLease.Complete(h.Rules, null, normalState, normalState.Generation);
            h.Rules.Stop(ex => throw ex);
            Assert.True(Game.ResourceEffectsCurrent(ordinary, currentState)); Assert.Equal(7, currentState.Mana.SpentRemainder);
            var ordinaryItems = Game.ItemsOf(ordinary);
            foreach (var old in ordinaryItems.Items(PlayerInventories.Flask).Where(i => i.X == 0).ToArray()) ordinaryItems.Remove(PlayerInventories.Flask, old.Id);
            ordinaryItems.Add(PlayerInventories.Flask, 0, 0, ItemCatalog.Create(Program.Items.Find("Small Mana Flask")!) with { Charges = 24 });
            await Game.UseFlask("Whisperer", h.Wire, h.Ciphers, ordinary, 0);
            var accepted = Assert.Single(ordinary.FlaskEffects.End(_ => true));
            accepted = accepted with { Started = 1, Ends = 1 + accepted.Ends - accepted.Started };
            Game.StartRecoveryEffectsLocked(ordinary, currentState, [accepted], copy: false);
            currentState.Mana.Hit(1, 1000, 20);
            double lifeRate = Game.LifeRegenPerSecond(ordinary), manaRate = Game.ManaRegenPerSecond(ordinary);
            var (normalCommands, normalReplies) = h.CommandsFor(ordinary);
            Assert.True(await normalCommands.RunAsync("/blind 4")); Assert.Equal("Blind applied.", Assert.Single(normalReplies));
            ushort activeBlind = currentState.PlayerBlind.Buff;
            Has(h.Wire.Drain(h.Receive), BlindStats(Program.PlayerId, 20));
            nextFast.Advance(2); nextFast = await nextSchedule.Next(Game.RecoveryStepMs);
            Assert.Equal(reloadLife + (uint)lifeRate, ordinary.PlayerLife);
            Assert.Equal(reloadMana + (uint)(manaRate + accepted.ManaPerSecond + 50), ordinary.PlayerMana);
            Assert.True(ordinary.EnergyShield.Current > reloadEs); // The actual replacement writer recharges ES too.
            nextSlow.Advance(2); nextSlow = await nextSchedule.Next(Game.PlayerTickMs);
            h.Wire.Drain(h.Receive);
            nextSlow.Advance(6); await nextSchedule.Next(Game.PlayerTickMs);
            var expired = h.Wire.Drain(h.Receive);
            Has(expired, InstanceEntities.RemoveBuff(Program.PlayerId, activeBlind)); Has(expired, BlindStats(Program.PlayerId, 0));
            Assert.True(Game.PlayerUpdatesCurrent(ordinary, nextUpdates));
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
        await nextUpdates.Completion; Assert.Null(ordinary.PlayerUpdatesState);
        ordinary.Closed = true;
        await AcrossGameAssemblies(h);
        await UtilityAcrossGameAssemblies(h);
        Assert.Equal(definition.Identity, CharacterSaves.Load(c.Character!)!.MainAscendancy!.Selection);
    }

    // Exercise real distinct Game identities, sharing only the framework/catalog/protocol assemblies.
    static async Task AcrossGameAssemblies(Harness h)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        var previousLogic = Program.Logic; var previousRules = Game.CombatLifetime; var previousSchedule = Game.PlayerUpdatesSchedule;
        var oldOwner = new ModuleLifetime("Game"); var owner = new ModuleLifetime("Game");
        var context = new AssemblyLoadContext("Whisperer current Game regression", isCollectible: true);
        context.Resolving += (_, name) => AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.GetName().Name == name.Name);
        var assembly = context.LoadFromAssemblyPath(typeof(Game).Assembly.Location);
        Assert.NotSame(typeof(Game).Assembly, assembly);
        var game = assembly.GetType("Game")!;
        object? Call(string name, params object?[] args) => game.GetMethods(flags).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(null, args);
        static object Field(object value, string name) => value.GetType().GetField(name, flags)!.GetValue(value)!;
        var c = h.NewConnection("cross" + Guid.NewGuid().ToString("N"), optional: false); c.MainAscendancy = new();
        c.PlayerSent = true; c.Wire = ("Whisperer", h.Wire, h.Ciphers); c.AiEnabled = false;
        c.IncomingHitRandom = new HighDraw(); c.Pools = new(68, 1000, 1000, 100, 0) { Armour = 0, Evasion = 0 };
        c.PlayerLife = c.PlayerMana = 1000; c.EnergyShield.SetMaximum(0, false); c.Ward.SetMaximum(0);
        c.PlayerStats[Key("gain_x_rage_when_hit")] = 1; c.PlayerStats[Key(ManaRecoveryRules.RagePer200)] = 10;
        c.PlayerStats[Key("no_inherent_rage_loss")] = 1; c.PlayerStats[Key("virtual_intelligence_for_requirements")] = 1000;
        var runtime = new ManualRuntime(); c.ProjectileRuntimeState = runtime;
        var before = new ManualUpdates(runtime); var after = new ManualUpdates(runtime);
        PlayerUpdatesState? oldState = null; object? currentState = null; Task? currentCompletion = null;
        try
        {
            Game.CombatLifetime = oldOwner; Game.PlayerUpdatesSchedule = before; Program.SetGameLogic(new GameServer.Modules.Game.GameLogic(oldOwner));
            Program.ActivatePlayerUpdates(c); oldState = Assert.IsType<PlayerUpdatesState>(c.PlayerUpdatesState); before.Completion = oldState.Completion;
            await before.Next(Game.RecoveryStepMs); await before.Next(Game.PlayerTickMs); await before.OtherRoles();
            var attack = new MonsterAttack(0x102, 0, 10, DamagePacket.Of(DamageType.Physical, 100, 100));
            var enemy = new SpawnedMonster("replacement AI", new(105, 100, 0), 100, 5, 1000) { Attack = attack, Aggro = true };
            var stage = new SpawnedMonster("pending stage", new(180, 180, 0), 0, 0, 1000)
                { Stage = new(50, 1, 1, 1), StageRunning = true, StageTaken = true, Life = 500, BusyUntil = 0 };
            c.Monsters[43] = stage;
            c.Monsters[42] = enemy; c.AiEnabled = true; c.Walk.Teleport(100, 100, 0);
            oldOwner.Stop(ex => throw ex); await oldState.Completion;
            game.GetField("CombatLifetime", flags)!.SetValue(null, owner);
            game.GetField("PlayerUpdatesSchedule", flags)!.SetValue(null, after);
            var logic = (IGameLogic)Activator.CreateInstance(assembly.GetType("GameServer.Modules.Game.GameLogic")!, flags, null, [owner], null)!;
            Program.SetGameLogic(logic); currentState = c.PlayerUpdatesState!; Assert.NotSame(oldState, currentState);
            currentCompletion = (Task)Field(currentState, "Completion"); after.Completion = currentCompletion;
            await after.Next(Game.RecoveryStepMs); await after.Next(Game.PlayerTickMs);
            await after.Next(PlayerUpdateRole.MonsterAi); await after.Next(PlayerUpdateRole.MonsterRegeneration);
            await after.Next(PlayerUpdateRole.LivingCollisions); await after.Next(PlayerUpdateRole.Charges);
            await after.Next(PlayerUpdateRole.Fortification); var rageFrame = await after.Next(PlayerUpdateRole.Rage);
            await after.Next(PlayerUpdateRole.FlaskCharges);
            // A retired stage continuation cannot leave a surviving boss permanently StageRunning.
            while (stage.StageRunning)
            {
                var read = h.Wire.Plain.Reader.ReadAsync().AsTask();
                if (await Task.WhenAny(read, currentCompletion) == currentCompletion)
                { await currentCompletion; throw new InvalidOperationException("Area owner stopped before stage completion"); }
                var packet = await read;
                if (packet.AsSpan().IndexOf(InstanceProtocol.LifeUpdate(43, 1000)) >= 0) break;
            }
            Assert.False(stage.StageRunning); Assert.Equal(1000u, stage.Life);
            Assert.Equal(900u, c.PlayerLife); Assert.False(c.Closed); // Started current AI did the actual 100-damage contact.
            Assert.Equal(1, after.Count(PlayerUpdateRole.MonsterAi)); Assert.Equal(1, after.Count(PlayerUpdateRole.Rage));
            var retiredContact = Game.CommitMonsterHitLocked("Whisperer", c, 42, enemy, attack,
                c.Clock.Elapsed.TotalSeconds, (100, 100), (105, 100), false);
            Assert.Equal(900u, c.PlayerLife);
            Assert.True((bool)Call("InflictPlayerBlind", c, 4d, 0, null)!);
            Call("BlindPresentation", c, null);
            var items = Game.ItemsOf(c);
            foreach (var old in items.Items(PlayerInventories.Weapon1).ToArray()) items.Remove(PlayerInventories.Weapon1, old.Id);
            var gem = ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SkillGemFireball")!);
            items.Add(PlayerInventories.Weapon1, 0, 0, ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!) with
                { Sockets = [new(SocketColour.White, gem)], Links = [1] });
            ushort skill = (ushort)((IEnumerable)Call("GemSkillEffects", c)!).Cast<object>().Single().GetType().GetField("Item1")!.GetValue(
                ((IEnumerable)Call("GemSkillEffects", c)!).Cast<object>().Single())!;
            void Spend()
            {
                for (int i = 0; i < 40; i++)
                {
                    object?[] args = [c, skill, 0u]; Assert.True((bool)Call("PayMana", args)!); Assert.Equal(5u, args[2]);
                }
            }
            Spend(); // Accepted ordinary skill payments naturally change Rage after Blind was sent.
            var sent = c.SentRage; var keys = c.SentStatKeys.Order().ToArray();
            var stale = Game.RagePresentation(c);
            bool untouched = sent == c.SentRage && keys.SequenceEqual(c.SentStatKeys.Order());
            Spend(); // Ensure a further real change even on the red path that wrongly acknowledged the earlier value.
            while (h.Wire.Plain.Reader.TryRead(out _)) { }
            var gate = Program.SendGates.GetValue(h.Ciphers, _ => new(1, 1)); await gate.WaitAsync();
            var oldContact = Game.MonsterContact("Whisperer", h.Wire, h.Ciphers, c, 42, enemy, enemy.ActionGeneration);
            Assert.True(oldContact.IsCompleted); await oldContact; Assert.Equal(900u, c.PlayerLife);
            int oldReads = 0; Task oldSend;
            using (GameEvents.PlayerStatSources.Add("Game", (_, stats) => { oldReads++; return stats; })) oldSend = Game.SendRage("Whisperer", h.Wire, h.Ciphers, c);
            bool refusedBeforeBuild = oldSend.IsCompleted && oldReads == 0;
            rageFrame.Advance(0); gate.Release(); await oldSend; await after.Next(PlayerUpdateRole.Rage);
            var packets = new List<byte>(); while (h.Wire.Plain.Reader.TryRead(out var packet)) packets.AddRange(packet);
            var rows = PlayerStatRows([.. packets]);
            Assert.True(retiredContact is null && stale is null && untouched && refusedBeforeBuild,
                $"Retired accepted contact={retiredContact is not null}; retired Rage bytes={stale?.Length}; bookkeeping unchanged={untouched}; refused before build={refusedBeforeBuild}");
            Assert.Contains((973u, 1), rows); Assert.Contains((15023u, -20), rows); Assert.Contains((15024u, -20), rows);
            Assert.DoesNotContain(rows, row => row.Key is 973 or 15023 or 15024 && row.Value == 0);
            Assert.Null(Call("BlindPresentation", c, null)); Assert.Equal(80, Call("PlayerAccuracy", c, null));
            c.PlayerUpdatesWorker.Complete(oldOwner, null, oldState, oldState.Generation);
            Assert.True((bool)Call("PlayerUpdatesCurrent", c, currentState)!);
            Assert.False(c.PlayerUpdatesWorker.TryStart(c, owner, null, currentState, out _));
        }
        finally
        {
            c.Closed = true; oldOwner.Stop(_ => { }); owner.Stop(_ => { });
            if (currentCompletion is not null) await currentCompletion;
            Game.CombatLifetime = previousRules; Game.PlayerUpdatesSchedule = previousSchedule; Program.SetGameLogic(previousLogic);
            context.Unload();
        }
    }

    // Actual accepted catalog flasks, real expiry/drainer loops, distinct Game assemblies and a held send gate.
    static async Task UtilityAcrossGameAssemblies(Harness h)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        var previousLogic = Program.Logic; var previousRules = Game.CombatLifetime; var previousSchedule = Game.PlayerUpdatesSchedule;
        var oldOwner = new ModuleLifetime("Game"); var owner = new ModuleLifetime("Game");
        var context = new AssemblyLoadContext("Whisperer utility handover regression", isCollectible: true);
        context.Resolving += (_, name) => AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.GetName().Name == name.Name);
        var assembly = context.LoadFromAssemblyPath(typeof(Game).Assembly.Location); Assert.NotSame(typeof(Game).Assembly, assembly);
        var game = assembly.GetType("Game")!;
        object? Call(string name, params object?[] args) => game.GetMethods(flags).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(null, args);
        var c = h.NewConnection("utility" + Guid.NewGuid().ToString("N"), optional: false); c.MainAscendancy = new();
        c.PlayerSent = true; c.Wire = ("Whisperer", h.Wire, h.Ciphers); c.AiEnabled = false;
        var runtime = new ManualRuntime(); c.ProjectileRuntimeState = runtime;
        var before = new ManualUpdates(runtime); var after = new ManualUpdates(runtime);
        PlayerUpdatesState? oldState = null; object? currentState = null; Task? currentCompletion = null;
        var gate = Program.SendGates.GetValue(h.Ciphers, _ => new(1, 1)); bool held = false;
        try
        {
            Game.CombatLifetime = oldOwner; Game.PlayerUpdatesSchedule = before; Program.SetGameLogic(new GameServer.Modules.Game.GameLogic(oldOwner));
            Program.ActivatePlayerUpdates(c); oldState = Assert.IsType<PlayerUpdatesState>(c.PlayerUpdatesState); before.Completion = oldState.Completion;
            var oldFast = await before.Next(PlayerUpdateRole.Recovery); await before.Next(PlayerUpdateRole.Player); await before.OtherRoles();
            var type = Program.Items.All.First(t => t.Class == "UtilityFlask" && t.Supported
                && Program.Items.UtilityFlaskOf(t) is { ChargesPerUse: > 0 } u && u.MaxCharges >= 2 * u.ChargesPerUse);
            var utility = Program.Items.UtilityFlaskOf(type)!;
            var plain = ItemCatalog.Create(type);
            var enchanted = Program.Items.Mods.All.Where(m => m.GenerationType == 22 && m.WeightFor(type.Tags) > 0)
                .Select(m => plain with { Mods = [.. plain.Mods, m.Roll(ModSection.Enchant, new Random(1))] })
                .Single(i => FlaskEnchantments.Matches(ItemModStats.Local(i, Program.Items.Mods), FlaskEnchantments.Trigger.End, 0, 0, false));
            var items = Game.ItemsOf(c);
            var one = items.Add(PlayerInventories.Flask, 0, 0, enchanted with { Charges = utility.MaxCharges });
            var two = items.Add(PlayerInventories.Flask, 1, 0, enchanted with { Charges = utility.MaxCharges });
            async Task Accept(InventoryItem flask, double at)
            {
                before.At = runtime.At = at; Game.AreaUpdateContext.Value = oldState;
                try { Assert.True(await Game.UseUtilityFlask("Whisperer", h.Wire, h.Ciphers, c, flask.X, flask, type, utility)); }
                finally { Game.AreaUpdateContext.Value = null; }
            }
            await Accept(one, 1); await Accept(two, 3);
            var first = c.UtilityFlaskEffects.Of(one.Id)!; var second = c.UtilityFlaskEffects.Of(two.Id)!;
            Assert.Equal(1, first.Started); Assert.Equal(3, second.Started);
            var cost = FlaskCharges.Of(utility.Charges, ItemModStats.Local(enchanted, Program.Items.Mods)).ChargesPerUse;
            int Charges(uint id) => items.Items(PlayerInventories.Flask).Single(f => f.Id == id).Item.Charges;
            Assert.Equal(utility.MaxCharges - cost, Charges(one.Id)); Assert.Equal(utility.MaxCharges - cost, Charges(two.Id));
            // Existing clocks freeze on pause; an unchanged clock must not shorten the accepted effect.
            double halfway = (first.Started + first.Ends) / 2;
            oldFast.Advance(halfway); oldFast = await before.Next(PlayerUpdateRole.Recovery);
            c.Clock.Stop(); oldFast.Advance(halfway); oldFast = await before.Next(PlayerUpdateRole.Recovery); c.Clock.Start();
            Assert.Same(first, c.UtilityFlaskEffects.Of(one.Id)); Assert.Same(second, c.UtilityFlaskEffects.Of(two.Id));
            Assert.DoesNotContain(c.FlaskAutomation.Events, e => e.Trigger == FlaskEnchantments.Trigger.End);
            // Related stat-dependent ground expiry is owned too; establish its published smoke/consecration state.
            c.Walk.Teleport(100, 100, halfway);
            var monster = new SpawnedMonster("flask smoke", new(100, 100, 0), 0, 0, 1000); c.Monsters[44] = monster;
            c.FlaskGrounds.Add(new(FlaskGrounds.Kind.SmokeCloud, 100, 100, 10, second.Ends));
            c.FlaskGrounds.Add(new(FlaskGrounds.Kind.ConsecratedGround, 100, 100, 10, second.Ends));
            Game.AdvanceFlaskGrounds(c, oldState); Assert.Equal(80, c.FlaskGrounds.Accuracy(44, 100)); Assert.NotEmpty(c.FlaskGrounds.PlayerStats());
            await Game.PublishFlaskChanges("Whisperer", h.Wire, h.Ciphers, c);
            h.Wire.Drain(h.Receive); while (h.Wire.Plain.Reader.TryRead(out _)) { }
            await gate.WaitAsync(); held = true;
            oldFast.Advance(first.Ends); await oldFast.Observed.Task;
            lock (c.Monsters)
            {
                Assert.Null(c.UtilityFlaskEffects.Of(one.Id)); Assert.Same(second, c.UtilityFlaskEffects.Of(two.Id));
                var edge = Assert.Single(c.FlaskAutomation.Events, e => e.Trigger == FlaskEnchantments.Trigger.End);
                Assert.Equal(new(FlaskEnchantments.Trigger.End, one.Id, one.X, first.Activation), edge);
                Assert.NotEmpty(c.FlaskAutomation.Packets);
            }
            // Stop without releasing the old output. The other flask becomes due in the no-current-Game gap.
            oldOwner.Stop(ex => throw ex); before.At = runtime.At = second.Ends + .25;
            int receipts = c.FlaskAutomation.Packets.Count; long revision = c.FlaskAutomation.StatRevision;
            Game.AdvanceUtilityFlasks(c, oldState); Game.AdvanceFlaskGrounds(c, oldState);
            var oldPublish = Game.PublishFlaskChanges("Whisperer", h.Wire, h.Ciphers, c);
            Assert.True(oldPublish.IsCompleted); await oldPublish;
            Assert.Same(second, c.UtilityFlaskEffects.Of(two.Id)); Assert.Equal(receipts, c.FlaskAutomation.Packets.Count);
            Assert.Equal(revision, c.FlaskAutomation.StatRevision); Assert.NotEmpty(c.FlaskGrounds.PlayerStats());
            after.At = runtime.At;
            game.GetField("CombatLifetime", flags)!.SetValue(null, owner); game.GetField("PlayerUpdatesSchedule", flags)!.SetValue(null, after);
            Program.SetGameLogic((IGameLogic)Activator.CreateInstance(assembly.GetType("GameServer.Modules.Game.GameLogic")!, flags, null, [owner], null)!);
            currentState = c.PlayerUpdatesState!; Assert.NotSame(oldState, currentState);
            currentCompletion = (Task)currentState.GetType().GetField("Completion", flags)!.GetValue(currentState)!; after.Completion = currentCompletion;
            var fast = await after.Next(PlayerUpdateRole.Recovery);
            Assert.Same(second, c.UtilityFlaskEffects.Of(two.Id)); Assert.Equal(second.Ends, c.UtilityFlaskEffects.Of(two.Id)!.Ends);
            fast.Advance(after.At); await fast.Observed.Task;
            lock (c.Monsters)
            {
                Assert.Null(c.UtilityFlaskEffects.Of(two.Id));
                Assert.Equal(2, c.FlaskAutomation.Events.Count(e => e.Trigger == FlaskEnchantments.Trigger.End));
            }
            gate.Release(); held = false;
            await oldState.Completion; fast = await after.Next(PlayerUpdateRole.Recovery);
            await after.Next(PlayerUpdateRole.Player);
            foreach (var role in new[] { PlayerUpdateRole.MonsterAi, PlayerUpdateRole.MonsterRegeneration, PlayerUpdateRole.LivingCollisions,
                PlayerUpdateRole.Charges, PlayerUpdateRole.Fortification, PlayerUpdateRole.Rage }) await after.Next(role);
            var drain = await after.Next(PlayerUpdateRole.FlaskCharges); var ground = await after.Next(PlayerUpdateRole.FlaskGrounds);
            // Actual replacement ground producer removes expired smoke/consecration and publishes current state.
            ground.Advance(after.At); await after.Next(PlayerUpdateRole.FlaskGrounds);
            Assert.Equal(100, c.FlaskGrounds.Accuracy(44, 100)); Assert.Empty(c.FlaskGrounds.PlayerStats());
            drain.Advance(after.At); drain = await after.Next(PlayerUpdateRole.FlaskCharges);
            var reusedOne = c.UtilityFlaskEffects.Of(one.Id)!; var reusedTwo = c.UtilityFlaskEffects.Of(two.Id)!;
            Assert.NotNull(reusedOne); Assert.NotNull(reusedTwo);
            Assert.Equal(after.At, reusedOne.Started); Assert.Equal(first.Ends - first.Started, reusedOne.Ends - reusedOne.Started, 8);
            Assert.Equal(second.Ends - second.Started, reusedTwo.Ends - reusedTwo.Started, 8);
            Assert.Equal(utility.MaxCharges - 2 * cost, Charges(one.Id)); Assert.Equal(utility.MaxCharges - 2 * cost, Charges(two.Id));
            Assert.True(reusedOne.Activation > first.Activation); Assert.True(reusedTwo.Activation > second.Activation);
            Assert.DoesNotContain(c.FlaskAutomation.Events, e => e.Trigger == FlaskEnchantments.Trigger.End);
            // A late old completion/expiry and a replayed old activation cannot remove/reuse the new effect.
            Game.AdvanceUtilityFlasks(c, oldState); Game.AdvanceFlaskGrounds(c, oldState);
            c.PlayerUpdatesWorker.Complete(oldOwner, null, oldState, oldState.Generation);
            c.FlaskAutomation.Enqueue(new(FlaskEnchantments.Trigger.End, one.Id, one.X, first.Activation));
            drain.Advance(after.At); drain = await after.Next(PlayerUpdateRole.FlaskCharges);
            fast.Advance(after.At); fast = await after.Next(PlayerUpdateRole.Recovery);
            Assert.Same(reusedOne, c.UtilityFlaskEffects.Of(one.Id)); Assert.Same(reusedTwo, c.UtilityFlaskEffects.Of(two.Id));
            Assert.Equal(utility.MaxCharges - 2 * cost, Charges(one.Id)); Assert.True((bool)Call("PlayerUpdatesCurrent", c, currentState)!);
            Assert.Equal(1, after.Count(PlayerUpdateRole.Player)); Assert.Equal(1, after.Count(PlayerUpdateRole.MonsterAi));
            var sent = new List<byte>(); while (h.Wire.Plain.Reader.TryRead(out var packet)) sent.AddRange(packet);
            int Occurrences(byte[] needle) { int count = 0, at = 0; while (at <= sent.Count - needle.Length)
                { int found = sent.ToArray().AsSpan(at).IndexOf(needle); if (found < 0) break; count++; at += found + needle.Length; } return count; }
            Assert.Equal(1, Occurrences(InstanceEntities.RemoveBuff(Program.PlayerId, first.Buff)));
            Assert.Equal(1, Occurrences(InstanceEntities.RemoveBuff(Program.PlayerId, second.Buff)));
            // An End edge cannot follow an item to another cell or survive removal of its triggering enchant.
            items.Change(PlayerInventories.Flask, one.Id, i => i with { Charges = utility.MaxCharges });
            items.Change(PlayerInventories.Flask, two.Id, i => i with { Charges = utility.MaxCharges });
            fast.Advance(Math.Max(reusedOne.Ends, reusedTwo.Ends) + .25); fast = await after.Next(PlayerUpdateRole.Recovery);
            Assert.Equal(2, c.FlaskAutomation.Events.Count(e => e.Trigger == FlaskEnchantments.Trigger.End));
            var moved = items.Remove(PlayerInventories.Flask, one.Id)!; items.Put(PlayerInventories.Flask, moved with { X = 2 });
            items.Change(PlayerInventories.Flask, two.Id, i => i with { Mods = [.. i.Mods.Where(m => m.Section != ModSection.Enchant)] });
            drain.Advance(after.At); drain = await after.Next(PlayerUpdateRole.FlaskCharges);
            Assert.Equal(utility.MaxCharges, Charges(one.Id)); Assert.Equal(utility.MaxCharges, Charges(two.Id));
            Assert.Null(c.UtilityFlaskEffects.Of(one.Id)); Assert.Null(c.UtilityFlaskEffects.Of(two.Id));
            // Replacing the original item in the same cell also cannot transfer its old activation to a new flask.
            var removed = items.Remove(PlayerInventories.Flask, two.Id)!;
            var replacement = items.Add(PlayerInventories.Flask, two.X, two.Y, enchanted with { Charges = utility.MaxCharges });
            c.FlaskAutomation.Enqueue(new(FlaskEnchantments.Trigger.End, removed.Id, removed.X, reusedTwo.Activation));
            drain.Advance(after.At); await after.Next(PlayerUpdateRole.FlaskCharges);
            Assert.Equal(utility.MaxCharges, Charges(replacement.Id)); Assert.Null(c.UtilityFlaskEffects.Of(replacement.Id));
        }
        finally
        {
            c.Closed = true; oldOwner.Stop(_ => { }); owner.Stop(_ => { }); if (held) gate.Release();
            if (oldState is not null) await oldState.Completion; if (currentCompletion is not null) await currentCompletion;
            Game.CombatLifetime = previousRules; Game.PlayerUpdatesSchedule = previousSchedule; Program.SetGameLogic(previousLogic); context.Unload();
        }
    }

    static List<(uint Key, int Value)> PlayerStatRows(byte[] packet)
    {
        var result = new List<(uint, int)>(); byte[] header = InstanceEntities.StatsUpdate(Program.PlayerId, [])[..12];
        int at = 0;
        while (at < packet.Length && packet.AsSpan(at).IndexOf(header) is int found && found >= 0)
        {
            at += found + header.Length; int count = (int)Read(false);
            for (int i = 0; i < count; i++) result.Add((Read(false), (int)Read(true)));
        }
        return result;
        uint Read(bool signed)
        {
            uint first = packet[at++]; int length = first < 128 ? 1 : first < 192 ? 2 : first < 224 ? 3 : first < 240 ? 4 : 5;
            int bits = length == 5 ? 32 : 8 * length - length;
            uint value = first & (length == 5 ? 0u : (uint)((1 << (8 - length)) - 1));
            for (int i = 1; i < length; i++) value = (value << 8) | packet[at++];
            if (signed && bits < 32 && (value & (1u << (bits - 1))) != 0) value |= uint.MaxValue << bits;
            return value;
        }
    }

    sealed class FixedDraw(int value) : Random { public override int Next(int maxValue) => value; public override double NextDouble() => 0; }
    sealed class HighDraw : Random { public override int Next(int maxValue) => maxValue - 1; public override double NextDouble() => 0; }
    sealed class ManualRuntime : IProjectileRuntime
    {
        public double At; public bool Active { get; private set; } = true;
        public double NowTicks => At * 120000; public void Invalidate() => Active = false;
    }
    sealed class ManualUpdates(ManualRuntime runtime) : IPlayerUpdateSchedule
    {
        readonly ConcurrentDictionary<PlayerUpdateRole, Channel<Frame>> queues = new();
        readonly ConcurrentDictionary<PlayerUpdateRole, int> waits = new();
        readonly ManualRuntime runtime = runtime;
        internal Task? Completion;
        internal double At;
        internal int FastWaits => Count(PlayerUpdateRole.Recovery);
        internal int Count(PlayerUpdateRole role) => waits.GetValueOrDefault(role);
        Frame? observing;
        Channel<Frame> Queue(PlayerUpdateRole role) => queues.GetOrAdd(role, _ => Channel.CreateUnbounded<Frame>());
        internal sealed class Frame(ManualUpdates schedule)
        {
            readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal readonly TaskCompletionSource Observed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal Task Wait(CancellationToken stop) => release.Task.WaitAsync(stop);
            internal void Advance(double at)
            { schedule.At = at; schedule.runtime.At = at; schedule.observing = this; release.SetResult(); }
        }
        public double Now(ClientState conn) { observing?.Observed.TrySetResult(); return At; }
        public Task Wait(int milliseconds, CancellationToken stopping) =>
            Wait(milliseconds == Game.RecoveryStepMs ? PlayerUpdateRole.Recovery : PlayerUpdateRole.Player, milliseconds, stopping);
        public Task Wait(PlayerUpdateRole role, int milliseconds, CancellationToken stopping)
        {
            var frame = new Frame(this); waits.AddOrUpdate(role, 1, (_, n) => n + 1);
            Queue(role).Writer.TryWrite(frame); return frame.Wait(stopping);
        }
        internal Task<Frame> Next(int milliseconds) => Next(milliseconds == Game.RecoveryStepMs ? PlayerUpdateRole.Recovery : PlayerUpdateRole.Player);
        internal async Task<Frame> Next(PlayerUpdateRole role)
        {
            var read = Queue(role).Reader.ReadAsync().AsTask();
            if (Completion is not null && await Task.WhenAny(read, Completion) == Completion)
            { await Completion; throw new InvalidOperationException("Area updates stopped before " + role); }
            return await read;
        }
        internal async Task OtherRoles()
        {
            foreach (var role in new[] { PlayerUpdateRole.MonsterAi, PlayerUpdateRole.MonsterRegeneration,
                PlayerUpdateRole.LivingCollisions, PlayerUpdateRole.Charges, PlayerUpdateRole.Fortification,
                PlayerUpdateRole.Rage, PlayerUpdateRole.FlaskCharges, PlayerUpdateRole.FlaskGrounds }) await Next(role);
        }
    }
    sealed class CaptureStream : NetworkStream
    {
        readonly MemoryStream bytes = new(); readonly Socket peer;
        readonly ConnectionCiphers observer = new(new byte[64], true);
        internal readonly Channel<byte[]> Plain = Channel.CreateUnbounded<byte[]>();
        static (Socket Writer, Socket Peer) Pair()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            var writer = new Socket(SocketType.Stream, ProtocolType.Tcp); writer.Connect(listener.LocalEndpoint);
            return (writer, listener.AcceptSocket());
        }
        public CaptureStream() : this(Pair()) { }
        CaptureStream((Socket Writer, Socket Peer) pair) : base(pair.Writer, ownsSocket: true) => peer = pair.Peer;
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        { bytes.Write(buffer.Span); var plain = buffer.ToArray(); observer.Receive.Process(plain); Plain.Writer.TryWrite(plain); return ValueTask.CompletedTask; }
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
        readonly IPlayerUpdateSchedule oldSchedule = Game.PlayerUpdatesSchedule;
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
        internal (ChatCommands Commands, List<string> Replies) CommandsFor(ClientState connection)
        {
            List<string> replies = [];
            var commands = new ChatCommands { Send = (_, _) => Task.CompletedTask, Reply = text => { replies.Add(text); return Task.CompletedTask; } };
            connection.Commands = commands;
            typeof(GameServer.Modules.M_commands.CommandsModule).Assembly.GetType("CommandsFeature")!
                .GetMethod("Register", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [commands, "Whisperer", Wire, Ciphers, connection]);
            return (commands, replies);
        }
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
            foreach (var c in connections) { c.Closed = true; c.RetirePlayerUpdates(); c.RetireResourceEffects(); c.RetireOwnedCombat(); PlayerInventories.Forget(c.Character!); }
            registration.Dispose(); Provider.Stop(_ => { }); Rules.Stop(_ => { }); Wire.Dispose();
            Game.CombatLifetime = oldRules; Game.PlayerUpdatesSchedule = oldSchedule;
            Program.SetGameLogic(oldLogic!); Environment.SetEnvironmentVariable(Flag, oldFlag);
            Saves.Db.Dispose(); foreach (string suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }
    }
}
