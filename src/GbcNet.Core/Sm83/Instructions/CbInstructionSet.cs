namespace GbcNet.Core.Sm83.Instructions;

/// <summary>
/// SM83 CB-prefixed opcode table.
/// </summary>
internal static class CbInstructionSet
{
    private static readonly Instruction?[] _instructions = CreateInstructions();

    /// <summary>
    /// Gets the prefixed opcode entry, or null when that opcode is not implemented yet.
    /// </summary>
    public static Instruction? Find(byte opcode) => _instructions[opcode];

    private static Instruction?[] CreateInstructions()
    {
        var instructions = new Instruction?[256];
        var builder = new OpcodeTableBuilder(instructions);

        CbRotateShiftInstructions.Map(builder);
        BitSetResetInstructions.Map(builder);

        return instructions;
    }
}
