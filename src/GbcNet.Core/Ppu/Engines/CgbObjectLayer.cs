using System.Runtime.InteropServices;
using GbcNet.Core.Memory;

namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// CGB object layer state for one selected scanline.
/// </summary>
internal sealed class CgbObjectLayer
{
    private const byte LowThreeBitsMask = 0x07;

    private readonly ScanlineObject[] _scanlineObjects = new ScanlineObject[
        PpuObjectAttributes.MaxObjectsPerScanline
    ];
    private int _scanlineObjectCount;
    private int _scanlineObjectHeight = PpuObjectAttributes.Size8;
    private bool _scanlineObjectsSelected;

    public void Clear()
    {
        _scanlineObjectCount = 0;
        _scanlineObjectHeight = PpuObjectAttributes.Size8;
        _scanlineObjectsSelected = false;
    }

    public void EnsureSelected(PpuEngineInputs inputs, byte lcdYCoordinate, bool oamScanComplete)
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
            _scanlineObjectsSelected = true;
            return;
        }

        SelectScanlineObjects(inputs, lcdYCoordinate);
        _scanlineObjectsSelected = true;
    }

    public CgbObjectPixel? SelectPixel(int screenX, byte lcdYCoordinate, PpuEngineInputs inputs)
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

            return CreateObjectPixel(scanlineObject, colorId);
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
    }

    private byte ReadColorId(
        ScanlineObject scanlineObject,
        int screenX,
        byte lcdYCoordinate,
        PpuEngineInputs inputs
    )
    {
        int objectLine = ResolveObjectTileLine(scanlineObject, lcdYCoordinate);
        byte tileId = ResolveObjectTileId(scanlineObject.Tile, objectLine);
        ushort tileRowAddress = GetObjectTileRowAddress(tileId, objectLine);

        ReadObjectTileRow(
            inputs,
            ResolveObjectTileBank(scanlineObject),
            tileRowAddress,
            out byte lowByte,
            out byte highByte
        );

        return PpuTileData.DecodeColorId(
            lowByte,
            highByte,
            ResolveObjectPixelBit(scanlineObject, screenX)
        );
    }

    private static CgbObjectPixel CreateObjectPixel(ScanlineObject scanlineObject, byte colorId) =>
        new(
            colorId,
            (byte)(scanlineObject.Flags & PpuObjectAttributes.CgbPaletteMask),
            (scanlineObject.Flags & PpuObjectAttributes.BackgroundPriorityMask) != 0
        );

    private int ResolveObjectTileLine(ScanlineObject scanlineObject, byte lcdYCoordinate)
    {
        int objectLine = lcdYCoordinate - (scanlineObject.Y - PpuObjectAttributes.YScreenOffset);

        return (scanlineObject.Flags & PpuObjectAttributes.YFlipMask) == 0
            ? objectLine
            : _scanlineObjectHeight - 1 - objectLine;
    }

    private byte ResolveObjectTileId(byte tileId, int objectLine) =>
        _scanlineObjectHeight == PpuObjectAttributes.Size16
            ? (byte)((tileId & 0xFE) | (objectLine / PpuTileData.TileSizePixels))
            : tileId;

    private static ushort GetObjectTileRowAddress(byte tileId, int objectLine) =>
        PpuTileData.GetUnsignedTileRowAddress(tileId, objectLine & LowThreeBitsMask);

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

    private static int ResolveObjectPixelBit(ScanlineObject scanlineObject, int screenX)
    {
        int pixel = screenX - (scanlineObject.X - PpuObjectAttributes.XScreenOffset);

        return (scanlineObject.Flags & PpuObjectAttributes.XFlipMask) == 0 ? 7 - pixel : pixel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct ScanlineObject(byte X, byte Y, byte Tile, byte Flags);
}

/// <summary>
/// Decoded non-transparent CGB OBJ pixel attributes selected for composition.
/// </summary>
internal readonly record struct CgbObjectPixel(
    byte ColorId,
    byte PaletteIndex,
    bool HasBackgroundPriority
);
