namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// CGB object layer state for one selected scanline.
/// </summary>
internal sealed class CgbObjectLayer
{
    private readonly ScanlineObjectSelector _selector = new();

    public void Clear()
    {
        _selector.Clear();
    }

    public void EnsureSelected(PpuEngineInputs inputs, byte lcdYCoordinate, bool oamScanComplete)
    {
        _selector.TrySelect(inputs, lcdYCoordinate, oamScanComplete, inputs.ObjectPriorityMode);
    }

    public CgbObjectPixel? SelectPixel(int screenX, byte lcdYCoordinate, PpuEngineInputs inputs)
    {
        if ((inputs.LcdControl & PpuLcdControlRegister.ObjectEnableMask) == 0)
        {
            return null;
        }

        foreach (var scanlineObject in _selector.Objects)
        {
            var objectLeft = scanlineObject.X - PpuObjectAttributes.XScreenOffset;
            if (screenX < objectLeft || screenX >= objectLeft + PpuTileData.TileSizePixels)
            {
                continue;
            }

            var colorId = ReadColorId(scanlineObject, screenX, lcdYCoordinate, inputs);
            if (colorId == 0)
            {
                continue;
            }

            return CreateObjectPixel(scanlineObject, colorId);
        }

        return null;
    }

    private byte ReadColorId(
        ScanlineObject scanlineObject,
        int screenX,
        byte lcdYCoordinate,
        PpuEngineInputs inputs
    )
    {
        var objectLine = PpuObjectTile.ResolveTileLine(
            scanlineObject.Y,
            scanlineObject.Flags,
            _selector.ObjectHeight,
            lcdYCoordinate
        );
        var tileId = PpuObjectTile.ResolveTileId(
            scanlineObject.Tile,
            objectLine,
            _selector.ObjectHeight
        );
        var tileRowAddress = PpuObjectTile.GetTileRowAddress(tileId, objectLine);

        ReadObjectTileRow(
            inputs,
            ResolveObjectTileBank(scanlineObject),
            tileRowAddress,
            out var lowByte,
            out var highByte
        );

        return PpuTileData.DecodeColorId(
            lowByte,
            highByte,
            PpuObjectTile.ResolvePixelBit(scanlineObject.X, scanlineObject.Flags, screenX)
        );
    }

    private static CgbObjectPixel CreateObjectPixel(ScanlineObject scanlineObject, byte colorId) =>
        new(
            colorId,
            (byte)(scanlineObject.Flags & PpuObjectAttributes.CgbPaletteMask),
            (scanlineObject.Flags & PpuObjectAttributes.BackgroundPriorityMask) != 0
        );

    private static int ResolveObjectTileBank(ScanlineObject scanlineObject) =>
        (scanlineObject.Flags & PpuObjectAttributes.CgbTileBankMask) == 0 ? 0 : 1;

    private static void ReadObjectTileRow(
        PpuEngineInputs inputs,
        int bank,
        ushort tileRowAddress,
        out byte lowByte,
        out byte highByte
    )
    {
        lowByte = inputs.VideoRam.ReadBank(bank, tileRowAddress);
        highByte = inputs.VideoRam.ReadBank(bank, (ushort)(tileRowAddress + 1));
    }
}

/// <summary>
/// Decoded non-transparent CGB OBJ pixel attributes selected for composition.
/// </summary>
internal readonly record struct CgbObjectPixel(
    byte ColorId,
    byte PaletteIndex,
    bool HasBackgroundPriority
);
