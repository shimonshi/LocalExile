using GameServer.Core.Protocol.Instance;

namespace GameServer.Gateway.Tests;

public sealed class DeadeyeGaleEngineTests
{
    [Fact]
    public void Allied_Tailwind_requires_explicit_same_world_and_rebased_actor_namespace()
    {
        var first = new object(); var second = new object();
        var source = new DeadeyeGaleAura.Source(new(0x1000, 1), 1, new(0, 0), Start()) { World = first };
        // Same PlayerId/epoch on another connection is not the same actor/world.
        Assert.Null(DeadeyeGaleAura.ForRecipient([source], new(0x1000, 1), 1, new(0, 0), world: second));
        Assert.Null(DeadeyeGaleAura.ForRecipient([source], new(0x1000, 1), 1, new(0, 0)));
        var ally = DeadeyeGaleAura.ForRecipient([source], new(0x2000, 1), 1, new(0, 0), world: first)!.Value;
        int Recipient(string name) => name switch
        { ActionSpeed.HasTailwind => 1, ActionSpeed.Reduction => -8, ActionSpeed.TailwindEffect => 50, _ => 0 };
        Assert.Equal(1.12, ally.ActionFactor(Recipient), 9); // own50%, no duplicate base8
    }

    [Fact]
    public void Ally_native_gate_samples_removal_and_new_lease_generations_without_stale_source_adds()
    {
        var world = new object(); var state = Start();
        var source = new DeadeyeGaleAura.Source(new(1), 1, new(0, 0), state) { World = world };
        var presentation = new DeadeyeGaleAura.Presentation(); ushort buff = 10;
        DeadeyeGaleAura.Received? Query() => DeadeyeGaleAura.ForRecipient([source], new(2), 1, new(10, 0), world: world);
        Assert.Equal(InstanceEntities.AddBuff(2, 10, 1094, 1),
            presentation.Build(Query(), new(2), world, () => buff++));
        // A queued operation retains only this query, never a captured Received grant.
        state.Observe(1, false, Sources.GetValueOrDefault);
        Assert.Equal(InstanceEntities.RemoveBuff(2, 10), presentation.Build(Query(), new(2), world, () => buff++));
        Assert.Empty(presentation.Build(Query(), new(2), world, () => buff++));
        state.Observe(2, true, Sources.GetValueOrDefault);
        var newLease = new object();
        Assert.Empty(presentation.Build(DeadeyeGaleAura.ForRecipient([source], new(2, 1), 1, new(0, 0), world: newLease),
            new(2, 1), newLease, () => buff++));
    }
    [Fact]
    public void Nearby_allies_receive_base_Tailwind_not_owners_Gale_effect_and_sources_never_stack()
    {
        var s = Start(); for (int i = 0; i < 10; i++) s.AdmitUse(0, true);
        var world = new object();
        var source = new DeadeyeGaleAura.Source(new(1), 1, new(0, 0), s) { World = world };
        var self = DeadeyeGaleAura.ForRecipient([source], new(1), 1, new(0, 0), world: world);
        Assert.Equal(1.16, self!.Value.ActionFactor(_ => 0), 9);
        var ally = DeadeyeGaleAura.ForRecipient([source, source with { Owner = new(3) }], new(2), 1, new(60, 0), world: world);
        Assert.Equal(1.08, ally!.Value.ActionFactor(_ => 0), 9);
        Assert.Equal(InstanceEntities.AddBuff(2, 5, 1094, 1), ally.Value.Native(2, 5));
        Assert.Null(DeadeyeGaleAura.ForRecipient([source], new(2), 1, new(60.01, 0), world: world));
        Assert.Null(DeadeyeGaleAura.ForRecipient([source], new(2), 2, new(0, 0), world: world));
        Assert.Null(DeadeyeGaleAura.ForRecipient([source], new(2), 1, new(0, 0), alive: false, world: world));
        s.Suspend(1); Assert.Null(DeadeyeGaleAura.ForRecipient([source], new(2), 1, new(0, 0), world: world));
        s.Resume(2); s.Clear(retire: true);
        Assert.Null(DeadeyeGaleAura.ForRecipient([source], new(2), 1, new(0, 0), world: world));
    }
    static Dictionary<string, int> Sources => new() { [DeadeyeGaleState.EffectPerStack] = 10,
        [DeadeyeGaleState.TakenPerStack] = -4, [DeadeyeGaleState.LoseOnHit] = 1 };
    static DeadeyeGaleState Start(Dictionary<string, int>? stats = null)
    { var state = new DeadeyeGaleState(); state.Observe(0, true, (stats ?? Sources).GetValueOrDefault); return state; }

    [Fact]
    public void Accepted_owner_uses_grant_one_independent_four_second_stack_rejected_and_actor_uses_do_not()
    {
        var s = Start();
        Assert.False(s.AdmitUse(0, false)); Assert.False(s.AdmitUse(0, true, false));
        Assert.True(s.AdmitUse(0, true)); Assert.True(s.AdmitUse(1, true));
        s.Advance(3.999); Assert.Equal(2, s.Count);
        s.Advance(4); Assert.Equal(1, s.Count);
        Assert.Equal(.96, s.DamageTakenFactor, 9);
        s.Advance(5); Assert.Equal(0, s.Count); Assert.True(s.Tailwind);
    }

    [Fact]
    public void Ten_stack_cap_retains_newest_independent_grants_without_refreshing_the_other_nine()
    {
        var s = Start(); for (int i = 0; i < 12; i++) s.AdmitUse(i / 10d, true);
        Assert.Equal(10, s.Count); Assert.Equal(100, s.TailwindEffect); Assert.Equal(.6, s.DamageTakenFactor);
        s.Advance(4.19); Assert.Equal(10, s.Count);
        s.Advance(4.21); Assert.Equal(9, s.Count);
        s.Advance(5.1); Assert.Equal(0, s.Count);
    }

    [Fact]
    public void Wind_Ward_mitigates_the_accepted_hit_before_loss_evade_and_dot_keep_stacks()
    {
        var s = Start(); for (int i = 0; i < 10; i++) s.AdmitUse(0, true);
        Assert.Equal(.6, s.IncomingHit(1, false)); Assert.Equal(10, s.Count);
        Assert.Equal(.6, s.IncomingHit(1, true)); Assert.Equal(0, s.Count);
        Assert.Equal(1, s.IncomingHit(1, true));
        s.AdmitUse(1, true); Assert.Equal(1, s.Count);
        var noWard = Sources; noWard[DeadeyeGaleState.LoseOnHit] = 0;
        s.Observe(1, true, noWard.GetValueOrDefault); s.IncomingHit(1, true); Assert.Equal(1, s.Count);
    }

    [Fact]
    public void Recipient_rate_changes_integrate_elapsed_old_rate_then_new_rate_without_affecting_world_time()
    {
        var stats = Sources; var s = Start(stats); s.AdmitUse(0, true);
        stats["buff_time_passed_+%"] = 100;
        s.Observe(1, true, stats.GetValueOrDefault);
        s.Advance(2.49); Assert.Equal(1, s.Count);
        s.Advance(2.5); Assert.Equal(0, s.Count);
        // Gale category18 excludes category-only acceleration (categories1/7/13/16).
        stats["buff_time_passed_+%"] = 0; stats["buff_time_passed_+%_only_buff_category"] = 900;
        s.Observe(3, true, stats.GetValueOrDefault); s.AdmitUse(3, true);
        s.Advance(6.99); Assert.Equal(1, s.Count); s.Advance(7); Assert.Equal(0, s.Count);
    }

    [Fact]
    public void Source_removal_death_and_retirement_discard_stacks_reallocation_and_relog_start_empty()
    {
        var s = Start(); s.AdmitUse(0, true);
        s.Observe(1, false, Sources.GetValueOrDefault); Assert.False(s.Tailwind); Assert.Equal(0, s.Count);
        s.Observe(1, true, Sources.GetValueOrDefault); Assert.True(s.Tailwind); Assert.Equal(0, s.Count);
        s.AdmitUse(1, true); s.Observe(2, true, Sources.GetValueOrDefault, alive: false);
        Assert.False(s.Tailwind); Assert.Equal(0, s.Count);
        s.Observe(2, true, Sources.GetValueOrDefault); s.AdmitUse(2, true); s.Clear(retire: true);
        s.Observe(3, true, Sources.GetValueOrDefault); Assert.False(s.AdmitUse(3, true));
        Assert.Equal(0, Start().Count);
    }

    [Fact]
    public void Pause_and_failed_loading_do_not_spend_effect_time_successful_transfer_rebases_remaining_duration()
    {
        var s = Start(); s.AdmitUse(0, true); s.Advance(1); s.Advance(1); Assert.Equal(1, s.Count);
        var carried = s.Suspend(1); Assert.Equal(3, Assert.Single(carried));
        s.Advance(100); Assert.Equal(0, s.Count); // suspended source exposes no effects during loading
        s.Resume(100); Assert.Equal(1, s.Count); s.Advance(102.9); Assert.Equal(1, s.Count);
        var next = Start(); next.Restore(0, s.Suspend(102.9)); next.Advance(.11); Assert.Equal(0, next.Count);
    }

    [Fact]
    public void Native_packets_use_exact_buff_rows_base_value_independent_expiry_and_at_gate_source_removal()
    {
        var s = Start(); var p = new DeadeyeGalePresentation(); ushort next = 10;
        Assert.Equal(new[] { InstanceEntities.AddBuff(1, 10, 1094, 1) },
            p.Build(s, 0, 1, () => next++));
        s.AdmitUse(0, true); s.AdmitUse(1, true);
        var packets = p.Build(s, 1, 1, () => next++);
        Assert.Equal(2, packets.Count);
        Assert.Equal(InstanceEntities.AddBuff(1, 11, 1095, 1,
            u68: BitConverter.SingleToUInt32Bits(3), stacks: 1), packets[0]);
        Assert.Empty(p.Build(s, 1, 1, () => next++));
        s.Observe(1, false, Sources.GetValueOrDefault);
        // Queued presentation samples now: only removals, never the stale grant.
        Assert.Equal(3, p.Build(s, 1, 1, () => next++).Count);
        Assert.Empty(p.Build(s, 1, 1, () => next++));
    }

    [Fact]
    public void Changed_recipient_clock_replaces_native_duration_without_duplicate_tailwind_or_stack()
    {
        var stats = Sources; var s = Start(stats); s.AdmitUse(0, true);
        var p = new DeadeyeGalePresentation(); ushort next = 1;
        Assert.Equal(2, p.Build(s, 0, 1, () => next++).Count);
        long before = s.Revision;
        stats["buff_time_passed_+%"] = 100; s.Observe(1, true, stats.GetValueOrDefault);
        Assert.True(s.Revision > before); // the live ticker must publish changed remaining duration
        var changes = p.Build(s, 1, 1, () => next++);
        Assert.Equal(2, changes.Count);
        Assert.Equal(InstanceEntities.RemoveBuff(1, 2), changes[0]);
        Assert.Equal(InstanceEntities.AddBuff(1, 3, 1095, 1,
            u68: BitConverter.SingleToUInt32Bits(1.5f), stacks: 1), changes[1]);
    }

    [Theory]
    [InlineData(0, 8)] [InlineData(1, 8)] [InlineData(5, 12)] [InlineData(10, 16)]
    public void Tailwind_uses_shared_action_speed_contract_once_and_keeps_cooldown_recovery_separate(int count, int expected)
    {
        var s = Start(); for (int i = 0; i < count; i++) s.AdmitUse(0, true);
        var raw = new Dictionary<string, int> { [ActionSpeed.Reduction] = -8, [ActionSpeed.HasTailwind] = 1,
            [ActionSpeed.TailwindEffect] = s.TailwindEffect, ["base_cooldown_speed_+%"] = 500 };
        var action = ActionSpeed.From(raw.GetValueOrDefault); Assert.Equal(expected, action.Percent);
        Assert.Equal(1.5 * (1 + expected / 100d), action.MovementFactor(50), 9);
        // Independent ordinary attack/cast speed composes multiplicatively with this factor.
        Assert.Equal(1 / (1.5 * action.Factor), SkillAttackTiming.Seconds(1000, 50, 0) / action.Factor, 9);
        var cast = SpellCastTiming.Calculate(1500, 50).DurationMilliseconds!.Value;
        Assert.Equal(1 / action.Factor, cast / 1000d / action.Factor, 9);
        raw["base_cooldown_speed_+%"] = 0; Assert.Equal(action, ActionSpeed.From(raw.GetValueOrDefault));
    }
}
