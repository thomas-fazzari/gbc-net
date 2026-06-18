namespace GbcNet.App.Rendering.Palettes;

/// <summary>
/// LCD colors used by the GUI to display DMG shade indices.
/// </summary>
internal static class DmgLcdPalette
{
    /// <summary>
    /// Four BGRA8888 colors ordered by DMG shade index 0-3.
    /// </summary>
    public static ReadOnlySpan<byte> Bgra =>
        [
            0xD0,
            0xF8,
            0xE0,
            0xFF,
            0x70,
            0xC0,
            0x88,
            0xFF,
            0x56,
            0x68,
            0x34,
            0xFF,
            0x18,
            0x18,
            0x08,
            0xFF,
        ];
}
