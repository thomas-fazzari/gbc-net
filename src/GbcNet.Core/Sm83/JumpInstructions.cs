namespace GbcNet.Core.Sm83;

/// <summary>
/// SM83 jump instructions.
/// </summary>
internal static class JumpInstructions
{
    private const byte JumpRelativeImmediate8Opcode = 0x18;

    private const byte Immediate8ByteLength = 2;

    private const int JumpRelativeImmediate8MachineCycles = 3;

    /// <summary>
    /// Maps implemented jump instructions into the opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        builder.Map(
            JumpRelativeImmediate8Opcode,
            Immediate8ByteLength,
            JumpRelativeImmediate8MachineCycles,
            static (cpu, offset, _) => JumpRelativeImmediate8(cpu, offset)
        );
    }

    /// <summary>
    /// Executes JR imm8 by adding the signed offset to PC after the operand byte.
    /// </summary>
    private static void JumpRelativeImmediate8(Cpu cpu, byte offset)
    {
        cpu.Registers.PC = unchecked((ushort)(cpu.Registers.PC + (sbyte)offset));
    }
}
