namespace GbcNet.Core.Sm83.Instructions;

/// <summary>
/// Builds the SM83 opcode table while keeping instruction construction in one place.
/// </summary>
internal readonly struct OpcodeTableBuilder(Instruction?[] instructions)
{
    /// <summary>
    /// Stores one implemented primary opcode in the instruction table.
    /// </summary>
    public void Map(byte opcode, byte byteLength, InstructionExecutor execute)
    {
        instructions[opcode] = new Instruction(byteLength, execute);
    }
}
