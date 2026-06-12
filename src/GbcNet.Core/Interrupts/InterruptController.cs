namespace GbcNet.Core.Interrupts;

/// <summary>
/// Stores Game Boy interrupt request and enable registers.
/// </summary>
internal sealed class InterruptController
{
    private const byte RequestedInterruptMask = 0x1F;
    private const byte InterruptFlagReadMask = 0xE0;
    private const ushort FirstInterruptVector = 0x0040;
    private const ushort InterruptVectorStride = 0x0008;

    /// <summary>
    /// IE register at FFFF. Bits 0-4 enable interrupt servicing by source.
    /// </summary>
    public byte InterruptEnable { get; set; }

    /// <summary>
    /// Internal IF register state at FF0F. Only bits 0-4 are stored as interrupt requests.
    /// </summary>
    public byte InterruptFlag { get; private set; }

    /// <summary>
    /// Interrupt requests that are both requested in IF and enabled in IE.
    /// </summary>
    public byte RequestedAndEnabledMask =>
        (byte)(InterruptEnable & InterruptFlag & RequestedInterruptMask);

    /// <summary>
    /// Returns whether at least one interrupt is both requested and enabled.
    /// </summary>
    public bool HasRequestedAndEnabledInterrupt => RequestedAndEnabledMask != 0;

    /// <summary>
    /// Reads IF as seen by the CPU, with unused bits 5-7 set.
    /// </summary>
    public byte ReadInterruptFlag() => (byte)(InterruptFlag | InterruptFlagReadMask);

    /// <summary>
    /// Sets IF request bits, storing only interrupt request bits 0-4.
    /// </summary>
    internal void SetInterruptFlag(byte value)
    {
        InterruptFlag = (byte)(value & RequestedInterruptMask);
    }

    /// <summary>
    /// Requests an interrupt by setting its IF bit.
    /// </summary>
    public void Request(InterruptSource source)
    {
        InterruptFlag = (byte)(InterruptFlag | GetMask(source));
    }

    /// <summary>
    /// Acknowledges an interrupt by clearing its IF bit.
    /// </summary>
    public void Clear(InterruptSource source)
    {
        InterruptFlag = (byte)(InterruptFlag & ~GetMask(source));
    }

    /// <summary>
    /// Finds the highest-priority source from a combined IE and IF mask.
    /// </summary>
    internal static bool TryGetHighestPriority(
        byte requestedAndEnabledMask,
        out InterruptSource source,
        out ushort vector
    )
    {
        requestedAndEnabledMask = (byte)(requestedAndEnabledMask & RequestedInterruptMask);

        for (var bit = 0; bit <= (int)InterruptSource.Joypad; bit++)
        {
            var mask = (byte)(1 << bit);
            if ((requestedAndEnabledMask & mask) == 0)
            {
                continue;
            }

            source = (InterruptSource)bit;
            vector = (ushort)(FirstInterruptVector + (bit * InterruptVectorStride));
            return true;
        }

        source = default;
        vector = 0;
        return false;
    }

    private static byte GetMask(InterruptSource source) => (byte)(1 << (int)source);
}
