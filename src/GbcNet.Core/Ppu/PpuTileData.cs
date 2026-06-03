namespace GbcNet.Core.Ppu;

/// <summary>
/// Game Boy background/window tile-map and tile-data layout shared by DMG and CGB PPU models.
/// </summary>
internal static class PpuTileData
{
    /// <summary>
    /// Tile map 0 starts at 9800.
    /// </summary>
    public const ushort TileMap0Start = 0x9800;

    /// <summary>
    /// Tile map 1 starts at 9C00.
    /// </summary>
    public const ushort TileMap1Start = 0x9C00;

    /// <summary>
    /// Unsigned tile addressing starts at 8000.
    /// </summary>
    public const ushort UnsignedTileDataStart = 0x8000;

    /// <summary>
    /// Signed tile addressing is based at 9000.
    /// </summary>
    public const ushort SignedTileDataBase = 0x9000;

    /// <summary>
    /// A tile is 8x8 pixels.
    /// </summary>
    public const int TileSizePixels = 8;

    /// <summary>
    /// One tile occupies 16 bytes.
    /// </summary>
    public const int TileDataBytes = 16;

    /// <summary>
    /// Each tile row occupies two bytes.
    /// </summary>
    public const int TileRowBytes = 2;

    /// <summary>
    /// Tile maps are 32 tiles wide.
    /// </summary>
    public const int TilesPerMapRow = 32;
}
