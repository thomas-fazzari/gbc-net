using System.Runtime.InteropServices;
using GbcNet.Core.Memory;

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

    private const byte LowThreeBitsMask = 0x07;

    private readonly ScanlineObject[] _scanlineObjects = new ScanlineObject[
        PpuObjectAttributes.MaxObjectsPerScanline
    ];
    private int _scanlineObjectCount;
    private int _scanlineObjectHeight = PpuObjectAttributes.Size8;
    private bool _scanlineObjectsSelected;

    public int PenaltyDots { get; private set; }

    public void Clear()
    {
        _scanlineObjectCount = 0;
        _scanlineObjectHeight = PpuObjectAttributes.Size8;
        PenaltyDots = 0;
        _scanlineObjectsSelected = false;
    }

    public void EnsureSelected(
        PpuEngineInputs inputs,
        byte lcdYCoordinate,
        bool oamScanComplete,
        byte scrollXLowBits
    )
    {
        if (
            _scanlineObjectsSelected
            || lcdYCoordinate >= PpuGeometry.VBlankStartLine
            || !oamScanComplete
        )
        {
            return;
        }

        if ((inputs.LcdControl & PpuLcdControlRegister.ObjectEnableMask) == 0)
        {
            _scanlineObjectCount = 0;
            _scanlineObjectHeight = PpuObjectAttributes.Size8;
            PenaltyDots = 0;
            _scanlineObjectsSelected = true;
            return;
        }

        SelectScanlineObjects(inputs, lcdYCoordinate);
        PenaltyDots = CalculatePenaltyDots(scrollXLowBits);
        _scanlineObjectsSelected = true;
    }

    public DmgObjectPixel? SelectPixel(int screenX, byte lcdYCoordinate, PpuEngineInputs inputs)
    {
        if ((inputs.LcdControl & PpuLcdControlRegister.ObjectEnableMask) == 0)
        {
            return null;
        }

        foreach (ScanlineObject scanlineObject in _scanlineObjects.AsSpan(0, _scanlineObjectCount))
        {
            int objectLeft = scanlineObject.X - PpuObjectAttributes.XScreenOffset;
            if (screenX < objectLeft || screenX >= objectLeft + PpuTileData.TileSizePixels)
            {
                continue;
            }

            byte colorId = ReadColorId(scanlineObject, screenX, lcdYCoordinate, inputs);
            if (colorId == 0)
            {
                continue;
            }

            return new DmgObjectPixel(
                colorId,
                (scanlineObject.Flags & PpuObjectAttributes.DmgPalette1Mask) != 0,
                (scanlineObject.Flags & PpuObjectAttributes.BackgroundPriorityMask) != 0
            );
        }

        return null;
    }

    private void SelectScanlineObjects(PpuEngineInputs inputs, byte lcdYCoordinate)
    {
        _scanlineObjectHeight =
            (inputs.LcdControl & PpuLcdControlRegister.ObjectSizeMask) == 0
                ? PpuObjectAttributes.Size8
                : PpuObjectAttributes.Size16;
        _scanlineObjectCount = 0;

        for (int objectIndex = 0; objectIndex < PpuObjectAttributes.ObjectCount; objectIndex++)
        {
            ushort objectAddress = (ushort)(
                AddressMap.ObjectAttributeMemoryStart
                + (objectIndex * PpuObjectAttributes.AttributeSize)
            );
            byte objectY = inputs.ObjectAttributeMemory.Read(
                (ushort)(objectAddress + PpuObjectAttributes.YCoordinateOffset)
            );
            int objectTop = objectY - PpuObjectAttributes.YScreenOffset;

            if (objectTop > lcdYCoordinate || objectTop + _scanlineObjectHeight <= lcdYCoordinate)
            {
                continue;
            }

            _scanlineObjects[_scanlineObjectCount] = new(
                objectIndex,
                inputs.ObjectAttributeMemory.Read(
                    (ushort)(objectAddress + PpuObjectAttributes.XCoordinateOffset)
                ),
                objectY,
                inputs.ObjectAttributeMemory.Read(
                    (ushort)(objectAddress + PpuObjectAttributes.TileIndexOffset)
                ),
                inputs.ObjectAttributeMemory.Read(
                    (ushort)(objectAddress + PpuObjectAttributes.FlagsOffset)
                )
            );

            _scanlineObjectCount++;

            if (_scanlineObjectCount == PpuObjectAttributes.MaxObjectsPerScanline)
            {
                break;
            }
        }

        _scanlineObjects
            .AsSpan(0, _scanlineObjectCount)
            .Sort(
                static (x, y) =>
                {
                    int xComparison = x.X.CompareTo(y.X);
                    return xComparison != 0 ? xComparison : x.Index.CompareTo(y.Index);
                }
            );
    }

    private int CalculatePenaltyDots(byte scrollXLowBits)
    {
        if (_scanlineObjectCount == 0)
        {
            return 0;
        }

        ReadOnlySpan<ScanlineObject> objects = _scanlineObjects.AsSpan(0, _scanlineObjectCount);
        int penaltyDots = 0;
        int index = 0;

        while (index < objects.Length)
        {
            ScanlineObject firstObject = objects[index];
            if (firstObject.X >= PpuObjectAttributes.FirstFullyHiddenRightX)
            {
                index++;
                continue;
            }

            int tileIndex = GetObjectTileIndex(firstObject.X, scrollXLowBits);
            int sessionEnd = index + 1;
            while (
                sessionEnd < objects.Length
                && objects[sessionEnd].X < PpuObjectAttributes.FirstFullyHiddenRightX
                && GetObjectTileIndex(objects[sessionEnd].X, scrollXLowBits) == tileIndex
            )
            {
                sessionEnd++;
            }

            penaltyDots += (firstObject.X & LowThreeBitsMask) switch
            {
                <= 1 => SlowObjectSessionStartupDots,
                <= 3 => NormalObjectSessionStartupDots,
                _ => FastObjectSessionStartupDots,
            };
            penaltyDots += (sessionEnd - index - 1) * ObjectBasePenaltyDots;

            for (int laterIndex = sessionEnd; laterIndex < objects.Length; laterIndex++)
            {
                if (objects[laterIndex].X >= PpuObjectAttributes.FirstFullyHiddenRightX)
                {
                    continue;
                }

                penaltyDots += (objects[sessionEnd - 1].X & LowThreeBitsMask) switch
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

    private byte ReadColorId(
        ScanlineObject scanlineObject,
        int screenX,
        byte lcdYCoordinate,
        PpuEngineInputs inputs
    )
    {
        int objectLine = lcdYCoordinate - (scanlineObject.Y - PpuObjectAttributes.YScreenOffset);

        if ((scanlineObject.Flags & PpuObjectAttributes.YFlipMask) != 0)
        {
            objectLine = _scanlineObjectHeight - 1 - objectLine;
        }

        byte tileId =
            _scanlineObjectHeight == PpuObjectAttributes.Size16
                ? (byte)((scanlineObject.Tile & 0xFE) | (objectLine / PpuTileData.TileSizePixels))
                : scanlineObject.Tile;
        int tileLine = objectLine & LowThreeBitsMask;
        int tileAddress =
            PpuTileData.UnsignedTileDataStart
            + (tileId * PpuTileData.TileDataBytes)
            + (tileLine * PpuTileData.TileRowBytes);
        int pixel = screenX - (scanlineObject.X - PpuObjectAttributes.XScreenOffset);
        int bit = (scanlineObject.Flags & PpuObjectAttributes.XFlipMask) == 0 ? 7 - pixel : pixel;
        byte lowByte = inputs.VideoRam.Read((ushort)tileAddress);
        byte highByte = inputs.VideoRam.Read((ushort)(tileAddress + 1));

        return (byte)((((highByte >> bit) & 0x01) << 1) | ((lowByte >> bit) & 0x01));
    }

    private static int GetObjectTileIndex(byte objectX, byte scrollXLowBits)
    {
        int pixel = objectX - PpuObjectAttributes.XScreenOffset + scrollXLowBits;
        return (pixel >> 3) & (PpuTileData.TilesPerMapRow - 1);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct ScanlineObject(int Index, byte X, byte Y, byte Tile, byte Flags);
}

internal readonly struct DmgObjectPixel(byte colorId, bool usesPalette1, bool hasBackgroundPriority)
{
    public byte ColorId { get; } = colorId;

    public bool UsesPalette1 { get; } = usesPalette1;

    public bool HasBackgroundPriority { get; } = hasBackgroundPriority;
}
