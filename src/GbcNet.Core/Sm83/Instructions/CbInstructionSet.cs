namespace GbcNet.Core.Sm83;

/// <summary>
/// SM83 CB-prefixed opcode table.
/// </summary>
internal static class CbInstructionSet
{
    private const int OpcodeCount = 256;

    private static readonly Instruction?[] _instructions = CreateInstructions();

    /// <summary>
    /// Gets the prefixed opcode entry, or null when that opcode is not implemented yet.
    /// </summary>
    public static Instruction? Find(byte opcode) => _instructions[opcode];

    private static Instruction?[] CreateInstructions()
    {
        var instructions = new Instruction?[OpcodeCount];
        var builder = new OpcodeTableBuilder(instructions);

        CbRotateShiftInstructions.Map(builder);
        BitInstructions.Map(builder);

        return instructions;
    }
}
