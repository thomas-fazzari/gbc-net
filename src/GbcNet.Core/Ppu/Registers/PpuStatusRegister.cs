namespace GbcNet.Core.Ppu;

/// <summary>
/// Bit masks and decoding helpers for FF41 STAT.
/// </summary>
internal static class PpuStatusRegister
{
    /// <summary>
    /// STAT bit 7 reads back as set.
    /// </summary>
    public const byte ReadMask = 0x80;

    /// <summary>
    /// STAT bits 6-3 select LCD interrupt sources and are writable by the CPU.
    /// </summary>
    public const byte InterruptSelectMask = 0x78;

    /// <summary>
    /// STAT bit 3 enables Mode 0 LCD STAT interrupts.
    /// </summary>
    public const byte Mode0InterruptSelectMask = 0x08;

    /// <summary>
    /// STAT bit 4 enables Mode 1 LCD STAT interrupts.
    /// </summary>
    public const byte Mode1InterruptSelectMask = 0x10;

    /// <summary>
    /// STAT bit 5 enables Mode 2 LCD STAT interrupts.
    /// </summary>
    public const byte Mode2InterruptSelectMask = 0x20;

    /// <summary>
    /// STAT bit 6 enables LYC=LY LCD STAT interrupts.
    /// </summary>
    public const byte LycEqualsLyInterruptSelectMask = 0x40;

    /// <summary>
    /// STAT bit 2 is set when LY equals LYC.
    /// </summary>
    public const byte LycEqualsLyMask = 0x04;

    /// <summary>
    /// STAT bits 1-0 expose the current PPU mode.
    /// </summary>
    public const byte ModeMask = 0x03;

    /// <summary>
    /// Returns the STAT interrupt-enable bit for a PPU mode.
    /// </summary>
    public static byte GetInterruptSelectMask(PpuMode mode) =>
        mode switch
        {
            PpuMode.HBlank => Mode0InterruptSelectMask,
            PpuMode.VBlank => Mode1InterruptSelectMask,
            PpuMode.OamScan => Mode2InterruptSelectMask,
            _ => 0,
        };
}
