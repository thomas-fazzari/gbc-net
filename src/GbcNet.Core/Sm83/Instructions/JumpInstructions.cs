namespace GbcNet.Core.Sm83;

/// <summary>
/// SM83 jump instructions.
/// </summary>
internal static class JumpInstructions
{
    private const byte JumpRelativeImmediate8Opcode = 0x18;
    private const byte JumpRelativeNotZeroImmediate8Opcode = 0x20;
    private const byte JumpRelativeZeroImmediate8Opcode = 0x28;
    private const byte JumpRelativeNotCarryImmediate8Opcode = 0x30;
    private const byte JumpRelativeCarryImmediate8Opcode = 0x38;

    private const byte Immediate8ByteLength = 2;

    private const int JumpRelativeImmediate8MachineCycles = 3;

    /// <summary>
    /// M-cycles consumed when a conditional JR changes PC.
    /// </summary>
    private const int JumpRelativeConditionalTakenMachineCycles = 3;

    /// <summary>
    /// M-cycles consumed when a conditional JR only reads its offset.
    /// </summary>
    private const int JumpRelativeConditionalNotTakenMachineCycles = 2;

    /// <summary>
    /// Maps implemented jump instructions into the opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        builder.Map(
            JumpRelativeImmediate8Opcode,
            Immediate8ByteLength,
            static (cpu, offset, _) => JumpRelativeImmediate8(cpu, offset)
        );
        builder.Map(
            JumpRelativeNotZeroImmediate8Opcode,
            Immediate8ByteLength,
            static (cpu, offset, _) => JumpRelativeImmediate8If(cpu, offset, ConditionCode.NotZero)
        );
        builder.Map(
            JumpRelativeZeroImmediate8Opcode,
            Immediate8ByteLength,
            static (cpu, offset, _) => JumpRelativeImmediate8If(cpu, offset, ConditionCode.Zero)
        );
        builder.Map(
            JumpRelativeNotCarryImmediate8Opcode,
            Immediate8ByteLength,
            static (cpu, offset, _) => JumpRelativeImmediate8If(cpu, offset, ConditionCode.NotCarry)
        );
        builder.Map(
            JumpRelativeCarryImmediate8Opcode,
            Immediate8ByteLength,
            static (cpu, offset, _) => JumpRelativeImmediate8If(cpu, offset, ConditionCode.Carry)
        );
    }

    /// <summary>
    /// Executes JR imm8 by adding the signed offset to PC after the operand byte.
    /// </summary>
    private static int JumpRelativeImmediate8(Cpu cpu, byte offset)
    {
        ApplyRelativeOffset(cpu, offset);
        return JumpRelativeImmediate8MachineCycles;
    }

    /// <summary>
    /// Executes JR cond, imm8 after the offset byte has already been fetched.
    /// </summary>
    private static int JumpRelativeImmediate8If(Cpu cpu, byte offset, ConditionCode conditionCode)
    {
        if (!cpu.Registers.IsConditionMet(conditionCode))
        {
            return JumpRelativeConditionalNotTakenMachineCycles;
        }

        ApplyRelativeOffset(cpu, offset);
        return JumpRelativeConditionalTakenMachineCycles;
    }

    /// <summary>
    /// Adds a signed 8-bit relative offset to the already advanced PC.
    /// </summary>
    private static void ApplyRelativeOffset(Cpu cpu, byte offset)
    {
        cpu.Registers.PC = unchecked((ushort)(cpu.Registers.PC + (sbyte)offset));
    }
}
