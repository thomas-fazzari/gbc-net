using System.Globalization;
using GbcNet.Core.Memory;

namespace GbcNet.Core.Cpu;

/// <summary>
/// Executes SM83 instructions against the CPU-visible memory bus.
/// </summary>
internal sealed class Sm83Cpu(MemoryBus bus)
{
    private const ushort PostBootProgramCounter = 0x0100;
    private const ushort PostBootStackPointer = 0xFFFE;
    private const byte NopOpcode = 0x00;
    private const int NopMachineCycles = 1;

    private readonly MemoryBus _bus = bus;

    /// <summary>
    /// Mutable SM83 register file.
    /// </summary>
    public CpuRegisters Registers { get; } =
        new() { PC = PostBootProgramCounter, SP = PostBootStackPointer };

    /// <summary>
    /// Fetches and executes one instruction.
    /// </summary>
    /// <returns>
    /// Elapsed machine cycles.
    /// </returns>
    public int Step()
    {
        byte opcode = FetchOpcode();
        return opcode switch
        {
            NopOpcode => NopMachineCycles,
            _ => throw new NotSupportedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Opcode 0x{0:X2} is not supported yet.",
                    opcode
                )
            ),
        };
    }

    private byte FetchOpcode()
    {
        byte opcode = _bus.ReadByte(Registers.PC);
        Registers.PC = unchecked((ushort)(Registers.PC + 1));
        return opcode;
    }
}
