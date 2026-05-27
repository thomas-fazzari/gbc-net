namespace GbcNet.Core.Sm83;

/// <summary>
/// SM83 CB-prefixed bit test, reset, and set instructions.
/// </summary>
internal static class BitSetResetInstructions
{
    private const byte BitStartOpcode = 0x40;
    private const byte BitEndOpcode = 0x7F;
    private const byte ResetStartOpcode = 0x80;
    private const byte ResetEndOpcode = 0xBF;
    private const byte SetStartOpcode = 0xC0;
    private const byte SetEndOpcode = 0xFF;

    private const byte BitIndexMask = 0x07;
    private const int BitIndexShift = 3;

    private const byte PrefixedInstructionByteLength = 2;

    private const int BitAddressHlMachineCycles = 3;
    private const int BitRegisterMachineCycles = 2;
    private const int MutationAddressHlMachineCycles = 4;
    private const int MutationRegisterMachineCycles = 2;

    /// <summary>
    /// Maps all BIT, RES, and SET b3, r8 instructions into the CB-prefixed opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        MapBitTests(builder);
        MapBitMutations(builder, ResetStartOpcode, ResetEndOpcode, setBit: false);
        MapBitMutations(builder, SetStartOpcode, SetEndOpcode, setBit: true);
    }

    /// <summary>
    /// Maps all BIT b3, r8 instructions, which test one bit and update flags.
    /// </summary>
    private static void MapBitTests(OpcodeTableBuilder builder)
    {
        for (int opcode = BitStartOpcode; opcode <= BitEndOpcode; opcode++)
        {
            byte opcodeByte = (byte)opcode;
            byte mask = DecodeBitMask(opcodeByte);
            Register8Operand operand = Register8Operands.DecodeSource(opcodeByte);

            builder.Map(
                opcodeByte,
                PrefixedInstructionByteLength,
                (cpu, _, _) => ExecuteBitTest(cpu, mask, operand)
            );
        }
    }

    /// <summary>
    /// Maps all RES or SET b3, r8 instructions, which change one bit and leave flags unchanged.
    /// </summary>
    private static void MapBitMutations(
        OpcodeTableBuilder builder,
        byte startOpcode,
        byte endOpcode,
        bool setBit
    )
    {
        for (int opcode = startOpcode; opcode <= endOpcode; opcode++)
        {
            byte opcodeByte = (byte)opcode;
            byte mask = DecodeBitMask(opcodeByte);
            Register8Operand operand = Register8Operands.DecodeSource(opcodeByte);

            builder.Map(
                opcodeByte,
                PrefixedInstructionByteLength,
                (cpu, _, _) => ExecuteBitMutation(cpu, mask, operand, setBit)
            );
        }
    }

    /// <summary>
    /// Executes BIT b3, r8 by testing one bit while preserving C and leaving the operand unchanged.
    /// </summary>
    private static int ExecuteBitTest(Cpu cpu, byte mask, Register8Operand operand)
    {
        byte value = Register8Operands.Read(cpu, operand);

        cpu.Registers.SetFlag(CpuFlag.Zero, (value & mask) == 0);
        cpu.Registers.SetFlag(CpuFlag.Subtract, isSet: false);
        cpu.Registers.SetFlag(CpuFlag.HalfCarry, isSet: true);

        return Register8Operands.UsesMemory(operand)
            ? BitAddressHlMachineCycles
            : BitRegisterMachineCycles;
    }

    /// <summary>
    /// Executes RES or SET b3, r8 by mutating one bit and preserving all flags.
    /// </summary>
    private static int ExecuteBitMutation(Cpu cpu, byte mask, Register8Operand operand, bool setBit)
    {
        byte value = Register8Operands.Read(cpu, operand);
        byte result = setBit ? (byte)(value | mask) : (byte)(value & ~mask);

        Register8Operands.Write(cpu, operand, result);

        return Register8Operands.UsesMemory(operand)
            ? MutationAddressHlMachineCycles
            : MutationRegisterMachineCycles;
    }

    /// <summary>
    /// Decodes the selected bit mask from CB opcode bits 5-3.
    /// </summary>
    private static byte DecodeBitMask(byte opcode) =>
        (byte)(1 << ((opcode >> BitIndexShift) & BitIndexMask));
}
