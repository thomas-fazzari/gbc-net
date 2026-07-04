// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Sm83.Instructions;

/// <summary>
/// Describes one executable SM83 opcode entry.
/// </summary>
internal readonly struct Instruction(byte byteLength, InstructionExecutor execute)
{
    /// <summary>
    /// Total instruction length in bytes, including the opcode byte.
    /// </summary>
    public byte ByteLength { get; } = byteLength;

    /// <summary>
    /// Instruction body. Operand bytes are passed in program order after the opcode.
    /// </summary>
    public InstructionExecutor Execute { get; } = execute;
}

/// <summary>
/// Executes one decoded SM83 instruction by consuming machine cycles through CPU primitives.
/// </summary>
internal delegate void InstructionExecutor(Cpu cpu, byte firstOperand, byte secondOperand);
