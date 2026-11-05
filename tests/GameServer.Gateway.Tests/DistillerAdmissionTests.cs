using GameServer.Core.World;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.Projectiles;
namespace GameServer.Gateway.Tests;

public sealed class DistillerAdmissionTests
{
    public Task Default_melee_and_ranged_pay_once(bool melee) => OrdinaryGameplayChecks.DistillerDefault(melee);
    public void Additional_costs_are_validated_and_debited_once() => OrdinaryGameplayChecks.DistillerAdditionalCosts();
    public Task Pickup_waits_for_the_resource_and_charge_batch() => OrdinaryGameplayChecks.DistillerPickupRace();
    public Task Retained_paid_revision_precedes_actual_pickup_removal() => OrdinaryGameplayChecks.DistillerPickupWireOrder();
    public Task Targeted_attack_and_pickup_share_ordered_wire_revisions() => OrdinaryGameplayChecks.DistillerTargetedPickupWireOrder();
    public Task Projectile_flask_output_orders_pickup_placement_and_swap(string operation,bool duringMutation) =>
        OrdinaryGameplayChecks.ProjectileInventoryWireOrder(operation,duringMutation);
    public Task Gate_wait_death_or_epoch_change_spends_nothing(bool death) => OrdinaryGameplayChecks.DistillerWait(death);
    public Task Vaal_and_channel_policy_and_Alchemist_admission() => OrdinaryGameplayChecks.DistillerVaalAndFlask();
    public Task Multistrike_uses_one_payment_across_targets() => OrdinaryGameplayChecks.DistillerRepeats();
    public Task Actual_gem_payment_and_wait_invalidation() => OrdinaryGameplayChecks.DistillerActual();
    public void Batch_pickup_and_resource_rejection_are_atomic() => OrdinaryGameplayChecks.DistillerBatch();
    public void Paid_release_survives_refund_without_panel_or_future_use_leak() => OrdinaryGameplayChecks.DistillerRelease();
}

static partial class OrdinaryGameplayChecks
{
    internal static async Task ProjectileInventoryWireOrder(string operation,bool duringMutation)
    {
        using var h = DistillerHarness(); var c = h.Conn;
        c.PlayerStats[Key("recharge_flasks_on_crit")] = 1;
        var flask = h.Items.Items(PlayerInventories.Flask)[0];
        InventoryItem? incoming = null;
        if (operation == "place") Assert.NotNull(h.Items.PickUp(PlayerInventories.Flask,flask.Id));
        if (operation == "swap") incoming = h.Items.Add(PlayerInventories.Cursor,0,0,
            ItemCatalog.Create(Program.Items.Find("Quartz Flask")!) with { Charges=10 });
        var clientBar = h.Items.Items(PlayerInventories.Flask).Select(f=>f.Id).ToHashSet();
        uint gained = h.Items.Items(PlayerInventories.Flask)[0].Id;
        var session = new Game.ProjectileSession("delayed projectile", h.Wire, h.Ciphers, c);
        c.ProjectileRuntimeState = session;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Enqueue(async () => { entered.SetResult(); await release.Task; });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var target = new SpawnedMonster("flask crit", new(102,100,0), 0,0,100000);
            c.Monsters[808] = target;
            var shot = new ProjectileOffense(DamagePacket.Of(DamageType.Physical,10,10),true,0,10000,150,"flask crit")
                { CriticalRoll = new(0) };
            using (Game.ProjectileMutation(c, advance:false))
            {
                session.Runtime.Release(new(new("flask crit") { Speed=1, Range=10 },new(Program.PlayerId),new(Program.PlayerId),1,
                    new(100,100),new(110,100)) { Payload=shot });
                var entity = Assert.Single(session.Runtime.Projectiles);
                void Contact() => Assert.Equal(ProjectileHitResult.Hit,
                    session.Hit(new(entity,session.ActorKey(808,target),session.Runtime.Now,"direct",new(102,100))));
                if (duringMutation) session.Runtime.Schedule(0,Contact);
                else Contact();
            }
            // In the scheduled case the initial flask drain sees no gain: Items' own mutation must advance it.
            Assert.Equal(duringMutation ? 10 : 11,h.Items.Items(PlayerInventories.Flask).Single(f=>f.Id==gained).Item.Charges);
            uint inventory = PlayerInventories.InventoryId(PlayerInventories.Flask);
            if (operation == "pickup") await Game.PickUpItem("delayed pickup",h.Wire,h.Ciphers,c,new(inventory,flask.Id,0,0,0));
            else if (operation == "place") await Game.PlaceItem("delayed place",h.Wire,h.Ciphers,c,new(inventory,0,0,0,0,0,0));
            else await Game.HeldOntoItem("delayed swap",h.Wire,h.Ciphers,c,new(inventory,flask.Id,0,0,0),"");
            session.Enqueue(() => { drained.SetResult(); return Task.CompletedTask; });
            release.TrySetResult(); await drained.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var packets=Program.HistoryFor(h.Ciphers).Lines().Where(l=>l.Contains("=> 0x00bf"))
                .Select(l=>Convert.FromHexString(l.Split(" B ")[1].TrimEnd('…'))).ToArray();
            uint U32(byte[] p,int offset)=>System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(p.AsSpan(offset,4));
            var barPackets=packets.Where(p=>U32(p,3)==inventory).ToArray();
            Assert.Equal(2,barPackets.Length); Assert.True(U32(barPackets[0],7)<U32(barPackets[1],7),operation);
            foreach (var p in barPackets)
            {
                int removed=(int)U32(p,11), offset=15;
                for (int i=0;i<removed;i++,offset+=4) clientBar.Remove(U32(p,offset));
                uint added=U32(p,offset); Assert.InRange(added,0u,1u);
                if (added==1) clientBar.Add(U32(p,offset+4));
            }
            Assert.True(clientBar.SetEquals(h.Items.Items(PlayerInventories.Flask).Select(f=>f.Id)),operation);
            if (operation == "place")
            {
                Assert.Empty(h.Items.Items(PlayerInventories.Cursor));
                Assert.Contains(h.Items.Items(PlayerInventories.Flask),f=>f.Id==flask.Id);
                Assert.Equal(11,h.Items.Items(PlayerInventories.Flask).Single(f=>f.Id==gained).Item.Charges);
            }
            else
            {
                Assert.DoesNotContain(h.Items.Items(PlayerInventories.Flask),f=>f.Id==flask.Id);
                var held=Assert.Single(h.Items.Items(PlayerInventories.Cursor)); Assert.Equal(flask.Id,held.Id); Assert.Equal(11,held.Item.Charges);
                if (incoming is not null) Assert.Contains(h.Items.Items(PlayerInventories.Flask),f=>f.Id==incoming.Id);
            }
            Assert.True(target.Life < target.MaxLife);
        }
        finally { release.TrySetResult(); session.Invalidate(); await session.OutputCompleted; }
    }
    internal static async Task DistillerTargetedPickupWireOrder()
    {
        using var h = DistillerHarness(); var c = h.Conn;
        foreach (var old in h.Items.Items(PlayerInventories.Weapon1)) h.Items.Remove(PlayerInventories.Weapon1, old.Id);
        h.Items.Add(PlayerInventories.Weapon1, 0, 0, ItemCatalog.Create(Program.Items.Find("Rusted Sword")!));
        c.Walk.Teleport(100, 100, Game.ProjectileWorldTime(c));
        var target = new SpawnedMonster("pickup race", new(102, 100, 0), 0, 0, 100000) { Hostile = true };
        c.Monsters[808] = target;
        var flask = h.Items.Items(PlayerInventories.Flask)[0];
        var sendGate = Program.SendGates.GetValue(h.Ciphers, _ => new SemaphoreSlim(1, 1));
        await sendGate.WaitAsync();
        Task attack = Task.CompletedTask, pickup = Task.CompletedTask;
        try
        {
            attack = Game.Attack("targeted pickup race", h.Wire, h.Ciphers, c,
                new InstanceTransfer.UseSkillOnObject(808, SkillCatalog.FirstSkillId, 1, 0), target, c.Clock.Elapsed.TotalSeconds);
            // The real attack has committed, but cannot publish its retained revision yet.
            Assert.False(attack.IsCompleted);
            Assert.All(h.Items.Items(PlayerInventories.Flask), f => Assert.Equal(7, f.Item.Charges));
            pickup = Game.PickUpItem("concurrent pickup", h.Wire, h.Ciphers, c,
                new InstanceItems.PickUpItem(PlayerInventories.InventoryId(PlayerInventories.Flask), flask.Id, 0, 0, 0));
            Assert.False(pickup.IsCompleted);
            Assert.DoesNotContain(Program.HistoryFor(h.Ciphers).Lines(), l => l.Contains("=> 0x00bf"));
        }
        finally { sendGate.Release(); }
        await Task.WhenAll(attack, pickup).WaitAsync(TimeSpan.FromSeconds(10));
        await Game.PublishFlaskChanges("final drain", h.Wire, h.Ciphers, c);
        var packets = Program.HistoryFor(h.Ciphers).Lines().Where(l => l.Contains("=> 0x00bf"))
            .Select(l => Convert.FromHexString(l.Split(" B ")[1].TrimEnd('…'))).ToArray();
        uint U32(byte[] p, int offset) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(p.AsSpan(offset, 4));
        var revisions = packets.Where(p => U32(p, 3) == PlayerInventories.InventoryId(PlayerInventories.Flask))
            .Select(p => U32(p, 7)).ToArray();
        Assert.Equal(2, revisions.Length);
        Assert.True(revisions[0] < revisions[1]);
        Assert.DoesNotContain(h.Items.Items(PlayerInventories.Flask), f => f.Id == flask.Id);
        var held = Assert.Single(h.Items.Items(PlayerInventories.Cursor));
        Assert.Equal(flask.Id, held.Id); Assert.Equal(7, held.Item.Charges);
        Assert.All(h.Items.Items(PlayerInventories.Flask), f => Assert.Equal(7, f.Item.Charges));
        Assert.Equal(1000u, c.PlayerMana);
        Assert.True(target.Life < target.MaxLife);
    }

    internal static async Task DistillerPickupWireOrder()
    {
        using var h=DistillerHarness(); var c=h.Conn;
        var flask=h.Items.Items(PlayerInventories.Flask)[0];
        // Stop at the targeted admission's shared production transaction, before its asynchronous publisher runs.
        await c.FlaskBarGate.WaitAsync();
        try { using (Game.ProjectileMutation(c)) Assert.True(Game.PayDistillerUseLocked(c,true,()=>true)); }
        finally { c.FlaskBarGate.Release(); }
        await Game.PickUpItem("paid pickup",h.Wire,h.Ciphers,c,new InstanceItems.PickUpItem(PlayerInventories.InventoryId(PlayerInventories.Flask),flask.Id,0,0,0));
        await Game.PublishFlaskChanges("late attack publisher",h.Wire,h.Ciphers,c);
        var packets=Program.HistoryFor(h.Ciphers).Lines().Where(l=>l.Contains("=> 0x00bf")).Select(l=>
            Convert.FromHexString(l.Split(" B ")[1].TrimEnd('…'))).ToArray();
        uint U32(byte[] p,int offset)=>System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(p.AsSpan(offset,4));
        var revisions=packets.Where(p=>U32(p,3)==PlayerInventories.InventoryId(PlayerInventories.Flask)).Select(p=>U32(p,7)).ToArray();
        Assert.Equal(2,revisions.Length); Assert.True(revisions[0]<revisions[1]);
        Assert.DoesNotContain(h.Items.Items(PlayerInventories.Flask),f=>f.Id==flask.Id);
        var held=Assert.Single(h.Items.Items(PlayerInventories.Cursor)); Assert.Equal(flask.Id,held.Id); Assert.Equal(7,held.Item.Charges);
        Assert.All(h.Items.Items(PlayerInventories.Flask),f=>Assert.Equal(7,f.Item.Charges));
    }
    static Harness DistillerHarness()
    {
        var h = new Harness(new AscendancyCatalog().Definitions.Single(d => d.Identity.Family == AscendancyFamily.Normal && d.Name == "Pathfinder"));
        var c = h.Conn;
        c.PlayerStats[Key(OrdinaryPathfinderRules.DistillCost)] = 3;
        c.PlayerStats[Key("pathfinder_skills_penetrate_elemental_resistances_%_if_charges_consumed_from_bismuth_flask")] = 25;
        c.PlayerStats[Key("pathfinder_skills_critical_strike_chance_+%_if_charges_consumed_from_diamond_flask")] = 250;
        c.PlayerStats[Key("pathfinder_skills_physical_damage_%_to_add_as_chaos_if_charges_consumed_from_amethyst_flask")] = 37;
        foreach (var f in h.Items.Items(PlayerInventories.Flask)) h.Items.Remove(PlayerInventories.Flask, f.Id);
        byte slot = 0;
        foreach (string suffix in new[] { "15", "1", "7" })
            h.Items.Add(PlayerInventories.Flask, slot++, 0, ItemCatalog.Create(Program.Items.Find("Metadata/Items/Flasks/FlaskUtility" + suffix)!) with { Charges = 10 });
        return h;
    }

    internal static void DistillerAdditionalCosts()
    {
        using var h = DistillerHarness(); var c = h.Conn; var gem = h.Fireball;
        using var scope = Game.BeginOrdinaryUse(c);
        bool Pay(uint mana, uint life, bool current) => Game.PayDistillerUseLocked(c,true,
            () => Game.PayMana(c,gem.Id,out _,out _,extraMana:mana,extraLife:life,validateAdditionalCost:() => current));
        Assert.False(Pay(10,10,false)); Assert.Equal(1000u,c.PlayerMana); Assert.Equal(1000u,c.PlayerLife);
        Assert.False(Pay(uint.MaxValue,0,true)); Assert.Equal(1000u,c.PlayerMana);
        Assert.False(Pay(0,1000,true)); Assert.Equal(1000u,c.PlayerLife);
        Assert.All(h.Items.Items(PlayerInventories.Flask), f => Assert.Equal(10,f.Item.Charges));
        uint baseCost = (uint)Game.SupportedStats(c,gem.Id)[Key("mana_cost")];
        Assert.True(Pay(10,20,true));
        Assert.Equal(1000u-baseCost-10,c.PlayerMana); Assert.Equal(980u,c.PlayerLife);
        Assert.Equal(baseCost+10,scope.Distillation!.Mana); Assert.Equal(20u,scope.Distillation.Life);
        Assert.All(h.Items.Items(PlayerInventories.Flask), f => Assert.Equal(7,f.Item.Charges));
    }

    internal static async Task DistillerDefault(bool melee)
    {
        using var h = DistillerHarness(); var c = h.Conn;
        if (melee)
        {
            foreach (var old in h.Items.Items(PlayerInventories.Weapon1)) h.Items.Remove(PlayerInventories.Weapon1,old.Id);
            h.Items.Add(PlayerInventories.Weapon1,0,0,ItemCatalog.Create(Program.Items.Find("Rusted Sword")!));
        }
        c.Walk.Teleport(100,100,Game.ProjectileWorldTime(c));
        var target = new SpawnedMonster("default",new(102,100,0),0,0,100000) { Hostile = true };
        c.Monsters[808] = target;
        await Game.Attack("Distiller default",h.Wire,h.Ciphers,c,
            new GameServer.Core.Protocol.Instance.InstanceTransfer.UseSkillOnObject(808,SkillCatalog.FirstSkillId,1,0),target,c.Clock.Elapsed.TotalSeconds);
        Assert.All(h.Items.Items(PlayerInventories.Flask), f => Assert.Equal(7,f.Item.Charges));
        Assert.Equal(1000u,c.PlayerMana);
        if (melee) Assert.True(target.Life < target.MaxLife);
    }

    internal static async Task DistillerPickupRace()
    {
        using var h = DistillerHarness(); var c = h.Conn;
        var bar = h.Items.Items(PlayerInventories.Flask).ToArray();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pickupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var payment = Task.Run(() =>
        {
            using (Game.ProjectileMutation(c))
                return h.Items.TryPaySkillFlasks(bar.Select(f => (f,3)).ToArray(), () =>
                {
                    entered.SetResult();
                    if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("batch release");
                    c.PlayerMana--; return true;
                }, out _, out _);
        });
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var pickup = Task.Run(() => { pickupStarted.SetResult(); return h.Items.Remove(PlayerInventories.Flask,bar[0].Id); });
        try
        {
            await pickupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pickup.IsCompleted);
        }
        finally { release.Set(); }
        Assert.True(await payment);
        Assert.Equal(7,(await pickup)!.Item.Charges);
        Assert.Equal(999u,c.PlayerMana);
        Assert.All(h.Items.Items(PlayerInventories.Flask), f => Assert.Equal(7,f.Item.Charges));
    }


    internal static async Task DistillerWait(bool death)
    {
        using var h = DistillerHarness(); var c = h.Conn; var gem = h.Fireball;
        await c.FlaskBarGate.WaitAsync();
        var waiting = Game.CastGemSkill("stale Distiller", h.Wire, h.Ciphers, c, gem.Id,(100,100),(150,100));
        Assert.False(waiting.IsCompleted);
        if (death) c.PlayerLife = 0; else c.ProjectileEpoch++;
        c.FlaskBarGate.Release();
        Assert.False(await waiting); Assert.Equal(1000u,c.PlayerMana);
        Assert.All(h.Items.Items(PlayerInventories.Flask), f => Assert.Equal(10,f.Item.Charges));
    }

    internal static async Task DistillerVaalAndFlask()
    {
        using var h = DistillerHarness(); var c = h.Conn;
        int cyclone = Game.SkillsFor(c).EffectRow("Cyclone");
        Assert.False(Game.DistillerEligible(c, cyclone, 1, 0));
        foreach (var old in h.Items.Items(PlayerInventories.Weapon1)) h.Items.Remove(PlayerInventories.Weapon1, old.Id);
        var vaalGem = ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SkillGemVaalGroundSlam")!);
        h.Items.Add(PlayerInventories.Weapon1,0,0, ItemCatalog.Create(Program.Items.Find("Driftwood Club")!) with
            { Sockets = [new(SocketColour.White, vaalGem)], Links = [1] });
        c.PlayerStats[Key("virtual_strength_for_requirements")] = 1000;
        c.PlayerStats[Key("no_cost")] = 1;
        var gem = Game.GemSkillEffects(c).Single(g => Game.SkillsFor(c).EffectId(g.Effect) == "VaalGroundSlam");
        Assert.True(Game.DistillerEligible(c, gem.Effect, gem.Level, gem.Quality));
        Assert.True((await Game.BeginSkillCooldownUseAsync(c, gem.Id, gem.Effect, null, false, () => c.ActionCount++)).Accepted);
        Assert.All(h.Items.Items(PlayerInventories.Flask), f => Assert.Equal(7, f.Item.Charges));
        Assert.Equal(3, c.VaalUse!.Distillation!.Witnesses.Length);
        Assert.True(await Game.CastGemSkill("Vaal Distiller", h.Wire, h.Ciphers, c, gem.Id,(100,100),(102,100), actionToken:c.ActionCount));
        Assert.False(await Game.CastGemSkill("duplicate Vaal", h.Wire, h.Ciphers, c, gem.Id,(100,100),(102,100), actionToken:c.ActionCount));
        Assert.All(h.Items.Items(PlayerInventories.Flask), f => Assert.Equal(7, f.Item.Charges));
        c.PlayerStats[Key(OrdinaryPathfinderRules.Preserve)] = 100;
        c.PlayerStats[Key(OrdinaryPathfinderRules.Dispel)] = 1;
        double now = Game.ResourceEffectsTime(c);
        var stats = Game.CharacterPlayerStats(c);
        c.ElementalAilments.Inflict(PlayerElementalAilments.Kind.Shock,20,10,now,n => stats.GetValueOrDefault(Key(n)),new(1));
        Assert.Equal(20,c.ElementalAilments.Magnitude(PlayerElementalAilments.Kind.Shock,now));
        var flask = h.Items.Items(PlayerInventories.Flask)[0];
        Assert.NotNull(await Game.SpendFlask("Alchemist",h.Wire,h.Ciphers,c,flask,new(0,0,4,7,50),0));
        Assert.Equal(7,h.Items.Items(PlayerInventories.Flask)[0].Item.Charges);
        Assert.Equal(0,c.ElementalAilments.Magnitude(PlayerElementalAilments.Kind.Shock,now));
        c.ElementalAilments.Inflict(PlayerElementalAilments.Kind.Shock,20,10,now,n => stats.GetValueOrDefault(Key(n)),new(1));
        h.Items.Change(PlayerInventories.Flask,flask.Id,i => i with { Charges = 6 });
        Assert.Null(await Game.SpendFlask("Alchemist refused",h.Wire,h.Ciphers,c,flask,new(0,0,4,7,50),0));
        Assert.Equal(6,h.Items.Items(PlayerInventories.Flask)[0].Item.Charges);
        Assert.Equal(20,c.ElementalAilments.Magnitude(PlayerElementalAilments.Kind.Shock,now));
    }

    internal static async Task DistillerRepeats()
    {
        using var h = DistillerHarness(); var c = h.Conn;
        foreach (var old in h.Items.Items(PlayerInventories.Weapon1)) h.Items.Remove(PlayerInventories.Weapon1, old.Id);
        var cleave = ItemCatalog.Create(Program.Items.Find("Cleave")!);
        var multistrike = ItemCatalog.Create(Program.Items.Find("Multistrike Support")!);
        h.Items.Add(PlayerInventories.Weapon1, 0, 0, ItemCatalog.Create(Program.Items.Find("Rusted Sword")!) with
            { Sockets = [new(SocketColour.White, cleave), new(SocketColour.White, multistrike)], Links = [2] });
        c.PlayerStats[Key("virtual_strength_for_requirements")] = 1000;
        c.PlayerStats[Key("virtual_dexterity_for_requirements")] = 1000;
        c.PlayerStats[Key("base_chance_to_poison_on_hit_%")] = 100;
        c.PlayerStats[Key("global_always_hit")] = 1;
        c.Walk.Teleport(100,100,0);
        var gem = Game.GemSkillEffects(c).Single(g => Game.SkillsFor(c).EffectId(g.Effect) == "Cleave");
        Assert.Equal(2, Game.MeleeAttackRepeats(c, gem.Id));
        c.Monsters[501] = new("repeat A", new(102,100,0), 0,0,100000) { Hostile = true };
        c.Monsters[502] = new("repeat B", new(103,100,0), 0,0,100000) { Hostile = true };
        Assert.True(await Game.CastGemSkill("Distiller repeats", h.Wire, h.Ciphers, c, gem.Id, (100,100),(102,100)));
        Assert.All(h.Items.Items(PlayerInventories.Flask), f => Assert.Equal(7, f.Item.Charges));
        Assert.All(c.Monsters.Values, m => { Assert.True(m.Life < m.MaxLife); Assert.True(m.Poisons.Stacks(Game.ProjectileWorldTime(c)) >= 3); });
    }

    internal static async Task DistillerActual()
    {
        using var h = DistillerHarness(); var c = h.Conn; var gem = h.Fireball;
        uint mana = c.PlayerMana;
        Assert.True(await Game.CastGemSkill("Distiller", h.Wire, h.Ciphers, c, gem.Id, (100,100), (150,100)));
        Assert.All(h.Items.Items(PlayerInventories.Flask), f => Assert.Equal(7, f.Item.Charges));
        Assert.True(c.PlayerMana < mana);
        c.PlayerMana = 0;
        Assert.False(await Game.CastGemSkill("Distiller", h.Wire, h.Ciphers, c, gem.Id, (100,100), (150,100)));
        Assert.All(h.Items.Items(PlayerInventories.Flask), f => Assert.Equal(7, f.Item.Charges));
        c.PlayerMana = 1000;
        await c.FlaskBarGate.WaitAsync();
        var waiting = Game.CastGemSkill("Distiller", h.Wire, h.Ciphers, c, gem.Id, (100,100), (150,100));
        Assert.False(waiting.IsCompleted);
        c.LeavingArea = true; c.FlaskBarGate.Release();
        Assert.False(await waiting);
        Assert.Equal(1000u, c.PlayerMana);
        Assert.All(h.Items.Items(PlayerInventories.Flask), f => Assert.Equal(7, f.Item.Charges));
    }

    internal static void DistillerBatch()
    {
        using var h = DistillerHarness(); var c = h.Conn;
        var bar = h.Items.Items(PlayerInventories.Flask).ToArray(); int debits = 0;
        Assert.False(h.Items.TryPaySkillFlasks(bar.Select(f => (f,3)).ToArray(), () => false, out var changed, out var revision));
        Assert.Empty(changed); Assert.Equal(0u, revision);
        h.Items.Remove(PlayerInventories.Flask, bar[1].Id); // pickup bypasses FlaskBarGate
        Assert.False(h.Items.TryPaySkillFlasks(bar.Select(f => (f,3)).ToArray(), () => { debits++; return true; }, out changed, out revision));
        Assert.Equal(0, debits); Assert.All(h.Items.Items(PlayerInventories.Flask), f => Assert.Equal(10, f.Item.Charges));
        h.Items.Put(PlayerInventories.Flask, bar[1]);
        uint previous = h.Items.NextRevision(PlayerInventories.Flask);
        Assert.True(h.Items.TryPaySkillFlasks(bar.Select(f => (f,3)).ToArray(), () => { debits++; return true; }, out changed, out revision));
        Assert.Equal(1, debits); Assert.Equal(previous + 1, revision); Assert.Equal(3, changed.Count);
        Assert.False(h.Items.TryPaySkillFlasks(bar.Select(f => (f,3)).ToArray(), () => { debits++; return true; }, out _, out _));
        Assert.Equal(1, debits);
    }

    internal static void DistillerRelease()
    {
        using var h = DistillerHarness(); var c = h.Conn; var gem = h.Fireball;
        var session = Game.ProjectilesFor("Distiller", h.Wire, h.Ciphers, c);
        c.PlayerStats[Key("spell_minimum_added_physical_damage")] = 100;
        c.PlayerStats[Key("spell_maximum_added_physical_damage")] = 100;
        c.PlayerStats[Key("base_chance_to_poison_on_hit_%")] = 100;
        var baseline = Game.CaptureGemProjectile(c, session, gem.Id, gem, (150,100), Game.CaptureSkillCriticalRoll(c, gem.Id));
        using var paid = Game.BeginOrdinaryUse(c);
        Assert.True(Game.PayDistillerUseLocked(c, true, () => Game.PayMana(c, gem.Id, out _, out _)));
        var capture = Game.CaptureOrdinaryRelease(c, () => Game.CaptureGemProjectile(c, session, gem.Id, gem, (150,100), Game.CaptureSkillCriticalRoll(c, gem.Id)));
        c.PlayerStats[Key(OrdinaryPathfinderRules.DistillCost)] = 0;
        c.PlayerStats[Key("pathfinder_skills_critical_strike_chance_+%_if_charges_consumed_from_diamond_flask")] = 0;
        // A panel read in a paid flow sees ordinary inputs, without consuming charges or including paid offense.
        Assert.Equal(0, Game.SupportedHitInputsFor(c, gem.Id)!.Get("physical_damage_%_to_add_as_chaos"));
        var shot = Assert.IsType<ProjectileOffense>(capture().Payload);
        var original = Assert.IsType<ProjectileOffense>(baseline.Payload);
        Assert.True(shot.CriticalChance.Project() > original.CriticalChance.Project());
        Assert.Equal(shot.Hit[DamageType.Physical].Min * .37, shot.Hit[DamageType.Chaos].Min, 6);
        Assert.True(shot.Poison.Min > original.Poison.Min);
        var target = new SpawnedMonster("penetration", new(110,100,0),0,0,100000);
        foreach (var type in new[] { DamageType.Fire, DamageType.Cold, DamageType.Lightning })
            Assert.Equal(125, Game.MonsterHitDamage(c, target, DamagePacket.Of(type,100,100),false,false,0,default,shot.Penetration).Damage);
        Assert.Equal(100, Game.MonsterHitDamage(c,target,DamagePacket.Of(DamageType.Chaos,100,100),false,false,0,default,shot.Penetration).Damage);
        var panelAfterRefund = Game.PlayerSkills(c).Single(g => g.Id == gem.Id).Stats!.ToArray();
        using (var reading = new Game.DistillerOffense())
            Assert.Equal(panelAfterRefund, Game.PlayerSkills(c).Single(g => g.Id == gem.Id).Stats!.ToArray());
        Assert.Equal(37, shot.SkillStats[Key("physical_damage_%_to_add_as_chaos")]);
        Assert.Equal(25, shot.SkillStats[Key("base_reduce_enemy_fire_resistance_%")]);
        for (int i=0; i<3; i++) _ = capture(); // release continuations/repeats reuse the frozen contribution
        Assert.All(h.Items.Items(PlayerInventories.Flask), f => Assert.Equal(7, f.Item.Charges));
        using var next = Game.BeginOrdinaryUse(c);
        var unpaid = Assert.IsType<ProjectileOffense>(Game.CaptureGemProjectile(c, session, gem.Id, gem, (150,100), Game.CaptureSkillCriticalRoll(c, gem.Id)).Payload);
        Assert.Equal(original.CriticalChance, unpaid.CriticalChance);
        Assert.Equal(0, unpaid.SkillStats.GetValueOrDefault(Key("physical_damage_%_to_add_as_chaos")));
    }
}
