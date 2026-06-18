using GbcNet.Core.Memory;

namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// Selects up to ten OAM objects visible on one LCD scanline.
/// </summary>
internal sealed class ScanlineObjectSelector
{
    private readonly ScanlineObject[] _objects = new ScanlineObject[
        PpuObjectAttributes.MaxObjectsPerScanline
    ];
    private int _objectCount;
    private bool _selected;

    internal ReadOnlySpan<ScanlineObject> Objects => _objects.AsSpan(0, _objectCount);

    internal int ObjectHeight { get; private set; } = PpuObjectAttributes.Size8;

    internal void Clear()
    {
        _objectCount = 0;
        ObjectHeight = PpuObjectAttributes.Size8;
        _selected = false;
    }

    internal bool TrySelect(
        PpuEngineInputs inputs,
        byte lcdYCoordinate,
        bool oamScanComplete,
        ObjectPriorityMode priorityMode
    )
    {
        if (_selected || lcdYCoordinate >= PpuGeometry.VBlankStartLine || !oamScanComplete)
        {
            return false;
        }

        if ((inputs.LcdControl & PpuLcdControlRegister.ObjectEnableMask) == 0)
        {
            _objectCount = 0;
            ObjectHeight = PpuObjectAttributes.Size8;
            _selected = true;
            return true;
        }

        SelectObjects(inputs, lcdYCoordinate);
        if (priorityMode is ObjectPriorityMode.XCoordinate)
        {
            _objects
                .AsSpan(0, _objectCount)
                .Sort(
                    static (x, y) =>
                    {
                        var xComparison = x.X.CompareTo(y.X);
                        return xComparison != 0 ? xComparison : x.Index.CompareTo(y.Index);
                    }
                );
        }

        _selected = true;
        return true;
    }

    private void SelectObjects(PpuEngineInputs inputs, byte lcdYCoordinate)
    {
        ObjectHeight =
            (inputs.LcdControl & PpuLcdControlRegister.ObjectSizeMask) == 0
                ? PpuObjectAttributes.Size8
                : PpuObjectAttributes.Size16;
        _objectCount = 0;

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

            if (objectTop > lcdYCoordinate || objectTop + ObjectHeight <= lcdYCoordinate)
            {
                continue;
            }

            _objects[_objectCount] = new ScanlineObject(
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

            _objectCount++;

            if (_objectCount == PpuObjectAttributes.MaxObjectsPerScanline)
            {
                break;
            }
        }
    }
}

internal readonly record struct ScanlineObject(int Index, byte X, byte Y, byte Tile, byte Flags);
