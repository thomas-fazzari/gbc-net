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
    private const byte JumpConditionStartOpcode = 0xC2;
    private const byte JumpConditionEndOpcode = 0xDA;
    private const byte JumpImmediate16Opcode = 0xC3;
    private const byte JumpHlOpcode = 0xE9;

    private const byte NoOperandByteLength = 1;
    private const byte Immediate8ByteLength = 2;
    private const byte Immediate16ByteLength = 3;

    private const int JumpHlMachineCycles = 1;
    private const int JumpImmediate16MachineCycles = 4;
    private const int JumpRelativeImmediate8MachineCycles = 3;

    /// <summary>
    /// M-cycles consumed when a conditional JP changes PC.
    /// </summary>
    private const int JumpConditionTakenMachineCycles = 4;

    /// <summary>
    /// M-cycles consumed when a conditional JP only reads its immediate target.
    /// </summary>
    private const int JumpConditionNotTakenMachineCycles = 3;

    /// <summary>
    /// M-cycles consumed when a conditional JR changes PC.
    /// </summary>
    private const int JumpRelativeConditionalTakenMachineCycles = 3;

    /// <summary>
    /// M-cycles consumed when a conditional JR only reads its offset.
    /// </summary>
    private const int JumpRelativeConditionalNotTakenMachineCycles = 2;

    private const int ConditionOpcodeStep = 0x08;

    /// <summary>
    /// Maps implemented jump instructions into the opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        builder.Map(JumpImmediate16Opcode, Immediate16ByteLength, JumpImmediate16);
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
        builder.Map(JumpHlOpcode, NoOperandByteLength, static (cpu, _, _) => JumpHl(cpu));
        MapJumpCondition(builder);
    }

    /// <summary>
    /// Maps JP cond, imm16 instructions whose condition code is encoded in bits 4-3.
    /// </summary>
    private static void MapJumpCondition(OpcodeTableBuilder builder)
    {
        for (
            int opcode = JumpConditionStartOpcode;
            opcode <= JumpConditionEndOpcode;
            opcode += ConditionOpcodeStep
        )
        {
            byte opcodeByte = (byte)opcode;
            ConditionCode conditionCode = InstructionOperands.DecodeConditionCode(opcodeByte);
            builder.Map(
                opcodeByte,
                Immediate16ByteLength,
                (cpu, lowByte, highByte) => JumpImmediate16If(cpu, lowByte, highByte, conditionCode)
            );
        }
    }

    /// <summary>
    /// Executes JP imm16 by setting PC to the immediate address.
    /// </summary>
    private static int JumpImmediate16(Cpu cpu, byte lowByte, byte highByte)
    {
        cpu.Registers.PC = InstructionOperands.ReadImmediate16(lowByte, highByte);
        return JumpImmediate16MachineCycles;
    }

    /// <summary>
    /// Executes JP cond, imm16 after the target address has already been fetched.
    /// </summary>
    private static int JumpImmediate16If(
        Cpu cpu,
        byte lowByte,
        byte highByte,
        ConditionCode conditionCode
    )
    {
        if (!cpu.Registers.IsConditionMet(conditionCode))
        {
            return JumpConditionNotTakenMachineCycles;
        }

        cpu.Registers.PC = InstructionOperands.ReadImmediate16(lowByte, highByte);
        return JumpConditionTakenMachineCycles;
    }

    /// <summary>
    /// Executes JP HL by setting PC to the address stored in HL.
    /// </summary>
    private static int JumpHl(Cpu cpu)
    {
        cpu.Registers.PC = cpu.Registers.HL;
        return JumpHlMachineCycles;
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
