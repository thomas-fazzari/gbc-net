namespace GbcNet.Core.Ppu;

/// <summary>
/// Pixel encoding used by an LCD frame snapshot.
/// </summary>
public enum LcdPixelFormat
{
    /// <summary>
    /// One byte per DMG pixel, containing the palette shade index 0-3.
    /// </summary>
    DmgShadeIndex8 = 0,

    /// <summary>
    /// Two bytes per CGB pixel, containing little-endian RGB555 color data.
    /// </summary>
    Rgb555Le = 1,
}
