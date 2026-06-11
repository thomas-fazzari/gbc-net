namespace GbcNet.Core.Ppu.Engines;

internal static class PpuObjectTile
{
    public const byte LowThreeBitsMask = 0x07;

    public static int ResolveTileLine(
        byte objectY,
        byte flags,
        int objectHeight,
        byte lcdYCoordinate
    )
    {
        int objectLine = lcdYCoordinate - (objectY - PpuObjectAttributes.YScreenOffset);

        return (flags & PpuObjectAttributes.YFlipMask) == 0
            ? objectLine
            : objectHeight - 1 - objectLine;
    }

    public static byte ResolveTileId(byte tileId, int objectLine, int objectHeight) =>
        objectHeight == PpuObjectAttributes.Size16
            ? (byte)((tileId & 0xFE) | (objectLine / PpuTileData.TileSizePixels))
            : tileId;

    public static ushort GetTileRowAddress(byte tileId, int objectLine) =>
        PpuTileData.GetUnsignedTileRowAddress(tileId, objectLine & LowThreeBitsMask);

    public static int ResolvePixelBit(byte objectX, byte flags, int screenX)
    {
        int pixel = screenX - (objectX - PpuObjectAttributes.XScreenOffset);

        return (flags & PpuObjectAttributes.XFlipMask) == 0 ? 7 - pixel : pixel;
    }
}
