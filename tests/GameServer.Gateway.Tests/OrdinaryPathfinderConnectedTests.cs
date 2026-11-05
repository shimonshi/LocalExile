using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed class OrdinaryPathfinderConnectedTests
{
    public OrdinaryPathfinderConnectedTests() => Saves.Open(TestEnvironment.ScratchSaves);

    static Dictionary<uint, int> Stats(params (string Name, int Value)[] rows) =>
        rows.ToDictionary(r => Program.Stats.Key(r.Name)!.Value, r => r.Value);

    static ClientState Connection() => new(new(1, "pathfinder", 300, 300, 100, 100, null, []), 1, 1)
    {
        Pools = new(1, 1000, 100, 2, 0), PlayerLife = 500, PlayerMana = 50,
    };

    public void Real_allocation_and_paid_refund_add_and_remove_the_duration_alias()
    {
        var conn = Connection();
        var catalog = new AscendancyCatalog();
        var definition = Assert.Single(catalog.Definitions, d => d.Identity.Family == AscendancyFamily.Normal
            && d.Identity.Id == "Pathfinder");
        var rules = new AscendancyRules(catalog);
        conn.MainAscendancy = rules.Select(definition.BaseClass, new(), definition.Identity);
        conn.MainAscendancy = rules.Award(definition.BaseClass, conn.MainAscendancy, "normal");
        conn.MainAscendancy = rules.Allocate(definition.BaseClass, conn.MainAscendancy, [20480]);
        uint duration = Program.Stats.Key("poison_duration_+%")!.Value;
        var allocated = Game.CharacterPlayerStats(conn);
        Assert.Equal(10, allocated.GetValueOrDefault(duration));
        Assert.Equal(10, Game.CharacterPlayerStats(conn).GetValueOrDefault(duration)); // cached path, no doubling
        var refund = rules.Refund(definition.BaseClass, conn.MainAscendancy, [20480], 0, 0);
        Assert.Equal(5, refund.Cost);
        conn.MainAscendancy = refund.Progress;
        Assert.Equal(0, Game.CharacterPlayerStats(conn).GetValueOrDefault(duration));
        Assert.Equal(10, allocated.GetValueOrDefault(duration)); // accepted source snapshot stays owned
    }

    public void Actual_poison_admission_returns_live_stack_receipt_and_rolls_only_with_current_flask()
    {
        var conn = Connection();
        var player = Stats(("base_chance_to_poison_on_hit_%", 100), (OrdinaryPathfinderRules.DoublePoison, 100));
        var target = new SpawnedMonster("target", new(100, 100, 0), 0, 0, 1000);
        var settings = new MonsterPoisons.Settings(100, 10, 10, 20, 20, 2);
        var hit = DamagePacket.Of(DamageType.Physical, 100, 100);
        conn.FlaskEffects.Start(new(0, 1, 1, 5, 0, 0, 1)); // expired by contact, still in engine
        Assert.NotNull(Game.PoisonOnHitLocked(conn, target, hit, false, false, player, null, settings, 2, new(1)));
        Assert.Equal(10, target.Poisons.TotalDps(2));
        conn.FlaskEffects.Start(new(0, 2, 1, 5, 0, 2, 5));
        var applied = Game.PoisonOnHitLocked(conn, target, hit, false, false, player, null, settings, 2, new(1));
        Assert.NotNull(applied);
        Assert.Equal(2, applied.Value.Poison.Stacks);
        Assert.Equal(20, applied.Value.Poison.StrongestDps);
        Assert.Equal(30, target.Poisons.TotalDps(2));
        target.Dead = true;
        Assert.Null(Game.PoisonOnHitLocked(conn, target, hit, false, false, player, null, settings, 2, new(1)));
        conn.LeavingArea = true;
        Assert.False(Game.ActualFlaskEffectLocked(conn, 2));
    }

    public void Recovery_predicate_projects_queue_expiry_without_consuming_resources_or_notifications()
    {
        var effects = new Flasks.Effects();
        var first = new Flasks.Effect(0, 1, 1, 20, 0, 0, 1);
        var queued = new Flasks.Effect(1, 2, 1, 10, 0, 0, 2);
        effects.Start(first);
        effects.Start(queued);
        Assert.True(effects.ActiveAt(2, false, false)); // queued use has one second left
        Assert.False(effects.ActiveAt(3, false, false));
        Assert.False(effects.ActiveAt(.5, true, false));
        Assert.Equal(2, effects.Remaining(queued));
        var recovery = effects.Advance(0, 3);
        Assert.Equal(40, recovery.Life);
        Assert.Equal(2, recovery.Ended.Count);
        Assert.False(effects.ActiveAt(3, false, false));
    }

    public async Task Accepted_owner_lethal_transition_spreads_strongest_once_and_excludes_minions()
    {
        var conn = Connection();
        conn.PlayerStats[Program.Stats.Key(OrdinaryPathfinderRules.SpreadAlways)!.Value] = 1;
        var corpse = new SpawnedMonster("corpse", new(100, 100, 0), 0, 0, 100);
        var near = new SpawnedMonster("near", new(110, 100, 0), 0, 0, 100);
        var far = new SpawnedMonster("far", new(116, 100, 0), 0, 0, 100);
        var friendly = new SpawnedMonster("friendly", new(105, 100, 0), 0, 0, 100) { Hostile = false };
        conn.Monsters[1] = corpse; conn.Monsters[2] = near; conn.Monsters[3] = far; conn.Monsters[4] = friendly;
        double now = Game.ResourceEffectsTime(conn);
        corpse.Poisons.Add(now, 40, 20);
        corpse.Poisons.Add(now, 60, 20);
        Assert.NotNull(Game.ApplyMonsterHitLocked(conn, corpse, 100));
        Assert.Equal(60, near.Poisons.TotalDps(Game.ResourceEffectsTime(conn)));
        Assert.Equal(1, near.Poisons.Stacks(Game.ResourceEffectsTime(conn)));
        Assert.Equal(0, far.Poisons.TotalDps(now));
        Assert.Equal(0, friendly.Poisons.TotalDps(now));
        Assert.Null(Game.ApplyMonsterHitLocked(conn, corpse, 100));
        Assert.Equal(1, near.Poisons.Stacks(Game.ResourceEffectsTime(conn)));
        near.Poisons.Clear();
        var minionVictim = new SpawnedMonster("minion victim", new(100, 100, 0), 0, 0, 100);
        minionVictim.Poisons.Add(now, 100, 20);
        conn.Monsters[5] = minionVictim;
        Game.ApplyMonsterHitLocked(conn, minionVictim, 100, allyAt: (100, 100));
        Assert.False(near.Poisons.Poisoned);
        await OrdinaryGameplayChecks.ConditionalToxicistProjectile();
    }

    public void Strongest_snapshot_is_frozen_before_cleanup_and_not_the_aggregate()
    {
        var engine = new MonsterPoisons();
        engine.Add(0, 100, 1); engine.Add(0, 40, 4); engine.Add(0, 60, 3);
        var receipt = engine.Capture(2);
        Assert.Equal(new MonsterPoisons.Snapshot(2, 60, 1), receipt);
        engine.Advance(5, 1); engine.Clear();
        Assert.Equal(new MonsterPoisons.Snapshot(2, 60, 1), receipt);
        Assert.Equal(default, engine.Capture(5));
    }
}
