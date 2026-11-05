static partial class Game
{
    /// <summary>Add to the existing single-use cooldown's recovery percentage BEFORE admission and native publication.</summary>
    internal static int HierophantBrandRecallRecovery(ClientState c, ushort skill)
    {
        var s = SupportedSkill(c, skill);
        return SkillsFor(c).EffectId(s.Effect) == "BrandRecall"
            ? MarkStat(SupportedStats(c, skill), "sigil_recall_cooldown_speed_+%") : 0;
    }
    internal sealed record HierophantRecallPlan(int Epoch, ushort Skill, int Action,
        IReadOnlyList<HierophantEngineActor> Brands, uint ExtraMana, uint ExtraLife);
    // Prepare before ANY payment. Parent folds ExtraMana into the same atomic resource admission, never PayMana twice.
    // Recalled brands retain both duration budgets. Failed extra payment does not detach or activate anything.
    internal static HierophantRecallPlan? PrepareHierophantRecall(ProjectileSession session, ushort skill, int action)
    {
        var c = session.Conn;
        if (!HierophantEngineLive(session) || c.Paused || InTown(c)) return null;
        var s = SupportedSkill(c, skill);
        if (!s.RequirementsMet || SkillsFor(c).EffectId(s.Effect) != "BrandRecall") return null;
        var brands = HierophantEngineActors(session).Where(a => a.Brand is not null && HierophantEngineCurrent(session, a)
            && MarkStat(SupportedStats(c, a.Skill), "brand_cannot_be_recalled") == 0).ToArray();
        int pct = MarkStat(SupportedStats(c, skill), "brand_recall_spend_%_of_recalled_brands_cost");
        ulong cost = 0, life = 0;
        foreach (var brand in brands)
        {
            // Recall uses a fraction of each brand's current supported mana cost, not its original receipt.
            var g = SupportedSkill(c, brand.Skill);
            uint amount = (uint)((ulong)Math.Max(0, MarkStat(SupportedStats(c, brand.Skill), "mana_cost"))
                * (uint)Math.Max(0, pct) / 100);
            if (Lifetap.ConvertsCostToLife(g.Stats)) life += amount;
            else { cost += amount; life += (uint)Math.Max(0, PetrifiedBlood.LifeCost(c, (int)Math.Min(int.MaxValue, amount))); }
        }
        return cost > uint.MaxValue || life > uint.MaxValue ? null : new(c.ProjectileEpoch, skill, action,
            Array.AsReadOnly(brands), (uint)cost, (uint)life);
    }
    internal static bool HierophantRecallCurrent(ProjectileSession session, HierophantRecallPlan plan)
    {
        var current = PrepareHierophantRecall(session, plan.Skill, plan.Action);
        return current is not null && current.Epoch == plan.Epoch && current.ExtraMana == plan.ExtraMana
            && current.ExtraLife == plan.ExtraLife && current.Brands.SequenceEqual(plan.Brands);
    }
    /// <summary>Call under the admission locks only after exact plan validation and atomic resource/cooldown commit.</summary>
    internal static bool CommitHierophantRecall(ProjectileSession session, HierophantRecallPlan plan)
    {
        var c = session.Conn; var world = HierophantEngineWorlds.GetValue(session, _ => new());
        if (!HierophantEngineLive(session) || c.Paused || plan.Epoch != c.ProjectileEpoch
            || !OrdinaryOf(c, _ => 0).Accepted(plan.Epoch, plan.Action) || plan.Action <= world.LastUse) return false;
        world.LastUse = plan.Action;
        double now = ResourceEffectsTime(c); var at = c.Walk.Position(now);
        foreach (var brand in plan.Brands)
        {
            if (!HierophantEngineCurrent(session, brand)) continue;
            RetireBrandPresentation(session, brand);
            brand.Attached = null; brand.Target = 0; brand.At = at;
            brand.RecallSearch = MarkStat(SupportedStats(c, plan.Skill), "recall_sigil_target_search_range_+%");
            // Recall's paid activation uses the recalled position; no duration refresh or free recurring use.
            HierophantEngineActivate(session, brand);
        }
        return true;
    }
}
