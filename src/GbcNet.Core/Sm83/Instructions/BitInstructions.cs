namespace GbcNet.Core.Sm83;

/// <summary>
/// SM83 CB-prefixed bit test instructions.
/// </summary>
internal static class BitInstructions
{
    private const byte BitStartOpcode = 0x40;
    private const byte BitEndOpcode = 0x7F;

    private const byte BitIndexMask = 0x07;
    private const int BitIndexShift = 3;

    private const byte PrefixedInstructionByteLength = 2;

    private const int BitAddressHlMachineCycles = 3;
    private const int BitRegisterMachineCycles = 2;

    /// <summary>
    /// Maps all BIT b3, r8 instructions into the CB-prefixed opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        for (int opcode = BitStartOpcode; opcode <= BitEndOpcode; opcode++)
        {
            byte opcodeByte = (byte)opcode;
            byte bitIndex = DecodeBitIndex(opcodeByte);
            Register8Operand operand = Register8Operands.DecodeSource(opcodeByte);

            builder.Map(
                opcodeByte,
                PrefixedInstructionByteLength,
                (cpu, _, _) => ExecuteBit(cpu, bitIndex, operand)
            );
        }
    }

    /// <summary>
    /// Executes BIT b3, r8 by testing one bit while preserving C and leaving the operand unchanged.
    /// </summary>
    private static int ExecuteBit(Cpu cpu, byte bitIndex, Register8Operand operand)
    {
        byte value = Register8Operands.Read(cpu, operand);
        byte mask = (byte)(1 << bitIndex);

        cpu.Registers.SetFlag(CpuFlag.Zero, (value & mask) == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, isSet: true);

        return Register8Operands.UsesMemory(operand)
            ? BitAddressHlMachineCycles
            : BitRegisterMachineCycles;
    }

    /// <summary>
    /// Decodes the tested bit index from CB opcode bits 5-3.
    /// </summary>
    private static byte DecodeBitIndex(byte opcode) =>
        (byte)((opcode >> BitIndexShift) & BitIndexMask);
}
