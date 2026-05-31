namespace GbcNet.Core.Ppu;

/// <summary>
/// Interrupts requested by a PPU timing transition.
/// </summary>
[Flags]
internal enum PpuInterruptRequest : byte
{
    /// <summary>
    /// No interrupt requested.
    /// </summary>
    None = 0,

    /// <summary>
    /// Request the VBlank interrupt.
    /// </summary>
    VBlank = 1 << 0,

    /// <summary>
    /// Request the LCD STAT interrupt.
    /// </summary>
    Lcd = 1 << 1,
}
