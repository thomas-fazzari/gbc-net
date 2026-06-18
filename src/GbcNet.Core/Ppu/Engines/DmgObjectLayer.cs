namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// DMG object layer state for one selected scanline.
/// </summary>
internal sealed class DmgObjectLayer
{
    /// <summary>
    /// OBJ fetch adds a six-dot minimum Mode 3 penalty.
    /// </summary>
    private const int ObjectBasePenaltyDots = 6;

    /// <summary>
    /// Startup penalty for an object fetch session beginning at X mod 8 equal to 0 or 1.
    /// </summary>
    private const int SlowObjectSessionStartupDots = 8;

    /// <summary>
    /// Startup penalty for an object fetch session beginning at X mod 8 equal to 2 or 3.
    /// </summary>
    private const int NormalObjectSessionStartupDots = 6;

    /// <summary>
    /// Startup penalty for an object fetch session beginning at X mod 8 equal to 4, 5, 6, or 7.
    /// </summary>
    private const int FastObjectSessionStartupDots = 4;

    private readonly ScanlineObjectSelector _selector = new();

    /// <summary>
    /// Additional Mode 3 dots caused by OBJ fetches on the selected scanline.
    /// </summary>
    public int PenaltyDots { get; private set; }

    /// <summary>
    /// Clears scanline-local OBJ selection and fetch penalty state.
    /// </summary>
    public void Clear()
    {
        _selector.Clear();
        PenaltyDots = 0;
    }

    /// <summary>
    /// Performs the once-per-scanline OAM selection pass after OAM scan has completed.
    /// </summary>
    public void EnsureSelected(
        PpuEngineInputs inputs,
        byte lcdYCoordinate,
        bool oamScanComplete,
        byte scrollXLowBits
    )
    {
        if (
            _selector.TrySelect(
                inputs,
                lcdYCoordinate,
                oamScanComplete,
                ObjectPriorityMode.XCoordinate
            )
        )
        {
            PenaltyDots = CalculatePenaltyDots(scrollXLowBits);
        }
    }

    /// <summary>
    /// Selects the frontmost non-transparent DMG OBJ pixel for a screen X position.
    /// </summary>
    public DmgObjectPixel? SelectPixel(int screenX, byte lcdYCoordinate, PpuEngineInputs inputs)
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

    private int CalculatePenaltyDots(byte scrollXLowBits)
    {
        var objects = _selector.Objects;
        if (objects.Length == 0)
        {
            return 0;
        }
        var penaltyDots = 0;
        var index = 0;

        while (index < objects.Length)
        {
            var firstObject = objects[index];
            if (firstObject.X >= PpuObjectAttributes.FirstFullyHiddenRightX)
            {
                index++;
                continue;
            }

            // Objects that begin in the same tile fetch slot share one fetch session penalty.
            var tileIndex = GetObjectTileIndex(firstObject.X, scrollXLowBits);
            var sessionEnd = index + 1;
            while (
                sessionEnd < objects.Length
                && objects[sessionEnd].X < PpuObjectAttributes.FirstFullyHiddenRightX
                && GetObjectTileIndex(objects[sessionEnd].X, scrollXLowBits) == tileIndex
            )
            {
                sessionEnd++;
            }

            penaltyDots += (firstObject.X & PpuObjectTile.LowThreeBitsMask) switch
            {
                <= 1 => SlowObjectSessionStartupDots,
                <= 3 => NormalObjectSessionStartupDots,
                _ => FastObjectSessionStartupDots,
            };
            penaltyDots += (sessionEnd - index - 1) * ObjectBasePenaltyDots;

            for (var laterIndex = sessionEnd; laterIndex < objects.Length; laterIndex++)
            {
                if (objects[laterIndex].X >= PpuObjectAttributes.FirstFullyHiddenRightX)
                {
                    continue;
                }

                // A later visible fetch session pays a gap penalty based on the previous session.
                penaltyDots += (objects[sessionEnd - 1].X & PpuObjectTile.LowThreeBitsMask) switch
                {
                    0 or 2 or 4 => 3,
                    _ => 2,
                };
                break;
            }

            index = sessionEnd;
        }

        return penaltyDots;
    }

    private static int GetObjectTileIndex(byte objectX, byte scrollXLowBits)
    {
        var pixel = objectX - PpuObjectAttributes.XScreenOffset + scrollXLowBits;
        return (pixel >> 3) & (PpuTileData.TilesPerMapRow - 1);
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

        ReadObjectTileRow(inputs, tileRowAddress, out var lowByte, out var highByte);

        return PpuTileData.DecodeColorId(
            lowByte,
            highByte,
            PpuObjectTile.ResolvePixelBit(scanlineObject.X, scanlineObject.Flags, screenX)
        );
    }

    private static DmgObjectPixel CreateObjectPixel(ScanlineObject scanlineObject, byte colorId) =>
        new(
            colorId,
            (scanlineObject.Flags & PpuObjectAttributes.DmgPalette1Mask) != 0,
            (scanlineObject.Flags & PpuObjectAttributes.BackgroundPriorityMask) != 0
        );

    private static void ReadObjectTileRow(
        PpuEngineInputs inputs,
        ushort tileRowAddress,
        out byte lowByte,
        out byte highByte
    )
    {
        lowByte = inputs.VideoRam.Read(tileRowAddress);
        highByte = inputs.VideoRam.Read((ushort)(tileRowAddress + 1));
    }
}

/// <summary>
/// Decoded non-transparent DMG OBJ pixel attributes selected for composition.
/// </summary>
internal readonly struct DmgObjectPixel(byte colorId, bool usesPalette1, bool hasBackgroundPriority)
{
    /// <summary>
    /// Two-bit OBJ color index; zero is transparent and never returned.
    /// </summary>
    public byte ColorId { get; } = colorId;

    /// <summary>
    /// Selects OBP1 instead of OBP0 for final shade mapping.
    /// </summary>
    public bool UsesPalette1 { get; } = usesPalette1;

    /// <summary>
    /// Indicates that non-zero background/window pixels have priority over this OBJ pixel.
    /// </summary>
    public bool HasBackgroundPriority { get; } = hasBackgroundPriority;
}
