using System.Reflection;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed partial class AscendancyGatewayIntegrationTests
{
    static object InquisitorSessionFor(ClientState state) => ProgramValue<object>("InquisitorOf", state);
    static T InquisitorSessionProperty<T>(ClientState state, string property) =>
        (T)InquisitorSessionFor(state).GetType().GetProperty(property)!.GetValue(InquisitorSessionFor(state))!;

    [Fact]
    public async Task Inquisitor_short_walk_between_ticks_observes_departure_arrival_and_the_next_departure()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        Assert.Null((await game.Change(InquisitorPath(32816), true)).Error);
        var ground = InquisitorSessionProperty<InquisitorGround>(game.State, "Ground");
        var walk = game.State.Walk;
        walk.SetSpeed(10, 10);
        walk.Order(33, 32, 10);
        Assert.Equal(14, ground.OwnerEffectUntil, 8);
        // No stat query or tick runs during this short walk. The next mutation must first observe its arrival.
        walk.Order(34, 32, 10.2);
        Assert.Equal(14.2, ground.OwnerEffectUntil, 8);
        Assert.True(ground.OwnerAffected(14.199));
        Assert.False(ground.OwnerAffected(14.2));
    }

    [Fact]
    public async Task Inquisitor_stationary_teleport_expires_old_target_linger_from_the_accepted_teleport()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        Assert.Null((await game.Change(InquisitorPath(32816), true)).Error);
        var ground = InquisitorSessionProperty<InquisitorGround>(game.State, "Ground");
        Assert.Equal(15, ground.EnemyDamageTaken(0, 42, (32, 32), true));
        game.State.Walk.Teleport(100, 100, 1);
        Assert.True(ground.OwnerAffected(20));
        Assert.Equal(0, ground.EnemyDamageTaken(5, 42, (32, 32), true));
    }

    [Fact]
    public async Task Inquisitor_native_owner_buff_is_removed_four_seconds_after_accepted_departure()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        Assert.Null((await game.Change(InquisitorPath(32816), true)).Error);
        await game.InquisitorPresentation();
        var session = InquisitorSessionFor(game.State);
        var buffField = session.GetType().GetField("GroundBuff", BindingFlags.Instance | BindingFlags.Public)!;
        ushort buff = (ushort)buffField.GetValue(session)!;
        Assert.NotEqual((ushort)0, buff);
        var ground = InquisitorSessionProperty<InquisitorGround>(game.State, "Ground");
        double departure = game.State.Clock.Elapsed.TotalSeconds;
        game.State.Walk.SetSpeed(1, departure);
        game.State.Walk.Order(100, 32, departure);
        Assert.Equal(departure + 4, ground.OwnerEffectUntil, 8);
        await game.InquisitorPresentation();
        Assert.Equal(buff, (ushort)buffField.GetValue(session)!);
        await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, departure + 4.05 - game.State.Clock.Elapsed.TotalSeconds)));
        var expired = await game.InquisitorPresentation();
        Assert.Equal((ushort)0, (ushort)buffField.GetValue(session)!);
        Assert.True(expired.Bytes.AsSpan().IndexOf(InstanceEntities.RemoveBuff(0x1000, buff)) >= 0);
        Assert.Equal(0, InquisitorStat(game.State, "on_consecrated_ground"));
    }

    [Fact]
    public async Task Inquisitor_Fanaticism_reads_active_item_buff_effect_and_developer_replacements()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        Assert.Null((await game.Change(InquisitorPath(13851), true)).Error);
        InquisitorSet(game.State, "additional_dexterity", 100);
        var sword = ItemCatalog.Create(Program.Items.Find("Rusted Sword")!) with
        {
            Mods = [Program.Items.Mods.ById("IncreasedBuffEffectivenessUniqueOneHandSword11")!
                .With(ModSection.Explicit, [10], Random.Shared)]
        };
        var placed = game.Items.Add(PlayerInventories.Weapon1, 0, 0, sword);
        game.State.Pools = Program.PoolsFor(game.State, 60);
        Assert.Equal(10, InquisitorStat(game.State, "buff_effect_on_self_+%"));
        var combat = InquisitorSessionProperty<InquisitorCombatState>(game.State, "Combat");
        var raw = ProgramValue<Dictionary<uint, int>>("InquisitorInputs", game.State);
        var inputs = InquisitorCombatState.Inputs.From(n => raw.GetValueOrDefault(InquisitorKey(n)), 5);
        combat.Restore(new([], null, 0, 1, 5), Program.ProjectileWorldTime(game.State), inputs);
        int fireball = Program.Skills.EffectRow("Fireball");
        Assert.Equal(82, ProgramValue<InquisitorOffense.FanaticismModifiers>("InquisitorFanaticism", game.State, fireball).CastSpeedMorePct);
        InquisitorSet(game.State, "buff_effect_on_self_+%", 0);
        Assert.Equal(75, ProgramValue<InquisitorOffense.FanaticismModifiers>("InquisitorFanaticism", game.State, fireball).CastSpeedMorePct);
        game.State.PlayerStats.Remove(InquisitorKey("buff_effect_on_self_+%"));
        game.Items.Remove(PlayerInventories.Weapon1, placed.Id);
        Assert.Equal(75, ProgramValue<InquisitorOffense.FanaticismModifiers>("InquisitorFanaticism", game.State, fireball).CastSpeedMorePct);
    }
}
