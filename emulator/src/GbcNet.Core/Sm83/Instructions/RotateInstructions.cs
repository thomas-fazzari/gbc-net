// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Sm83.Instructions;

/// <summary>
/// SM83 rotate instructions.
/// </summary>
internal static class RotateInstructions
{
    private const byte RotateLeftCircularAccumulatorOpcode = 0x07;
    private const byte RotateRightCircularAccumulatorOpcode = 0x0F;
    private const byte RotateLeftAccumulatorOpcode = 0x17;
    private const byte RotateRightAccumulatorOpcode = 0x1F;

    /// <summary>
    /// Bit moved into C by left accumulator rotations.
    /// </summary>
    private const byte Bit7Mask = 0x80;

    /// <summary>
    /// Bit moved into C by right accumulator rotations.
    /// </summary>
    private const byte Bit0Mask = 0x01;

    /// <summary>
    /// Maps implemented rotate instructions into the opcode table.
    /// </summary>
    public static void Map(OpcodeTableBuilder builder)
    {
        builder.MapNoOperand(RotateLeftCircularAccumulatorOpcode, RotateLeftCircularAccumulator);
        builder.MapNoOperand(RotateRightCircularAccumulatorOpcode, RotateRightCircularAccumulator);
        builder.MapNoOperand(RotateLeftAccumulatorOpcode, RotateLeftAccumulator);
        builder.MapNoOperand(RotateRightAccumulatorOpcode, RotateRightAccumulator);
    }

    /// <summary>
    /// Executes RLCA: bit 7 moves into both bit 0 and C.
    /// </summary>
    private static void RotateLeftCircularAccumulator(Cpu cpu)
    {
        var value = cpu.Registers.A;
        var carry = (value & Bit7Mask) != 0;

        cpu.Registers.A = unchecked((byte)((value << 1) | (carry ? Bit0Mask : 0)));
        SetAccumulatorRotateFlags(cpu, carry);
    }

    /// <summary>
    /// Executes RRCA: bit 0 moves into both bit 7 and C.
    /// </summary>
    private static void RotateRightCircularAccumulator(Cpu cpu)
    {
        var value = cpu.Registers.A;
        var carry = (value & Bit0Mask) != 0;

        cpu.Registers.A = (byte)((value >> 1) | (carry ? Bit7Mask : 0));
        SetAccumulatorRotateFlags(cpu, carry);
    }

    /// <summary>
    /// Executes RLA: bit 7 moves into C, and old C moves into bit 0.
    /// </summary>
    private static void RotateLeftAccumulator(Cpu cpu)
    {
        var value = cpu.Registers.A;
        var incomingCarry = cpu.Registers.IsFlagSet(CpuFlag.Carry);
        var carry = (value & Bit7Mask) != 0;

        cpu.Registers.A = unchecked((byte)((value << 1) | (incomingCarry ? Bit0Mask : 0)));
        SetAccumulatorRotateFlags(cpu, carry);
    }

    /// <summary>
    /// Executes RRA: bit 0 moves into C, and old C moves into bit 7.
    /// </summary>
    private static void RotateRightAccumulator(Cpu cpu)
    {
        var value = cpu.Registers.A;
        var incomingCarry = cpu.Registers.IsFlagSet(CpuFlag.Carry);
        var carry = (value & Bit0Mask) != 0;

        cpu.Registers.A = (byte)((value >> 1) | (incomingCarry ? Bit7Mask : 0));
        SetAccumulatorRotateFlags(cpu, carry);
    }

    /// <summary>
    /// Accumulator rotate opcodes always reset Z, N, and H; C receives the rotated-out bit.
    /// </summary>
    private static void SetAccumulatorRotateFlags(Cpu cpu, bool carry)
    {
        cpu.Registers.F = carry ? (byte)CpuFlag.Carry : (byte)0;
    }
}
