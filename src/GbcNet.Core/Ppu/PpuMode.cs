namespace GbcNet.Core.Ppu;

/// <summary>
/// Values exposed by STAT bits 1-0 for the current PPU mode.
/// </summary>
internal enum PpuMode : byte
{
    /// <summary>
    /// Mode 0, horizontal blank.
    /// </summary>
    HBlank = 0,

    /// <summary>
    /// Mode 1, vertical blank.
    /// </summary>
    VBlank = 1,

    /// <summary>
    /// Mode 2, Object Attribute Memory scan.
    /// </summary>
    OamScan = 2,

    /// <summary>
    /// Mode 3, drawing pixels.
    /// </summary>
    Drawing = 3,
}
