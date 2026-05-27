using System.Globalization;
using GbcNet.Core.Memory;

namespace GbcNet.Core.Sm83;

/// <summary>
/// Executes SM83 instructions against the CPU-visible memory bus.
/// </summary>
internal sealed class Cpu(MemoryBus bus)
{
    private const ushort PostBootProgramCounter = 0x0100;
    private const ushort PostBootStackPointer = 0xFFFE;

    private readonly MemoryBus _bus = bus;

    /// <summary>
    /// Mutable SM83 register file.
    /// </summary>
    public Registers Registers { get; } =
        new() { PC = PostBootProgramCounter, SP = PostBootStackPointer };

    /// <summary>
    /// Fetches and executes one instruction.
    /// </summary>
    /// <returns>
    /// Elapsed machine cycles.
    /// </returns>
    public int Step()
    {
        byte opcode = FetchByte();
        Instruction instruction =
            InstructionSet.Find(opcode)
            ?? throw new NotSupportedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Opcode 0x{0:X2} is not supported yet.",
                    opcode
                )
            );

        byte firstOperand = instruction.ByteLength > 1 ? FetchByte() : (byte)0;
        byte secondOperand = instruction.ByteLength > 2 ? FetchByte() : (byte)0;

        instruction.Execute(this, firstOperand, secondOperand);
        return instruction.MachineCycles;
    }

    /// <summary>
    /// Reads one byte from CPU-visible memory.
    /// </summary>
    internal byte ReadByte(ushort address) => _bus.ReadByte(address);

    /// <summary>
    /// Writes one byte to CPU-visible memory.
    /// </summary>
    internal void WriteByte(ushort address, byte value)
    {
        _bus.WriteByte(address, value);
    }

    /// <summary>
    /// Reads the byte at PC and advances PC by one.
    /// </summary>
    private byte FetchByte()
    {
        byte value = _bus.ReadByte(Registers.PC);
        Registers.PC = unchecked((ushort)(Registers.PC + 1));
        return value;
    }
}
