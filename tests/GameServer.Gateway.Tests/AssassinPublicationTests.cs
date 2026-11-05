namespace GameServer.Gateway.Tests;

// Serializable case IDs retain each original scenario and parameter row.
// Each dispatch creates the original fixture; its scenario owns setup and disposal.
public sealed class AssassinPublicationTests
{
    [Theory]
    [InlineData("AssassinCriticalExecutionTests.Deathmarked_uses_pinned_grant_replaces_and_removes_with_source_and_respects_admission(ruthless: false)")]
    [InlineData("AssassinCriticalExecutionTests.Deathmarked_uses_pinned_grant_replaces_and_removes_with_source_and_respects_admission(ruthless: true)")]
    [InlineData("AssassinCriticalExecutionTests.Selected_fireball_publishes_mystical_less_before_cap_and_independent_probabilities")]
    [InlineData("AssassinCriticalExecutionTests.Protected_projectile_hit_keeps_source_critical_impale(bifurcate: false, stored: 25)")]
    [InlineData("AssassinCriticalExecutionTests.Protected_projectile_hit_keeps_source_critical_impale(bifurcate: true, stored: 40)")]
    [InlineData("AssassinCriticalExecutionTests.Inquisitor_sources_and_assassin_release_compose_with_live_mark_and_refund")]
    [InlineData("AssassinCriticalExecutionTests.Released_real_fireball_retains_exact_chance_and_bonus_across_refund_and_live_target_life")]
    [InlineData("AssassinCriticalExecutionTests.Actual_rf_burn_uses_one_opportunistic_factor(ruthless: false, nearby: 1, expected: 125, resistance: 0)")]
    [InlineData("AssassinCriticalExecutionTests.Actual_rf_burn_uses_one_opportunistic_factor(ruthless: false, nearby: 2, expected: 100, resistance: 0)")]
    [InlineData("AssassinCriticalExecutionTests.Actual_rf_burn_uses_one_opportunistic_factor(ruthless: false, nearby: 0, expected: 125, resistance: 0)")]
    [InlineData("AssassinCriticalExecutionTests.Actual_rf_burn_uses_one_opportunistic_factor(ruthless: false, nearby: 1, expected: 100, resistance: 20)")]
    [InlineData("AssassinCriticalExecutionTests.Actual_rf_burn_uses_one_opportunistic_factor(ruthless: true, nearby: 1, expected: 120, resistance: 0)")]
    [InlineData("AssassinCriticalExecutionTests.Actual_rf_burn_uses_one_opportunistic_factor(ruthless: true, nearby: 2, expected: 100, resistance: 0)")]
    [InlineData("AssassinCriticalExecutionTests.Uncapped_rational_conditions_luck_and_noextra_keep_status_separate")]
    [InlineData("AssassinCriticalExecutionTests.Catalog_exclusive_choice_refund_and_pre_hit_boundary(option: 21264, lowChance: false)")]
    [InlineData("AssassinCriticalExecutionTests.Catalog_exclusive_choice_refund_and_pre_hit_boundary(option: 18335, lowChance: true)")]
    [InlineData("AssassinCriticalExecutionTests.Paid_area_contact_keeps_two_success_damage_and_actual_single_poison_admission")]
    [InlineData("AssassinCriticalExecutionTests.Existing_guaranteed_rod_policy_excludes_bifurcation_and_cannot_crit_wins")]
    [InlineData("AssassinCriticalExecutionTests.Mirage_ailments_do_not_grant_owner_assassin_procs")]
    [InlineData("AssassinCriticalExecutionTests.Owner_lease_and_poison_observation_hold_gate_for_the_entire_state_mutation")]
    [InlineData("AssassinCriticalExecutionTests.Stat_and_movement_observers_wait_for_monster_gate_through_expiry_and_application")]
    [InlineData("OrdinaryAssassinConnectedTests.Converted_elemental_poison_uses_history_and_applies_critical_bonus_once_at_real_admission")]
    [InlineData("OrdinaryAssassinConnectedTests.Accepted_poison_history_drives_merged_duration_and_single_live_to_dead_recovery")]
    [InlineData("OrdinaryAssassinConnectedTests.Catalog_choice_and_paid_refund_reload_preserve_ruthless_sources")]
    [InlineData("OrdinaryAssassinConnectedTests.Accepted_critical_hit_grants_owned_nonrefreshing_elusive_and_retirement_removes_native_buff")]
    [InlineData("OrdinaryAssassinRulesTests.Choice_conditions_use_pre_hit_life_and_keep_chance_separate_from_multiplier")]
    [InlineData("OrdinaryAssassinRulesTests.Spell_less_composes_before_cap_and_cannot_crit_wins")]
    [InlineData("OrdinaryAssassinRulesTests.Opportunistic_mode_values_and_two_enemy_boundary(more: 25, reduced: -35)")]
    [InlineData("OrdinaryAssassinRulesTests.Opportunistic_mode_values_and_two_enemy_boundary(more: 20, reduced: -20)")]
    [InlineData("OrdinaryAssassinRulesTests.Poison_uses_ailment_settings_not_hit_multiplier_and_tracks_successes_only")]
    [InlineData("OrdinaryAssassinRulesTests.Poison_eligibility_respects_all_damage_and_chaos_prohibition")]
    [InlineData("OrdinaryAssassinRulesTests.Poison_kill_recovery_uses_live_stacks_and_cannot_recover")]
    [InlineData("OrdinaryAssassinRulesTests.Bifurcation_reuses_two_independent_action_rolls_and_obeys_prohibition")]
    [InlineData("OrdinaryAssassinRulesTests.Deathmarked_scales_with_maximum_not_held_charges_and_removes_cleanly")]
    [InlineData("OrdinaryAssassinRulesTests.Adapters_gate_shared_table_identity_and_clear_dead_owner_state")]
    public async Task AssassinOffenseAndProvenanceCases(string caseId)
    {
        switch (caseId)
        {
            case "AssassinCriticalExecutionTests.Deathmarked_uses_pinned_grant_replaces_and_removes_with_source_and_respects_admission(ruthless: false)":
                new AssassinCriticalExecutionTests().Deathmarked_uses_pinned_grant_replaces_and_removes_with_source_and_respects_admission(false);
                break;
            case "AssassinCriticalExecutionTests.Deathmarked_uses_pinned_grant_replaces_and_removes_with_source_and_respects_admission(ruthless: true)":
                new AssassinCriticalExecutionTests().Deathmarked_uses_pinned_grant_replaces_and_removes_with_source_and_respects_admission(true);
                break;
            case "AssassinCriticalExecutionTests.Selected_fireball_publishes_mystical_less_before_cap_and_independent_probabilities":
                new AssassinCriticalExecutionTests().Selected_fireball_publishes_mystical_less_before_cap_and_independent_probabilities();
                break;
            case "AssassinCriticalExecutionTests.Protected_projectile_hit_keeps_source_critical_impale(bifurcate: false, stored: 25)":
                await new AssassinCriticalExecutionTests().Protected_projectile_hit_keeps_source_critical_impale(false,25);
                break;
            case "AssassinCriticalExecutionTests.Protected_projectile_hit_keeps_source_critical_impale(bifurcate: true, stored: 40)":
                await new AssassinCriticalExecutionTests().Protected_projectile_hit_keeps_source_critical_impale(true,40);
                break;
            case "AssassinCriticalExecutionTests.Inquisitor_sources_and_assassin_release_compose_with_live_mark_and_refund":
                await new AssassinCriticalExecutionTests().Inquisitor_sources_and_assassin_release_compose_with_live_mark_and_refund();
                break;
            case "AssassinCriticalExecutionTests.Released_real_fireball_retains_exact_chance_and_bonus_across_refund_and_live_target_life":
                await new AssassinCriticalExecutionTests().Released_real_fireball_retains_exact_chance_and_bonus_across_refund_and_live_target_life();
                break;
            case "AssassinCriticalExecutionTests.Actual_rf_burn_uses_one_opportunistic_factor(ruthless: false, nearby: 1, expected: 125, resistance: 0)":
                await new AssassinCriticalExecutionTests().Actual_rf_burn_uses_one_opportunistic_factor(false,1,125,0);
                break;
            case "AssassinCriticalExecutionTests.Actual_rf_burn_uses_one_opportunistic_factor(ruthless: false, nearby: 2, expected: 100, resistance: 0)":
                await new AssassinCriticalExecutionTests().Actual_rf_burn_uses_one_opportunistic_factor(false,2,100,0);
                break;
            case "AssassinCriticalExecutionTests.Actual_rf_burn_uses_one_opportunistic_factor(ruthless: false, nearby: 0, expected: 125, resistance: 0)":
                await new AssassinCriticalExecutionTests().Actual_rf_burn_uses_one_opportunistic_factor(false,0,125,0);
                break;
            case "AssassinCriticalExecutionTests.Actual_rf_burn_uses_one_opportunistic_factor(ruthless: false, nearby: 1, expected: 100, resistance: 20)":
                await new AssassinCriticalExecutionTests().Actual_rf_burn_uses_one_opportunistic_factor(false,1,100,20);
                break;
            case "AssassinCriticalExecutionTests.Actual_rf_burn_uses_one_opportunistic_factor(ruthless: true, nearby: 1, expected: 120, resistance: 0)":
                await new AssassinCriticalExecutionTests().Actual_rf_burn_uses_one_opportunistic_factor(true,1,120,0);
                break;
            case "AssassinCriticalExecutionTests.Actual_rf_burn_uses_one_opportunistic_factor(ruthless: true, nearby: 2, expected: 100, resistance: 0)":
                await new AssassinCriticalExecutionTests().Actual_rf_burn_uses_one_opportunistic_factor(true,2,100,0);
                break;
            case "AssassinCriticalExecutionTests.Uncapped_rational_conditions_luck_and_noextra_keep_status_separate":
                new AssassinCriticalExecutionTests().Uncapped_rational_conditions_luck_and_noextra_keep_status_separate();
                break;
            case "AssassinCriticalExecutionTests.Catalog_exclusive_choice_refund_and_pre_hit_boundary(option: 21264, lowChance: false)":
                new AssassinCriticalExecutionTests().Catalog_exclusive_choice_refund_and_pre_hit_boundary(21264, false);
                break;
            case "AssassinCriticalExecutionTests.Catalog_exclusive_choice_refund_and_pre_hit_boundary(option: 18335, lowChance: true)":
                new AssassinCriticalExecutionTests().Catalog_exclusive_choice_refund_and_pre_hit_boundary(18335, true);
                break;
            case "AssassinCriticalExecutionTests.Paid_area_contact_keeps_two_success_damage_and_actual_single_poison_admission":
                await new AssassinCriticalExecutionTests().Paid_area_contact_keeps_two_success_damage_and_actual_single_poison_admission();
                break;
            case "AssassinCriticalExecutionTests.Existing_guaranteed_rod_policy_excludes_bifurcation_and_cannot_crit_wins":
                new AssassinCriticalExecutionTests().Existing_guaranteed_rod_policy_excludes_bifurcation_and_cannot_crit_wins();
                break;
            case "AssassinCriticalExecutionTests.Mirage_ailments_do_not_grant_owner_assassin_procs":
                await new AssassinCriticalExecutionTests().Mirage_ailments_do_not_grant_owner_assassin_procs();
                break;
            case "AssassinCriticalExecutionTests.Owner_lease_and_poison_observation_hold_gate_for_the_entire_state_mutation":
                new AssassinCriticalExecutionTests().Owner_lease_and_poison_observation_hold_gate_for_the_entire_state_mutation();
                break;
            case "AssassinCriticalExecutionTests.Stat_and_movement_observers_wait_for_monster_gate_through_expiry_and_application":
                await new AssassinCriticalExecutionTests().Stat_and_movement_observers_wait_for_monster_gate_through_expiry_and_application();
                break;
            case "OrdinaryAssassinConnectedTests.Converted_elemental_poison_uses_history_and_applies_critical_bonus_once_at_real_admission":
                new OrdinaryAssassinConnectedTests().Converted_elemental_poison_uses_history_and_applies_critical_bonus_once_at_real_admission();
                break;
            case "OrdinaryAssassinConnectedTests.Accepted_poison_history_drives_merged_duration_and_single_live_to_dead_recovery":
                new OrdinaryAssassinConnectedTests().Accepted_poison_history_drives_merged_duration_and_single_live_to_dead_recovery();
                break;
            case "OrdinaryAssassinConnectedTests.Catalog_choice_and_paid_refund_reload_preserve_ruthless_sources":
                new OrdinaryAssassinConnectedTests().Catalog_choice_and_paid_refund_reload_preserve_ruthless_sources();
                break;
            case "OrdinaryAssassinConnectedTests.Accepted_critical_hit_grants_owned_nonrefreshing_elusive_and_retirement_removes_native_buff":
                new OrdinaryAssassinConnectedTests().Accepted_critical_hit_grants_owned_nonrefreshing_elusive_and_retirement_removes_native_buff();
                break;
            case "OrdinaryAssassinRulesTests.Choice_conditions_use_pre_hit_life_and_keep_chance_separate_from_multiplier":
                new OrdinaryAssassinRulesTests().Choice_conditions_use_pre_hit_life_and_keep_chance_separate_from_multiplier();
                break;
            case "OrdinaryAssassinRulesTests.Spell_less_composes_before_cap_and_cannot_crit_wins":
                new OrdinaryAssassinRulesTests().Spell_less_composes_before_cap_and_cannot_crit_wins();
                break;
            case "OrdinaryAssassinRulesTests.Opportunistic_mode_values_and_two_enemy_boundary(more: 25, reduced: -35)":
                new OrdinaryAssassinRulesTests().Opportunistic_mode_values_and_two_enemy_boundary(25, -35);
                break;
            case "OrdinaryAssassinRulesTests.Opportunistic_mode_values_and_two_enemy_boundary(more: 20, reduced: -20)":
                new OrdinaryAssassinRulesTests().Opportunistic_mode_values_and_two_enemy_boundary(20, -20);
                break;
            case "OrdinaryAssassinRulesTests.Poison_uses_ailment_settings_not_hit_multiplier_and_tracks_successes_only":
                new OrdinaryAssassinRulesTests().Poison_uses_ailment_settings_not_hit_multiplier_and_tracks_successes_only();
                break;
            case "OrdinaryAssassinRulesTests.Poison_eligibility_respects_all_damage_and_chaos_prohibition":
                new OrdinaryAssassinRulesTests().Poison_eligibility_respects_all_damage_and_chaos_prohibition();
                break;
            case "OrdinaryAssassinRulesTests.Poison_kill_recovery_uses_live_stacks_and_cannot_recover":
                new OrdinaryAssassinRulesTests().Poison_kill_recovery_uses_live_stacks_and_cannot_recover();
                break;
            case "OrdinaryAssassinRulesTests.Bifurcation_reuses_two_independent_action_rolls_and_obeys_prohibition":
                new OrdinaryAssassinRulesTests().Bifurcation_reuses_two_independent_action_rolls_and_obeys_prohibition();
                break;
            case "OrdinaryAssassinRulesTests.Deathmarked_scales_with_maximum_not_held_charges_and_removes_cleanly":
                new OrdinaryAssassinRulesTests().Deathmarked_scales_with_maximum_not_held_charges_and_removes_cleanly();
                break;
            case "OrdinaryAssassinRulesTests.Adapters_gate_shared_table_identity_and_clear_dead_owner_state":
                new OrdinaryAssassinRulesTests().Adapters_gate_shared_table_identity_and_clear_dead_owner_state();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(caseId), caseId, "Unknown Assassin scenario");
        }
    }

    [Theory]
    [InlineData("AssassinIncomingCompletionTests.Projectile_settled_incoming_damage_reaches_current_native_Life_and_death(lethal: false, trimBeforeRecovery: false)")]
    [InlineData("AssassinIncomingCompletionTests.Projectile_settled_incoming_damage_reaches_current_native_Life_and_death(lethal: true, trimBeforeRecovery: false)")]
    [InlineData("AssassinIncomingCompletionTests.Projectile_settled_incoming_damage_reaches_current_native_Life_and_death(lethal: true, trimBeforeRecovery: true)")]
    [InlineData("AssassinIncomingCompletionTests.Actual_motion_callback_settles_stationary_bleed_before_moving_interval")]
    [InlineData("AssassinIncomingCompletionTests.Executing_MonsterCleave_retains_unbaked_31_percent_with_real_poison_and_bleed_sources")]
    [InlineData("AssassinIncomingCompletionTests.Actual_monster_spell_skips_attack_evasion_and_uses_spell_block")]
    [InlineData("AssassinIncomingCompletionTests.Real_admission_ticks_life_and_unaffected_retains_presence_until_source_refund(ruthless: false)")]
    [InlineData("AssassinIncomingCompletionTests.Real_admission_ticks_life_and_unaffected_retains_presence_until_source_refund(ruthless: true)")]
    [InlineData("AssassinIncomingCompletionTests.Poison_stacks_and_critical_base_is_separate_from_hit_critical_multiplier")]
    [InlineData("AssassinIncomingCompletionTests.Pinned_real_monster_mod_admits_poison_without_granting_it_to_unrelated_monsters")]
    [InlineData("AssassinIncomingCompletionTests.Poison_bypasses_ES_bleed_uses_ES_and_ticks_never_start_hit_recoup")]
    [InlineData("AssassinIncomingCompletionTests.Live_chaos_resistance_and_physical_reduction_apply_to_ticks_but_armour_and_hit_avoidance_do_not")]
    [InlineData("AssassinIncomingCompletionTests.No_chance_no_unrelated_poison_spell_no_bleed_and_prohibitions_and_block_are_authoritative")]
    [InlineData("AssassinIncomingCompletionTests.Carry_transfer_freezes_duration_and_stale_owner_pause_and_death_cannot_mutate_resources")]
    [InlineData("AssassinIncomingCompletionTests.Moving_bleed_triples_only_bleed_and_weaker_bleed_resumes")]
    [InlineData("AssassinIncomingCompletionTests.Actual_walk_arrival_splits_bleed_ticks_without_tripling_poison")]
    [InlineData("AssassinIncomingCompletionTests.Legal_Shadowed_Blood_allocation_and_paid_refund_changes_damage_without_removing_ailments(ruthless: false)")]
    [InlineData("AssassinIncomingCompletionTests.Legal_Shadowed_Blood_allocation_and_paid_refund_changes_damage_without_removing_ailments(ruthless: true)")]
    [InlineData("AssassinIncomingCompletionTests.Source_retirement_blocks_all_new_mutations_and_ignored_fraction_never_catches_up")]
    [InlineData("AssassinIncomingCompletionTests.Avoided_attack_and_spell_admission_precedes_real_hit_HP_history_and_presentation(spell: false)")]
    [InlineData("AssassinIncomingCompletionTests.Avoided_attack_and_spell_admission_precedes_real_hit_HP_history_and_presentation(spell: true)")]
    [InlineData("OrdinaryAssassinConnectedTests.Unaffected_ignite_retains_presence_and_does_not_accumulate_fractional_damage")]
    [InlineData("OrdinaryAssassinConnectedTests.Opportunistic_more_and_additive_incoming_bucket_and_effective_maximum_instant_leech_are_connected")]
    [InlineData("OrdinaryAssassinRulesTests.Elusive_does_not_refresh_and_expires_at_zero(effect: 110, scale: 2.1)")]
    [InlineData("OrdinaryAssassinRulesTests.Elusive_does_not_refresh_and_expires_at_zero(effect: 10, scale: 1.1)")]
    [InlineData("OrdinaryAssassinRulesTests.Refund_source_removal_death_and_transfer_do_not_resurrect_state")]
    [InlineData("OrdinaryAssassinRulesTests.Shadowed_blood_existing_recoup_consumer_is_physical_hits_only")]
    public async Task AssassinIncomingAndLifecycleCases(string caseId)
    {
        switch (caseId)
        {
            case "AssassinIncomingCompletionTests.Projectile_settled_incoming_damage_reaches_current_native_Life_and_death(lethal: false, trimBeforeRecovery: false)":
                await new AssassinIncomingCompletionTests().Projectile_settled_incoming_damage_reaches_current_native_Life_and_death(false, false);
                break;
            case "AssassinIncomingCompletionTests.Projectile_settled_incoming_damage_reaches_current_native_Life_and_death(lethal: true, trimBeforeRecovery: false)":
                await new AssassinIncomingCompletionTests().Projectile_settled_incoming_damage_reaches_current_native_Life_and_death(true, false);
                break;
            case "AssassinIncomingCompletionTests.Projectile_settled_incoming_damage_reaches_current_native_Life_and_death(lethal: true, trimBeforeRecovery: true)":
                await new AssassinIncomingCompletionTests().Projectile_settled_incoming_damage_reaches_current_native_Life_and_death(true, true);
                break;
            case "AssassinIncomingCompletionTests.Actual_motion_callback_settles_stationary_bleed_before_moving_interval":
                new AssassinIncomingCompletionTests().Actual_motion_callback_settles_stationary_bleed_before_moving_interval();
                break;
            case "AssassinIncomingCompletionTests.Executing_MonsterCleave_retains_unbaked_31_percent_with_real_poison_and_bleed_sources":
                new AssassinIncomingCompletionTests().Executing_MonsterCleave_retains_unbaked_31_percent_with_real_poison_and_bleed_sources();
                break;
            case "AssassinIncomingCompletionTests.Actual_monster_spell_skips_attack_evasion_and_uses_spell_block":
                new AssassinIncomingCompletionTests().Actual_monster_spell_skips_attack_evasion_and_uses_spell_block();
                break;
            case "AssassinIncomingCompletionTests.Real_admission_ticks_life_and_unaffected_retains_presence_until_source_refund(ruthless: false)":
                new AssassinIncomingCompletionTests().Real_admission_ticks_life_and_unaffected_retains_presence_until_source_refund(false);
                break;
            case "AssassinIncomingCompletionTests.Real_admission_ticks_life_and_unaffected_retains_presence_until_source_refund(ruthless: true)":
                new AssassinIncomingCompletionTests().Real_admission_ticks_life_and_unaffected_retains_presence_until_source_refund(true);
                break;
            case "AssassinIncomingCompletionTests.Poison_stacks_and_critical_base_is_separate_from_hit_critical_multiplier":
                new AssassinIncomingCompletionTests().Poison_stacks_and_critical_base_is_separate_from_hit_critical_multiplier();
                break;
            case "AssassinIncomingCompletionTests.Pinned_real_monster_mod_admits_poison_without_granting_it_to_unrelated_monsters":
                new AssassinIncomingCompletionTests().Pinned_real_monster_mod_admits_poison_without_granting_it_to_unrelated_monsters();
                break;
            case "AssassinIncomingCompletionTests.Poison_bypasses_ES_bleed_uses_ES_and_ticks_never_start_hit_recoup":
                new AssassinIncomingCompletionTests().Poison_bypasses_ES_bleed_uses_ES_and_ticks_never_start_hit_recoup();
                break;
            case "AssassinIncomingCompletionTests.Live_chaos_resistance_and_physical_reduction_apply_to_ticks_but_armour_and_hit_avoidance_do_not":
                new AssassinIncomingCompletionTests().Live_chaos_resistance_and_physical_reduction_apply_to_ticks_but_armour_and_hit_avoidance_do_not();
                break;
            case "AssassinIncomingCompletionTests.No_chance_no_unrelated_poison_spell_no_bleed_and_prohibitions_and_block_are_authoritative":
                new AssassinIncomingCompletionTests().No_chance_no_unrelated_poison_spell_no_bleed_and_prohibitions_and_block_are_authoritative();
                break;
            case "AssassinIncomingCompletionTests.Carry_transfer_freezes_duration_and_stale_owner_pause_and_death_cannot_mutate_resources":
                new AssassinIncomingCompletionTests().Carry_transfer_freezes_duration_and_stale_owner_pause_and_death_cannot_mutate_resources();
                break;
            case "AssassinIncomingCompletionTests.Moving_bleed_triples_only_bleed_and_weaker_bleed_resumes":
                new AssassinIncomingCompletionTests().Moving_bleed_triples_only_bleed_and_weaker_bleed_resumes();
                break;
            case "AssassinIncomingCompletionTests.Actual_walk_arrival_splits_bleed_ticks_without_tripling_poison":
                new AssassinIncomingCompletionTests().Actual_walk_arrival_splits_bleed_ticks_without_tripling_poison();
                break;
            case "AssassinIncomingCompletionTests.Legal_Shadowed_Blood_allocation_and_paid_refund_changes_damage_without_removing_ailments(ruthless: false)":
                new AssassinIncomingCompletionTests().Legal_Shadowed_Blood_allocation_and_paid_refund_changes_damage_without_removing_ailments(false);
                break;
            case "AssassinIncomingCompletionTests.Legal_Shadowed_Blood_allocation_and_paid_refund_changes_damage_without_removing_ailments(ruthless: true)":
                new AssassinIncomingCompletionTests().Legal_Shadowed_Blood_allocation_and_paid_refund_changes_damage_without_removing_ailments(true);
                break;
            case "AssassinIncomingCompletionTests.Source_retirement_blocks_all_new_mutations_and_ignored_fraction_never_catches_up":
                new AssassinIncomingCompletionTests().Source_retirement_blocks_all_new_mutations_and_ignored_fraction_never_catches_up();
                break;
            case "AssassinIncomingCompletionTests.Avoided_attack_and_spell_admission_precedes_real_hit_HP_history_and_presentation(spell: false)":
                new AssassinIncomingCompletionTests().Avoided_attack_and_spell_admission_precedes_real_hit_HP_history_and_presentation(false);
                break;
            case "AssassinIncomingCompletionTests.Avoided_attack_and_spell_admission_precedes_real_hit_HP_history_and_presentation(spell: true)":
                new AssassinIncomingCompletionTests().Avoided_attack_and_spell_admission_precedes_real_hit_HP_history_and_presentation(true);
                break;
            case "OrdinaryAssassinConnectedTests.Unaffected_ignite_retains_presence_and_does_not_accumulate_fractional_damage":
                new OrdinaryAssassinConnectedTests().Unaffected_ignite_retains_presence_and_does_not_accumulate_fractional_damage();
                break;
            case "OrdinaryAssassinConnectedTests.Opportunistic_more_and_additive_incoming_bucket_and_effective_maximum_instant_leech_are_connected":
                new OrdinaryAssassinConnectedTests().Opportunistic_more_and_additive_incoming_bucket_and_effective_maximum_instant_leech_are_connected();
                break;
            case "OrdinaryAssassinRulesTests.Elusive_does_not_refresh_and_expires_at_zero(effect: 110, scale: 2.1)":
                new OrdinaryAssassinRulesTests().Elusive_does_not_refresh_and_expires_at_zero(110, 2.1);
                break;
            case "OrdinaryAssassinRulesTests.Elusive_does_not_refresh_and_expires_at_zero(effect: 10, scale: 1.1)":
                new OrdinaryAssassinRulesTests().Elusive_does_not_refresh_and_expires_at_zero(10, 1.1);
                break;
            case "OrdinaryAssassinRulesTests.Refund_source_removal_death_and_transfer_do_not_resurrect_state":
                new OrdinaryAssassinRulesTests().Refund_source_removal_death_and_transfer_do_not_resurrect_state();
                break;
            case "OrdinaryAssassinRulesTests.Shadowed_blood_existing_recoup_consumer_is_physical_hits_only":
                new OrdinaryAssassinRulesTests().Shadowed_blood_existing_recoup_consumer_is_physical_hits_only();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(caseId), caseId, "Unknown Assassin scenario");
        }
    }

}
