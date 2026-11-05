namespace GameServer.Gateway.Tests;

// Each original scenario/parameter row remains a separate serializable xUnit case.
// Scenario constructors and their local using/finally lifetimes run afresh for every row.
public sealed class PathfinderPublicationTests
{
    [Theory]
    [InlineData("OrdinaryPathfinderConnectedTests.Real_allocation_and_paid_refund_add_and_remove_the_duration_alias")]
    [InlineData("OrdinaryPathfinderConnectedTests.Actual_poison_admission_returns_live_stack_receipt_and_rolls_only_with_current_flask")]
    [InlineData("OrdinaryPathfinderConnectedTests.Recovery_predicate_projects_queue_expiry_without_consuming_resources_or_notifications")]
    [InlineData("OrdinaryPathfinderConnectedTests.Accepted_owner_lethal_transition_spreads_strongest_once_and_excludes_minions")]
    [InlineData("OrdinaryPathfinderConnectedTests.Strongest_snapshot_is_frozen_before_cleanup_and_not_the_aggregate")]
    [InlineData("OrdinaryPathfinderPinnedTests.Entire_Default_class_has_fifteen_nodes_and_Ruthless_replaces_not_adds_sources")]
    [InlineData("OrdinaryPathfinderRulesTests.Preservation_requires_admitted_cost_and_rolls_at_strict_boundary")]
    [InlineData("OrdinaryPathfinderRulesTests.Surgeon_halves_only_life_amount_not_duration_or_mana_and_removal_restores_future_uses")]
    [InlineData("OrdinaryPathfinderRulesTests.Toxicist_is_per_poison_chance_during_real_flask_effect_only")]
    [InlineData("OrdinaryPathfinderRulesTests.Spread_uses_existing_engine_once_without_rerolling_or_doubling_and_respects_expiry_immunity")]
    [InlineData("OrdinaryPathfinderRulesTests.Distiller_selects_first_affordable_of_each_kind_and_never_spends_on_triggers")]
    [InlineData("OrdinaryPathfinderRulesTests.Duration_alias_is_additive_and_saturates_without_affecting_other_ailments")]
    public async Task PathfinderRulesAndPoisonCases(string caseId)
    {
        switch (caseId)
        {
            case "OrdinaryPathfinderConnectedTests.Real_allocation_and_paid_refund_add_and_remove_the_duration_alias":
                new OrdinaryPathfinderConnectedTests().Real_allocation_and_paid_refund_add_and_remove_the_duration_alias();
                break;
            case "OrdinaryPathfinderConnectedTests.Actual_poison_admission_returns_live_stack_receipt_and_rolls_only_with_current_flask":
                new OrdinaryPathfinderConnectedTests().Actual_poison_admission_returns_live_stack_receipt_and_rolls_only_with_current_flask();
                break;
            case "OrdinaryPathfinderConnectedTests.Recovery_predicate_projects_queue_expiry_without_consuming_resources_or_notifications":
                new OrdinaryPathfinderConnectedTests().Recovery_predicate_projects_queue_expiry_without_consuming_resources_or_notifications();
                break;
            case "OrdinaryPathfinderConnectedTests.Accepted_owner_lethal_transition_spreads_strongest_once_and_excludes_minions":
                await new OrdinaryPathfinderConnectedTests().Accepted_owner_lethal_transition_spreads_strongest_once_and_excludes_minions();
                break;
            case "OrdinaryPathfinderConnectedTests.Strongest_snapshot_is_frozen_before_cleanup_and_not_the_aggregate":
                new OrdinaryPathfinderConnectedTests().Strongest_snapshot_is_frozen_before_cleanup_and_not_the_aggregate();
                break;
            case "OrdinaryPathfinderPinnedTests.Entire_Default_class_has_fifteen_nodes_and_Ruthless_replaces_not_adds_sources":
                new OrdinaryPathfinderPinnedTests().Entire_Default_class_has_fifteen_nodes_and_Ruthless_replaces_not_adds_sources();
                break;
            case "OrdinaryPathfinderRulesTests.Preservation_requires_admitted_cost_and_rolls_at_strict_boundary":
                new OrdinaryPathfinderRulesTests().Preservation_requires_admitted_cost_and_rolls_at_strict_boundary();
                break;
            case "OrdinaryPathfinderRulesTests.Surgeon_halves_only_life_amount_not_duration_or_mana_and_removal_restores_future_uses":
                new OrdinaryPathfinderRulesTests().Surgeon_halves_only_life_amount_not_duration_or_mana_and_removal_restores_future_uses();
                break;
            case "OrdinaryPathfinderRulesTests.Toxicist_is_per_poison_chance_during_real_flask_effect_only":
                new OrdinaryPathfinderRulesTests().Toxicist_is_per_poison_chance_during_real_flask_effect_only();
                break;
            case "OrdinaryPathfinderRulesTests.Spread_uses_existing_engine_once_without_rerolling_or_doubling_and_respects_expiry_immunity":
                new OrdinaryPathfinderRulesTests().Spread_uses_existing_engine_once_without_rerolling_or_doubling_and_respects_expiry_immunity();
                break;
            case "OrdinaryPathfinderRulesTests.Distiller_selects_first_affordable_of_each_kind_and_never_spends_on_triggers":
                new OrdinaryPathfinderRulesTests().Distiller_selects_first_affordable_of_each_kind_and_never_spends_on_triggers();
                break;
            case "OrdinaryPathfinderRulesTests.Duration_alias_is_additive_and_saturates_without_affecting_other_ailments":
                new OrdinaryPathfinderRulesTests().Duration_alias_is_additive_and_saturates_without_affecting_other_ailments();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(caseId), caseId, "Unknown Pathfinder scenario");
        }
    }

    [Theory]
    [InlineData("DistillerAdmissionTests.Default_melee_and_ranged_pay_once(melee: false)")]
    [InlineData("DistillerAdmissionTests.Default_melee_and_ranged_pay_once(melee: true)")]
    [InlineData("DistillerAdmissionTests.Additional_costs_are_validated_and_debited_once")]
    [InlineData("DistillerAdmissionTests.Pickup_waits_for_the_resource_and_charge_batch")]
    [InlineData("DistillerAdmissionTests.Retained_paid_revision_precedes_actual_pickup_removal")]
    [InlineData("DistillerAdmissionTests.Targeted_attack_and_pickup_share_ordered_wire_revisions")]
    [InlineData("DistillerAdmissionTests.Projectile_flask_output_orders_pickup_placement_and_swap(operation: \"pickup\", duringMutation: false)")]
    [InlineData("DistillerAdmissionTests.Projectile_flask_output_orders_pickup_placement_and_swap(operation: \"pickup\", duringMutation: true)")]
    [InlineData("DistillerAdmissionTests.Projectile_flask_output_orders_pickup_placement_and_swap(operation: \"place\", duringMutation: false)")]
    [InlineData("DistillerAdmissionTests.Projectile_flask_output_orders_pickup_placement_and_swap(operation: \"place\", duringMutation: true)")]
    [InlineData("DistillerAdmissionTests.Projectile_flask_output_orders_pickup_placement_and_swap(operation: \"swap\", duringMutation: false)")]
    [InlineData("DistillerAdmissionTests.Projectile_flask_output_orders_pickup_placement_and_swap(operation: \"swap\", duringMutation: true)")]
    [InlineData("DistillerAdmissionTests.Gate_wait_death_or_epoch_change_spends_nothing(death: false)")]
    [InlineData("DistillerAdmissionTests.Gate_wait_death_or_epoch_change_spends_nothing(death: true)")]
    [InlineData("DistillerAdmissionTests.Vaal_and_channel_policy_and_Alchemist_admission")]
    [InlineData("DistillerAdmissionTests.Multistrike_uses_one_payment_across_targets")]
    [InlineData("DistillerAdmissionTests.Actual_gem_payment_and_wait_invalidation")]
    [InlineData("DistillerAdmissionTests.Batch_pickup_and_resource_rejection_are_atomic")]
    [InlineData("DistillerAdmissionTests.Paid_release_survives_refund_without_panel_or_future_use_leak")]
    public async Task PathfinderAdmissionAndFlaskTransactionCases(string caseId)
    {
        switch (caseId)
        {
            case "DistillerAdmissionTests.Default_melee_and_ranged_pay_once(melee: false)":
                await new DistillerAdmissionTests().Default_melee_and_ranged_pay_once(false);
                break;
            case "DistillerAdmissionTests.Default_melee_and_ranged_pay_once(melee: true)":
                await new DistillerAdmissionTests().Default_melee_and_ranged_pay_once(true);
                break;
            case "DistillerAdmissionTests.Additional_costs_are_validated_and_debited_once":
                new DistillerAdmissionTests().Additional_costs_are_validated_and_debited_once();
                break;
            case "DistillerAdmissionTests.Pickup_waits_for_the_resource_and_charge_batch":
                await new DistillerAdmissionTests().Pickup_waits_for_the_resource_and_charge_batch();
                break;
            case "DistillerAdmissionTests.Retained_paid_revision_precedes_actual_pickup_removal":
                await new DistillerAdmissionTests().Retained_paid_revision_precedes_actual_pickup_removal();
                break;
            case "DistillerAdmissionTests.Targeted_attack_and_pickup_share_ordered_wire_revisions":
                await new DistillerAdmissionTests().Targeted_attack_and_pickup_share_ordered_wire_revisions();
                break;
            case "DistillerAdmissionTests.Projectile_flask_output_orders_pickup_placement_and_swap(operation: \"pickup\", duringMutation: false)":
                await new DistillerAdmissionTests().Projectile_flask_output_orders_pickup_placement_and_swap("pickup",false);
                break;
            case "DistillerAdmissionTests.Projectile_flask_output_orders_pickup_placement_and_swap(operation: \"pickup\", duringMutation: true)":
                await new DistillerAdmissionTests().Projectile_flask_output_orders_pickup_placement_and_swap("pickup",true);
                break;
            case "DistillerAdmissionTests.Projectile_flask_output_orders_pickup_placement_and_swap(operation: \"place\", duringMutation: false)":
                await new DistillerAdmissionTests().Projectile_flask_output_orders_pickup_placement_and_swap("place",false);
                break;
            case "DistillerAdmissionTests.Projectile_flask_output_orders_pickup_placement_and_swap(operation: \"place\", duringMutation: true)":
                await new DistillerAdmissionTests().Projectile_flask_output_orders_pickup_placement_and_swap("place",true);
                break;
            case "DistillerAdmissionTests.Projectile_flask_output_orders_pickup_placement_and_swap(operation: \"swap\", duringMutation: false)":
                await new DistillerAdmissionTests().Projectile_flask_output_orders_pickup_placement_and_swap("swap",false);
                break;
            case "DistillerAdmissionTests.Projectile_flask_output_orders_pickup_placement_and_swap(operation: \"swap\", duringMutation: true)":
                await new DistillerAdmissionTests().Projectile_flask_output_orders_pickup_placement_and_swap("swap",true);
                break;
            case "DistillerAdmissionTests.Gate_wait_death_or_epoch_change_spends_nothing(death: false)":
                await new DistillerAdmissionTests().Gate_wait_death_or_epoch_change_spends_nothing(false);
                break;
            case "DistillerAdmissionTests.Gate_wait_death_or_epoch_change_spends_nothing(death: true)":
                await new DistillerAdmissionTests().Gate_wait_death_or_epoch_change_spends_nothing(true);
                break;
            case "DistillerAdmissionTests.Vaal_and_channel_policy_and_Alchemist_admission":
                await new DistillerAdmissionTests().Vaal_and_channel_policy_and_Alchemist_admission();
                break;
            case "DistillerAdmissionTests.Multistrike_uses_one_payment_across_targets":
                await new DistillerAdmissionTests().Multistrike_uses_one_payment_across_targets();
                break;
            case "DistillerAdmissionTests.Actual_gem_payment_and_wait_invalidation":
                await new DistillerAdmissionTests().Actual_gem_payment_and_wait_invalidation();
                break;
            case "DistillerAdmissionTests.Batch_pickup_and_resource_rejection_are_atomic":
                new DistillerAdmissionTests().Batch_pickup_and_resource_rejection_are_atomic();
                break;
            case "DistillerAdmissionTests.Paid_release_survives_refund_without_panel_or_future_use_leak":
                new DistillerAdmissionTests().Paid_release_survives_refund_without_panel_or_future_use_leak();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(caseId), caseId, "Unknown Pathfinder scenario");
        }
    }

}
