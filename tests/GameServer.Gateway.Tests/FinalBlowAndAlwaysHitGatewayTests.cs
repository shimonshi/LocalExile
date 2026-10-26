using System.Reflection;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

[CollectionDefinition("Final Blow and always hit Gateway", DisableParallelization = true)]
public sealed class FinalBlowAndAlwaysHitGatewayCollection;

// The two Behemoth/Chieftain notables' conditional crit and cannot-be-evaded stats, through the real passive catalog
// and the real pools/panel consumers (PoolsFor, CharacterPlayerStats, OnLowLife, GamblerFor).
[Collection("Final Blow and always hit Gateway")]
public sealed class FinalBlowAndAlwaysHitGatewayTests : IDisposable
{
    readonly string _cwd = Directory.GetCurrentDirectory();
    readonly string _name = "FinalBlow" + Guid.NewGuid().ToString("N");
    static MethodInfo Method(string name) => typeof(Program).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!;

    public FinalBlowAndAlwaysHitGatewayTests()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "data", "tables")))
            {
                Directory.SetCurrentDirectory(dir.FullName);
                break;
            }
        TestSaves.Ensure();
    }

    ClientState Character(bool armed = false, string? suffix = null)
    {
        string name = _name + suffix;
        var conn = new ClientState(new(0, "CritTest", 128, 128, 32, 32, null, []), 0, 1)
        { Character = name, ClassRow = 1 };
        if (armed)
            PlayerInventories.For(name, Program.Items).Add(PlayerInventories.Weapon1, 0, 0,
                ItemCatalog.Create(Program.Items.Row(384)!));
        return conn;
    }

    static void Set(ClientState conn, string name, int value) => conn.PlayerStats[Program.Stats.Key(name)!.Value] = value;
    static CharacterStats.Pools Pools(ClientState conn) =>
        (CharacterStats.Pools)Method("PoolsFor").Invoke(null, [conn, 20])!;
    static Dictionary<uint, int> Panel(ClientState conn) =>
        (Dictionary<uint, int>)Method("CharacterPlayerStats").Invoke(null, [conn])!;
    static GamblerModifiers Gambler(Dictionary<uint, int> panel) =>
        (GamblerModifiers)Method("GamblerFor").Invoke(null, [panel])!;

    int PanelValue(Dictionary<uint, int> panel, string name) => panel[Program.Stats.Key(name)!.Value];

    [Fact]
    public void Final_Blow_multiplies_the_unarmed_melee_critical_only_with_an_empty_active_main_hand()
    {
        var conn = Character();
        conn.Passives.Add(10135); // Final Blow: base melee crit while unarmed 10%, low-life extra roll, +50% multiplier.
        conn.Pools = Pools(conn);
        var pools = Pools(conn);
        Assert.Equal(1000, pools.AttackCriticalStrikeChance); // 10.00% unarmed base, no increased.
        // The bonus is stored separately (the melee context decides at the roll); the panel row shows the sum.
        Assert.Equal(150, pools.CriticalStrikeMultiplier);
        Assert.Equal(50, conn.Pools.UnarmedCriticalStrikeMultiplierBonus);
        var panel = Panel(conn);
        Assert.Equal(1000, PanelValue(panel, "main_hand_critical_strike_chance"));
        Assert.Equal(200, PanelValue(panel, "main_hand_critical_strike_multiplier"));
        // An unarmed non-melee attack gem keeps the plain attack multiplier (its own catalog's classification).
        int concoction = Program.Skills.EffectRow("ExplosiveConcoction");
        Assert.True(Program.Skills.IsAttack(concoction));
        Assert.False(Program.Skills.IsMelee(concoction));
        Assert.Equal(150, Program.CriticalMultiplierFor(conn, spell: false, melee: Program.MeleeAttack(conn, concoction)));
        // The unarmed melee attack's context and the panel keep the +50.
        Assert.Equal(200, Program.CriticalMultiplierFor(conn, spell: false, melee: Program.MeleeAttack(conn)));
        // The spell route reads neither the unarmed base nor the unarmed multiplier.
        Assert.Equal(150, pools.SpellCriticalStrikeMultiplier);
        Assert.Equal(500, pools.SpellCriticalStrikeChance);
        // A weapon in the active main hand suppresses the unarmed pair (its base moves to the weapon's 8.30%).
        var armed = Character(armed: true, "armed");
        armed.Passives.Add(10135);
        var armedPools = Pools(armed);
        Assert.Equal(830, armedPools.AttackCriticalStrikeChance);
        Assert.Equal(150, armedPools.CriticalStrikeMultiplier);
        Assert.Equal(0, armedPools.UnarmedCriticalStrikeMultiplierBonus);
        // A weapon in the inactive set does not make the empty active hand armed, and swapping into it stops the grant.
        var swapped = Character(suffix: "swapped");
        PlayerInventories.For(_name + "swapped", Program.Items).Add(PlayerInventories.Weapon2, 0, 0,
            ItemCatalog.Create(Program.Items.Row(384)!));
        swapped.Passives.Add(10135);
        Assert.Equal(50, Pools(swapped).UnarmedCriticalStrikeMultiplierBonus);
        PlayerInventories.For(_name + "swapped", Program.Items).SwapWeaponSets();
        var swappedPools = Pools(swapped);
        Assert.Equal(0, swappedPools.UnarmedCriticalStrikeMultiplierBonus);
        Assert.Equal(150, swappedPools.CriticalStrikeMultiplier);
        // The refund restores the base multiplier and drops the experimental path too.
        conn.Passives.Remove(10135);
        var refunded = Pools(conn);
        Assert.Equal(150, refunded.CriticalStrikeMultiplier);
        Assert.Equal(0, refunded.UnarmedCriticalStrikeMultiplierBonus);
        Assert.Equal(0, refunded.AttackCriticalStrikeChance);
    }

    [Fact]
    public void The_final_blow_extra_critical_roll_follows_the_live_low_life_boundary()
    {
        var conn = Character();
        // The production Gambler carries the low-life extra roll only from the allocated node.
        Assert.Equal(0, Gambler(Panel(conn)).LowLifeCriticalExtraRolls);
        conn.Passives.Add(10135);
        Assert.Equal(1, Gambler(Panel(conn)).LowLifeCriticalExtraRolls);
        // The boundary reads the live life values, inclusive at exactly half of the pool's maximum.
        conn.Pools = Pools(conn);
        conn.PlayerLife = conn.PlayerMaxLife / 2;
        Assert.True(Program.OnLowLife(conn));
        conn.PlayerLife = conn.PlayerMaxLife / 2 + 1;
        Assert.False(Program.OnLowLife(conn));
        // The Life Mastery-style raised threshold (the override percentage) moves the boundary, and boundaries stay
        // inclusive there too; the ordinary conditional percentage applies when no override is set.
        Set(conn, "low_life_threshold_%_override", 55);
        conn.Pools = Pools(conn);
        conn.PlayerLife = (uint)Math.Floor(conn.PlayerMaxLife * 0.55);
        Assert.True(Program.OnLowLife(conn));
        conn.PlayerLife = (uint)Math.Floor(conn.PlayerMaxLife * 0.55) + 1;
        Assert.False(Program.OnLowLife(conn));
        Set(conn, "low_life_threshold_%_override", 0);
        Set(conn, "low_life_threshold_%", 60);
        conn.Pools = Pools(conn);
        conn.PlayerLife = (uint)Math.Floor(conn.PlayerMaxLife * 0.59);
        Assert.True(Program.OnLowLife(conn));
        // Zero life is the boundary itself (a dead player never rolls, but the answer keeps its meaning).
        conn.PlayerLife = 0;
        Assert.True(Program.OnLowLife(conn));
    }

    [Fact]
    public void Exhibit_122_makes_the_reference_hit_chance_100_without_touching_rating_or_crit()
    {
        var baseline = Character(armed: true, "base");
        var baselinePools = Pools(baseline);
        var withNode = Character(armed: true, "node");
        withNode.Passives.Add(36978); // Exhibit 122: global_always_hit, dexterity_+% -50.
        var poolsNode = Pools(withNode);
        withNode.Pools = poolsNode;
        Assert.True(poolsNode.AlwaysHit);
        // The cannot-be-evaded flag is not an accuracy or crit value: the critical chance and multiplier match the
        // baseline (the node's separate -50% dexterity penalty moves only the attributes it names, at PassiveBonus).
        Assert.Equal(baselinePools.AttackCriticalStrikeChance, poolsNode.AttackCriticalStrikeChance);
        Assert.Equal(baselinePools.CriticalStrikeMultiplier, poolsNode.CriticalStrikeMultiplier);
        var panelNode = Panel(withNode);
        // Both reference rows (the level's normal and high-dexterity monster) read 100; the flagged character never
        // rolls a miss, including against that high-evasion reference.
        Assert.Equal(100, PanelValue(panelNode, "main_hand_chance_to_hit_%"));
        Assert.Equal(100, PanelValue(panelNode, "main_hand_chance_to_hit_evasive_monsters_%"));
        Assert.InRange(PanelValue(Panel(baseline), "main_hand_chance_to_hit_evasive_monsters_%"), 5, 99);
        // A dev /set_stat input reaches the same flag, /accuracy's rating stays, and both removals drop it again.
        var dev = Character(armed: true, "dev");
        Set(dev, "global_always_hit", 1);
        var poolsDev = Pools(dev);
        dev.Pools = poolsDev;
        Assert.True(poolsDev.AlwaysHit);
        Assert.Equal(baselinePools.Accuracy, poolsDev.Accuracy);
        Assert.Equal(100, PanelValue(Panel(dev), "main_hand_chance_to_hit_%"));
        Set(dev, "global_always_hit", 0);
        Assert.False(Pools(dev).AlwaysHit);
        withNode.Passives.Remove(36978);
        withNode.Pools = Pools(withNode);
        Assert.False(withNode.Pools.AlwaysHit);
        Assert.InRange(PanelValue(Panel(withNode), "main_hand_chance_to_hit_evasive_monsters_%"), 5, 99);
    }

    public void Dispose()
    {
        PlayerInventories.Forget(_name);
        PlayerInventories.Forget(_name + "base");
        PlayerInventories.Forget(_name + "node");
        PlayerInventories.Forget(_name + "dev");
        PlayerInventories.Forget(_name + "armed");
        PlayerInventories.Forget(_name + "swapped");
        Directory.SetCurrentDirectory(_cwd);
    }
}
