using GbcNet.Core.Sm83;

namespace GbcNet.Core.Sm83;

/// <summary>
/// SM83 primary opcode table.
/// </summary>
internal static class InstructionSet
{
    private const int OpcodeCount = 256;

    private static readonly Instruction?[] _instructions = CreateInstructions();

    /// <summary>
    /// Gets the opcode entry, or null when that opcode is not implemented yet.
    /// </summary>
    public static Instruction? Find(byte opcode) => _instructions[opcode];

    private static Instruction?[] CreateInstructions()
    {
        var instructions = new Instruction?[OpcodeCount];
        var builder = new OpcodeTableBuilder(instructions);

        ControlInstructions.Map(builder);
        Load8Instructions.Map(builder);
        LoadRegisterPairInstructions.Map(builder);
        Arithmetic8Instructions.Map(builder);
        Arithmetic16Instructions.Map(builder);
        RotateInstructions.Map(builder);
        FlagInstructions.Map(builder);

        return instructions;
    }
}
