// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Sm83.Instructions;

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

    private const int ConditionOpcodeStep = 0x08;

    /// <summary>
    /// Maps implemented jump instructions into the opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        builder.MapImmediate16(JumpImmediate16Opcode, JumpImmediate16);
        builder.MapImmediate8(
            JumpRelativeImmediate8Opcode,
            static (cpu, offset) => JumpRelativeImmediate8(cpu, offset)
        );
        builder.MapImmediate8(
            JumpRelativeNotZeroImmediate8Opcode,
            static (cpu, offset) => JumpRelativeImmediate8If(cpu, offset, ConditionCode.NotZero)
        );
        builder.MapImmediate8(
            JumpRelativeZeroImmediate8Opcode,
            static (cpu, offset) => JumpRelativeImmediate8If(cpu, offset, ConditionCode.Zero)
        );
        builder.MapImmediate8(
            JumpRelativeNotCarryImmediate8Opcode,
            static (cpu, offset) => JumpRelativeImmediate8If(cpu, offset, ConditionCode.NotCarry)
        );
        builder.MapImmediate8(
            JumpRelativeCarryImmediate8Opcode,
            static (cpu, offset) => JumpRelativeImmediate8If(cpu, offset, ConditionCode.Carry)
        );
        builder.MapNoOperand(JumpHlOpcode, JumpHl);
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
            var opcodeByte = (byte)opcode;
            var conditionCode = InstructionOperands.DecodeConditionCode(opcodeByte);
            builder.MapImmediate16(
                opcodeByte,
                (cpu, lowByte, highByte) => JumpImmediate16If(cpu, lowByte, highByte, conditionCode)
            );
        }
    }

    /// <summary>
    /// Executes JP imm16 by setting PC to the immediate address.
    /// </summary>
    private static void JumpImmediate16(Cpu cpu, byte lowByte, byte highByte)
    {
        cpu.Registers.PC = InstructionOperands.ReadImmediate16(lowByte, highByte);
        cpu.IdleCycle();
    }

    /// <summary>
    /// Executes JP cond, imm16 after the target address has already been fetched.
    /// </summary>
    private static void JumpImmediate16If(
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

        cpu.Registers.PC = InstructionOperands.ReadImmediate16(lowByte, highByte);
        cpu.IdleCycle();
    }

    /// <summary>
    /// Executes JP HL by setting PC to the address stored in HL.
    /// </summary>
    private static void JumpHl(Cpu cpu)
    {
        cpu.Registers.PC = cpu.Registers.HL;
    }

    /// <summary>
    /// Executes JR imm8 by adding the signed offset to PC after the operand byte.
    /// </summary>
    private static void JumpRelativeImmediate8(Cpu cpu, byte offset)
    {
        ApplyRelativeOffset(cpu, offset);
        cpu.IdleCycle();
    }

    /// <summary>
    /// Executes JR cond, imm8 after the offset byte has already been fetched.
    /// </summary>
    private static void JumpRelativeImmediate8If(Cpu cpu, byte offset, ConditionCode conditionCode)
    {
        if (!cpu.Registers.IsConditionMet(conditionCode))
        {
            return;
        }

        ApplyRelativeOffset(cpu, offset);
        cpu.IdleCycle();
    }

    /// <summary>
    /// Adds a signed 8-bit relative offset to the already advanced PC.
    /// </summary>
    private static void ApplyRelativeOffset(Cpu cpu, byte offset)
    {
        cpu.Registers.PC = unchecked((ushort)(cpu.Registers.PC + (sbyte)offset));
    }
}
