using System.Globalization;
using GbcNet.Core.Interrupts;
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
    /// Hardware interrupt service sequence duration: two waits, two stack writes, one vector load.
    /// </summary>
    private const int InterruptServiceMachineCycles = 5;

    private const int HaltedMachineCycles = 1;

    /// <summary>
    /// Indicates that HALT has stopped opcode fetching until an interrupt becomes pending.
    /// </summary>
    public bool Halted { get; private set; }

    /// <summary>
    /// Indicates that the next opcode fetch must not advance PC because of the HALT bug.
    /// </summary>
    public bool HaltBugPending { get; private set; }

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
        if (TryServiceInterrupt(out int interruptMachineCycles))
        {
            return interruptMachineCycles;
        }

        if (Halted)
        {
            return StepHalted();
        }

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
    /// Enables interrupt servicing immediately and cancels any delayed EI effect.
    /// </summary>
    internal void EnableInterruptsImmediately()
    {
        Ime = true;
        ImeEnablePending = false;
    }

    /// <summary>
    /// Executes HALT by stopping fetches or triggering the documented HALT bug edge cases.
    /// </summary>
    internal void Halt()
    {
        if (!bus.Interrupts.HasRequestedAndEnabledInterrupt)
        {
            Halted = true;
            return;
        }

        if (ImeEnablePending)
        {
            Registers.PC = unchecked((ushort)(Registers.PC - 1));
            return;
        }

        if (!Ime)
        {
            HaltBugPending = true;
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
        ApplyHaltBugToFetchedOpcode();

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

    private int StepHalted()
    {
        if (bus.Interrupts.HasRequestedAndEnabledInterrupt)
        {
            Halted = false;
        }

        return HaltedMachineCycles;
    }

    private bool TryServiceInterrupt(out int machineCycles)
    {
        if (
            !Ime
            || !bus.Interrupts.TryGetHighestPriority(out InterruptSource source, out ushort vector)
        )
        {
            machineCycles = 0;
            return false;
        }

        bus.Interrupts.Clear(source);
        Halted = false;
        Ime = false;
        PushWord(Registers.PC);
        Registers.PC = vector;

        machineCycles = InterruptServiceMachineCycles;
        return true;
    }

    private void ApplyHaltBugToFetchedOpcode()
    {
        if (!HaltBugPending)
        {
            return;
        }

        HaltBugPending = false;
        Registers.PC = unchecked((ushort)(Registers.PC - 1));
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
