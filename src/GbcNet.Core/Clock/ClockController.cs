using GbcNet.Core.Apu;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Serial;
using GbcNet.Core.Timers;

namespace GbcNet.Core.Clock;

/// <summary>
/// Owns the shared system counter and dispatches DIV falling-edge effects.
/// </summary>
internal sealed class ClockController
{
    private readonly SystemCounter _systemCounter = new();
    private readonly SerialController _serial;
    private readonly ApuController _apu;

    public ClockController(
        InterruptController interrupts,
        SerialController serial,
        ApuController apu
    )
    {
        _serial = serial;
        _apu = apu;
        Timers = new TimerController(interrupts, _systemCounter);
    }

    /// <summary>
    /// Programmable timer registers clocked from the shared system counter.
    /// </summary>
    public TimerController Timers { get; }

    /// <summary>
    /// Reads the CPU-visible DIV register value.
    /// </summary>
    public byte ReadDivider() => _systemCounter.ReadDivider();

    /// <summary>
    /// Advances the system counter one CPU machine cycle and dispatches falling-edge events.
    /// </summary>
    public void TickMachineCycle()
    {
        Timers.AdvanceReloadPipeline();
        DispatchSystemCounterFallingEdges(_systemCounter.AdvanceMachineCycle());
    }

    /// <summary>
    /// Clears DIV as a CPU write would, including falling-edge side effects.
    /// </summary>
    public void ResetDivider()
    {
        DispatchSystemCounterFallingEdges(_systemCounter.Reset());
    }

    /// <summary>
    /// Seeds the visible divider state without CPU write side effects.
    /// </summary>
    internal void SetDivider(byte value)
    {
        _systemCounter.SetDivider(value);
        _serial.SetMasterClockStateFromCounter(_systemCounter.Value);
    }

    /// <summary>
    /// Seeds the raw system counter state without CPU write side effects.
    /// </summary>
    internal void SetCounter(ushort value)
    {
        _systemCounter.SetCounter(value);
        _serial.SetMasterClockStateFromCounter(_systemCounter.Value);
    }

    private void DispatchSystemCounterFallingEdges(ushort fallingEdges)
    {
        Timers.TickSystemCounter(fallingEdges);
        _serial.TickSystemCounter(fallingEdges);
        _apu.TickSystemCounter(new ApuTickInputs(fallingEdges, CgbDoubleSpeed: false));
    }
}

/// <summary>
/// Owns the 16-bit divider counter shared by DIV, TIMA, and serial clocks.
/// </summary>
internal sealed class SystemCounter
{
    private const int DividerVisibleShift = 8;

    /// <summary>
    /// Full 16-bit divider counter that feeds DIV, timer, and serial edge detection.
    /// </summary>
    public ushort Value { get; private set; }

    /// <summary>
    /// Reads the CPU-visible DIV register value from the high byte of the counter.
    /// </summary>
    public byte ReadDivider() => (byte)(Value >> DividerVisibleShift);

    /// <summary>
    /// Advances the counter by one machine cycle and returns bits that changed from high to low.
    /// </summary>
    public ushort AdvanceMachineCycle()
    {
        ushort previousValue = Value;
        Value = unchecked((ushort)(Value + HardwareTiming.MachineCycleTCycles));
        return GetFallingEdges(previousValue, Value);
    }

    /// <summary>
    /// Clears the counter as a DIV write would and returns bits that changed from high to low.
    /// </summary>
    public ushort Reset()
    {
        ushort previousValue = Value;
        Value = 0;
        return GetFallingEdges(previousValue, Value);
    }

    internal void SetDivider(byte value)
    {
        Value = (ushort)(value << DividerVisibleShift);
    }

    internal void SetCounter(ushort value)
    {
        Value = value;
    }

    private static ushort GetFallingEdges(ushort previousValue, ushort value) =>
        (ushort)(previousValue & ~value);
}
