namespace GbcNet.Core.Clock;

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
