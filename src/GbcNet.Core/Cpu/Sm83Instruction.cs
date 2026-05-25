namespace GbcNet.Core.Cpu;

/// <summary>
/// Describes one executable SM83 opcode entry.
/// </summary>
/// <param name="byteLength">
/// Total instruction length in bytes, including the opcode byte.
/// </param>
/// <param name="machineCycles">
/// Instruction duration in machine cycles.
/// </param>
/// <param name="execute">
/// Instruction body. Operand bytes are passed in program order after the opcode.
/// </param>
internal sealed class Sm83Instruction(
    byte byteLength,
    int machineCycles,
    Action<Sm83Cpu, byte, byte> execute
)
{
    /// <summary>
    /// Total instruction length in bytes, including the opcode byte.
    /// </summary>
    public byte ByteLength { get; } = byteLength;

    /// <summary>
    /// Instruction duration in machine cycles.
    /// </summary>
    public int MachineCycles { get; } = machineCycles;

    /// <summary>
    /// Instruction body. Operand bytes are passed in program order after the opcode.
    /// </summary>
    public Action<Sm83Cpu, byte, byte> Execute { get; } = execute;
}
