using GbcNet.Core.Ppu;

namespace GbcNet.Core.Hardware.Profiles;

/// <summary>
/// Seeds the DMG Nintendo logo tilemap written by the retail CGB boot ROM for selected compatibility palettes.
/// </summary>
/// <remarks>
/// Retail CGB boot ROM's conditional compat tilemap write sets only VRAM bank 0 tile IDs.
/// Bank 1 attributes remain cleared by the pre-handoff VRAM clear.
/// </remarks>
internal static class CgbCompatibilityLogoTilemap
{
    private const byte TrademarkSymbolTileId = 0x19;
    private const int LogoRowTileCount = 12;

    private const ushort TopRowStart =
        PpuTileData.TileMap0Start + (8 * PpuTileData.TilesPerMapRow) + 4;
    private const ushort TrademarkSymbolAddress =
        PpuTileData.TileMap0Start + (8 * PpuTileData.TilesPerMapRow) + 16;
    private const ushort BottomRowStart =
        PpuTileData.TileMap0Start + (9 * PpuTileData.TilesPerMapRow) + 4;

    internal static void Apply(VideoRam videoRam)
    {
        for (var tile = 0; tile < LogoRowTileCount; tile++)
        {
            videoRam.WriteBank(0, (ushort)(TopRowStart + tile), (byte)(tile + 1));
            videoRam.WriteBank(0, (ushort)(BottomRowStart + tile), (byte)(tile + 0x0D));
        }

        videoRam.WriteBank(0, TrademarkSymbolAddress, TrademarkSymbolTileId);
    }
}
