using GameServer.Core.World;
using GameServer.Core.Protocol.Instance;

using League = GameServer.Core.Accounts.League;

public sealed partial class WhispererRuntimeTests
{
    internal static void DeadeyeGameplayTransactions()
    {
        var definition = Assert.Single(new AscendancyCatalog().Definitions,
            d => d.Identity.Family == AscendancyFamily.Normal && d.Name == "Deadeye");
        using var h = new Harness(definition); var c = h.Conn;
        c.MainAscendancy = new(definition.Identity, [24848, 23169, 45313, 44482], []);
        c.LeagueModule = new(new League("Standard", "Standard", false, false), new HashSet<string>(["Game"]));
        c.StatSourcesCacheState = null;
        Assert.Equal(8, Game.OrdinaryTailwindSpeed(c));
        for (int i = 1; i <= 10; i++) Game.OrdinaryAcceptedUse(c, i);
        Game.OrdinaryAcceptedUse(c, 10); // Same admitted action cannot grant twice.
        Assert.Equal(16, Game.OrdinaryTailwindSpeed(c));
        var player = Game.CharacterPlayerStats(c);
        Assert.Equal(.6, Game.OrdinaryIncomingHit(c, player, 0), 8);
        Assert.Equal(8, Game.OrdinaryTailwindSpeed(c));
        Game.OrdinaryAcceptedUse(c, 11);
        var native = Game.OrdinaryPresentation(c);
        Assert.NotEmpty(native); Assert.NotEqual((ushort)0, c.OrdinaryTailwindBuff);
        Assert.Empty(Game.OrdinaryPresentation(c));
        var gem = h.Fireball;
        var session = Game.ProjectilesFor("ordinary", h.Wire, h.Ciphers, c);
        var released = Game.CaptureGemProjectile(c, session, gem.Id, gem, (170, 100), Game.CaptureSkillCriticalRoll(c, gem.Id));
        var offense = Assert.IsType<ProjectileOffense>(released.Payload);
        Assert.Equal(1.3, offense.OrdinaryDistance.HitMultiplier(70, false), 8);
        c.MainAscendancy = new(definition.Identity, [], []); c.StatSourcesCacheState = null;
        Assert.Equal(0, Game.OrdinaryTailwindSpeed(c));
        Assert.NotEmpty(Game.OrdinaryPresentation(c)); Assert.Equal((ushort)0, c.OrdinaryTailwindBuff);
        Assert.Equal(1.3, offense.OrdinaryDistance.HitMultiplier(70, false), 8); // Release survives refund.
        Assert.Equal(1, Game.CaptureGemProjectile(c, session, gem.Id, gem, (170, 100),
            Game.CaptureSkillCriticalRoll(c, gem.Id)).Payload is ProjectileOffense next
                ? next.OrdinaryDistance.HitMultiplier(70, false) : 0);
    }

    internal static void OrdinaryGameplayTransactions()
    {
        var definition = Assert.Single(new AscendancyCatalog().Definitions,
            d => d.Identity.Family == AscendancyFamily.Normal && d.Name == "Slayer");
        using var h = new Harness(definition);
        var c = h.Conn;
        c.MainAscendancy = new(definition.Identity, [], []);
        // Ordinary admission does not depend on the optional Phrecia provider.
        c.LeagueModule = new(new League("Standard", "Standard", false, false), new HashSet<string>(["Game"]));
        c.StatSourcesCacheState = null;
        void Set(string name, int value) => c.PlayerStats[Key(name)] = value;
        Set(SlayerRules.KillMovementSpeed, 20); Set(SlayerRules.RareKillAttackSpeed, 20);
        Set(SlayerRules.RecentKillDamage, 10); Set(SlayerRules.UniqueDamage, 20);
        Set(SlayerRules.OverkillLeech, 20); Set("life_leech_does_not_stop_at_full_life", 1);
        var player = Game.CharacterPlayerStats(c);
        var target = new SpawnedMonster("ordinary lethal", new(100, 100, 0), 0, 0, 100)
            { Hostile = true, Rarity = MonsterRarity.Rare };
        c.Monsters[42] = target;
        var sources = new LeechSources(DamagePacket.Empty, DamagePacket.Empty, DamagePacket.Empty);
        Assert.Equal(0, Game.LeechOf(c).Life.Count);
        Game.LeechOnHitLocked(c, target, DamagePacket.Of(DamageType.Physical, 180, 180), 180, sources);
        Assert.Equal(1, Game.LeechOf(c).Life.Count);
        Assert.Equal(0u, Game.ApplyMonsterHitLocked(c, target, 180, player: player)!.Value.Life);
        Assert.Null(Game.ApplyMonsterHitLocked(c, target, 180, player: player));
        Assert.Equal(1, c.OrdinaryDeathSequence);
        Assert.Equal(1.1, Game.OrdinaryTargetMore(c, target, player), 8);
        var current = Game.CharacterPlayerStats(c);
        Assert.True(current[Key("attack_speed_+%")] >= 20);
        Assert.NotNull(c.OrdinaryResidualPayload);
        var phase = new SpawnedMonster("phase", new(100, 100, 0), 0, 0, 100) { StageRunning = true };
        Game.LeechOnHitLocked(c, phase, DamagePacket.Of(DamageType.Physical, 180, 180), 180, sources);
        Assert.Equal(1, Game.LeechOf(c).Life.Count);
        Assert.True(Game.ApplyMonsterHitLocked(c, phase, 180, player: player)!.Value.Life > 0);
        Assert.Equal(1, c.OrdinaryDeathSequence);
        Set(SlayerRules.KillMovementSpeed, 0); Set(SlayerRules.RareKillAttackSpeed, 0);
        _ = Game.CharacterPlayerStats(c);
        Set(SlayerRules.KillMovementSpeed, 20); Set(SlayerRules.RareKillAttackSpeed, 20);
        current = Game.CharacterPlayerStats(c);
        Assert.Equal(0, current.GetValueOrDefault(Key("attack_speed_+%")));
        // Accepted travel freezes remaining time and rebases the destination epoch; relog has no ticket grant.

        var nextVictim = new SpawnedMonster("carry", new(100, 100, 0), 0, 0, 100) { Rarity = MonsterRarity.Rare };
        Assert.Equal(0u, Game.ApplyMonsterHitLocked(c, nextVictim, 180, player: Game.CharacterPlayerStats(c))!.Value.Life);
        Game.CarryOrdinary(c, 987);
        var destination = h.NewConnection(c.Character!, ticket: 987, optional: false);
        destination.MainAscendancy = c.MainAscendancy;
        destination.ProjectileRuntimeState = new ManualRuntime { At = 100 };
        foreach (var (key, value) in c.PlayerStats) destination.PlayerStats[key] = value;
        Game.RestoreOrdinary(destination);
        int Get(string name) => destination.PlayerStats.GetValueOrDefault(Key(name));
        double arrived = Game.ProjectileWorldTime(destination);
        var carried = Game.OrdinaryOf(destination, Get).Slayer.Read(arrived, Get);
        Assert.Equal(2, carried.RecentKills); Assert.Equal(20, carried.AttackSpeedIncrease);
        Assert.Equal(0, Game.OrdinaryOf(destination, Get).Slayer.Read(arrived + 20, Get).AttackSpeedIncrease);
        Assert.False(OrdinaryResidualTransfers.Tickets.ContainsKey(987));
        Game.ClearOrdinary(c);
        Assert.Equal(1, Game.OrdinaryTargetMore(c, target, player));
    }
}
