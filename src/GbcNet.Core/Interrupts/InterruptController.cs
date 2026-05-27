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
    /// Writes IF as seen by the CPU, storing only interrupt request bits 0-4.
    /// </summary>
    public void WriteInterruptFlag(byte value)
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
    /// Finds the highest-priority requested and enabled interrupt.
    /// </summary>
    public bool TryGetHighestPriority(out InterruptSource source, out ushort vector)
    {
        byte requestedAndEnabled = RequestedAndEnabledMask;

        for (int bit = 0; bit <= (int)InterruptSource.Joypad; bit++)
        {
            byte mask = (byte)(1 << bit);
            if ((requestedAndEnabled & mask) == 0)
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

/// <summary>
/// Game Boy interrupt sources ordered by hardware priority.
/// </summary>
internal enum InterruptSource : byte
{
    /// <summary>
    /// VBlank interrupt, bit 0, vector 0040.
    /// </summary>
    VBlank = 0,

    /// <summary>
    /// LCD STAT interrupt, bit 1, vector 0048.
    /// </summary>
    Lcd = 1,

    /// <summary>
    /// Timer interrupt, bit 2, vector 0050.
    /// </summary>
    Timer = 2,

    /// <summary>
    /// Serial interrupt, bit 3, vector 0058.
    /// </summary>
    Serial = 3,

    /// <summary>
    /// Joypad interrupt, bit 4, vector 0060.
    /// </summary>
    Joypad = 4,
}
