namespace GbcNet.Core.Ppu;

/// <summary>
/// Game Boy LCD dimensions and scanline geometry shared by DMG, SGB, and CGB PPU models.
/// </summary>
internal static class PpuGeometry
{
    /// <summary>
    /// Visible LCD width in pixels.
    /// </summary>
    public const int FrameWidth = 160;

    /// <summary>
    /// Visible LCD height in pixels.
    /// </summary>
    public const int FrameHeight = 144;

    /// <summary>
    /// One PPU scanline is 456 dots.
    /// </summary>
    public const int ScanlineDots = 456;

    /// <summary>
    /// First LY value in VBlank.
    /// </summary>
    public const byte VBlankStartLine = 144;

    /// <summary>
    /// LY wraps after line 153.
    /// </summary>
    public const byte LastScanline = 153;
}
