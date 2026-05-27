namespace GbcNet.Core.Sm83;

/// <summary>
/// SM83 call, return, and restart instructions.
/// </summary>
internal static class CallReturnInstructions
{
    private const byte ReturnConditionStartOpcode = 0xC0;
    private const byte ReturnConditionEndOpcode = 0xD8;
    private const byte ReturnOpcode = 0xC9;
    private const byte CallConditionStartOpcode = 0xC4;
    private const byte CallConditionEndOpcode = 0xDC;
    private const byte CallImmediate16Opcode = 0xCD;
    private const byte RestartStartOpcode = 0xC7;
    private const byte RestartEndOpcode = 0xFF;

    private const byte NoOperandByteLength = 1;
    private const byte Immediate16ByteLength = 3;

    private const int CallImmediate16MachineCycles = 6;
    private const int CallConditionTakenMachineCycles = 6;
    private const int CallConditionNotTakenMachineCycles = 3;
    private const int ReturnMachineCycles = 4;
    private const int ReturnConditionTakenMachineCycles = 5;
    private const int ReturnConditionNotTakenMachineCycles = 2;
    private const int RestartMachineCycles = 4;

    private const int ConditionOpcodeStep = 0x08;
    private const byte ConditionCodeMask = 0x03;
    private const int ConditionCodeShift = 3;
    private const int RestartOpcodeStep = 0x08;
    private const byte RestartTargetMask = 0x38;

    /// <summary>
    /// Maps implemented call, return, and restart instructions into the opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        MapReturnCondition(builder);
        builder.Map(ReturnOpcode, NoOperandByteLength, static (cpu, _, _) => Return(cpu));

        MapCallCondition(builder);
        builder.Map(CallImmediate16Opcode, Immediate16ByteLength, CallImmediate16);

        MapRestart(builder);
    }

    /// <summary>
    /// Maps RET cond instructions whose condition code is encoded in bits 4-3.
    /// </summary>
    private static void MapReturnCondition(OpcodeTableBuilder builder)
    {
        for (
            int opcode = ReturnConditionStartOpcode;
            opcode <= ReturnConditionEndOpcode;
            opcode += ConditionOpcodeStep
        )
        {
            byte opcodeByte = (byte)opcode;
            ConditionCode conditionCode = DecodeConditionCode(opcodeByte);
            builder.Map(
                opcodeByte,
                NoOperandByteLength,
                (cpu, _, _) => ReturnIf(cpu, conditionCode)
            );
        }
    }

    /// <summary>
    /// Maps CALL cond, imm16 instructions whose condition code is encoded in bits 4-3.
    /// </summary>
    private static void MapCallCondition(OpcodeTableBuilder builder)
    {
        for (
            int opcode = CallConditionStartOpcode;
            opcode <= CallConditionEndOpcode;
            opcode += ConditionOpcodeStep
        )
        {
            byte opcodeByte = (byte)opcode;
            ConditionCode conditionCode = DecodeConditionCode(opcodeByte);
            builder.Map(
                opcodeByte,
                Immediate16ByteLength,
                (cpu, lowByte, highByte) => CallImmediate16If(cpu, lowByte, highByte, conditionCode)
            );
        }
    }

    /// <summary>
    /// Maps RST tgt3 instructions whose target is encoded in bits 5-3.
    /// </summary>
    private static void MapRestart(OpcodeTableBuilder builder)
    {
        for (
            int opcode = RestartStartOpcode;
            opcode <= RestartEndOpcode;
            opcode += RestartOpcodeStep
        )
        {
            byte opcodeByte = (byte)opcode;
            ushort targetAddress = DecodeRestartTarget(opcodeByte);
            builder.Map(
                opcodeByte,
                NoOperandByteLength,
                (cpu, _, _) => Restart(cpu, targetAddress)
            );
        }
    }

    /// <summary>
    /// Executes CALL imm16 by pushing the next PC and jumping to the immediate address.
    /// </summary>
    private static int CallImmediate16(Cpu cpu, byte lowByte, byte highByte)
    {
        Call(cpu, InstructionOperands.ReadImmediate16(lowByte, highByte));
        return CallImmediate16MachineCycles;
    }

    /// <summary>
    /// Executes CALL cond, imm16 after the target address has already been fetched.
    /// </summary>
    private static int CallImmediate16If(
        Cpu cpu,
        byte lowByte,
        byte highByte,
        ConditionCode conditionCode
    )
    {
        if (!cpu.Registers.IsConditionMet(conditionCode))
        {
            return CallConditionNotTakenMachineCycles;
        }

        Call(cpu, InstructionOperands.ReadImmediate16(lowByte, highByte));
        return CallConditionTakenMachineCycles;
    }

    /// <summary>
    /// Pushes the current PC and jumps to the call target.
    /// </summary>
    private static void Call(Cpu cpu, ushort targetAddress)
    {
        cpu.PushWord(cpu.Registers.PC);
        cpu.Registers.PC = targetAddress;
    }

    /// <summary>
    /// Executes RET by popping PC from the stack.
    /// </summary>
    private static int Return(Cpu cpu)
    {
        cpu.Registers.PC = cpu.PopWord();
        return ReturnMachineCycles;
    }

    /// <summary>
    /// Executes RET cond after the condition has been decoded from the opcode.
    /// </summary>
    private static int ReturnIf(Cpu cpu, ConditionCode conditionCode)
    {
        if (!cpu.Registers.IsConditionMet(conditionCode))
        {
            return ReturnConditionNotTakenMachineCycles;
        }

        cpu.Registers.PC = cpu.PopWord();
        return ReturnConditionTakenMachineCycles;
    }

    /// <summary>
    /// Executes RST tgt3 by pushing the next PC and jumping to the encoded vector.
    /// </summary>
    private static int Restart(Cpu cpu, ushort targetAddress)
    {
        Call(cpu, targetAddress);
        return RestartMachineCycles;
    }

    /// <summary>
    /// Decodes the condition code stored in opcode bits 4-3.
    /// </summary>
    private static ConditionCode DecodeConditionCode(byte opcode) =>
        (ConditionCode)((opcode >> ConditionCodeShift) & ConditionCodeMask);

    /// <summary>
    /// Decodes the RST target address stored in opcode bits 5-3.
    /// </summary>
    private static ushort DecodeRestartTarget(byte opcode) => (ushort)(opcode & RestartTargetMask);
}
