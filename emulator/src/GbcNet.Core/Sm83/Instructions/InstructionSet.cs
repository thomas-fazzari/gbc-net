// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Sm83.Instructions;

/// <summary>
/// SM83 primary opcode table.
/// </summary>
internal static class InstructionSet
{
    private static readonly Instruction?[] _instructions = CreateInstructions();

    /// <summary>
    /// Gets the opcode entry, or null when that opcode is not implemented yet.
    /// </summary>
    public static Instruction? Find(byte opcode) => _instructions[opcode];

    private static Instruction?[] CreateInstructions()
    {
        var instructions = new Instruction?[256];
        var builder = new OpcodeTableBuilder(instructions);

        ControlInstructions.Map(builder);
        Load8Instructions.Map(builder);
        LoadRegisterPairInstructions.Map(builder);
        StackInstructions.Map(builder);
        CallReturnInstructions.Map(builder);
        Arithmetic8Instructions.Map(builder);
        Arithmetic16Instructions.Map(builder);
        RotateInstructions.Map(builder);
        FlagInstructions.Map(builder);
        JumpInstructions.Map(builder);

        return instructions;
    }
}
