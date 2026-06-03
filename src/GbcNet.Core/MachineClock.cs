using GbcNet.Core.Memory;

namespace GbcNet.Core;

internal static class HardwareTiming
{
    public const ushort MachineCycleTCycles = 4;
}

/// <summary>
/// Advances hardware components that run from CPU machine cycles.
/// </summary>
internal sealed class MachineClock(MemoryBus bus)
{
    public void TickMachineCycle()
    {
        bus.Timers.AdvanceReloadPipeline();
        ushort fallingEdges = bus.SystemCounter.AdvanceMachineCycle();
        bus.Timers.TickSystemCounter(fallingEdges);
        bus.Serial.TickSystemCounter(fallingEdges);
        bus.Ppu.Tick(HardwareTiming.MachineCycleTCycles);
        bus.TickDma(1);
    }
}

/// <summary>
/// Owns the 16-bit divider counter shared by DIV, TIMA, and serial clocks.
/// </summary>
internal sealed class SystemCounter
{
    private const int DividerVisibleShift = 8;

    public ushort Value { get; private set; }

    public byte ReadDivider() => (byte)(Value >> DividerVisibleShift);

    public ushort AdvanceMachineCycle()
    {
        ushort previousValue = Value;
        Value = unchecked((ushort)(Value + HardwareTiming.MachineCycleTCycles));
        return GetFallingEdges(previousValue, Value);
    }

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
