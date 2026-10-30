using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed partial class AscendancyGatewayIntegrationTests
{
    [Fact]
    public async Task Inquisitor_observations_from_an_older_connection_do_not_publish_its_equipment_qualification()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        Assert.Null((await game.Change(InquisitorPath(32816), true)).Error);
        var bow = ItemCatalog.Create(Program.Items.Find("Short Bow")!) with
        {
            Sockets = [new(SocketColour.White, FamilyGem("Fireball"))],
        };
        uint host = game.Items.Add(PlayerInventories.Weapon1, 0, 0, bow).Id;
        var older = game.Reload(); // The same character's earlier area has no connection-local raw Dexterity override.
        Assert.Contains(host, Program.InactiveItems[game.Character]);
        InquisitorSet(game.State, "base_dexterity", 100);
        game.State.Pools = Program.PoolsFor(game.State, 60);
        Assert.DoesNotContain(host, Program.InactiveItems[game.Character]);
        ushort fireball = Program.GemSkillIdPool.Value[0];
        Assert.Contains(ProgramValue<IReadOnlyList<GrantedSkill>>("PlayerSkills", game.State), s => s.Id == fireball);
        var published = Program.InactiveItems[game.Character];
        for (int i = 0; i < 10; i++)
        {
            ProgramValue<Dictionary<uint, int>>("InquisitorInputs", older);
            double now = Program.ProjectileWorldTime(older);
            ProgramValue<double>("InquisitorMirroredRecovery", older, now, now + .016,
                (Func<string, int>)(name => name == "base_life_regeneration_rate_per_minute" ? 6000 : 0));
            Program.InquisitorMotionChanged(older, older.Walk, now);
            // Check before a current stat rebuild could repair a stale global write.
            Assert.Same(published, Program.InactiveItems[game.Character]);
            Assert.DoesNotContain(host, Program.InactiveItems[game.Character]);
        }
        Assert.Contains(ProgramValue<IReadOnlyList<GrantedSkill>>("PlayerSkills", game.State), s => s.Id == fireball);
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("leaving")]
    [InlineData("retired")]
    public async Task Inquisitor_motion_recovery_and_queued_ES_sends_reject_old_lifecycles_before_observing_sources(string lifecycle)
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        Assert.Null((await game.Change(InquisitorPath(32816), true)).Error);
        Program.CharacterPlayerStats(game.State);
        var cache = game.State.StatSourcesCache;
        double now = Program.ProjectileWorldTime(game.State);
        switch (lifecycle)
        {
            case "closed": game.State.Closed = true; break;
            case "leaving": game.State.LeavingArea = true; break;
            case "retired": game.State.RageRetired = true; break;
        }
        // Changing an input makes a source read observable through its connection-local cache.
        InquisitorSet(game.State, "base_dexterity", 100);
        for (int i = 0; i < 10; i++)
        {
            Program.InquisitorMotionChanged(game.State, game.State.Walk, now);
            Assert.Equal(0, ProgramValue<double>("InquisitorMirroredRecovery", game.State, now, now + .016,
                (Func<string, int>)(name => name == "base_life_regeneration_rate_per_minute" ? 6000 : 0)));
            Assert.Same(cache, game.State.StatSourcesCache);
        }
        Assert.Empty((await game.InquisitorEnergyShieldSnapshot(game.State)).Bytes);
        Assert.Same(cache, game.State.StatSourcesCache);
    }

    [Fact]
    public async Task Inquisitor_transfer_can_still_capture_accepted_history_after_leaving_is_set()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        Assert.Null((await game.Change(InquisitorPath(19417), true)).Error);
        Program.InquisitorSkillUsed(game.State, attack: false, spell: true);
        game.State.LeavingArea = true;
        uint ticket = (uint)Random.Shared.Next(1, int.MaxValue);
        ProgramValue<object?>("CarryInquisitor", game.State, ticket);
        var destination = new ClientState(game.State.Layout, ticket, 1)
            { Character = game.Character, ClassRow = game.State.ClassRow };
        ProgramValue<int>("LoadProgress", destination, 60);
        destination.PlayerLife = destination.Pools.Life;
        ProgramValue<object?>("RestoreInquisitor", destination);
        Assert.Equal(1.1, ProgramValue<double>("InquisitorAttackFactor", destination));
    }

    [Fact]
    public async Task Inquisitor_retired_open_connection_resource_writer_finishes_without_changing_current_qualification_or_pools()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        var bow = ItemCatalog.Create(Program.Items.Find("Short Bow")!) with
            { Sockets = [new(SocketColour.White, FamilyGem("Fireball"))] };
        uint host = game.Items.Add(PlayerInventories.Weapon1, 0, 0, bow).Id;
        var older = game.Reload();
        Assert.Contains(host, Program.InactiveItems[game.Character]);
        InquisitorSet(game.State, "base_dexterity", 100);
        game.State.Pools = Program.PoolsFor(game.State, 60);
        var published = Program.InactiveItems[game.Character];
        Assert.DoesNotContain(host, published);
        older.PlayerLife = 1;
        older.PlayerMana = 0;
        uint currentLife = game.State.PlayerLife, currentMana = game.State.PlayerMana;
        await game.InquisitorRecoverySession(older, async recovery =>
        {
            Assert.False(recovery.IsCompleted);
            lock (older.Monsters) older.RageRetired = true;
            await recovery.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.False(older.Closed); // Retirement must stop this writer before the old socket closes.
            Assert.Equal(1u, older.PlayerLife);
            Assert.Equal(0u, older.PlayerMana);
            Assert.Equal(currentLife, game.State.PlayerLife);
            Assert.Equal(currentMana, game.State.PlayerMana);
            Assert.Same(published, Program.InactiveItems[game.Character]);
            Assert.DoesNotContain(host, Program.InactiveItems[game.Character]);
        });
    }

    [Fact]
    public async Task Inquisitor_temporarily_leaving_resource_writer_skips_loading_time_and_resumes_after_rollback()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        InquisitorSet(game.State, "base_life_regeneration_rate_per_minute", 6000);
        game.State.PlayerLife = 1;
        game.State.LeavingArea = true;
        await game.InquisitorRecoverySession(game.State, async recovery =>
        {
            await Task.Delay(100);
            Assert.Equal(1u, game.State.PlayerLife);
            Assert.False(recovery.IsCompleted);
            var resumed = System.Diagnostics.Stopwatch.StartNew();
            lock (game.State.Monsters) game.State.LeavingArea = false;
            while (game.State.PlayerLife == 1 && resumed.Elapsed.TotalSeconds < 1) await Task.Delay(5);
            Assert.True(game.State.PlayerLife > 1);
            Assert.InRange(game.State.PlayerLife - 1, 1u, (uint)Math.Ceiling(resumed.Elapsed.TotalSeconds * 100) + 2);
            Assert.False(recovery.IsCompleted);
        });
    }

    static ClientState InquisitorOlderObserver(GatewayCharacter game)
    {
        var bow = ItemCatalog.Create(Program.Items.Find("Short Bow")!) with
            { Sockets = [new(SocketColour.White, FamilyGem("Fireball"))] };
        uint host = game.Items.Add(PlayerInventories.Weapon1, 0, 0, bow).Id;
        var older = game.Reload();
        Assert.Contains(host, Program.InactiveItems[game.Character]);
        InquisitorSet(game.State, "base_dexterity", 100);
        game.State.Pools = Program.PoolsFor(game.State, 60);
        Assert.DoesNotContain(host, Program.InactiveItems[game.Character]);
        return older;
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("leaving")]
    [InlineData("retired")]
    public void Inquisitor_stale_stat_calculations_cannot_publish_over_current_equipment_qualification(string lifecycle)
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        var older = InquisitorOlderObserver(game);
        var published = Program.InactiveItems[game.Character];
        switch (lifecycle)
        {
            case "closed": older.Closed = true; break;
            case "leaving": older.LeavingArea = true; break;
            case "retired": older.RageRetired = true; break;
        }
        for (int i = 0; i < 5; i++)
        {
            Program.CharacterPlayerStats(older);
            Assert.Same(published, Program.InactiveItems[game.Character]);
        }
        Assert.Contains(ProgramValue<IReadOnlyList<GrantedSkill>>("PlayerSkills", game.State),
            s => s.Id == Program.GemSkillIdPool.Value[0]);
    }

    [Theory]
    [InlineData("RunPlayerTick")]
    [InlineData("RunCharges")]
    [InlineData("RunFortification")]
    [InlineData("RunFlaskChargeRegen")]
    [InlineData("RunUtilityFlasks")]
    [InlineData("RunFlaskGrounds")]
    [InlineData("SendRage")]
    public async Task Inquisitor_retired_continuous_stat_publishers_finish_before_the_old_socket_closes(string method)
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        var older = InquisitorOlderObserver(game);
        var published = Program.InactiveItems[game.Character];
        older.RageRetired = true;
        await game.InquisitorContinuousSession(method, older, async task =>
        {
            await task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.False(older.Closed);
            Assert.Same(published, Program.InactiveItems[game.Character]);
        });
    }

    [Fact]
    public async Task Inquisitor_player_tick_skips_temporarily_leaving_sources_without_stopping_until_retirement()
    {
        if (!DataAvailable()) return;
        using var game = InquisitorGame();
        var older = InquisitorOlderObserver(game);
        var published = Program.InactiveItems[game.Character];
        older.LeavingArea = true;
        await game.InquisitorContinuousSession("RunPlayerTick", older, async task =>
        {
            await Task.Delay(300);
            Assert.False(task.IsCompleted);
            Assert.Same(published, Program.InactiveItems[game.Character]);
            lock (older.Monsters) older.RageRetired = true;
            await task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.False(older.Closed);
        });
    }

    sealed partial class GatewayCharacter
    {
        public Task<(string? Error, byte[] Bytes)> InquisitorContinuousSession(string method, ClientState state,
            Func<Task, Task> inspect) => Exchange(async (stream, cipher) =>
            {
                object?[] args = ["inquisitor-continuous-lifecycle", stream, cipher, state];
                if (method == "RunUtilityFlasks") args = [.. args, 0];
                var task = ProgramValue<Task>(method, args);
                try { await inspect(task); }
                finally
                {
                    state.Closed = true;
                    await task.WaitAsync(TimeSpan.FromSeconds(2));
                }
                return null;
            });

        public Task<(string? Error, byte[] Bytes)> InquisitorEnergyShieldSnapshot(ClientState state) =>
            Exchange(async (stream, cipher) =>
            {
                await ProgramValue<Task>("SendPlayerEnergyShield", "inquisitor-ES-lifecycle", stream, cipher, state);
                return null;
            });

        public Task<(string? Error, byte[] Bytes)> InquisitorRecoverySession(ClientState state, Func<Task, Task> inspect) =>
            Exchange(async (stream, cipher) =>
            {
                var recovery = ProgramValue<Task>("RunResourceRecovery", "inquisitor-recovery-lifecycle", stream, cipher, state);
                try { await inspect(recovery); }
                finally
                {
                    state.Closed = true;
                    await recovery.WaitAsync(TimeSpan.FromSeconds(2));
                }
                return null;
            });
    }
}
