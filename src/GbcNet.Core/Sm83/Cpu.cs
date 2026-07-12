// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

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
    /// Indicates that STOP has suspended CPU fetches until a selected joypad line goes low.
    /// </summary>
    public bool Stopped { get; private set; }

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
        new() { PC = AddressMap.CartridgeEntryPointAddress, SP = AddressMap.HighRamEnd };

    /// <summary>
    /// Captures CPU execution state without allocating.
    /// </summary>
    internal CpuState CaptureState() =>
        new(Registers.CaptureState(), Halted, Stopped, HaltBugPending, Ime, ImeEnablePending);

    /// <summary>
    /// Restores CPU execution state without executing instructions or raising events.
    /// </summary>
    internal void RestoreState(CpuState state)
    {
        Registers.RestoreState(state.Registers);
        Halted = state.Halted;
        Stopped = state.Stopped;
        HaltBugPending = state.HaltBugPending;
        Ime = state.Ime;
        ImeEnablePending = state.ImeEnablePending;
    }

    /// <summary>
    /// Raised after an instruction has executed for debugger and breakpoint instrumentation.
    /// </summary>
    internal event EventHandler<CpuInstructionExecutedEventArgs>? InstructionExecuted;

    /// <summary>
    /// Fetches and executes one instruction.
    /// </summary>
    /// <returns>
    /// Elapsed machine cycles.
    /// </returns>
    public int Step()
    {
        _currentInstructionMachineCycles = 0;

        if (Stopped)
        {
            return StepStopped();
        }

        if (Halted)
        {
            return StepHalted();
        }

        if (TryServiceInterrupt(out var interruptMachineCycles))
        {
            return interruptMachineCycles;
        }

        var enableImeAfterThisInstruction = ImeEnablePending;
        var machineCycles = ExecuteNextInstruction();

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

        // EI followed by HALT with a pending interrupt fetches HALT twice instead of entering HALT
        if (ImeEnablePending)
        {
            Registers.PC = unchecked((ushort)(Registers.PC - 1));
            return;
        }

        // With IME disabled and an interrupt pending, the next opcode is fetched without PC advance
        if (!Ime)
        {
            HaltBugPending = true;
        }
    }

    /// <summary>
    /// Executes STOP by starting a CGB speed-switch pause when armed, or entering the low-power stopped state.
    /// </summary>
    internal void Stop()
    {
        Halted = false;

        if (bus.Clock.TryStartSpeedSwitch())
        {
            return;
        }

        bus.Clock.ResetDivider();
        Stopped = true;
    }

    /// <summary>
    /// Reads one byte from CPU-visible memory and consumes one machine cycle.
    /// </summary>
    internal byte ReadBus(ushort address)
    {
        var value = bus.ReadByte(address);
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
        var lowByte = ReadBus(Registers.SP);
        Registers.SP = unchecked((ushort)(Registers.SP + 1));

        var highByte = ReadBus(Registers.SP);
        Registers.SP = unchecked((ushort)(Registers.SP + 1));

        return (ushort)((highByte << 8) | lowByte);
    }

    private int ExecuteNextInstruction()
    {
        var opcode = FetchProgramByte();
        ApplyHaltBugToFetchedOpcode();

        var instruction =
            InstructionSet.Find(opcode)
            ?? throw new NotSupportedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Opcode 0x{0:X2} is not supported yet.",
                    opcode
                )
            );

        var firstOperand = instruction.ByteLength > 1 ? FetchProgramByte() : (byte)0;
        var secondOperand = instruction.ByteLength > 2 ? FetchProgramByte() : (byte)0;

        instruction.Execute(this, firstOperand, secondOperand);
        InstructionExecuted?.Invoke(this, new CpuInstructionExecutedEventArgs(opcode, Registers));
        return _currentInstructionMachineCycles;
    }

    private int StepStopped()
    {
        if (bus.Joypad.HasSelectedLineLow)
        {
            Stopped = false;
        }

        return 0;
    }

    private int StepHalted()
    {
        IdleCycle();

        if (!bus.Interrupts.HasRequestedAndEnabledInterrupt)
        {
            return _currentInstructionMachineCycles;
        }

        Halted = false;

        if (Ime)
        {
            ServiceInterrupt();
        }

        return _currentInstructionMachineCycles;
    }

    private bool TryServiceInterrupt(out int machineCycles)
    {
        if (!Ime || !bus.Interrupts.HasRequestedAndEnabledInterrupt)
        {
            machineCycles = 0;
            return false;
        }

        ServiceInterrupt();
        machineCycles = _currentInstructionMachineCycles;
        return true;
    }

    private void ServiceInterrupt()
    {
        Halted = false;
        Ime = false;

        IdleCycle();
        IdleCycle();

        var returnAddress = Registers.PC;

        Registers.SP = unchecked((ushort)(Registers.SP - 1));
        WriteBus(Registers.SP, (byte)(returnAddress >> 8));

        var interruptEnableAfterHighPush = bus.Interrupts.InterruptEnable;

        Registers.SP = unchecked((ushort)(Registers.SP - 1));
        var lowByteWritesInterruptFlag = Registers.SP == AddressMap.InterruptFlagRegister;
        var interruptFlagBeforeLowPush = bus.Interrupts.InterruptFlag;
        WriteBus(Registers.SP, (byte)returnAddress);

        var interruptFlagForDispatch = lowByteWritesInterruptFlag
            ? interruptFlagBeforeLowPush
            : bus.Interrupts.InterruptFlag;

        var requestedAndEnabledAfterPushes = (byte)(
            interruptEnableAfterHighPush & interruptFlagForDispatch
        );

        if (
            InterruptController.TryGetHighestPriority(
                requestedAndEnabledAfterPushes,
                out var source,
                out var vector
            )
        )
        {
            bus.Interrupts.Clear(source);
            Registers.PC = vector;
        }
        else
        {
            Registers.PC = 0;
        }

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
        var value = ReadBus(Registers.PC);
        Registers.PC = unchecked((ushort)(Registers.PC + 1));
        return value;
    }

    private void TickMachineCycle()
    {
        TickSingleMachineCycle();

        while (bus.VramDma.TryConsumeCpuStallMachineCycle())
        {
            TickSingleMachineCycle();
        }
    }

    private void TickSingleMachineCycle()
    {
        bus.VramDma.SetCpuHalted(Halted);
        tickMachineCycle?.Invoke();
        _currentInstructionMachineCycles++;
    }
}

/// <summary>
/// Captures SM83 CPU execution state.
/// </summary>
internal readonly record struct CpuState(
    RegistersState Registers,
    bool Halted,
    bool Stopped,
    bool HaltBugPending,
    bool Ime,
    bool ImeEnablePending
);

/// <summary>
/// Provides the opcode and register state observed after one completed instruction.
/// </summary>
internal sealed class CpuInstructionExecutedEventArgs(byte opcode, Registers registers) : EventArgs
{
    /// <summary>
    /// Opcode byte that was executed.
    /// </summary>
    public byte Opcode { get; } = opcode;

    /// <summary>
    /// Mutable CPU register file after the instruction completed.
    /// </summary>
    public Registers Registers { get; } = registers;
}
