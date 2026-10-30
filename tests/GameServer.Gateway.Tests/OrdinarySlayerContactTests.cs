using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public partial class ConditionalCriticalGatewayTests
{
    static void Slayer(Contact f, params ushort[] notables)
    {
        var definition = Program.Ascendancies.Find(4, AscendancyFamily.Normal, "Slayer")!;
        var nodes = new HashSet<ushort>();
        foreach (ushort end in notables)
        {
            var queue = new Queue<(ushort Node, ushort[] Path)>();
            var seen = new HashSet<ushort> { definition.StartNode };
            queue.Enqueue((definition.StartNode, []));
            while (queue.TryDequeue(out var current))
            {
                if (current.Node == end) { nodes.UnionWith(current.Path); break; }
                foreach (ushort next in definition.Nodes[current.Node].Adjacent)
                    if (seen.Add(next)) queue.Enqueue((next, [.. current.Path, next]));
            }
            Assert.Contains(end, nodes);
        }

        f.Conn.MainAscendancy = new(definition.Identity, nodes.ToArray(), AscendancyRules.AwardKeys);
        new AscendancyRules(Program.Ascendancies).Validate(4, f.Conn.MainAscendancy);
        f.Refresh();
        f.Set("global_cannot_crit", 1);
    }

    static Task SlayerContact(Contact f, string? gem) => gem is "Fireball" or "BurningArrow" ? f.Launch()
        : gem is null ? f.Use() : f.Sweep();

    [Theory]
    [InlineData(null, null)] [InlineData("Cyclone", null)] [InlineData("IceNova", null)]
    [InlineData("Fireball", null)] [InlineData("BurningArrow", "Crude Bow")]
    public async Task Slayer_Headsman_culls_at_actual_owner_contacts_without_extra_leech(string? gem, string? weapon)
    {
        using var f = new Contact(new Draws(), gem, weapon, classRow: 4, targetLife: 10000);
        Slayer(f, 3184, 34484);
        f.Conn.Pools = f.Conn.Pools with { Life = 1000 };
        f.Set("damage_+%", -10000); // Accepted zero damage, not a rejected hit.
        f.Target.Life = 2000;
        await SlayerContact(f, gem);
        Assert.True(f.Target.Dead);
        Assert.Equal(0u, f.Target.Life);
        Assert.Equal(0, Program.LeechOf(f.Conn).Life.Count);
        Assert.Equal(50u, f.Conn.PlayerLife);
    }

    [Theory]
    [InlineData(null, null)] [InlineData("Cyclone", null)] [InlineData("IceNova", null)]
    [InlineData("Fireball", null)] [InlineData("BurningArrow", "Crude Bow")]
    public async Task Slayer_overkill_joins_ordinary_leech_before_one_cap_on_attack_and_spell_contacts(string? gem, string? weapon)
    {
        using var f = new Contact(new Draws(), gem, weapon, classRow: 4, targetLife: 10000);
        Slayer(f, 34484);
        f.Conn.Pools = f.Conn.Pools with { Life = 1000 };
        f.Set("leech_%_is_instant", 100);
        f.Set("life_leech_from_any_damage_permyriad", 10000);
        f.Target.Life = 1;
        await SlayerContact(f, gem);
        Assert.True(f.Target.Dead);
        Assert.Equal(250u, f.Conn.PlayerLife); // Path includes Brutal Fervour: one 200-point cap, not two.
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public async Task Slayer_spell_overkill_alone_converts_and_obeys_target_prohibitions(bool ghost, bool prohibited)
    {
        string stat = ghost ? "cannot_have_energy_shield_leeched_from" : "cannot_have_life_leeched_from";
        using var f = new Contact(new Draws(), "Fireball", classRow: 4, targetLife: 10000,
            targetMods: new([], [], new(new Dictionary<string, int> { [stat] = prohibited ? 1 : 0 })));
        Slayer(f, 34484);
        f.Conn.Pools = f.Conn.Pools with { Life = 1000, EnergyShield = 1000 };
        f.Set("leech_%_is_instant", 100); f.Set("keystone_ghost_reaver", ghost ? 1 : 0);
        f.Target.Life = 1;
        await f.Launch();
        Assert.True(f.Target.Dead);
        Assert.Equal(50u + (!ghost && !prohibited ? 200u : 0u), f.Conn.PlayerLife);
        Assert.Equal(ghost && !prohibited ? 100u : 0u, f.Conn.EnergyShield.Current);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Slayer_phase_interception_and_running_phase_exclude_cull_and_overkill(bool running)
    {
        using var f = new Contact(new Draws(), "Fireball", classRow: 4, targetLife: 1000, stage: new(50, 2612, 3, 2));
        Slayer(f, 3184, 34484);
        f.Conn.Pools = f.Conn.Pools with { Life = 1000 };
        f.Target.Life = 1;
        f.Target.StageRunning = f.Target.StageTaken = running;
        await f.Launch();
        Assert.Equal(running ? 1u : 1000u, f.Target.Life);
        Assert.True(f.Target.StageTaken);
        Assert.False(f.Target.Dead);
        Assert.Equal(0, Program.LeechOf(f.Conn).Life.Count); // Spell has no ordinary Life leech source.
    }

    [Theory]
    [InlineData("blocked")] [InlineData("evaded")] [InlineData("above")]
    public async Task Slayer_cull_cannot_bypass_contact_admission_or_threshold(string condition)
    {
        using var f = new Contact(new Draws(), "Cyclone", classRow: 4, targetLife: 10000,
            targetMods: new([], [], new(new Dictionary<string, int> { ["monster_base_block_%"] = condition == "blocked" ? 100 : 0 })));
        Slayer(f, 3184, 34484);
        f.Set("damage_+%", -10000);
        f.Target.Life = condition == "above" ? 2001u : 2000u;
        uint before = f.Target.Life;
        if (condition == "evaded")
        {
            f.Conn.Pools = f.Conn.Pools with { Accuracy = 0 };
            f.Conn.AttackAccuracyRandom = new AccuracyDraws(0);
        }
        if (condition == "blocked") Assert.False(await f.ContactDamage(() => Program.Blocked));
        else await f.Sweep();
        Assert.Equal(before, f.Target.Life);
        Assert.False(f.Target.Dead);
        Assert.Equal(0, Program.LeechOf(f.Conn).Life.Count);
    }

    [Fact]
    public async Task Slayer_leech_conditions_follow_real_full_Life_instances_source_removal_and_stun_admission()
    {
        using var f = new Contact(new Draws(), "Cyclone", classRow: 4);
        Slayer(f, 34484, 10143);
        f.Conn.Pools = f.Conn.Pools with { Life = 1000 };
        f.Conn.PlayerLife = 1000;
        int Value(string name) => Program.CharacterPlayerStats(f.Conn).GetValueOrDefault(Program.Stats.Key(name)!.Value);
        int speed = Value("main_hand_attack_speed_+%");
        await f.Sweep();
        Assert.True(Program.LeechOf(f.Conn).Active(f.Conn.Clock.Elapsed.TotalSeconds).Life);
        Assert.Equal(speed + 22, Value("main_hand_attack_speed_+%")); // Unarmed's inherent 10% MORE.
        Assert.Equal(-10, Value("base_damage_taken_+%"));
        Assert.Equal(1, Value("cannot_be_stunned"));
        Assert.Null(Program.CommitPlayerHitRecoveryLocked(f.Conn, 1000, f.Conn.Clock.Elapsed.TotalSeconds));
        Assert.Equal(0, f.Conn.HitRecovery.Generation);
        f.Conn.MainAscendancy = f.Conn.MainAscendancy with
            { Nodes = f.Conn.MainAscendancy.Allocated.Where(n => n != 34484 && n != 10143).ToArray() };
        Assert.Equal(speed, Value("main_hand_attack_speed_+%"));
        Assert.Equal(0, Value("base_damage_taken_+%"));
        Assert.Equal(0, Value("cannot_be_stunned"));
        Assert.False(Program.LeechOf(f.Conn).Active(f.Conn.Clock.Elapsed.TotalSeconds).Life);
        Assert.NotNull(Program.CommitPlayerHitRecoveryLocked(f.Conn, 1000, f.Conn.Clock.Elapsed.TotalSeconds));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task Slayer_stun_condition_preserves_explicit_override_and_restores_after_clearing(int value)
    {
        using var f = new Contact(new Draws(), "Cyclone", classRow: 4);
        Slayer(f, 34484, 10143);
        f.Conn.Pools = f.Conn.Pools with { Life = 1000 };
        f.Conn.PlayerLife = 1000;
        await f.Sweep();
        uint key = Program.Stats.Key("cannot_be_stunned")!.Value;
        Assert.True(Program.LeechOf(f.Conn).Active(f.Conn.Clock.Elapsed.TotalSeconds).Life);
        Assert.Equal(1, Program.CharacterPlayerStats(f.Conn).GetValueOrDefault(key));
        f.Set("cannot_be_stunned", value);
        Assert.Equal(value, Program.CharacterPlayerStats(f.Conn).GetValueOrDefault(key));
        var recovery = Program.CommitPlayerHitRecoveryLocked(f.Conn, 1000, f.Conn.Clock.Elapsed.TotalSeconds);
        Assert.Equal(value == 0, recovery is not null);
        int generation = f.Conn.HitRecovery.Generation;
        f.Conn.PlayerStats.Remove(key);
        Assert.Equal(1, Program.CharacterPlayerStats(f.Conn).GetValueOrDefault(key));
        Assert.Null(Program.CommitPlayerHitRecoveryLocked(f.Conn, 1000, f.Conn.Clock.Elapsed.TotalSeconds));
        Assert.Equal(generation, f.Conn.HitRecovery.Generation); // Projection does not cancel started recovery.
    }

    [Theory]
    [InlineData(null)] [InlineData("Cyclone")] [InlineData("Fireball")] [InlineData("IceNova")]
    public async Task Slayer_overkill_uses_mitigated_direct_damage_and_pre_hit_Life_without_cull_or_Impale(string? gem)
    {
        using var f = new Contact(new Draws(), gem, classRow: 4, targetLife: 10000,
            targetMods: new([], [], new(new Dictionary<string, int> { ["base_chaos_damage_resistance_%"] = 50 })));
        Slayer(f, 34484);
        f.Conn.Pools = f.Conn.Pools with { Life = 10000 };
        f.Set("leech_%_is_instant", 100);
        f.Set("elemental_damage_+%", -10000); // Only the fixture's fixed Chaos component survives.
        f.Set("physical_damage_+%", -10000); // The allocated attack increases must not leave random unarmed Physical.
        // Measure the production hit after mitigation on an otherwise identical nonlethal contact.
        await SlayerContact(f, gem);
        int direct = (int)(f.Target.MaxLife - f.Target.Life);
        Assert.InRange(direct, 101, 3000);
        uint ordinary = f.Conn.PlayerLife - 50;
        f.Conn.PlayerLife = 50;
        Program.LeechOf(f.Conn).LifeInstantCarry = 0;
        f.Conn.NextAttackAt = 0;
        f.Target.Life = (uint)(direct - 100);
        f.Target.Impale.Apply(1000, f.Conn.Clock.Elapsed.TotalSeconds); // A separate reflected 100.
        await SlayerContact(f, gem);
        Assert.True(f.Target.Dead);
        Assert.Equal(50u + ordinary + 20u, f.Conn.PlayerLife); // 20% of exactly 100 direct overkill, not 200.
    }

    [Theory]
    [InlineData("Fireball")] [InlineData("Cyclone")]
    public async Task Slayer_Impale_only_lethal_contacts_do_not_generate_overkill_leech(string gem)
    {
        using var f = new Contact(new Draws(), gem, classRow: 4, targetLife: 1000);
        Slayer(f, 34484);
        f.Set("damage_+%", -10000);
        f.Target.Life = 1;
        f.Target.Impale.Apply(1000, f.Conn.Clock.Elapsed.TotalSeconds);
        await SlayerContact(f, gem);
        Assert.True(f.Target.Dead);
        Assert.Equal(0, Program.LeechOf(f.Conn).Life.Count);
        Assert.Equal(50u, f.Conn.PlayerLife);
    }

    [Fact]
    public async Task Slayer_taken_joins_shock_and_increased_bucket_and_supports_existing_ignite_consumer()
    {
        using var f = new Contact(new Draws(), "Cyclone", classRow: 4);
        Slayer(f, 34484, 10143);
        f.Conn.Pools = f.Conn.Pools with { Life = 10000 };
        f.Set("base_damage_taken_+%", 30);
        await f.Sweep();
        var stats = Program.CharacterPlayerStats(f.Conn);
        int Get(string name) => stats.GetValueOrDefault(Program.Stats.Key(name)!.Value);
        var ailments = f.Conn.ElementalAilments;
        double now = f.Conn.Clock.Elapsed.TotalSeconds;
        ailments.Apply(DamagePacket.Of(DamageType.Fire, 100, 100), DamagePacket.Of(DamageType.Lightning, 1000, 1000),
            true, 1000, now, _ => 0, new Random(1));
        Assert.Equal(50, ailments.Magnitude(PlayerElementalAilments.Kind.Shock, now));
        Assert.Equal(1.7, (double)Method("PlayerDamageTakenFactor").Invoke(null, [f.Conn, now, stats, 0])!, 6);
        // The existing DoT engine receives the same increased bucket, additive with Shock, not 1.3 * .9 * 1.5.
        int dot = ailments.Advance(now, now + 1, Get, 0, 75, false);
        Assert.Equal((int)Math.Floor(100 * .9 * 1.3 * 1.7), dot);
        int mitigated = (int)Method("MitigateTakenWithArmour").Invoke(null,
            [f.Conn, DamageType.Chaos, 100, 0, 75, 0, stats])!;
        Assert.Equal(170, mitigated);
    }

    [Fact]
    public async Task Slayer_conditions_expire_pause_and_failed_transfer_resume_with_the_existing_leech_session()
    {
        using var f = new Contact(new Draws(), "Cyclone", classRow: 4);
        Slayer(f, 34484, 10143);
        f.Conn.Pools = f.Conn.Pools with { Life = 1000 };
        await f.Sweep();
        int Taken() => Program.CharacterPlayerStats(f.Conn).GetValueOrDefault(Program.Stats.Key("base_damage_taken_+%")!.Value);
        Assert.Equal(-10, Taken());
        f.Conn.Clock.Stop();
        await Task.Delay(30);
        Assert.Equal(-10, Taken());
        f.Conn.LeavingArea = true;
        Assert.Equal(0, Taken());
        f.Conn.ResumeAfterFailedTransfer();
        Assert.Equal(-10, Taken());
        Program.LeechOf(f.Conn).Active(f.Conn.Clock.Elapsed.TotalSeconds + 20);
        Assert.Equal(0, Taken());
        Assert.Equal(0, Program.CharacterPlayerStats(f.Conn).GetValueOrDefault(Program.Stats.Key("cannot_be_stunned")!.Value));
    }

    [Fact]
    public async Task Slayer_held_stat_output_rebuilds_after_source_removal_and_retirement_disables_consumers()
    {
        using var f = new Contact(new Draws(), "Cyclone", classRow: 4);
        Slayer(f, 34484, 10143);
        f.Conn.Pools = f.Conn.Pools with { Life = 1000 };
        await f.Sweep();
        Assert.Equal(-10, Program.CharacterPlayerStats(f.Conn)[Program.Stats.Key("base_damage_taken_+%")!.Value]);
        f.Conn.Retire();
        Assert.Equal(0, Program.CharacterPlayerStats(f.Conn)[Program.Stats.Key("base_damage_taken_+%")!.Value]);
        // A fresh connection avoids reviving retirement and has no unread contact packets.
        using var publication = new Contact(new Draws(), "Cyclone", classRow: 4);
        Slayer(publication, 34484, 10143);
        publication.Conn.Pools = publication.Conn.Pools with { Life = 1000 };
        Program.CharacterPlayerStats(publication.Conn);
        Program.LeechOf(publication.Conn).Hit(publication.Conn.Clock.Elapsed.TotalSeconds, (100, 0, 0), false, false, false);
        await publication.SendGate.WaitAsync();
        var send = publication.RefreshStats(charges: false);
        Assert.False(send.IsCompleted);
        publication.Conn.MainAscendancy = new();
        publication.SendGate.Release();
        await send;
        await publication.AssertNextPlayerStats();
    }
}
