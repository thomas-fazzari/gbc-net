// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Sm83;

/// <summary>
/// Decodes and accesses SM83 r8 operands, including the [HL] memory operand.
/// </summary>
internal static class Register8Operands
{
    /// <summary>
    /// Mask for one r8 operand after shifting it to bits 2-0.
    /// </summary>
    private const byte OperandMask = 0x07;

    /// <summary>
    /// Right shift for the destination r8 operand encoded in opcode bits 5-3.
    /// </summary>
    private const int DestinationOperandShift = 3;

    /// <summary>
    /// Decodes the destination r8 operand from opcode bits 5-3.
    /// </summary>
    public static Register8Operand DecodeDestination(byte opcode) =>
        (Register8Operand)((opcode >> DestinationOperandShift) & OperandMask);

    /// <summary>
    /// Decodes the source r8 operand from opcode bits 2-0.
    /// </summary>
    public static Register8Operand DecodeSource(byte opcode) =>
        (Register8Operand)(opcode & OperandMask);

    /// <summary>
    /// Returns whether the operand reads or writes the byte at HL.
    /// </summary>
    public static bool UsesMemory(Register8Operand operand) =>
        operand is Register8Operand.AddressHl;

    /// <summary>
    /// Reads either a CPU register or the byte at HL.
    /// </summary>
    public static byte Read(Cpu cpu, Register8Operand operand) =>
        UsesMemory(operand)
            ? cpu.ReadBus(cpu.Registers.HL)
            : cpu.Registers.GetRegister((Register8)operand);

    /// <summary>
    /// Writes either a CPU register or the byte at HL.
    /// </summary>
    public static void Write(Cpu cpu, Register8Operand operand, byte value)
    {
        if (UsesMemory(operand))
        {
            cpu.WriteBus(cpu.Registers.HL, value);
            return;
        }

        cpu.Registers.SetRegister((Register8)operand, value);
    }
}
