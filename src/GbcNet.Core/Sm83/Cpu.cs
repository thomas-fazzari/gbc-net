using System.Globalization;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83.Instructions;

namespace GbcNet.Core.Sm83;

/// <summary>
/// Executes SM83 instructions against the CPU-visible memory bus.
/// </summary>
internal sealed class Cpu(MemoryBus bus, Action? tickMachineCycle = null)
{
    private int _currentInstructionMachineCycles;

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
        new() { PC = AddressMap.CartridgeEntryPointStart, SP = AddressMap.HighRamEnd };

    /// <summary>
    /// Fetches and executes one instruction.
    /// </summary>
    /// <returns>
    /// Elapsed machine cycles.
    /// </returns>
    public int Step()
    {
        _currentInstructionMachineCycles = 0;

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
    /// Reads one byte from CPU-visible memory and consumes one machine cycle.
    /// </summary>
    internal byte ReadBus(ushort address)
    {
        byte value = bus.ReadByte(address);
        TickMachineCycle();
        return value;
    }

    /// <summary>
    /// Writes one byte to CPU-visible memory and consumes one machine cycle.
    /// </summary>
    internal void WriteBus(ushort address, byte value)
    {
        bus.WriteByte(address, value);
        TickMachineCycle();
    }

    /// <summary>
    /// Consumes one machine cycle without accessing the bus.
    /// </summary>
    internal void IdleCycle()
    {
        TickMachineCycle();
    }

    /// <summary>
    /// Pushes a 16-bit value on the stack as high byte, then low byte.
    /// </summary>
    internal void PushWord(ushort value)
    {
        Registers.SP = unchecked((ushort)(Registers.SP - 1));
        WriteBus(Registers.SP, (byte)(value >> 8));

        Registers.SP = unchecked((ushort)(Registers.SP - 1));
        WriteBus(Registers.SP, (byte)value);
    }

    /// <summary>
    /// Pops a 16-bit value from the stack by reading low byte, then high byte.
    /// </summary>
    internal ushort PopWord()
    {
        byte lowByte = ReadBus(Registers.SP);
        Registers.SP = unchecked((ushort)(Registers.SP + 1));

        byte highByte = ReadBus(Registers.SP);
        Registers.SP = unchecked((ushort)(Registers.SP + 1));

        return (ushort)((highByte << 8) | lowByte);
    }

    private int ExecuteNextInstruction()
    {
        byte opcode = FetchProgramByte();
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

        byte firstOperand = instruction.ByteLength > 1 ? FetchProgramByte() : (byte)0;
        byte secondOperand = instruction.ByteLength > 2 ? FetchProgramByte() : (byte)0;

        instruction.Execute(this, firstOperand, secondOperand);
        return _currentInstructionMachineCycles;
    }

    private int StepHalted()
    {
        if (bus.Interrupts.HasRequestedAndEnabledInterrupt)
        {
            Halted = false;
        }

        IdleCycle();
        return _currentInstructionMachineCycles;
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

        ServiceInterrupt(source, vector);
        machineCycles = _currentInstructionMachineCycles;
        return true;
    }

    private void ServiceInterrupt(InterruptSource source, ushort vector)
    {
        bus.Interrupts.Clear(source);
        Halted = false;
        Ime = false;

        IdleCycle();
        IdleCycle();

        PushWord(Registers.PC);
        Registers.PC = vector;
        IdleCycle();
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
    private byte FetchProgramByte()
    {
        byte value = ReadBus(Registers.PC);
        Registers.PC = unchecked((ushort)(Registers.PC + 1));
        return value;
    }

    private void TickMachineCycle()
    {
        tickMachineCycle?.Invoke();
        _currentInstructionMachineCycles++;
    }
}
