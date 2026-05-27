namespace GbcNet.Core.Sm83;

/// <summary>
/// Describes one executable SM83 opcode entry.
/// </summary>
/// <param name="byteLength">
/// Total instruction length in bytes, including the opcode byte.
/// </param>
/// <param name="execute">
/// Instruction body. Operand bytes are passed in program order after the opcode; the return
/// value is the elapsed machine cycles.
/// </param>
internal sealed class Instruction(byte byteLength, InstructionExecutor execute)
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
/// Executes one decoded SM83 instruction and returns elapsed machine cycles.
/// </summary>
internal delegate int InstructionExecutor(Cpu cpu, byte firstOperand, byte secondOperand);
