using System.Text.Json;
using GameServer.Core.Projectiles;

namespace GameServer.Gateway.Tests;

public class OrdinaryResidualMigrationTests
{
    // The same extracted ordinary node vectors consumed by the production passive catalog. Hardmode replaces
    // the vector, including absent stats and passive buff templates; no synthetic class-wide stat bundle.
    static Dictionary<string, int> Sources(bool ruthless, params string[] nodes)
    {
        using var stats = JsonDocument.Parse(File.ReadAllText("data/tables/Stats.json"));
        using var passives = JsonDocument.Parse(File.ReadAllText("data/tables/PassiveSkills.json"));
        var result = new Dictionary<string, int>();
        foreach (var node in passives.RootElement.EnumerateArray().Where(n => nodes.Contains(n.GetProperty("Name").GetString())))
        {
            bool alternate = ruthless && node.GetProperty("HasHardmodeAlternate").GetBoolean();
            int i = 0;
            foreach (var key in node.GetProperty(alternate ? "StatsHardmode" : "Stats").EnumerateArray())
            {
                string name = stats.RootElement[key.GetInt32()].GetProperty("Id").GetString()!;
                result[name] = result.GetValueOrDefault(name)
                    + node.GetProperty($"Stat{++i}Value{(alternate ? "Hardmode" : "")}").GetInt32();
            }
        }
        return result;
    }

    [Fact]
    public void SlayerCommittedKillsQualifiedConditionsAndReloadKeepTheirOriginalDeadlines()
    {
        OrdinaryGameplayChecks.SlayerGameplayTransactions();
        OrdinaryGameplayChecks.TransferComposition();
        foreach (bool ruthless in new[] { false, true })
        {
            var stats = Sources(ruthless, "Headsman", "Bane of Legends", "Impact", "Overwhelm",
                "Endless Hunger", "Brutal Fervour", "Masterful Form");
            int Get(string name) => stats.GetValueOrDefault(name);
            var state = new OrdinaryResidualSession(7);
            var kill = new OrdinaryResidualSession.Lethal(1, 1, OrdinaryResidualSession.Credit.OwnerDirect,
                true, false, true, 100, 180);
            Assert.False(state.Kill(0, kill with { Committed = false }, Get).Recorded);
            Assert.False(state.Kill(0, kill with { PhaseTransition = true }, Get).Recorded);
            Assert.False(state.Kill(0, kill with { Source = OrdinaryResidualSession.Credit.Reflected }, Get).Recorded);
            var accepted = state.Kill(0, kill, Get);
            Assert.True(accepted.Recorded);
            Assert.Equal(16, accepted.OverkillLife);
            Assert.False(state.Kill(0, kill, Get).Recorded);
            Assert.Equal(0, state.Kill(1, kill with { Event = 2, Source = OrdinaryResidualSession.Credit.OwnedProxy }, Get).OverkillLife);
            Assert.Equal(2, state.Slayer.Read(1, Get).RecentKills);
            Assert.Equal(ruthless ? 1.2 : 1.32, SlayerRules.DamageMultiplier(Get, true, true), 8);
            Assert.Equal(1, SlayerRules.DamageMultiplier(Get, false, false));
            Assert.Equal(ruthless ? 0 : 100, SlayerRules.CriticalMultiplierBonus(Get, 50));
            Assert.Equal(7, SlayerRules.EnduranceMaximum(Get, 3, 7));
            Assert.Equal(default, SlayerRules.WhileLeeching(Get, false, false, false));
            Assert.Equal(ruthless ? 0 : -10,
                SlayerRules.WhileLeeching(Get, false, true, false).DamageTakenIncrease);
            Assert.True(SlayerRules.WhileLeeching(Get, false, false, true).UnaffectedByBleeding);
            Assert.Equal(!ruthless, SlayerRules.WhileLeeching(Get, true, false, false).CannotBeStunned);
            Assert.Equal(ruthless ? 0 : -30, SlayerRules.EnemyCriticalMultiplierDelta(!ruthless, true, 60));
            Assert.Equal(0, SlayerRules.EnemyCriticalMultiplierDelta(true, true, 60.01));
            string payload = state.Capture(2);
            state.Retire();
            var restored = OrdinaryResidualSession.Restore(payload, 8, 3, false, Get);
            Assert.False(state.CanPublish(7));
            Assert.True(restored.CanPublish(8));
            Assert.False(restored.CanPublish(7));
            Assert.Equal(1, restored.Slayer.Read(4, Get).RecentKills);
            Assert.Equal(0, restored.Slayer.Read(5, Get).RecentKills);
            Assert.False(restored.Kill(5, kill, Get).Recorded);
            Assert.Equal(0, restored.Slayer.Read(21, Get).MovementSpeedIncrease);
            // A rate observation settles the old interval; reload never starts a fresh buff duration.
            var clock = new OrdinaryEffectClock(20, 0);
            clock.ChangeRate(5, 2);
            var resumed = OrdinaryEffectClock.Restore(clock.Capture(6), 7);
            Assert.Equal(12.5, resumed.Deadline);
            resumed.Advance(12.5);
            Assert.Equal(0, resumed.Remaining);
        }
    }

    [Fact]
    public void DeadeyeReleaseDistanceAcceptedUsesMarksAndActorsKeepTheirScopes()
    {
        var bounded = new OrdinaryResidualSession(1);
        int Neutral(string _) => 0;
        bounded.Use(0, new(1, 1, true, true));
        bounded.Kill(0, new(1, 1, OrdinaryResidualSession.Credit.OwnerDirect, true, false, false, 1, 1), Neutral);
        int initialLength = bounded.Capture(10).Length;
        for (int i = 2; i <= 2048; i++)
        {
            bounded.Use(i, new(1, i, true, true));
            bounded.Use(i, new(1, -i, true, true));
            bounded.Kill(i, new(1, i, OrdinaryResidualSession.Credit.OwnerDirect, true, false, false, 1, 1), Neutral);
        }
        string boundedCarry = bounded.Capture(2060);
        Assert.True(boundedCarry.Length < initialLength + 128, "Reload payload must not grow with area action/kill history.");
        var boundedRestored = OrdinaryResidualSession.Restore(boundedCarry, 2, 2060, false, Neutral);
        Assert.False(boundedRestored.Use(2060, new(1, 1, true, true)));
        Assert.False(boundedRestored.Use(2060, new(1, -1, true, true)));
        Assert.False(boundedRestored.Kill(2060, new(1, 1, OrdinaryResidualSession.Credit.OwnerDirect,
            true, false, false, 1, 1), Neutral).Recorded);
        var legacy = JsonSerializer.Deserialize<OrdinaryResidualSession.Payload>(boundedCarry)! with
        {
            Kills = Enumerable.Range(1, 2048).Select(i => new long[] { 1, i }).ToArray(),
            Uses = Enumerable.Range(1, 2048).SelectMany(i => new[] { new long[] { 1, i }, new long[] { 1, -i } }).ToArray(),
        };
        var compacted = OrdinaryResidualSession.Restore(JsonSerializer.Serialize(legacy), 3, 2060, false, Neutral);
        Assert.True(compacted.Capture(2060).Length < initialLength + 128);
        Assert.False(compacted.Use(2060, new(1, 1, true, true)));
        Assert.False(compacted.Use(2060, new(1, -1, true, true)));
        OrdinaryGameplayChecks.DeadeyeGameplayTransactions();
        foreach (bool ruthless in new[] { false, true })
        {
            var stats = Sources(ruthless, "Far Shot", "Gathering Winds", "Wind Ward", "Focal Point", "Occupying Force");
            int Get(string name) => stats.GetValueOrDefault(name);
            var distance = DeadeyeProjectileDamage.Capture(Get);
            // Far Shot has hardmode columns but HasHardmodeAlternate=false: production keeps Standard's 30.
            double far = 1.3;
            Assert.Equal(1, distance.AilmentMultiplier(35));
            Assert.Equal(far, distance.HitMultiplier(70, false), 8);
            var pointBlank = distance with { PointBlank = true };
            Assert.Equal(far * .7, pointBlank.HitMultiplier(120, true), 8);
            Assert.Equal(far, pointBlank.AilmentMultiplier(120), 8);
            var ricochet = DeadeyeResidualProjectile.Capture(Get);
            Assert.True(ricochet.NoBarrageSpread);
            var state = new OrdinaryResidualSession(4);
            state.Gale.Observe(0, true, Get);
            var use = new OrdinaryResidualSession.AcceptedUse(1, 1, true, true);
            Assert.False(state.Use(0, use with { Paid = false }));
            Assert.False(state.Use(0, use with { Triggered = true }));
            Assert.True(state.Use(0, use));
            Assert.False(state.Use(0, use));
            Assert.Equal(1, state.Gale.Count);
            var world = new object();
            var owner = new ProjectileActorKey(1, 4);
            var source = new DeadeyeGaleAura.Source(owner, 1, new(0, 0), state.Gale) { World = world };
            var self = DeadeyeGaleAura.ForRecipient([source], owner, 1, new(0, 0), world: world)!.Value;
            var ally = DeadeyeGaleAura.ForRecipient([source], new(2, 4), 1, new(60, 0), world: world)!.Value;
            Assert.Equal((int)Math.Floor(8 * (1 + Get(DeadeyeGaleState.EffectPerStack) / 100d)), self.ActionSpeedIncrease(0));
            Assert.Equal(8, ally.ActionSpeedIncrease(0));
            Assert.Null(DeadeyeGaleAura.ForRecipient([source], owner, 1, new(0, 0), world: new object()));
            string carry = state.Capture(1);
            var restored = OrdinaryResidualSession.Restore(carry, 5, 2, true, Get);
            Assert.Equal(4, restored.Gale.Stacks[0].Deadline);
            Assert.Equal(Math.Max(0, 1 + Get(DeadeyeGaleState.TakenPerStack) / 100d),
                restored.Gale.IncomingHit(2, true), 8);
            restored.Gale.Advance(4);
            Assert.Equal(0, restored.Gale.Count);
            Assert.Equal(ruthless ? .8 : 1, DeadeyeMarkRules.Incoming(Get, true, 0, ruthless), 8);
            Assert.Equal(ruthless ? 1.1 : .75, DeadeyeMarkRules.Incoming(Get, false, 60, ruthless), 8);
            state.Marks.Apply(0, 10, 1, "mark", 4, _ => 0);
            Assert.Null(state.Marks.Current(1, 10, 2, true, _ => 0));
            Assert.True(state.Marks.Transfer(10, 1, 11, 2));
            Assert.NotNull(state.Marks.Current(3, 11, 2, true, _ => 0));
            Assert.Null(state.Marks.Current(4, 11, 2, true, _ => 0));
            // Real support source duration, with equipment/signature and generation supplied by its release.
            int Mirage(string name) => name == "support_mirage_archer_base_duration" ? 4000 : Get(name);
            var at = new ProjectileVector(0, 0);
            var release = new DeadeyeMirageLifecycle.Release(1, 1);
            Assert.Null(state.Mirages.OwnerHit(0, release, false, true, 4, "bow/gem", 1, at, Mirage));
            var actor = state.Mirages.OwnerHit(0, release, true, true, 4, "bow/gem", 1, at, Mirage)!;
            Assert.NotNull(actor);
            Assert.Null(state.Mirages.OwnerHit(0, release, true, true, 4, "bow/gem", 1, at, Mirage));
            Assert.True(state.Mirages.TryAttack(actor, 0, 4, "bow/gem", 1, out long attack));
            Assert.True(state.Mirages.Current(actor, attack, 4));
            Assert.False(state.Mirages.TryAttack(actor, .5, 4, "bow/gem", 1, out _));
            state.Mirages.Step(1, 5, "bow/gem");
            Assert.False(state.Mirages.Current(actor, attack, 4));
            int Detached(string name) => name switch
            {
                "mirage_archers_do_not_attach" => 1,
                "additional_max_mirage_archers" => 2,
                _ => Mirage(name),
            };
            var first = state.Mirages.OwnerHit(2, new(2, 1), true, true, 5, "bow/first", 1, at, Detached)!;
            var second = state.Mirages.OwnerHit(2, new(2, 2), true, true, 5, "bow/second", 2, new(100, 0), Detached)!;
            Assert.NotNull(first); Assert.NotNull(second); Assert.Equal(2, state.Mirages.Actors.Count);
            Assert.True(state.Mirages.TryAttack(first, 2, 5, "bow/first", 1, out _));
            Assert.True(state.Mirages.TryAttack(second, 2, 5, "bow/second", 1, out _));
            Assert.Equal(2, state.Mirages.Actors.Count);
            Assert.False(state.Mirages.TryAttack(first, 3, 5, "wrong/source", 1, out _));
        }
    }
}
