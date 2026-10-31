using System.Buffers.Binary;
using System.Text.Json;
using GameServer.Core.Protocol.Instance;

namespace GameServer.Gateway.Tests;

// Decode the retained native 0x141fdf870 contract using the shipped row flags, independently of AddBuff.
static class OrdinaryBuffWire
{
    static readonly JsonDocument Definitions = JsonDocument.Parse(File.ReadAllBytes("data/tables/BuffDefinitions.json"));
    public static int End(byte[] bytes, int offset)
    {
        void Need(int at, long length)
        {
            if (at < 0 || length < 0 || at + length > bytes.Length) throw new InvalidDataException("Truncated native buff");
        }
        Need(offset, 14);
        ushort opcode = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset));
        if (opcode == InstanceEntities.RemoveBuffId) return offset + 14;
        Assert.Equal(InstanceEntities.AddBuffId, opcode);
        Need(offset, 23);
        ushort row = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 14));
        var definition = Definitions.RootElement[row];
        byte flags = bytes[offset + 22];
        int at = offset + 23;
        if ((flags & 1) != 0) at += 8;
        if ((flags & 2) != 0) at += 4;
        if ((flags & 128) != 0) at += 4;
        if ((flags & 4) != 0) at += 2;
        if ((flags & 8) != 0) at += 4;
        if ((flags & 16) != 0) at += 2;
        if ((flags & 32) != 0) at += 1;
        if (definition.GetProperty("__col44").GetBoolean()) at += 2;
        if (definition.GetProperty("IsRecovery").GetBoolean())
            at += 4 * definition.GetProperty("GrantedStats").GetArrayLength();
        Need(at, 4);
        uint count = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at));
        Need(at + 4, 4L * count);
        return checked(at + 4 + (int)(4L * count));
    }

    public static void AssertFollowingSentinel(byte[] packets)
    {
        byte[] sentinel = [0x7f, 0xfe, 0x12, 0x34, 0x56, 0x78];
        byte[] wire = [.. packets, .. sentinel];
        int offset = 0;
        while (offset < packets.Length) offset = End(wire, offset);
        Assert.Equal(packets.Length, offset);
        Assert.Equal(sentinel, wire[offset..]);
    }
}

public sealed class OrdinaryAscendancyBuffFramingTests
{
    [Fact]
    public void Headsman_normal_and_rare_timed_grants_preserve_the_following_packet()
    {
        var state = new SlayerKillState();
        int Get(string name) => name is SlayerRules.KillMovementSpeed or SlayerRules.RareKillAttackSpeed ? 10 : 0;
        state.RecordKill(0, true, Get);
        ushort next = 1;
        byte[] packets = new SlayerBuffPresentation().Build(state.ReadBuffs(0, Get), Program.PlayerId, () => next++);
        Assert.Equal(62, packets.Length); // Two non-recovery timed adds, 31 bytes each.
        OrdinaryBuffWire.AssertFollowingSentinel(packets);
    }

    [Fact]
    public void Self_and_allied_Tailwind_and_independent_Gale_timers_preserve_the_following_packet()
    {
        var state = new DeadeyeGaleState();
        state.Observe(0, true, _ => 0);
        state.AdmitUse(0, true);
        ushort next = 1;
        var self = new DeadeyeGalePresentation().Build(state, 0, Program.PlayerId, () => next++);
        Assert.Equal(27, self[0].Length); // Tailwind is not a recovery buff.
        Assert.Equal(33, self[1].Length); // Gale also has a countdown and u16 stack count.
        byte[] ally = new DeadeyeGaleAura.Received(new(42), 0).Native(Program.PlayerId, 3);
        Assert.Equal(27, ally.Length);
        OrdinaryBuffWire.AssertFollowingSentinel([.. self.SelectMany(p => p), .. ally]);
    }
}
