using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public partial class ConditionalCriticalGatewayTests
{
    [Theory]
    [InlineData("Rusted Sword")] [InlineData("Crude Bow")]
    public async Task OrdinaryHarbinger_ground_dispatch_combines_raw_action_and_ailment_clocks_once(string weapon)
    {
        using var f = new Contact(new Draws(), weaponName: weapon, classRow: 3);
        GroundGale(f);
        f.Set(ActionSpeed.Virtual, 0);
        var weaponTiming = (SkillCatalog.Unarmed)Method("WeaponFor").Invoke(null, [f.Conn])!;
        double baseline = weaponTiming.AttackSeconds(Program.CharacterPlayerStats(f.Conn));
        f.Conn.PlayerStats.Remove(Program.Stats.Key(ActionSpeed.Virtual)!.Value);
        f.Set(ActionSpeed.Reduction, -18); // Tailwind 8 plus the Harbinger raw 10 input.
        f.Conn.ElementalAilments.Apply(DamagePacket.Empty, DamagePacket.Of(DamageType.Cold, 100, 100),
            false, 100, f.Conn.Clock.Elapsed.TotalSeconds, _ => 0, new Random(1));
        double expected = baseline / (1.18 * .7);
        double before = f.Conn.Clock.Elapsed.TotalSeconds;
        await f.GroundAdmission(0x102, 32, 32);
        double after = f.Conn.Clock.Elapsed.TotalSeconds;
        Assert.InRange(expected, f.Conn.NextAttackAt - after, f.Conn.NextAttackAt - before);
        Assert.Equal(1, GroundGaleCount(f));
    }

    [Fact]
    public void OrdinaryHarbinger_native_freeze_keeps_zero_action_instead_of_the_server_timer_floor()
    {
        using var f = new Contact(new Draws(), classRow: 3);
        CombinedDeadeye(f, 59837, 24848);
        f.Set(ActionSpeed.Reduction, -18);
        f.Conn.ElementalAilments.Apply(DamagePacket.Empty, DamagePacket.Of(DamageType.Cold, 100, 100),
            true, 100, 0, _ => 0, new Random(1));
        var native = ((IEnumerable<(uint, int)>)Method("FinalPlayerStats").Invoke(null, [f.Conn])!)
            .ToDictionary(s => s.Item1, s => s.Item2);
        Assert.Equal(-100, native[Program.Stats.Key(ActionSpeed.Virtual)!.Value]);
        Assert.Equal(-100, native[Program.Stats.Key("intermediary_combined_action_speed_+%")!.Value]);
        Assert.True(Program.PlayerActionBlocked(f.Conn, 0, 0));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OrdinaryHarbinger_raw_action_and_chill_are_consumed_once_with_final_replacement(bool replacement)
    {
        using var f = new Contact(new Draws(), "BurningArrow", "Crude Bow", classRow: 3);
        CombinedDeadeye(f, 59837, 24848);
        f.Set(ActionSpeed.Virtual, 0);
        double baseline = (double)Method("SupportedAttackSeconds").Invoke(null, [f.Conn, f.Skill])!;
        f.Conn.PlayerStats.Remove(Program.Stats.Key(ActionSpeed.Virtual)!.Value);
        // Tailwind 8 + Time of Need 10 share the signed raw bucket, as in Harbinger's native buff.
        f.Set(ActionSpeed.Reduction, -18);
        f.Conn.ElementalAilments.Apply(DamagePacket.Empty, DamagePacket.Of(DamageType.Cold, 100, 100),
            false, 100, 0, _ => 0, new Random(1));
        if (replacement) f.Set(ActionSpeed.Virtual, 50);
        double factor = replacement ? 1.5 : 1.18 * .7;
        var raw = Program.CharacterPlayerStats(f.Conn);
        Assert.Equal(replacement ? 50 : 18, raw[Program.Stats.Key(ActionSpeed.Virtual)!.Value]);
        Assert.Equal(factor, Program.PlayerActionFactor(f.Conn), 8);
        Assert.Equal(baseline / factor,
            (double)Method("SupportedAttackSeconds").Invoke(null, [f.Conn, f.Skill])!, 8);
        var native = ((IEnumerable<(uint, int)>)Method("FinalPlayerStats").Invoke(null, [f.Conn])!)
            .ToDictionary(s => s.Item1, s => s.Item2);
        int projected = (int)Math.Round(100 * (factor - 1));
        Assert.Equal(projected, native[Program.Stats.Key(ActionSpeed.Virtual)!.Value]);
        Assert.Equal(projected, native[Program.Stats.Key("intermediary_combined_action_speed_+%")!.Value]);
        _ = f.CurrentSkills();
        Assert.Equal(baseline / factor,
            (double)Method("SupportedAttackSeconds").Invoke(null, [f.Conn, f.Skill])!, 8);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OrdinaryHarbinger_cast_timing_consumes_raw_action_before_the_separate_ailment_clock(bool replacement)
    {
        using var f = new Contact(new Draws(), "Fireball", classRow: 3);
        CombinedDeadeye(f, 59837, 24848);
        f.Set(ActionSpeed.Reduction, -18);
        f.Conn.ElementalAilments.Apply(DamagePacket.Empty, DamagePacket.Of(DamageType.Cold, 100, 100),
            false, 100, 0, _ => 0, new Random(1));
        if (replacement) f.Set(ActionSpeed.Virtual, 50);
        var gem = ((IEnumerable<(ushort Id, int Effect, string Name, int Level, int Quality, uint Gem)>)
            Method("GemSkillEffects").Invoke(null, [f.Conn])!).Single(g => g.Id == f.Skill);
        var own = (Dictionary<uint, int>)Method("SupportedStats").Invoke(null, [f.Conn, f.Skill])!;
        int speed = own.GetValueOrDefault(Program.Stats.Key("cast_speed_+%")!.Value);
        int rawDuration = SpellCastTiming.ForSkill(Program.Skills, gem.Effect, speed, replacement ? 1.5 : 1.18)!
            .Value.DurationMilliseconds!.Value;
        int expected = (int)Math.Ceiling(rawDuration / (replacement ? 1 : .7));
        Assert.Equal(expected, (int)Method("CastMs").Invoke(null, [f.Conn, gem.Effect, f.Skill])!);
        _ = f.CurrentSkills();
        Assert.Equal(expected, (int)Method("CastMs").Invoke(null, [f.Conn, gem.Effect, f.Skill])!);
    }

    [Fact]
    public void OrdinaryHarbinger_bleeding_keeps_recipient_clock_channel_taken_and_ES_bypass()
    {
        using var f = new Contact(new Draws(), classRow: 4);
        f.Conn.PlayerLife = 1000;
        f.Conn.Pools = f.Conn.Pools with { Life = 1000, EnergyShield = 1000, Armour = 0 };
        f.Conn.EnergyShield.SetMaximum(1000, refill: true);
        f.Set("buff_time_passed_+%_other_than_temporal_chains", -50);
        f.Set("harbinger_channel_buff_damage_taken_+%_final", -20);
        f.Set("base_non_chaos_damage_bypass_energy_shield_%", 50);
        var stats = Program.CharacterPlayerStats(f.Conn);
        int Get(string name) => Program.DeadeyeValue(stats, name);
        Assert.True(f.Conn.Bleeding.Apply(100, 0, 5, Get));
        // 70 damage/s, then channel's .8; half of the resulting 56 bypasses ES.
        Assert.Equal(56, Program.AdvancePlayerBleedingLocked(f.Conn, 0, 1, stats));
        Assert.Equal(972u, f.Conn.PlayerLife);
        Assert.Equal(972u, f.Conn.EnergyShield.Current);
        Assert.True(f.Conn.Bleeding.Active(6)); // Recipient slowdown extends duration, never lowers damage/s.
        Assert.False(f.Conn.Bleeding.Active(10));
    }

    [Fact]
    public async Task OrdinaryHarbinger_mark_reads_target_temporal_chains_and_proxy_cadence_discards_owner_slow()
    {
        using var f = MirageContact();
        Focal(f);
        ushort skill = LinkIndependentMark(f, "ProjectileWeakness");
        await using var runtime = new ProjectileRuntimeFixture(f.Conn);
        f.Conn.TemporalChains[42] = new(40, 1, new EffectClock(20, 0), -50, -50, -50, false);
        Assert.True(Program.TryDeadeyeMarkCast(runtime.Session, skill, 42));
        runtime.Advance(1);
        var mark = Assert.IsType<Program.DeadeyeMarkInstance>(Program.DeadeyeMarkOf(f.Conn).Active);
        Assert.Equal(.5, mark.Clock.Rate);
        f.Set(ActionSpeed.Reduction, -18);
        f.Conn.ElementalAilments.Apply(DamagePacket.Empty, DamagePacket.Of(DamageType.Cold, 100, 100),
            false, 100, f.Conn.Clock.Elapsed.TotalSeconds, _ => 0, new Random(1));
        var actor = Assert.IsType<DeadeyeMirageActor>(Program.DeadeyeMirageBowHit(runtime.Session, f.Skill, f.Target));
        var raw = (double)Method("SupportedAttackSeconds").Invoke(null, [f.Conn, f.Skill])!;
        double proxySpeed = 1 + Program.DeadeyeValue((Dictionary<uint, int>)Method("SupportedStats")
            .Invoke(null, [f.Conn, f.Skill])!, "support_mirage_archer_attack_speed_+%_final") / 100d;
        Program.DeadeyeMirageStep(runtime.Session);
        Assert.Equal(1 + raw * Program.PlayerActionFactor(f.Conn) / proxySpeed, actor.NextAttack, 8);
        f.Conn.TemporalChains.Remove(42);
        Program.RefreshDeadeyeMarks(runtime.Session);
        Assert.Equal(1, mark.Clock.Rate);
    }
}
