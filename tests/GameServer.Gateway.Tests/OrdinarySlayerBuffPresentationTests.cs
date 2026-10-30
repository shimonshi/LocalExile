using System.Buffers.Binary;
using System.Text.Json;
using GameServer.Core.Protocol.Instance;
using GameServer.Core.World;

namespace GameServer.Gateway.Tests;

public sealed partial class AscendancyGatewayIntegrationTests
{
    sealed record HeadsmanPacket(bool Add, ushort Id, ushort Row = 0, float Seconds = 0, int Value = 0);
    static List<HeadsmanPacket> HeadsmanPackets(byte[] bytes)
    {
        var result = new List<HeadsmanPacket>();
        for (int offset = 0; offset < bytes.Length;)
        {
            ushort opcode = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset));
            Assert.Equal(Program.PlayerId, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 2)));
            ushort id = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 12));
            if (opcode == InstanceEntities.RemoveBuffId) { result.Add(new(false, id)); offset += 14; continue; }
            Assert.Equal(InstanceEntities.AddBuffId, opcode);
            ushort row = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 14));
            Assert.Equal(Program.PlayerId, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 16)));
            Assert.Equal(2, bytes[offset + 22]); // Native +0x68 countdown; exactly one ordered row stat follows it.
            float seconds = BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 23)));
            int value = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset + 27));
            Assert.Equal(0u, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 31)));
            result.Add(new(true, id, row, seconds, value)); offset += 35;
        }
        return result;
    }

    static async Task<GatewayCharacter> HeadsmanCharacter()
    {
        Assert.True(DataAvailable(), "Native buff regression requires the pinned Default public data.");
        var game = new GatewayCharacter(choice: "Slayer", family: AscendancyFamily.Normal, level: 60, refundPoints: 100);
        var definition = Program.Ascendancies.Find(4, AscendancyFamily.Normal, "Slayer")!;
        Assert.Null((await game.Change(PathTo(definition, 3184), true)).Error);
        game.State.PlayerSent = true;
        game.State.Clock.Stop();
        game.State.NextBuff = 100;
        return game;
    }

    static void HeadsmanKill(ClientState conn, bool rare = false)
    {
        lock (conn.Monsters)
        {
            uint id = (uint)(conn.Monsters.Count + 5000);
            var target = new SpawnedMonster("Headsman presentation hostile", new(34, 32, 0), 0, 2, 100)
                { Rarity = rare ? MonsterRarity.Rare : MonsterRarity.Normal };
            conn.Monsters[id] = target;
            var stats = Program.CharacterPlayerStats(conn);
            var hit = new Program.OwnerSkillHit(1000, DamagePacket.Of(DamageType.Chaos, 1000, 1000),
                Program.HitLeechSources(stats, false), stats);
            ProgramValue<object>("ApplyMonsterHitLocked", conn, target, 1000, hit);
            Assert.True(target.Dead);
        }
    }

    static async Task<List<HeadsmanPacket>> PublishHeadsman(GatewayCharacter game, ClientState? conn = null)
    {
        var output = await game.StatLifecycleExchange((stream, ciphers) =>
            Program.SendSlayerBuffs("headsman-native-adapter-test", stream, ciphers, conn ?? game.State));
        return HeadsmanPackets(output.Bytes);
    }

    [Fact]
    public async Task Slayer_Headsman_native_packets_pin_rows_ordered_values_normal_rare_refresh_and_no_extra_stats()
    {
        using var table = JsonDocument.Parse(File.ReadAllBytes("data/tables/BuffDefinitions.json"));
        foreach (var (row, id, key) in new[] { (1341, "bane_of_legends_movement_speed", 179),
            (1342, "bane_of_legends_attack_speed", 70) })
        {
            var definition = table.RootElement[row];
            Assert.Equal(id, definition.GetProperty("Id").GetString());
            Assert.Equal(18, definition.GetProperty("BuffCategory").GetInt32());
            Assert.Equal(key, Assert.Single(definition.GetProperty("StatsKeys").EnumerateArray()).GetInt32());
        }
        using var game = await HeadsmanCharacter();
        Assert.Empty(await PublishHeadsman(game));
        HeadsmanKill(game.State);
        var before = Program.CharacterPlayerStats(game.State);
        var normal = Assert.Single(await PublishHeadsman(game));
        Assert.Equal(new HeadsmanPacket(true, 100, 1341, 20, 10), normal);
        Assert.Empty(await PublishHeadsman(game)); // Remaining time alone is not a new grant.
        Assert.Equal(before.OrderBy(x => x.Key), Program.CharacterPlayerStats(game.State).OrderBy(x => x.Key));
        HeadsmanKill(game.State, rare: true);
        var rare = await PublishHeadsman(game);
        Assert.Equal(new[] { new HeadsmanPacket(false, 100), new(true, 101, 1341, 20, 10),
            new(true, 102, 1342, 20, 10) }, rare);
        HeadsmanKill(game.State);
        var refresh = await PublishHeadsman(game);
        Assert.Equal(new[] { new HeadsmanPacket(false, 101), new(true, 103, 1341, 20, 10) }, refresh);
        Assert.Empty(await PublishHeadsman(game)); // A normal kill never replaces the independent attack instance.
    }

    [Fact]
    public void Slayer_Headsman_native_model_independent_expiry_recipient_rate_and_refresh_identity()
    {
        var state = new SlayerKillState();
        var presentation = new SlayerBuffPresentation();
        var sources = new Dictionary<string, int> { [SlayerRules.KillMovementSpeed] = 10, [SlayerRules.RareKillAttackSpeed] = 10 };
        int Get(string name) => sources.GetValueOrDefault(name);
        ushort next = 1;
        state.RecordKill(0, true, Get);
        Assert.Equal(2, HeadsmanPackets(presentation.Build(state.ReadBuffs(0, Get), Program.PlayerId, () => next++)).Count);
        state.RecordKill(10, false, Get);
        var oldAttack = state.ReadBuffs(10, Get).Attack;
        var refreshed = HeadsmanPackets(presentation.Build(state.ReadBuffs(10, Get), Program.PlayerId, () => next++));
        Assert.Equal(new[] { new HeadsmanPacket(false, 1), new(true, 3, 1341, 20, 10) }, refreshed);
        sources["buff_time_passed_+%_only_buff_category"] = 100;
        Assert.False(presentation.NeedsPublication(state.ReadBuffs(10, Get))); // Category 18 excludes this input.
        sources["buff_time_passed_+%"] = 100;
        var rateChange = HeadsmanPackets(presentation.Build(state.ReadBuffs(10, Get), Program.PlayerId, () => next++));
        Assert.Equal(new[] { new HeadsmanPacket(false, 3), new(true, 4, 1341, 10, 10),
            new(false, 2), new(true, 5, 1342, 5, 10) }, rateChange);
        Assert.Equal(oldAttack!.Value.Identity, state.ReadBuffs(10, Get).Attack!.Value.Identity);
        var attackExpired = HeadsmanPackets(presentation.Build(state.ReadBuffs(15, Get), Program.PlayerId, () => next++));
        Assert.Equal(new[] { new HeadsmanPacket(false, 5) }, attackExpired);
        var movementExpired = HeadsmanPackets(presentation.Build(state.ReadBuffs(20, Get), Program.PlayerId, () => next++));
        Assert.Equal(new[] { new HeadsmanPacket(false, 4) }, movementExpired);
        state.RecordKill(21, false, Get);
        Assert.NotEqual(0, state.ReadBuffs(21, Get).Movement!.Value.Identity);
    }

    [Fact]
    public async Task Slayer_Headsman_native_paid_refund_reallocation_relog_and_death_remove_without_revival()
    {
        using var game = await HeadsmanCharacter();
        HeadsmanKill(game.State, true);
        Assert.Equal(2, (await PublishHeadsman(game)).Count);
        Assert.Null((await game.Change([3184], false, refundPoints: true)).Error);
        Assert.Equal((ushort)95, game.State.RefundPoints);
        Assert.Equal(new[] { new HeadsmanPacket(false, 100), new(false, 101) }, await PublishHeadsman(game));
        Assert.Null((await game.Change([3184], true)).Error);
        Assert.Empty(await PublishHeadsman(game));
        var relog = game.Reload(); relog.PlayerSent = true;
        Assert.Empty(await PublishHeadsman(game, relog));
        HeadsmanKill(game.State, true);
        Assert.Equal(2, (await PublishHeadsman(game)).Count);
        game.State.PlayerLife = 0;
        Assert.Equal(new[] { new HeadsmanPacket(false, 102), new(false, 103) }, await PublishHeadsman(game));
        Assert.Empty(await PublishHeadsman(game));
    }

    [Theory]
    [InlineData("refund")] [InlineData("death")] [InlineData("closed")] [InlineData("retired")]
    [InlineData("epoch")] [InlineData("leaving")]
    public async Task Slayer_Headsman_native_held_add_rebuilds_or_cancels_before_allocating_ids(string change)
    {
        using var game = await HeadsmanCharacter();
        HeadsmanKill(game.State, true);
        await game.StatLifecycleExchange(async (stream, ciphers) =>
        {
            var gate = Program.SendGates.GetValue(ciphers, _ => new(1, 1));
            await gate.WaitAsync();
            var sending = Program.SendSlayerBuffs("held-headsman-test", stream, ciphers, game.State);
            Assert.False(sending.IsCompleted);
            switch (change)
            {
                case "refund":
                    Assert.Null((await game.Change([3184], false, refundPoints: true)).Error);
                    Assert.Null((await game.Change([3184], true)).Error); break;
                case "death": game.State.PlayerLife = 0; break;
                case "closed": game.State.Closed = true; break;
                case "retired": game.State.Retire(); break;
                case "epoch": game.State.ProjectileEpoch++; break;
                case "leaving": Assert.True(game.State.TryBeginAreaChange(new(0, 0, false), out _)); break;
            }
            gate.Release(); await sending;
        });
        Assert.Empty(game.LastBytes);
        Assert.Equal((ushort)100, game.State.NextBuff);
    }

    [Fact]
    public async Task Slayer_Headsman_native_pause_keeps_authoritative_remaining_and_does_not_refresh_the_instance()
    {
        using var game = await HeadsmanCharacter();
        HeadsmanKill(game.State);
        Assert.Equal(20, Assert.Single(await PublishHeadsman(game)).Seconds);
        await Task.Delay(30);
        Assert.True(game.State.Paused);
        Assert.Empty(await PublishHeadsman(game));
        var stats = Program.CharacterPlayerStats(game.State);
        var state = game.State.SlayerKills.ReadBuffs(game.State.Clock.Elapsed.TotalSeconds,
            n => Program.Stats.Key(n) is uint key ? stats.GetValueOrDefault(key) : 0);
        Assert.Equal(20, state.Movement!.Value.Remaining);
        Assert.Equal((ushort)101, game.State.NextBuff);
    }

    [Fact]
    public async Task Slayer_Headsman_native_failed_transfer_rebases_and_old_lease_cannot_publish_after_resume()
    {
        using var game = await HeadsmanCharacter();
        HeadsmanKill(game.State, true);
        Assert.Equal(2, (await PublishHeadsman(game)).Count);
        game.State.Clock.Start();
        var carried = game.State.SlayerKills.Carry(game.State.Clock.Elapsed.TotalSeconds);
        Assert.True(game.State.TryBeginAreaChange(new(0, 0, false), out _));
        Assert.Empty(await PublishHeadsman(game));
        await Task.Delay(30);
        game.State.ResumeAfterFailedTransfer(); game.State.Clock.Stop();
        Assert.Null(Program.SlayerBuffPacketsAtGate(game.State, game.State.ProjectileEpoch - 1));
        var resumed = await PublishHeadsman(game);
        Assert.Equal(new[] { false, true, false, true }, resumed.Select(x => x.Add));
        Assert.InRange(resumed[1].Seconds, (float)carried.Movement - .03f, (float)carried.Movement);
        Assert.InRange(resumed[3].Seconds, (float)carried.Attack - .03f, (float)carried.Attack);
    }

    [Fact]
    public async Task Slayer_Headsman_native_successful_transfer_restores_remaining_into_fresh_world_instances()
    {
        using var game = await HeadsmanCharacter();
        HeadsmanKill(game.State, true);
        Assert.Equal(2, (await PublishHeadsman(game)).Count);
        Assert.True(game.State.TryBeginAreaChange(new(0, 0, false), out _));
        uint ticket = unchecked((uint)Random.Shared.Next(100000, int.MaxValue));
        var remaining = game.State.SlayerKills.Carry(game.State.SlayerFrozenAt!.Value);
        ProgramValue<object>("CarrySlayer", game.State, ticket);
        game.State.Retire();
        Assert.Empty(await PublishHeadsman(game));
        var next = new ClientState(game.State.Layout, ticket, 1)
        { Character = game.Character, ClassRow = 4, MainAscendancy = game.State.MainAscendancy, PlayerLife = 100,
            PlayerSent = true, NextBuff = 200 };
        next.Clock.Stop(); next.Pools = Program.PoolsFor(next, 60);
        await Task.Delay(30); // Destination load time does not consume ticket-carried effect lifetime.
        Program.RestoreSlayer(next);
        var restored = await PublishHeadsman(game, next);
        Assert.Equal(2, restored.Count);
        Assert.All(restored, p => Assert.True(p.Add)); // No removals for buff IDs belonging to the old native world.
        Assert.Equal((ushort)200, restored[0].Id); Assert.Equal((ushort)201, restored[1].Id);
        Assert.Equal((float)remaining.Movement / (float)remaining.Rate, restored[0].Seconds);
        Assert.Equal((float)remaining.Attack / (float)remaining.Rate, restored[1].Seconds);
        Program.RestoreSlayer(next);
        Assert.Empty(await PublishHeadsman(game, next));
    }
}
