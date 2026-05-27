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

    /// <summary>
    /// CPU-internal IME flag. When set, enabled and requested interrupts may be serviced.
    /// </summary>
    public bool Ime { get; internal set; }

    /// <summary>
    /// Indicates that EI has scheduled IME to become set after one more instruction.
    /// </summary>
    public bool ImeEnablePending { get; private set; }

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
        bool enableImeAfterThisInstruction = ImeEnablePending;
        int machineCycles = ExecuteNextInstruction();

        if (!enableImeAfterThisInstruction || !ImeEnablePending)
        {
            return machineCycles;
        }

        Ime = true;
        ImeEnablePending = false;

        return machineCycles;
    }

    /// <summary>
    /// Disables interrupt servicing immediately and cancels any delayed EI effect.
    /// </summary>
    internal void DisableInterrupts()
    {
        Ime = false;
        ImeEnablePending = false;
    }

    /// <summary>
    /// Schedules interrupt servicing to become enabled after the following instruction.
    /// </summary>
    internal void EnableInterruptsAfterNextInstruction()
    {
        if (!Ime && !ImeEnablePending)
        {
            ImeEnablePending = true;
        }
    }

    /// <summary>
    /// Reads one byte from CPU-visible memory.
    /// </summary>
    internal byte ReadByte(ushort address) => bus.ReadByte(address);

    /// <summary>
    /// Writes one byte to CPU-visible memory.
    /// </summary>
    internal void WriteByte(ushort address, byte value)
    {
        bus.WriteByte(address, value);
    }

    /// <summary>
    /// Pushes a 16-bit value on the stack as high byte, then low byte.
    /// </summary>
    internal void PushWord(ushort value)
    {
        Registers.SP = unchecked((ushort)(Registers.SP - 1));
        WriteByte(Registers.SP, (byte)(value >> 8));

        Registers.SP = unchecked((ushort)(Registers.SP - 1));
        WriteByte(Registers.SP, (byte)value);
    }

    /// <summary>
    /// Pops a 16-bit value from the stack by reading low byte, then high byte.
    /// </summary>
    internal ushort PopWord()
    {
        byte lowByte = ReadByte(Registers.SP);
        Registers.SP = unchecked((ushort)(Registers.SP + 1));

        byte highByte = ReadByte(Registers.SP);
        Registers.SP = unchecked((ushort)(Registers.SP + 1));

        return (ushort)((highByte << 8) | lowByte);
    }

    private int ExecuteNextInstruction()
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

        return instruction.Execute(this, firstOperand, secondOperand);
    }

    /// <summary>
    /// Reads the byte at PC and advances PC by one.
    /// </summary>
    private byte FetchByte()
    {
        byte value = bus.ReadByte(Registers.PC);
        Registers.PC = unchecked((ushort)(Registers.PC + 1));
        return value;
    }
}
