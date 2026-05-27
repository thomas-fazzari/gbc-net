namespace GbcNet.Core.Sm83;

/// <summary>
/// Helpers for decoding already-fetched SM83 instruction operands.
/// </summary>
internal static class InstructionOperands
{
    private const byte ConditionCodeMask = 0x03;
    private const int ConditionCodeShift = 3;

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
}
