using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;
using GameServer.Modding;
using League = GameServer.Core.Accounts.League;

namespace GameServer.Gateway.Tests;

public sealed class OrdinaryHierophantConnectedTests
{
    internal sealed class Time : IProjectileRuntime
    {
        public bool Active => true;
        public double NowTicks { get; set; }
        public void Invalidate() { }
        internal void At(double seconds) => NowTicks = seconds * 120000;
    }
    internal sealed class Setup : IDisposable
    {
        readonly IGameLogic? oldLogic = Program.GameLogicOrNull;
        readonly IModuleLifetime? oldLifetime = Game.CombatLifetime;
        internal readonly ModuleLifetime Lifetime = new("Game");
        internal readonly Time Time = new();
        internal readonly ClientState Conn;
        internal Setup(bool ruthless = false)
        {
            Saves.Open(TestEnvironment.ScratchSaves);
            Game.CombatLifetime = Lifetime;
            Program.SetGameLogic(new GameServer.Modules.Game.GameLogic());
            Conn = new(new(1, "hierophant", 300, 300, 100, 100, null, []), 1, 1)
            {
                Character = "HierophantConnected" + Guid.NewGuid().ToString("N"), PlayerLife = 1000, PlayerMana = 1000,
                Pools = new(100, 1000, 1000, 2, 0), ProjectileRuntimeState = Time,
                MainAscendancy = new(new(AscendancyFamily.Normal, "Hierophant", AscendancyCatalog.Revision)),
                LeagueModule = new(new League("test", "Standard", false, false) { DataSet = ruthless ? "HardMode" : "Default" },
                    new HashSet<string>(["Game"])),
            };
        }
        internal void Set(string name, int value) => Conn.PlayerStats[Program.Stats.Key(name)!.Value] = value;
        internal Dictionary<uint, int> Read() => Game.CharacterPlayerStats(Conn);
        public void Dispose()
        {
            Conn.Closed = true; Conn.RetireResourceEffects(); Lifetime.Stop(ex => throw ex);
            PlayerInventories.Forget(Conn.Character!);
            Game.CombatLifetime = oldLifetime; Program.SetGameLogic(oldLogic!);
        }
    }
    static int Get(IReadOnlyDictionary<uint, int> stats, string name) => stats.GetValueOrDefault(Program.Stats.Key(name)!.Value);


    public void Accepted_spell_hit_uses_pretrigger_snapshot_and_expiry_publishes_native_removal_and_stats()
    {
        using var s = new Setup(); var c = s.Conn;
        s.Set(OrdinaryHierophantRules.HitSurge, 1); s.Set(OrdinaryHierophantRules.SurgeMore, 20);
        s.Set("life_leech_from_spell_damage_permyriad_while_you_have_arcane_surge", 50);
        var before = s.Read();
        var hit = DamagePacket.Of(DamageType.Fire, 100, 100);
        DamagePacket Resolve(Dictionary<uint, int> stats) => PlayerHitDamage.Resolve(hit, n => Get(stats, n), new(), true);
        Assert.Equal(100, Resolve(before).MinTotal, 8);
        Assert.False(Game.OrdinaryHierophantSpellHit(c, before, false, true, true));
        Assert.False(Game.OrdinaryHierophantSpellHit(c, before, true, false, true));
        Assert.True(Game.OrdinaryHierophantSpellHit(c, before, true, true, true));
        Assert.Equal(100, Resolve(before).MinTotal, 8);
        var after = s.Read(); Assert.Equal(120, Resolve(after).MinTotal, 8);
        Assert.Equal(50, Game.HitLeechSources(after, false).Life[DamageType.Fire].Min);
        Assert.Equal(0, Game.HitLeechSources(after, true).Life[DamageType.Fire].Min);
        Assert.NotNull(Game.HierophantPresentation(c));
        Assert.Null(Game.HierophantPresentation(c));
        s.Time.At(4); Assert.NotNull(Game.HierophantPresentation(c));
        Assert.Equal(0, Get(s.Read(), "has_arcane_surge"));
        Assert.Equal(0, Game.HitLeechSources(s.Read(), false).Life[DamageType.Fire].Min);
        Assert.Null(Game.HierophantPresentation(c));
    }


    public void Actual_payment_free_refusal_and_lifetap_preserve_predebit_offense()
    {
        using var s = new Setup(true); var c = s.Conn;
        s.Set(OrdinaryHierophantRules.ManaSurge, 200); s.Set(OrdinaryHierophantRules.SurgeMore, 8);
        s.Set("virtual_strength_for_requirements", 1000); s.Set("virtual_dexterity_for_requirements", 1000);
        s.Set("virtual_intelligence_for_requirements", 1000);
        var gem = ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SkillGemFireball")!);
        var item = ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!);
        var equipped = Game.ItemsOf(c).Add(PlayerInventories.Weapon1, 0, 0, item with { Sockets = [new ItemSocket(SocketColour.White, gem)], Links = [1] });
        var skill = Assert.Single(Game.GemSkillEffects(c), g => g.Name == "Fireball");
        var state = Game.HierophantOf(c); s.Read();
        state.Spend(0, 199);
        using (Game.BeginOrdinaryUse(c))
        {
            c.PlayerMana = 0;
            Assert.False(Game.PayMana(c, skill.Id, out _, out _)); Assert.Equal(199, state.ManaRemainder);
            c.PlayerMana = 1000;
            Assert.True(Game.PayMana(c, skill.Id, out var cost, out _)); Assert.True(cost > 0);
            Assert.Equal(1000u - cost, c.PlayerMana); Assert.True(state.Active(0));
            Assert.Equal(0, Get(Game.HierophantOffenseStats(c), "has_arcane_surge"));
            Assert.Equal(1, Get(s.Read(), "has_arcane_surge"));
        }
        Game.ClearOrdinaryHierophant(c); s.Set("no_cost", 1);
        Assert.True(Game.PayMana(c, skill.Id, out var free, out _)); Assert.Equal(0u, free);
        Assert.Equal(0, state.ManaRemainder); Assert.False(state.Active(0));
        s.Set("no_cost", 0);
        // Actual debit notification is the same seam used by Vaal and ordinary/channel payment.
        Game.ManaSpentLocked(c, 0, s.Read()); Assert.Equal(0, state.ManaRemainder);
        var lifetap = ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SupportGemLifetap")!);
        Assert.NotNull(Game.ItemsOf(c).ChangeUnsaved(PlayerInventories.Weapon1, equipped!.Id,
            original => original with { Sockets = [new ItemSocket(SocketColour.White, gem), new ItemSocket(SocketColour.White, lifetap)], Links = [2] }));
        uint mana = c.PlayerMana, life = c.PlayerLife;
        Assert.True(Game.PayMana(c, skill.Id, out var manaCost, out var lifeCost));
        Assert.Equal(0u, manaCost); Assert.True(lifeCost > 0); Assert.Equal(mana, c.PlayerMana);
        Assert.Equal(life - lifeCost, c.PlayerLife); Assert.Equal(0, state.ManaRemainder);
    }


    public void Real_catalog_allocation_paid_refund_save_reload_and_class_replacement_reconcile_sources()
    {
        using var s = new Setup(); var c = s.Conn;
        var catalog = new AscendancyCatalog(); var rules = new AscendancyRules(catalog);
        var definition = catalog.Find(c.MainAscendancy.Selection!)!;
        c.MainAscendancy = rules.Select(definition.BaseClass, new(), definition.Identity);
        c.MainAscendancy = rules.Award(definition.BaseClass, c.MainAscendancy, "normal");
        c.MainAscendancy = rules.Allocate(definition.BaseClass, c.MainAscendancy, [44797, 40510]);
        CharacterSaves.SetMainAscendancy(c.Character!, c.MainAscendancy);
        Assert.Equal(1, Get(s.Read(), OrdinaryHierophantRules.HitSurge));
        Assert.True(Game.OrdinaryHierophantSpellHit(c, s.Read(), true, true, true));
        var refund = rules.Refund(definition.BaseClass, c.MainAscendancy, [40510], 0, 0);
        Assert.Equal(5, refund.Cost); c.MainAscendancy = refund.Progress;
        CharacterSaves.SetMainAscendancy(c.Character!, c.MainAscendancy);
        c.MainAscendancy = CharacterSaves.Load(c.Character!)!.MainAscendancy!;
        Assert.Equal(0, Get(s.Read(), "has_arcane_surge"));
        c.MainAscendancy = rules.Allocate(definition.BaseClass, c.MainAscendancy, [40510]);
        Assert.Equal(0, Get(s.Read(), "has_arcane_surge"));
        c.MainAscendancy = new(new(AscendancyFamily.Normal, "Pathfinder", AscendancyCatalog.Revision));
        s.Set("base_poison_duration_+%", 10);
        Assert.Equal(10, Get(s.Read(), "poison_duration_+%"));
        Assert.Equal(0, Get(s.Read(), "has_arcane_surge"));
    }


    public void Surge_ailment_contribution_reconciles_existing_ailments_and_ends_without_immunity()
    {
        using var s = new Setup(); var c = s.Conn;
        s.Set(OrdinaryHierophantRules.HitSurge, 1); s.Set(OrdinaryHierophantRules.SelfAilment, -50);
        s.Set("shocked_effect_on_self_+%", 50);
        var raw = s.Read();
        c.ElementalAilments.Inflict(PlayerElementalAilments.Kind.Shock, 20, 10, 0, n => Get(raw, n), new Random(1));
        Assert.Equal(30, c.ElementalAilments.Magnitude(PlayerElementalAilments.Kind.Shock, 0));
        Game.OrdinaryHierophantSpellHit(c, raw, true, true, true); s.Read();
        Assert.Equal(20, c.ElementalAilments.Magnitude(PlayerElementalAilments.Kind.Shock, 0));
        s.Time.At(4); s.Read();
        Assert.Equal(30, c.ElementalAilments.Magnitude(PlayerElementalAilments.Kind.Shock, 4));
    }


    public void Fresh_capacity_destination_overrides_and_owned_live_totems_reach_real_calculators()
    {
        using var s = new Setup(); var c = s.Conn;
        s.Set("additive_mana_modifiers_apply_to_damage_at_30%_value", 1); s.Set("maximum_mana_+%", 40);
        s.Set("hierophant_area_of_effect_+%_per_50_unreserved_mana_up_to_100%", 1);
        s.Set(OrdinaryHierophantRules.TotemMore, 3);
        s.Set("you_and_totem_life_regeneration_rate_per_minute_%_per_active_totem", 60);
        c.Totems.Place(new(101, 1, (100, 100), 100, 2, null), 1, 0);
        var raw = s.Read(); Assert.Equal(12, Get(raw, "damage_+%"));
        var pools = Game.BuildPoolsFor(c, c.Pools.Level);
        Assert.Equal((int)Math.Min(100u, pools.Mana / 50), Get(raw, "skill_area_of_effect_+%"));
        int pride = Game.SkillsFor(c).EffectRow("Pride");
        c.Auras[0x999] = new(0x999, pride, "Pride", "Pride", 1, AuraCatalog.BuffRow("Pride"), 50, 0);
        pools = Game.BuildPoolsFor(c, c.Pools.Level);
        int capacity = Math.Max(0, (int)pools.Mana - AuraCatalog.RequiredMana(pools, c.Auras.Values));
        Assert.Equal(Math.Min(100, capacity / 50), Get(s.Read(), "skill_area_of_effect_+%"));
        c.PlayerMana = 0; // Depletion must not change capacity-derived area.
        Assert.Equal(Math.Min(100, capacity / 50), Get(s.Read(), "skill_area_of_effect_+%"));
        c.Auras.Clear();
        Assert.Equal(60, Get(raw, "life_regeneration_rate_per_minute_%"));
        Assert.Equal(1.03, HierophantModifiers.Damage(n => Get(raw, n), false), 8);
        s.Set("damage_+%", 7); s.Set("skill_area_of_effect_+%", 9);
        Assert.Equal(7, Get(s.Read(), "damage_+%")); Assert.Equal(9, Get(s.Read(), "skill_area_of_effect_+%"));
        s.Time.At(2); Assert.Equal(0, Get(s.Read(), "number_of_active_totems"));
        Assert.Equal(1, HierophantModifiers.Damage(n => Get(s.Read(), n), false));
    }


    public void Transfer_freezes_once_denial_preserves_state_and_retirement_cannot_revive_it()
    {
        using var s = new Setup(); var c = s.Conn;
        s.Set(OrdinaryHierophantRules.HitSurge, 1);
        Assert.True(Game.OrdinaryHierophantSpellHit(c, s.Read(), true, true, true));
        var owner = Game.ResourceEffectsOf(c);
        owner.Freeze(1); owner.Resume(100); Assert.True(owner.Hierophant.Active(102.999));
        Assert.False(owner.Hierophant.Active(103));
        s.Time.At(100); Game.CarryResourceEffects(c, c.Ticket);
        Game.RestoreResourceEffects(c); Assert.True(owner.Loading);
        Assert.True(owner.Hierophant.Active(0));
        Game.ActivateResourceEffects(c, 200); Assert.True(owner.Hierophant.Active(202.999));
        owner.Hierophant.Clear(); Game.RestoreResourceEffects(c); Assert.False(owner.Hierophant.Active(200));
        s.Lifetime.Stop(ex => throw ex); Assert.False(Game.OrdinaryHierophantSpellHit(c, s.Read(), true, true, true));
        Assert.False(Game.HierophantOf(c).Active(0));
    }

    public async Task Scheduled_release_owns_immutable_surge_and_persistent_tasks_drop_first_use()
    {
        using var s = new Setup(true); var c = s.Conn;
        s.Set(OrdinaryHierophantRules.ManaSurge, 200); s.Set(OrdinaryHierophantRules.SurgeMore, 8);
        s.Set("life_leech_from_spell_damage_permyriad_while_you_have_arcane_surge", 50);
        foreach (var name in new[] { "strength", "dexterity", "intelligence" }) s.Set("virtual_" + name + "_for_requirements", 1000);
        var gem = ItemCatalog.Create(Program.Items.Find("Metadata/Items/Gems/SkillGemFireball")!);
        var wand = ItemCatalog.Create(Program.Items.Find("Driftwood Wand")!);
        Game.ItemsOf(c).Add(PlayerInventories.Weapon1, 0, 0, wand with { Sockets = [new ItemSocket(SocketColour.White, gem)], Links = [1] });
        var skill = Assert.Single(Game.GemSkillEffects(c), g => g.Name == "Fireball");
        var session = new Game.ProjectileSession("test", null!, null!, c);
        c.ProjectileRuntimeState = session;
        var shots = new List<ProjectileOffense>(); var areas = new List<int>(); s.Set("area_of_effect_+%_while_you_have_arcane_surge",30);
        void Schedule(long at) => session.Schedule(at, () => true, () => {
            var use = Game.CaptureGemProjectile(c, session, skill.Id, skill, (500, 300), new(0));
            shots.Add((ProjectileOffense)use.Payload!); areas.Add(Get(Game.HierophantOffenseStats(c),"skill_area_of_effect_+%"));
            throw new InvalidOperationException("captured without emitting network output");
        });
        s.Read(); Game.HierophantOf(c).Spend(0, 199);
        using (Game.BeginOrdinaryUse(c))
        {
            Assert.True(Game.PayMana(c, skill.Id, out _, out _));
            Assert.True(Game.HierophantOf(c).Active(0));
            Schedule(120000); // threshold-crossing use captured false
        }
        using (Game.BeginOrdinaryUse(c)) { Game.CaptureOrdinarySkillUse(c); Schedule(600000); }
        // An unrelated scope advances both releases, including the pre-active use after live expiry.
        using (Game.BeginOrdinaryUse(c)) { Game.CaptureOrdinarySkillUse(c); session.AdvanceTo(120000); }
        session.AdvanceTo(600000);
        Assert.Equal(2, shots.Count); Assert.Equal(new[] {0,30},areas);
        Assert.True(shots[1].Hit.MinTotal > shots[0].Hit.MinTotal);
        Assert.Equal(0, shots[0].Leech.Life[DamageType.Fire].Min);
        Assert.Equal(50, shots[1].Leech.Life[DamageType.Fire].Min);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (Game.BeginOrdinaryUse(c))
        {
            Game.CaptureOrdinarySkillUse(c); // false at t=5
            Game.StartProjectilePump(async () => { await resume.Task; read.SetResult(Get(Game.HierophantOffenseStats(c), "has_arcane_surge")); });
        }
        Game.HierophantOf(c).Spend(5, 200); resume.SetResult();
        Assert.Equal(1, await read.Task);
        session.Invalidate(); await session.OutputCompleted;
    }


    public void Live_chill_reconciliation_retimes_active_walk_on_surge_grant_and_expiry()
    {
        using var s = new Setup(); var c = s.Conn;
        s.Set(OrdinaryHierophantRules.HitSurge, 1); s.Set(OrdinaryHierophantRules.SelfAilment, -50);
        var raw = s.Read();
        c.ElementalAilments.Inflict(PlayerElementalAilments.Kind.Chill, 20, 20, 0, n => Get(raw, n), new(1));
        c.Use(c.Layout with { GridW = 4096, GridH = 4096, Walkable = null, Sight = null }); c.Pools = c.Pools with { MovementVelocityPct = -90 }; c.ApplyMovementSpeedAt(0); var start = c.Walk.Position(0); c.Walk.Order(start.X + 37, start.Y, 0);
        double slow = c.Walk.Speed;
        s.Time.At(1); var before = c.Walk.Position(1);
        Game.OrdinaryHierophantSpellHit(c, raw, true, true, true);
        Assert.NotNull(Game.HierophantPresentation(c));
        Assert.Equal(before, c.Walk.Position(1)); Assert.True(c.Walk.Speed > slow);
        double fast = c.Walk.Speed;
        s.Time.At(5); var atExpiry = c.Walk.Position(5);
        // An offense-only read cannot alter the live recipient or walk.
        using (Game.BeginOrdinaryUse(c)) { Game.CaptureOrdinarySkillUse(c); Game.HierophantOffenseStats(c); }
        Assert.Equal(fast, c.Walk.Speed);
        Assert.NotNull(Game.HierophantPresentation(c));
        Assert.Equal(atExpiry, c.Walk.Position(5)); Assert.Equal(slow, c.Walk.Speed, 8);
        Assert.True(c.Walk.Position(6).X > atExpiry.X, $"start {start}, expiry {atExpiry}, six {c.Walk.Position(6)}, destination {c.Walk.Destination}, speed {c.Walk.Speed}, arrival {c.Walk.ArrivesAt}");
    }
}
