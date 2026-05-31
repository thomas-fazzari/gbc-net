namespace GbcNet.Core.Sm83.Instructions;

/// <summary>
/// Builds the SM83 opcode table while keeping instruction construction in one place.
/// </summary>
internal readonly struct OpcodeTableBuilder(Instruction?[] instructions)
{
    private const byte NoOperandByteLength = 1;
    private const byte Immediate8ByteLength = 2;
    private const byte Immediate16ByteLength = 3;
    private const byte PrefixedInstructionByteLength = 2;

    /// <summary>
    /// Stores one implemented primary opcode in the instruction table.
    /// </summary>
    public void Map(byte opcode, byte byteLength, InstructionExecutor execute)
    {
        instructions[opcode] = new Instruction(byteLength, execute);
    }

    /// <summary>
    /// Stores one opcode that has no operand bytes.
    /// </summary>
    public void MapNoOperand(byte opcode, Action<Cpu> execute)
    {
        Map(opcode, NoOperandByteLength, (cpu, _, _) => execute(cpu));
    }

    /// <summary>
    /// Stores one opcode that has one immediate byte.
    /// </summary>
    public void MapImmediate8(byte opcode, Action<Cpu, byte> execute)
    {
        Map(opcode, Immediate8ByteLength, (cpu, value, _) => execute(cpu, value));
    }

    /// <summary>
    /// Stores one opcode that has a low-byte, high-byte immediate word.
    /// </summary>
    public void MapImmediate16(byte opcode, InstructionExecutor execute)
    {
        Map(opcode, Immediate16ByteLength, execute);
    }

    /// <summary>
    /// Stores one CB-prefixed opcode entry.
    /// </summary>
    public void MapPrefixed(byte opcode, InstructionExecutor execute)
    {
        Map(opcode, PrefixedInstructionByteLength, execute);
    }
}
