using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public partial class ConditionalCriticalGatewayTests
{
    sealed partial class Contact
    {
        public async Task NextBleedingUpdate(bool expected)
        {
            uint key = Program.Stats.Key("is_bleeding")!.Value;
            byte[] on = InstanceEntities.StatsUpdate(Program.PlayerId, [(key, 1)]);
            byte[] off = InstanceEntities.StatsUpdate(Program.PlayerId, [(key, 0)]);
            var seen = new List<byte>();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            var buffer = new byte[65536];
            while (true)
            {
                int count = await reader.GetStream().ReadAsync(buffer, timeout.Token);
                Assert.True(count > 0);
                byte[] bytes = buffer[..count];
                received.Receive.Process(bytes);
                seen.AddRange(bytes);
                int yes = seen.ToArray().AsSpan().IndexOf(on), no = seen.ToArray().AsSpan().IndexOf(off);
                if (yes < 0 && no < 0) continue;
                Assert.Equal(expected, yes >= 0 && (no < 0 || yes < no));
                return;
            }
        }
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task Slayer_area_drains_committed_quest_deaths_after_lethal_reflection_or_cancel_without_more_hits(bool reflect)
    {
        using var f = new Contact(new Draws(), "IceNova", classRow: 4);
        f.Set("global_cannot_crit", 1);
        f.Conn.Use(f.Conn.Layout with { Id = MercyMission.Area });
        var first = new SpawnedMonster("Hailrake", new(34, 32, 0), 0, 2, 1)
        {
            Variety = Program.Monsters.Find(MercyMission.Hailrake),
            Mods = new([], [], new(new Dictionary<string, int> { ["physical_damage_to_return_when_hit"] = reflect ? 1000 : 0 }))
        };
        var second = new SpawnedMonster("unhit", new(35, 32, 0), 0, 2, 100000);
        Assert.NotNull(first.Variety);
        f.Conn.Monsters.Clear(); f.Conn.Monsters[42] = first; f.Conn.Monsters[43] = second;
        await f.SendGate.WaitAsync();
        var sweep = f.Sweep();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            while (true)
            {
                // Death and reflection are one combat-lock commit; never inspect its intermediate dead flag.
                lock (f.Conn.Monsters)
                    if (first.Dead)
                    {
                        if (reflect) Assert.Equal(0u, f.Conn.PlayerLife);
                        else Interlocked.Increment(ref f.Conn.ActionCount);
                        break;
                    }
                await Task.Delay(5, timeout.Token);
            }
        }
        finally { f.SendGate.Release(); }
        await sweep;
        Assert.Equal(second.MaxLife, second.Life);
        uint chest = Program.Items.Find(MercyMission.MedicineChest)!.Hash;
        Assert.Single(f.Conn.Instance.Ground.All(), x => x.Item.Item.TypeHash == chest);
    }

    [Theory]
    [InlineData(DamageType.Physical, 0)] [InlineData(DamageType.Physical, 1000)]
    [InlineData(DamageType.Fire, 0)] [InlineData(DamageType.Chaos, 1000)]
    public void Slayer_reflected_reduction_joins_generic_taken_after_unreduced_armour_and_resistance(DamageType type, int armour)
    {
        using var f = new Contact(new Draws(), classRow: 4);
        f.Conn.Pools = f.Conn.Pools with { Life = 10000, Armour = armour, EnergyShield = 0 };
        f.Conn.PlayerLife = 10000;
        f.Set("base_damage_taken_+%", 50);
        f.Set("reflect_damage_taken_+%", -25);
        f.Set(type == DamageType.Physical ? "physical_reflect_damage_taken_+%" : "elemental_reflect_damage_taken_+%", -25);
        if (type == DamageType.Fire) f.Conn.Pools = f.Conn.Pools with { FireResistance = 50 };
        var target = new SpawnedMonster("reflector", new(34, 32, 0), 0, 2, 1000)
        { Mods = new([], [], new(new Dictionary<string, int> { [type.ToString().ToLowerInvariant() + "_damage_to_return_when_hit"] = 100 })) };
        var stats = Program.CharacterPlayerStats(f.Conn);
        var hit = new Program.OwnerSkillHit(100, DamagePacket.Of(type, 100, 100),
            Program.HitLeechSources(stats, true), stats);
        int damage = Program.ReflectOwnerHitLocked(f.Conn, target, hit);
        int expected = type switch
        {
            DamageType.Physical => DamageModel.MitigatePhysical(100, armour, 0),
            DamageType.Fire => 50,
            _ => 125 // Chaos has generic -25 reflection only, never elemental reflection's -25.
        };
        Assert.Equal(expected, damage);
        Assert.Equal(10000u - (uint)expected, f.Conn.PlayerLife);
    }

    [Fact]
    public void Slayer_reflection_retains_Shock_leech_taken_and_independent_Fortification_LESS()
    {
        using var f = new Contact(new Draws(), "Cyclone", classRow: 4);
        Slayer(f, 10143);
        f.Conn.Pools = f.Conn.Pools with { Life = 1000, Armour = 0, EnergyShield = 0 };
        f.Conn.PlayerLife = 1000;
        f.Set("base_damage_taken_+%", 50); f.Set("physical_reflect_damage_taken_+%", -50);
        Program.CharacterPlayerStats(f.Conn);
        double now = f.Conn.Clock.Elapsed.TotalSeconds;
        Program.LeechOf(f.Conn).Hit(now, (100, 0, 0), false, false, false);
        f.Conn.ElementalAilments.Apply(DamagePacket.Empty, DamagePacket.Of(DamageType.Lightning, 1000, 1000),
            true, 1000, now, _ => 0, new Random(1));
        Program.FortificationOf(f.Conn).State.GrantExplicitStacks(now, 20);
        Program.CharacterPlayerStats(f.Conn); // Adopt the changed Fortification pool revision before fixing the control pools.
        f.Conn.Pools = f.Conn.Pools with { Life = 1000, Armour = 0, EnergyShield = 0 };
        var stats = Program.CharacterPlayerStats(f.Conn);
        var target = new SpawnedMonster("reflector", new(34, 32, 0), 0, 2, 1000)
        { Mods = new([], [], new(new Dictionary<string, int> { ["physical_damage_to_return_when_hit"] = 100 })) };
        // Generic +50, reflection -50, Shock +50, leech -10 = +40 increased taken, then 20% LESS.
        Assert.Equal(112, Program.ReflectOwnerHitLocked(f.Conn, target, new(100,
            DamagePacket.Of(DamageType.Physical, 100, 100), Program.HitLeechSources(stats, true), stats)));
    }

    [Fact]
    public async Task Slayer_bleeding_status_reads_reconcile_atomically_with_concurrent_clear_and_mutation()
    {
        var bleed = new PlayerBleeding();
        bleed.Apply(100, 0, 10, _ => 0);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var clearing = new ManualResetEventSlim();
        var read = Task.Run(() => bleed.Read(1, _ => { entered.Set(); release.Wait(); return 0; }));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(3)));
        var clear = Task.Run(() => { clearing.Set(); bleed.Clear(); });
        try
        {
            Assert.True(clearing.Wait(TimeSpan.FromSeconds(3)));
            await Task.Delay(30);
            Assert.False(clear.IsCompleted);
        }
        finally { release.Set(); }
        Assert.True(await read); await clear;
        Assert.False(bleed.Active(1));
        await Task.WhenAll(Enumerable.Range(0, 4).Select(worker => Task.Run(() =>
        {
            for (int i = 0; i < 10000; i++)
                switch (worker)
                {
                    case 0: bleed.Apply(100, i, 2, _ => 0); break;
                    case 1: bleed.Clear(); break;
                    case 2: bleed.Advance(i, i + 1, _ => 0, false, false, 0, 1); break;
                    default: bleed.Active(i); break;
                }
        })));
        bleed.Clear(); Assert.False(bleed.Active(10000));
    }

    [Theory]
    [InlineData("expiry")] [InlineData("immunity")] [InlineData("unaffected")]
    public async Task Slayer_player_tick_publishes_bleed_admission_and_removal_while_unaffected_remains_bleeding(string mode)
    {
        using var f = new Contact(new Draws(), classRow: 4);
        f.Set("global_cannot_crit", 1);
        if (mode == "unaffected")
        {
            Slayer(f, 34484);
            f.Conn.Pools = f.Conn.Pools with { Life = 1000 };
            f.Conn.PlayerLife = 1000;
            Program.CharacterPlayerStats(f.Conn);
            Program.LeechOf(f.Conn).Hit(f.Conn.Clock.Elapsed.TotalSeconds, (100, 0, 0), false, false, false);
        }
        var tick = f.RunPanelTick();
        try
        {
            await f.NextBleedingUpdate(false);
            double now = f.Conn.Clock.Elapsed.TotalSeconds;
            f.Conn.Bleeding.Apply(100, now, mode == "expiry" ? 1 : 20, _ => 0);
            await f.NextBleedingUpdate(true);
            if (mode == "immunity") f.Set("immune_to_bleeding", 1);
            if (mode == "unaffected")
            {
                Program.AdvancePlayerBleedingLocked(f.Conn, now, now + 1, Program.CharacterPlayerStats(f.Conn));
                Assert.True(f.Conn.Bleeding.Active(now + 1));
                f.Conn.Bleeding.Clear();
            }
            await f.NextBleedingUpdate(false);
        }
        finally { f.Conn.Closed = true; await tick.WaitAsync(TimeSpan.FromSeconds(3)); }
    }

    [Fact]
    public async Task Slayer_held_bleed_admission_rebuilds_at_send_gate_after_immunity_removes_it()
    {
        using var f = new Contact(new Draws(), classRow: 4);
        f.Set("global_cannot_crit", 1);
        var tick = f.RunPanelTick();
        try
        {
            await f.NextBleedingUpdate(false);
            await Task.Delay(600); // Let the first tick finish its other initial condition publications.
            await f.SendGate.WaitAsync();
            f.Conn.Bleeding.Apply(100, f.Conn.Clock.Elapsed.TotalSeconds, 20, _ => 0);
            await Task.Delay(350);
            f.Set("immune_to_bleeding", 1);
            f.SendGate.Release();
            await f.NextBleedingUpdate(false);
            Assert.False(f.Conn.Bleeding.Active(f.Conn.Clock.Elapsed.TotalSeconds));
        }
        finally { f.Conn.Closed = true; await tick.WaitAsync(TimeSpan.FromSeconds(3)); }
    }
}
