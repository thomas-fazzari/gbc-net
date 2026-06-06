using System.Diagnostics.CodeAnalysis;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu;

namespace GbcNet.Core;

internal static class HardwareTiming
{
    /// <summary>
    /// Number of T-cycles in one CPU machine cycle.
    /// </summary>
    public const ushort MachineCycleTCycles = 4;
}

/// <summary>
/// Advances hardware components that run from CPU machine cycles.
/// </summary>
internal sealed class MachineClock(MemoryBus bus)
{
    private readonly Queue<LcdFrame> _completedFrames = new();

    /// <summary>
    /// Advances cycle-driven hardware once, preserving CPU M-cycle side-effect ordering.
    /// </summary>
    public void TickMachineCycle()
    {
        bus.Timers.AdvanceReloadPipeline();
        ushort fallingEdges = bus.SystemCounter.AdvanceMachineCycle();
        bus.Timers.TickSystemCounter(fallingEdges);
        bus.Serial.TickSystemCounter(fallingEdges);

        if (bus.Ppu.Tick(HardwareTiming.MachineCycleTCycles) is { } completedFrame)
        {
            _completedFrames.Enqueue(completedFrame);
        }

        bus.TickDma(1);
    }

    /// <summary>
    /// Removes the next frame completed during prior hardware ticks, if one is queued.
    /// </summary>
    public bool TryDequeueCompletedFrame([NotNullWhen(true)] out LcdFrame? frame) =>
        _completedFrames.TryDequeue(out frame);
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
