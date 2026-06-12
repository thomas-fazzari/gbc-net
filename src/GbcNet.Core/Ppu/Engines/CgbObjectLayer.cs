using System.Runtime.InteropServices;
using GbcNet.Core.Memory;

namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// CGB object layer state for one selected scanline.
/// </summary>
internal sealed class CgbObjectLayer
{
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

        foreach (var scanlineObject in _scanlineObjects.AsSpan(0, _scanlineObjectCount))
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

    private void SelectScanlineObjects(PpuEngineInputs inputs, byte lcdYCoordinate)
    {
        _scanlineObjectHeight =
            (inputs.LcdControl & PpuLcdControlRegister.ObjectSizeMask) == 0
                ? PpuObjectAttributes.Size8
                : PpuObjectAttributes.Size16;
        _scanlineObjectCount = 0;

        for (var objectIndex = 0; objectIndex < PpuObjectAttributes.ObjectCount; objectIndex++)
        {
            var objectAddress = (ushort)(
                AddressMap.ObjectAttributeMemoryStart
                + (objectIndex * PpuObjectAttributes.AttributeSize)
            );
            var objectY = inputs.ObjectAttributeMemory.Read(
                (ushort)(objectAddress + PpuObjectAttributes.YCoordinateOffset)
            );
            var objectTop = objectY - PpuObjectAttributes.YScreenOffset;

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
        var objectLine = PpuObjectTile.ResolveTileLine(
            scanlineObject.Y,
            scanlineObject.Flags,
            _scanlineObjectHeight,
            lcdYCoordinate
        );
        var tileId = PpuObjectTile.ResolveTileId(
            scanlineObject.Tile,
            objectLine,
            _scanlineObjectHeight
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
