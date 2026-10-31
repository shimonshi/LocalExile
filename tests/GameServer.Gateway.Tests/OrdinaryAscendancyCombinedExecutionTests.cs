using GameServer.Core.Projectiles;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public partial class ConditionalCriticalGatewayTests
{
    sealed class CombinedIncomingDraw : Random { public override double NextDouble() => 0; }
    static void CombinedDeadeye(Contact f, params ushort[] nodes)
    {
        var definition = Program.Ascendancies.Find(2, AscendancyFamily.Normal, "Deadeye")!;
        f.Conn.MainAscendancy = new(definition.Identity, nodes, AscendancyRules.AwardKeys);
        Assert.NotNull(new AscendancyRules(Program.Ascendancies).Validate(2, f.Conn.MainAscendancy));
        f.Refresh();
    }

    static int CombinedGaleCount(Contact f) =>
        Program.CharacterPlayerStats(f.Conn)[Program.Stats.Key("gale_force_stacks")!.Value];

    [Theory]
    [InlineData(false, 70)] [InlineData(true, 0)]
    public void OrdinaryCombined_native_unaffected_bleeding_source_suppresses_damage_without_removing_ailment(bool unaffected, int expected)
    {
        using var f = new Contact(new Draws(), classRow: 4);
        f.Conn.Pools = f.Conn.Pools with { Life = 1000, EnergyShield = 0, Armour = 0 };
        f.Conn.PlayerLife = 1000;
        f.Set("base_unaffected_by_bleeding", unaffected ? 1 : 0);
        Assert.True(f.Conn.Bleeding.Apply(100, 0, 5, _ => 0));
        Assert.Equal(expected, Program.AdvancePlayerBleedingLocked(f.Conn, 0, 1, Program.CharacterPlayerStats(f.Conn)));
        Assert.True(f.Conn.Bleeding.Active(1));
    }

    [Fact]
    public void OrdinaryCombined_selected_accuracy_reuses_final_character_pool_without_skill_accuracy_inputs()
    {
        var pools = new CharacterStats.Pools(1, 100, 100, 1, 0) { Accuracy = 0, AccuracyBase = 500 };
        Assert.Equal(0, DeadeyeAvidityAccuracy.Selected(pools, default, _ => 0, 5, 4));
        Assert.Equal(7, DeadeyeAvidityAccuracy.Selected(pools, default, _ => 0, 5, 4, replacement: 7));
    }

    [Fact]
    public void OrdinaryCombined_WindWard_scales_incoming_ignite_before_fractional_carry_and_does_not_consume_stacks()
    {
        using var f = new Contact(new Draws(), classRow: 3);
        CombinedDeadeye(f, 59837, 24848, 22852, 23169);
        f.Conn.PlayerLife = 100;
        for (int i = 0; i < 10; i++) Program.DeadeyeGaleAdmitUse(f.Conn, true);
        f.Conn.ElementalAilments.Apply(DamagePacket.Of(DamageType.Fire, 10, 10), DamagePacket.Empty,
            true, 100, 0, _ => 0, new Random(1));
        var stats = Program.CharacterPlayerStats(f.Conn);
        int total = 0;
        for (int i = 0; i < 10; i++) total += Program.AdvancePlayerAilmentsLocked(f.Conn, i / 10d, (i + 1) / 10d, stats);
        Assert.Equal(7, total); // floor(10 * .9 * 1.3 * .6), preserving the unrounded integral.
        Assert.Equal(93u, f.Conn.PlayerLife);
        Assert.Equal(10, CombinedGaleCount(f));
    }

    [Fact]
    public void OrdinaryCombined_Tailwind_server_cast_and_movement_use_once_and_native_base_cast_stays_unscaled()
    {
        using var f = new Contact(new Draws(), "Fireball", classRow: 3);
        int before = (int)Method("CastMs").Invoke(null, [f.Conn, Program.Skills.EffectRow("Fireball"), f.Skill])!;
        var prior = f.CurrentSkills().Single(s => s.Id == f.Skill).Stats!.ToDictionary(s => s.Key, s => s.Value);
        CombinedDeadeye(f, 59837, 24848);
        int after = (int)Method("CastMs").Invoke(null, [f.Conn, Program.Skills.EffectRow("Fireball"), f.Skill])!;
        var current = f.CurrentSkills().Single(s => s.Id == f.Skill).Stats!.ToDictionary(s => s.Key, s => s.Value);
        uint nativeBase = Program.Stats.Key("virtual_base_spell_cast_time_ms")!.Value;
        Assert.Equal(prior[nativeBase], current[nativeBase]);
        Assert.Equal(SpellCastTiming.Calculate(before, 0, actionSpeed: 1.08).DurationMilliseconds, after);
        Assert.Equal(8, current[Program.Stats.Key(ActionSpeed.Virtual)!.Value]);
        f.Conn.Pools = f.Conn.Pools with { MovementVelocityPct = 40 };
        Assert.Equal(1.4 * 1.08, Program.PlayerMovementFactor(f.Conn), 8);
        f.Set("base_cooldown_speed_+%", 500);
        Assert.Equal(1.08, Program.PlayerActionFactor(f.Conn), 8);
    }

    [Fact]
    public async Task OrdinaryCombined_parsed_default_use_grants_one_Gale_and_rejected_repeat_grants_none()
    {
        using var f = new Contact(new Draws(), classRow: 3);
        CombinedDeadeye(f, 59837, 24848);
        f.Set("global_cannot_crit", 1);
        Assert.Equal(1.08, Program.PlayerActionFactor(f.Conn), 8);
        await f.Use();
        Assert.Equal(1, CombinedGaleCount(f));
        f.Conn.NextAttackAt = 1000;
        await f.Use();
        Assert.Equal(1, CombinedGaleCount(f));
    }

    [Theory]
    [InlineData(false, 60)] [InlineData(true, 0)]
    public void OrdinaryCombined_WindWard_real_incoming_commit_uses_preloss_stacks_and_clears_blocked_hits(bool blocked, int damage)
    {
        using var f = new Contact(new Draws(), classRow: 3);
        CombinedDeadeye(f, 59837, 24848, 22852, 23169);
        f.Set("attack_block_%", blocked ? 100 : 0);
        f.Refresh();
        f.Conn.IncomingHitRandom = new CombinedIncomingDraw();
        f.Conn.PlayerLife = 100;
        f.Conn.Pools = f.Conn.Pools with { BlockChance = blocked ? 100 : 0, ChaosResistance = 0 };
        for (int i = 0; i < 10; i++) Program.DeadeyeGaleAdmitUse(f.Conn, true);
        object?[] args = [f.Conn, DamageType.Chaos, 100, blocked, false, false, null, false];
        Assert.True((bool)Method("TryDevEnemyHit").Invoke(null, args)!);
        Assert.Equal((uint)(100 - damage), f.Conn.PlayerLife);
        Assert.Equal(0, CombinedGaleCount(f));
    }

    [Fact]
    public async Task OrdinaryCombined_parsed_mark_owns_single_payment_admission_and_cancellable_timing()
    {
        using var f = MarkContact();
        CombinedDeadeye(f, 59837, 24848, 53086, 5443);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        uint mana = f.Conn.PlayerMana;
        await f.Use();
        uint paid = f.Conn.PlayerMana;
        Assert.True(paid < mana);
        Assert.Equal(1, CombinedGaleCount(f));
        Assert.Null(Program.DeadeyeMarkOf(f.Conn).Active);
        await f.Use();
        Assert.Equal(paid, f.Conn.PlayerMana);
        Assert.Equal(1, CombinedGaleCount(f));
        runtime.Advance(1);
        Assert.NotNull(Program.DeadeyeMarkOf(f.Conn).Active);
        Assert.False(f.Target.Dead);
        Assert.Equal(f.Target.MaxLife, f.Target.Life);
    }

    [Theory]
    [InlineData(true, 67)] [InlineData(false, 50)]
    public async Task OrdinaryCombined_real_projectile_contact_adds_Sniper_to_target_taken_bucket(bool qualified, int damage)
    {
        var mods = new MonsterModSet([], [], new(new Dictionary<string, int> { ["base_damage_taken_+%"] = -50 }));
        using var f = new Contact(new Draws(), "ProjectileWeakness", distance: 20, targetLife: 10000,
            targetMods: mods, classRow: 3);
        Focal(f); f.Set("global_cannot_crit", 1);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        CastMark(f, runtime);
        runtime.Release(runtime.Shot(new("Metadata/Projectiles/DefaultArrow")
        { Speed = 100, Range = 100, Radius = 1, Lifetime = ProjectileRuntime.Time(3), CountsAsProjectileDamage = qualified },
            new(DamagePacket.Of(DamageType.Fire, 100, 100), true, int.MaxValue, 0, 150, "combined mark"),
            new(32, 32), new(120, 32)));
        runtime.Advance(2);
        Assert.Equal((uint)(10000 - damage), f.Target.Life);
    }

    [Fact]
    public async Task OrdinaryCombined_Mirage_uses_common_Slayer_cull_kill_state_without_owner_leech_or_recursive_archer()
    {
        using var f = MirageContact(classRow: 4);
        Slayer(f, 3184);
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var actor = Assert.IsType<DeadeyeMirageActor>(Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target));
        f.Target.Life = f.Target.MaxLife / 5;
        uint ownerLife = f.Conn.PlayerLife;
        var use = Program.CaptureDeadeyeMirage(runtime.Session, actor, (52, 32), 1);
        var proxy = Assert.IsType<DeadeyeMirageOffense>(use.Payload);
        use = use with { Profile = use.Profile with { Speed = 100, Count = 1, Pierce = 0, Chain = 0, Fork = 0 },
            Origin = new(32, 32), Aim = new(120, 32), Payload = proxy with
            { Shot = proxy.Shot with { Hit = DamagePacket.Empty, AreaHit = DamagePacket.Empty, Ignite = default },
                DirectMarkSource = null, AreaMarkSource = null } };
        runtime.Release(use); runtime.Advance(.3);
        Assert.True(f.Target.Dead);
        Assert.Equal(1, SlayerValue(f).RecentKills);
        Assert.Equal(ownerLife, f.Conn.PlayerLife);
        Assert.Single(Program.DeadeyeMirageOf(f.Conn).Actors);
    }

    [Fact]
    public async Task OrdinaryCombined_Barrage_release_count_timing_and_no_spread_share_one_supported_profile()
    {
        using var f = new Contact(new Draws(), "BurningArrow", "Crude Bow", distance: 20, classRow: 3);
        CombinedDeadeye(f, 28995, 45313);
        double before = (double)Method("SupportedAttackSeconds").Invoke(null, [f.Conn, f.Skill])!;
        var inventory = PlayerInventories.For(f.Conn.Character!, Program.Items);
        var host = Assert.Single(inventory.Items(PlayerInventories.BodyArmour));
        inventory.Change(PlayerInventories.BodyArmour, host.Id, item => item with
        { Sockets = [item.Sockets[0], new(SocketColour.White,
            ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SupportGemBarrage")!))], Links = [2] });
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        var gems = (IEnumerable<(ushort Id, int Effect, string Name, int Level, int Quality, uint Gem)>)
            Method("GemSkillEffects").Invoke(null, [f.Conn])!;
        var gem = gems.Single(g => g.Id == f.Skill);
        var use = (ProjectileUse)Method("CaptureGemProjectile").Invoke(null,
            [f.Conn, runtime.Session, f.Skill, gem, (100, 32), new SkillCriticalRoll(0)])!;
        var shot = Assert.IsType<ProjectileOffense>(use.Payload);
        Assert.True(use.Profile.Barrage);
        Assert.True(use.Profile.Count > 1);
        Assert.Equal(0, use.Profile.SpreadRadians);
        Assert.True(use.Profile.SequentialDelay > 0);
        int per = shot.SkillStats[Program.Stats.Key("support_barrage_attack_time_+%_per_projectile_fired")!.Value];
        double total = (double)Method("SupportedAttackSeconds").Invoke(null, [f.Conn, f.Skill])!;
        Assert.Equal(before * (1 + per * use.Profile.Count / 100d), total, 8);
    }
}
