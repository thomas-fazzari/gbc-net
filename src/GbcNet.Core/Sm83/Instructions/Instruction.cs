// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Sm83.Instructions;

/// <summary>
/// Describes one executable SM83 opcode entry.
/// </summary>
internal readonly struct Instruction(byte byteLength, Action<Cpu, byte, byte> execute)
{
    /// <summary>
    /// Total instruction length in bytes, including the opcode byte.
    /// </summary>
    public byte ByteLength { get; } = byteLength;

    /// <summary>
    /// Instruction body. Operand bytes are passed in program order after the opcode.
    /// </summary>
    public Action<Cpu, byte, byte> Execute { get; } = execute;
}
