namespace GbcNet.Core.Sm83;

/// <summary>
/// Helpers for decoding already-fetched SM83 instruction operands.
/// </summary>
internal static class InstructionOperands
{
    private const byte ConditionCodeMask = 0x03;
    private const int ConditionCodeShift = 3;

    private const ushort LowByteMask = 0x00FF;
    private const ushort LowNibbleMask = 0x000F;

    /// <summary>
    /// Decodes a condition code stored in opcode bits 4-3.
    /// </summary>
    public static ConditionCode DecodeConditionCode(byte opcode) =>
        (ConditionCode)((opcode >> ConditionCodeShift) & ConditionCodeMask);

    /// <summary>
    /// Combines an imm16 operand stored by the SM83 as low byte, then high byte.
    /// </summary>
    public static ushort ReadImmediate16(byte lowByte, byte highByte) =>
        (ushort)((highByte << 8) | lowByte);

    /// <summary>
    /// Adds a signed imm8 operand to a 16-bit value and returns the SM83 flags used by SP+imm8
    /// instructions.
    /// </summary>
    public static (ushort Value, byte Flags) AddSignedImmediate8WithFlags(
        ushort value,
        byte immediate
    )
    {
        ushort result = unchecked((ushort)(value + (sbyte)immediate));
        byte flags = 0;

        if ((value & LowNibbleMask) + (immediate & LowNibbleMask) > LowNibbleMask)
        {
            flags |= (byte)CpuFlag.HalfCarry;
        }

        if ((value & LowByteMask) + immediate > LowByteMask)
        {
            flags |= (byte)CpuFlag.Carry;
        }

        return (result, flags);
    }
}
