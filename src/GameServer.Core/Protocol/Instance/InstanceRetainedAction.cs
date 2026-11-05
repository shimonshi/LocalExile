using System.Buffers.Binary;
using GameServer.Core.Protocol.Login;

namespace GameServer.Core.Protocol.Instance;

/// <summary>
/// Actor 0x2c6: match an existing action by full type/skill/original-use sequence/derived identity.
/// The outer fields are big-endian; the ordinary Brand state reader consumes its inner values little-endian.
/// This message cannot create an action. Native attachment acceptance is separate from this wire contract.
/// </summary>
public static class InstanceRetainedAction
{
    public readonly record struct Identity(ushort ActionType, ushort Skill, ushort Sequence, byte Derived);

    public static byte[] Attach(uint caster, Identity action, uint target, ushort buffInstance)
    {
        byte[] state = new byte[7];
        state[0] = 2;
        BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(1), target);
        BinaryPrimitives.WriteUInt16LittleEndian(state.AsSpan(5), buffInstance);
        return Encode(caster, action, state);
    }

    // Remaining attached budget; this is neither a location update nor a detached-budget reset.
    public static byte[] Detach(uint caster, Identity action, float attachedSeconds)
    {
        if (!float.IsFinite(attachedSeconds) || attachedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(attachedSeconds));
        byte[] state = new byte[5];
        state[0] = 1;
        BinaryPrimitives.WriteSingleLittleEndian(state.AsSpan(1), attachedSeconds);
        return Encode(caster, action, state);
    }

    public static byte[] Retire(uint caster, Identity action) => Encode(caster, action, [4]);

    static byte[] Encode(uint caster, Identity action, byte[] state) => new LoginWriter()
        .U16(0x2c6).U32(caster).U32(0).U16(0)
        .U16(action.ActionType).U16(action.Skill).U16(action.Sequence).U8(action.Derived)
        .U16(checked((ushort)state.Length)).Bytes(state).ToArray();
}
