namespace GbcNet.Core.Sm83;

/// <summary>
/// Builds the SM83 opcode table while keeping instruction construction in one place.
/// </summary>
internal readonly struct OpcodeTableBuilder(Instruction?[] instructions)
{
    /// <summary>
    /// Stores one implemented primary opcode in the instruction table.
    /// </summary>
    public void Map(
        byte opcode,
        byte byteLength,
        int machineCycles,
        Action<Cpu, byte, byte> execute
    )
    {
        instructions[opcode] = new Instruction(byteLength, machineCycles, execute);
    }
}
