namespace GbcNet.Core.Sm83.Instructions;

/// <summary>
/// SM83 call, return, and restart instructions.
/// </summary>
internal static class CallReturnInstructions
{
    private const byte ReturnConditionStartOpcode = 0xC0;
    private const byte ReturnConditionEndOpcode = 0xD8;
    private const byte ReturnOpcode = 0xC9;
    private const byte ReturnInterruptOpcode = 0xD9;
    private const byte CallConditionStartOpcode = 0xC4;
    private const byte CallConditionEndOpcode = 0xDC;
    private const byte CallImmediate16Opcode = 0xCD;
    private const byte RestartStartOpcode = 0xC7;
    private const byte RestartEndOpcode = 0xFF;

    private const int ConditionOpcodeStep = 0x08;
    private const int RestartOpcodeStep = 0x08;
    private const byte RestartTargetMask = 0x38;

    /// <summary>
    /// Maps implemented call, return, and restart instructions into the opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        MapReturnCondition(builder);
        builder.MapNoOperand(ReturnOpcode, Return);
        builder.MapNoOperand(ReturnInterruptOpcode, ReturnInterrupt);

        MapCallCondition(builder);
        builder.MapImmediate16(CallImmediate16Opcode, CallImmediate16);

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
            ConditionCode conditionCode = InstructionOperands.DecodeConditionCode(opcodeByte);
            builder.MapNoOperand(opcodeByte, cpu => ReturnIf(cpu, conditionCode));
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
            ConditionCode conditionCode = InstructionOperands.DecodeConditionCode(opcodeByte);
            builder.MapImmediate16(
                opcodeByte,
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
            builder.MapNoOperand(opcodeByte, cpu => Restart(cpu, targetAddress));
        }
    }

    /// <summary>
    /// Executes CALL imm16 by pushing the next PC and jumping to the immediate address.
    /// </summary>
    private static void CallImmediate16(Cpu cpu, byte lowByte, byte highByte)
    {
        Call(cpu, InstructionOperands.ReadImmediate16(lowByte, highByte));
    }

    /// <summary>
    /// Executes CALL cond, imm16 after the target address has already been fetched.
    /// </summary>
    private static void CallImmediate16If(
        Cpu cpu,
        byte lowByte,
        byte highByte,
        ConditionCode conditionCode
    )
    {
        if (!cpu.Registers.IsConditionMet(conditionCode))
        {
            return;
        }

        Call(cpu, InstructionOperands.ReadImmediate16(lowByte, highByte));
    }

    /// <summary>
    /// Pushes the current PC and jumps to the call target.
    /// </summary>
    private static void Call(Cpu cpu, ushort targetAddress)
    {
        cpu.IdleCycle();
        cpu.PushWord(cpu.Registers.PC);
        cpu.Registers.PC = targetAddress;
    }

    /// <summary>
    /// Executes RET by popping PC from the stack.
    /// </summary>
    private static void Return(Cpu cpu)
    {
        cpu.Registers.PC = cpu.PopWord();
        cpu.IdleCycle();
    }

    /// <summary>
    /// Executes RETI by popping PC and immediately re-enabling interrupt servicing.
    /// </summary>
    private static void ReturnInterrupt(Cpu cpu)
    {
        cpu.Registers.PC = cpu.PopWord();
        cpu.EnableInterruptsImmediately();
        cpu.IdleCycle();
    }

    /// <summary>
    /// Executes RET cond after the condition has been decoded from the opcode.
    /// </summary>
    private static void ReturnIf(Cpu cpu, ConditionCode conditionCode)
    {
        cpu.IdleCycle();

        if (!cpu.Registers.IsConditionMet(conditionCode))
        {
            return;
        }

        cpu.Registers.PC = cpu.PopWord();
        cpu.IdleCycle();
    }

    /// <summary>
    /// Executes RST tgt3 by pushing the next PC and jumping to the encoded vector.
    /// </summary>
    private static void Restart(Cpu cpu, ushort targetAddress)
    {
        Call(cpu, targetAddress);
    }

    /// <summary>
    /// Decodes the RST target address stored in opcode bits 5-3.
    /// </summary>
    private static ushort DecodeRestartTarget(byte opcode) => (ushort)(opcode & RestartTargetMask);
}
