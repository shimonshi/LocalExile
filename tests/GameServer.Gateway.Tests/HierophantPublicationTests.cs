namespace GameServer.Gateway.Tests;

// Serializable case IDs retain each original scenario and parameter row.
// Each dispatch creates the original fixture; its scenario owns setup and disposal.
public sealed class HierophantPublicationTests
{
    [Theory]
    [InlineData("OrdinaryHierophantAdapterTests.Candidate_adapter_projects_live_totem_sources_and_does_not_leak_to_phrecia_or_dead_player")]
    [InlineData("OrdinaryHierophantAdapterTests.Audit_covers_exactly_the_pinned_Default_graph_members")]
    [InlineData("OrdinaryHierophantConnectedTests.Accepted_spell_hit_uses_pretrigger_snapshot_and_expiry_publishes_native_removal_and_stats")]
    [InlineData("OrdinaryHierophantConnectedTests.Actual_payment_free_refusal_and_lifetap_preserve_predebit_offense")]
    [InlineData("OrdinaryHierophantConnectedTests.Real_catalog_allocation_paid_refund_save_reload_and_class_replacement_reconcile_sources")]
    [InlineData("OrdinaryHierophantConnectedTests.Surge_ailment_contribution_reconciles_existing_ailments_and_ends_without_immunity")]
    [InlineData("OrdinaryHierophantConnectedTests.Live_chill_reconciliation_retimes_active_walk_on_surge_grant_and_expiry")]
    [InlineData("OrdinaryHierophantTests.Identity_separates_families_classes_and_revisions")]
    [InlineData("OrdinaryHierophantTests.Only_landed_owned_spell_hits_grant_normal_surge_and_refresh_exact_expiry")]
    [InlineData("OrdinaryHierophantTests.Ruthless_spends_actual_mana_with_threshold_remainder_and_no_normal_hit_trigger")]
    [InlineData("OrdinaryHierophantTests.Refund_mode_change_identity_removal_and_death_clear_owned_progress")]
    [InlineData("OrdinaryHierophantTests.Transfer_freezes_remaining_duration_and_progress_but_cannot_restore_refunded_source")]
    [InlineData("OrdinaryHierophantTests.Area_uses_unreserved_maximum_complete_groups_and_cap(mana: 0, expected: 0)")]
    [InlineData("OrdinaryHierophantTests.Area_uses_unreserved_maximum_complete_groups_and_cap(mana: 49, expected: 0)")]
    [InlineData("OrdinaryHierophantTests.Area_uses_unreserved_maximum_complete_groups_and_cap(mana: 50, expected: 1)")]
    [InlineData("OrdinaryHierophantTests.Area_uses_unreserved_maximum_complete_groups_and_cap(mana: 4999, expected: 99)")]
    [InlineData("OrdinaryHierophantTests.Area_uses_unreserved_maximum_complete_groups_and_cap(mana: 5000, expected: 100)")]
    [InlineData("OrdinaryHierophantTests.Area_uses_unreserved_maximum_complete_groups_and_cap(mana: 10000, expected: 100)")]
    [InlineData("OrdinaryHierophantTests.Mana_conversion_final_cost_and_damage_preserve_scope_and_other_multipliers")]
    [InlineData("OrdinaryHierophantTests.Conditional_sources_reach_existing_leech_and_area_contracts_and_expire_cleanly")]
    [InlineData("OrdinaryHierophantTests.Existing_charge_contract_handles_normal_and_ruthless_minima_without_custom_charge_engine")]
    [InlineData("DistillerAdmissionTests.Mixed_Hierophant_payment_keeps_predebit_Surge")]
    public void HierophantRulesAndResourceCases(string caseId)
    {
        switch (caseId)
        {
            case "OrdinaryHierophantAdapterTests.Candidate_adapter_projects_live_totem_sources_and_does_not_leak_to_phrecia_or_dead_player":
                new OrdinaryHierophantAdapterTests().Candidate_adapter_projects_live_totem_sources_and_does_not_leak_to_phrecia_or_dead_player();
                break;
            case "OrdinaryHierophantAdapterTests.Audit_covers_exactly_the_pinned_Default_graph_members":
                new OrdinaryHierophantAdapterTests().Audit_covers_exactly_the_pinned_Default_graph_members();
                break;
            case "OrdinaryHierophantConnectedTests.Accepted_spell_hit_uses_pretrigger_snapshot_and_expiry_publishes_native_removal_and_stats":
                new OrdinaryHierophantConnectedTests().Accepted_spell_hit_uses_pretrigger_snapshot_and_expiry_publishes_native_removal_and_stats();
                break;
            case "OrdinaryHierophantConnectedTests.Actual_payment_free_refusal_and_lifetap_preserve_predebit_offense":
                new OrdinaryHierophantConnectedTests().Actual_payment_free_refusal_and_lifetap_preserve_predebit_offense();
                break;
            case "OrdinaryHierophantConnectedTests.Real_catalog_allocation_paid_refund_save_reload_and_class_replacement_reconcile_sources":
                new OrdinaryHierophantConnectedTests().Real_catalog_allocation_paid_refund_save_reload_and_class_replacement_reconcile_sources();
                break;
            case "OrdinaryHierophantConnectedTests.Surge_ailment_contribution_reconciles_existing_ailments_and_ends_without_immunity":
                new OrdinaryHierophantConnectedTests().Surge_ailment_contribution_reconciles_existing_ailments_and_ends_without_immunity();
                break;
            case "OrdinaryHierophantConnectedTests.Live_chill_reconciliation_retimes_active_walk_on_surge_grant_and_expiry":
                new OrdinaryHierophantConnectedTests().Live_chill_reconciliation_retimes_active_walk_on_surge_grant_and_expiry();
                break;
            case "OrdinaryHierophantTests.Identity_separates_families_classes_and_revisions":
                new OrdinaryHierophantTests().Identity_separates_families_classes_and_revisions();
                break;
            case "OrdinaryHierophantTests.Only_landed_owned_spell_hits_grant_normal_surge_and_refresh_exact_expiry":
                new OrdinaryHierophantTests().Only_landed_owned_spell_hits_grant_normal_surge_and_refresh_exact_expiry();
                break;
            case "OrdinaryHierophantTests.Ruthless_spends_actual_mana_with_threshold_remainder_and_no_normal_hit_trigger":
                new OrdinaryHierophantTests().Ruthless_spends_actual_mana_with_threshold_remainder_and_no_normal_hit_trigger();
                break;
            case "OrdinaryHierophantTests.Refund_mode_change_identity_removal_and_death_clear_owned_progress":
                new OrdinaryHierophantTests().Refund_mode_change_identity_removal_and_death_clear_owned_progress();
                break;
            case "OrdinaryHierophantTests.Transfer_freezes_remaining_duration_and_progress_but_cannot_restore_refunded_source":
                new OrdinaryHierophantTests().Transfer_freezes_remaining_duration_and_progress_but_cannot_restore_refunded_source();
                break;
            case "OrdinaryHierophantTests.Area_uses_unreserved_maximum_complete_groups_and_cap(mana: 0, expected: 0)":
                new OrdinaryHierophantTests().Area_uses_unreserved_maximum_complete_groups_and_cap(0, 0);
                break;
            case "OrdinaryHierophantTests.Area_uses_unreserved_maximum_complete_groups_and_cap(mana: 49, expected: 0)":
                new OrdinaryHierophantTests().Area_uses_unreserved_maximum_complete_groups_and_cap(49, 0);
                break;
            case "OrdinaryHierophantTests.Area_uses_unreserved_maximum_complete_groups_and_cap(mana: 50, expected: 1)":
                new OrdinaryHierophantTests().Area_uses_unreserved_maximum_complete_groups_and_cap(50, 1);
                break;
            case "OrdinaryHierophantTests.Area_uses_unreserved_maximum_complete_groups_and_cap(mana: 4999, expected: 99)":
                new OrdinaryHierophantTests().Area_uses_unreserved_maximum_complete_groups_and_cap(4999, 99);
                break;
            case "OrdinaryHierophantTests.Area_uses_unreserved_maximum_complete_groups_and_cap(mana: 5000, expected: 100)":
                new OrdinaryHierophantTests().Area_uses_unreserved_maximum_complete_groups_and_cap(5000, 100);
                break;
            case "OrdinaryHierophantTests.Area_uses_unreserved_maximum_complete_groups_and_cap(mana: 10000, expected: 100)":
                new OrdinaryHierophantTests().Area_uses_unreserved_maximum_complete_groups_and_cap(10000, 100);
                break;
            case "OrdinaryHierophantTests.Mana_conversion_final_cost_and_damage_preserve_scope_and_other_multipliers":
                new OrdinaryHierophantTests().Mana_conversion_final_cost_and_damage_preserve_scope_and_other_multipliers();
                break;
            case "OrdinaryHierophantTests.Conditional_sources_reach_existing_leech_and_area_contracts_and_expire_cleanly":
                new OrdinaryHierophantTests().Conditional_sources_reach_existing_leech_and_area_contracts_and_expire_cleanly();
                break;
            case "OrdinaryHierophantTests.Existing_charge_contract_handles_normal_and_ruthless_minima_without_custom_charge_engine":
                new OrdinaryHierophantTests().Existing_charge_contract_handles_normal_and_ruthless_minima_without_custom_charge_engine();
                break;
            case "DistillerAdmissionTests.Mixed_Hierophant_payment_keeps_predebit_Surge":
                OrdinaryGameplayChecks.DistillerHierophant();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(caseId), caseId, "Unknown Hierophant scenario");
        }
    }

    [Theory]
    [InlineData("OrdinaryHierophantConnectedTests.Fresh_capacity_destination_overrides_and_owned_live_totems_reach_real_calculators")]
    [InlineData("OrdinaryHierophantConnectedTests.Transfer_freezes_once_denial_preserves_state_and_retirement_cannot_revive_it")]
    [InlineData("OrdinaryHierophantConnectedTests.Scheduled_release_owns_immutable_surge_and_persistent_tasks_drop_first_use")]
    [InlineData("HierophantEngineCompletionTests.Published_Brand_durations_match_authoritative_budgets_and_supported_modifiers(ruthless: false)")]
    [InlineData("HierophantEngineCompletionTests.Published_Brand_durations_match_authoritative_budgets_and_supported_modifiers(ruthless: true)")]
    [InlineData("HierophantEngineCompletionTests.Zero_Brand_duration_is_published_and_cannot_admit_or_replay_a_live_actor")]
    [InlineData("HierophantEngineCompletionTests.Brand_dispatch_and_activation_preserve_time_conditional_crits_and_owner_procs")]
    [InlineData("HierophantEngineCompletionTests.Dispatcher_Recall_resource_failure_is_atomic_and_accepted_replay_never_pays_again")]
    [InlineData("HierophantEngineCompletionTests.Real_below_level_XP_preserves_actor_and_resource_replacement_never_revives_old_receipts")]
    [InlineData("HierophantEngineCompletionTests.Resource_replacement_rebinds_new_cast_and_never_revives_old_receipts")]
    [InlineData("HierophantEngineCompletionTests.Same_pulse_victims_share_pretrigger_offense_and_next_pulse_benefits")]
    [InlineData("HierophantEngineCompletionTests.Autonomous_projectile_keeps_release_critical_sources_after_live_change")]
    [InlineData("HierophantEngineCompletionTests.Paid_default_Brand_publishes_owned_attachment_before_retained_action")]
    [InlineData("HierophantEngineCompletionTests.Brand_output_revalidates_blocked_lifecycle_and_identity_admission")]
    [InlineData("HierophantEngineCompletionTests.Linked_payload_dispatch_pays_places_and_repeats_without_owner_fallback(payload: \"Ice Nova\")")]
    [InlineData("HierophantEngineCompletionTests.Linked_payload_dispatch_pays_places_and_repeats_without_owner_fallback(payload: \"Freezing Pulse\")")]
    [InlineData("HierophantEngineCompletionTests.New_totem_activation_captures_pre_surge_offense_and_support_damage_once(payload: \"Ice Nova\")")]
    [InlineData("HierophantEngineCompletionTests.New_totem_activation_captures_pre_surge_offense_and_support_damage_once(payload: \"Freezing Pulse\")")]
    [InlineData("HierophantEngineCompletionTests.New_totem_activation_captures_pre_surge_offense_and_support_damage_once(payload: \"Fireball\")")]
    [InlineData("HierophantEngineCompletionTests.New_totem_activation_captures_pre_surge_offense_and_support_damage_once(payload: \"Shockwave Totem\")")]
    [InlineData("HierophantEngineCompletionTests.Brand_budgets_detach_and_exact_late_boundary_are_separate")]
    [InlineData("HierophantEngineCompletionTests.Paid_brand_attaches_hits_detaches_pauses_and_retires_on_source_loss")]
    [InlineData("HierophantEngineCompletionTests.Totem_projectile_contact_has_damage_surge_own_regeneration_and_no_owner_leech_or_elusive(payload: \"Fireball\")")]
    [InlineData("HierophantEngineCompletionTests.Totem_projectile_contact_has_damage_surge_own_regeneration_and_no_owner_leech_or_elusive(payload: \"Freezing Pulse\")")]
    [InlineData("HierophantEngineCompletionTests.Brand_caps_use_original_sources_once_and_reconcile_refunded_limits")]
    [InlineData("HierophantEngineCompletionTests.Placement_reuses_real_native_totem_creation_life_cap_and_presentation")]
    [InlineData("HierophantEngineCompletionTests.Recall_has_real_cooldown_recovery_extra_resource_plan_and_source_validation")]
    [InlineData("HierophantEngineCompletionTests.Shockwave_uses_actual_supported_area_hit_and_death_clears_actor_generation")]
    [InlineData("RetainedBrandWireTests.Recovered_Brand_states_preserve_full_identity_and_inner_endian")]
    public async Task HierophantActorAndPresentationCases(string caseId)
    {
        switch (caseId)
        {
            case "OrdinaryHierophantConnectedTests.Fresh_capacity_destination_overrides_and_owned_live_totems_reach_real_calculators":
                new OrdinaryHierophantConnectedTests().Fresh_capacity_destination_overrides_and_owned_live_totems_reach_real_calculators();
                break;
            case "OrdinaryHierophantConnectedTests.Transfer_freezes_once_denial_preserves_state_and_retirement_cannot_revive_it":
                new OrdinaryHierophantConnectedTests().Transfer_freezes_once_denial_preserves_state_and_retirement_cannot_revive_it();
                break;
            case "OrdinaryHierophantConnectedTests.Scheduled_release_owns_immutable_surge_and_persistent_tasks_drop_first_use":
                await new OrdinaryHierophantConnectedTests().Scheduled_release_owns_immutable_surge_and_persistent_tasks_drop_first_use();
                break;
            case "HierophantEngineCompletionTests.Published_Brand_durations_match_authoritative_budgets_and_supported_modifiers(ruthless: false)":
                await new HierophantEngineCompletionTests().Published_Brand_durations_match_authoritative_budgets_and_supported_modifiers(false);
                break;
            case "HierophantEngineCompletionTests.Published_Brand_durations_match_authoritative_budgets_and_supported_modifiers(ruthless: true)":
                await new HierophantEngineCompletionTests().Published_Brand_durations_match_authoritative_budgets_and_supported_modifiers(true);
                break;
            case "HierophantEngineCompletionTests.Zero_Brand_duration_is_published_and_cannot_admit_or_replay_a_live_actor":
                await new HierophantEngineCompletionTests().Zero_Brand_duration_is_published_and_cannot_admit_or_replay_a_live_actor();
                break;
            case "HierophantEngineCompletionTests.Brand_dispatch_and_activation_preserve_time_conditional_crits_and_owner_procs":
                await new HierophantEngineCompletionTests().Brand_dispatch_and_activation_preserve_time_conditional_crits_and_owner_procs();
                break;
            case "HierophantEngineCompletionTests.Dispatcher_Recall_resource_failure_is_atomic_and_accepted_replay_never_pays_again":
                await new HierophantEngineCompletionTests().Dispatcher_Recall_resource_failure_is_atomic_and_accepted_replay_never_pays_again();
                break;
            case "HierophantEngineCompletionTests.Real_below_level_XP_preserves_actor_and_resource_replacement_never_revives_old_receipts":
                await new HierophantEngineCompletionTests().Real_below_level_XP_preserves_actor_and_resource_replacement_never_revives_old_receipts();
                break;
            case "HierophantEngineCompletionTests.Resource_replacement_rebinds_new_cast_and_never_revives_old_receipts":
                await new HierophantEngineCompletionTests().Resource_replacement_rebinds_new_cast_and_never_revives_old_receipts();
                break;
            case "HierophantEngineCompletionTests.Same_pulse_victims_share_pretrigger_offense_and_next_pulse_benefits":
                await new HierophantEngineCompletionTests().Same_pulse_victims_share_pretrigger_offense_and_next_pulse_benefits();
                break;
            case "HierophantEngineCompletionTests.Autonomous_projectile_keeps_release_critical_sources_after_live_change":
                await new HierophantEngineCompletionTests().Autonomous_projectile_keeps_release_critical_sources_after_live_change();
                break;
            case "HierophantEngineCompletionTests.Paid_default_Brand_publishes_owned_attachment_before_retained_action":
                await new HierophantEngineCompletionTests().Paid_default_Brand_publishes_owned_attachment_before_retained_action();
                break;
            case "HierophantEngineCompletionTests.Brand_output_revalidates_blocked_lifecycle_and_identity_admission":
                await new HierophantEngineCompletionTests().Brand_output_revalidates_blocked_lifecycle_and_identity_admission();
                break;
            case "HierophantEngineCompletionTests.Linked_payload_dispatch_pays_places_and_repeats_without_owner_fallback(payload: \"Ice Nova\")":
                await new HierophantEngineCompletionTests().Linked_payload_dispatch_pays_places_and_repeats_without_owner_fallback("Ice Nova");
                break;
            case "HierophantEngineCompletionTests.Linked_payload_dispatch_pays_places_and_repeats_without_owner_fallback(payload: \"Freezing Pulse\")":
                await new HierophantEngineCompletionTests().Linked_payload_dispatch_pays_places_and_repeats_without_owner_fallback("Freezing Pulse");
                break;
            case "HierophantEngineCompletionTests.New_totem_activation_captures_pre_surge_offense_and_support_damage_once(payload: \"Ice Nova\")":
                await new HierophantEngineCompletionTests().New_totem_activation_captures_pre_surge_offense_and_support_damage_once("Ice Nova");
                break;
            case "HierophantEngineCompletionTests.New_totem_activation_captures_pre_surge_offense_and_support_damage_once(payload: \"Freezing Pulse\")":
                await new HierophantEngineCompletionTests().New_totem_activation_captures_pre_surge_offense_and_support_damage_once("Freezing Pulse");
                break;
            case "HierophantEngineCompletionTests.New_totem_activation_captures_pre_surge_offense_and_support_damage_once(payload: \"Fireball\")":
                await new HierophantEngineCompletionTests().New_totem_activation_captures_pre_surge_offense_and_support_damage_once("Fireball");
                break;
            case "HierophantEngineCompletionTests.New_totem_activation_captures_pre_surge_offense_and_support_damage_once(payload: \"Shockwave Totem\")":
                await new HierophantEngineCompletionTests().New_totem_activation_captures_pre_surge_offense_and_support_damage_once("Shockwave Totem");
                break;
            case "HierophantEngineCompletionTests.Brand_budgets_detach_and_exact_late_boundary_are_separate":
                new HierophantEngineCompletionTests().Brand_budgets_detach_and_exact_late_boundary_are_separate();
                break;
            case "HierophantEngineCompletionTests.Paid_brand_attaches_hits_detaches_pauses_and_retires_on_source_loss":
                await new HierophantEngineCompletionTests().Paid_brand_attaches_hits_detaches_pauses_and_retires_on_source_loss();
                break;
            case "HierophantEngineCompletionTests.Totem_projectile_contact_has_damage_surge_own_regeneration_and_no_owner_leech_or_elusive(payload: \"Fireball\")":
                await new HierophantEngineCompletionTests().Totem_projectile_contact_has_damage_surge_own_regeneration_and_no_owner_leech_or_elusive("Fireball");
                break;
            case "HierophantEngineCompletionTests.Totem_projectile_contact_has_damage_surge_own_regeneration_and_no_owner_leech_or_elusive(payload: \"Freezing Pulse\")":
                await new HierophantEngineCompletionTests().Totem_projectile_contact_has_damage_surge_own_regeneration_and_no_owner_leech_or_elusive("Freezing Pulse");
                break;
            case "HierophantEngineCompletionTests.Brand_caps_use_original_sources_once_and_reconcile_refunded_limits":
                await new HierophantEngineCompletionTests().Brand_caps_use_original_sources_once_and_reconcile_refunded_limits();
                break;
            case "HierophantEngineCompletionTests.Placement_reuses_real_native_totem_creation_life_cap_and_presentation":
                await new HierophantEngineCompletionTests().Placement_reuses_real_native_totem_creation_life_cap_and_presentation();
                break;
            case "HierophantEngineCompletionTests.Recall_has_real_cooldown_recovery_extra_resource_plan_and_source_validation":
                await new HierophantEngineCompletionTests().Recall_has_real_cooldown_recovery_extra_resource_plan_and_source_validation();
                break;
            case "HierophantEngineCompletionTests.Shockwave_uses_actual_supported_area_hit_and_death_clears_actor_generation":
                await new HierophantEngineCompletionTests().Shockwave_uses_actual_supported_area_hit_and_death_clears_actor_generation();
                break;
            case "RetainedBrandWireTests.Recovered_Brand_states_preserve_full_identity_and_inner_endian":
                new RetainedBrandWireTests().Recovered_Brand_states_preserve_full_identity_and_inner_endian();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(caseId), caseId, "Unknown Hierophant scenario");
        }
    }

}
