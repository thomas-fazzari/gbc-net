// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Memory;

namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// Selects up to ten OAM objects visible on one LCD scanline.
/// </summary>
internal sealed class ScanlineObjectSelector
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

    private readonly ScanlineObject[] _objects = new ScanlineObject[
        PpuObjectAttributes.MaxObjectsPerScanline
    ];
    private int _objectCount;
    private bool _selected;

    internal ReadOnlySpan<ScanlineObject> Objects => _objects.AsSpan(0, _objectCount);

    internal int ObjectHeight { get; private set; } = PpuObjectAttributes.Size8;

    internal ScanlineObjectSelectorState CaptureState() =>
        new((ScanlineObject[])_objects.Clone(), _objectCount, _selected, ObjectHeight);

    internal void ValidateState(ScanlineObjectSelectorState state)
    {
        var objects = state.Objects;
        if (objects is null || objects.Length != _objects.Length)
        {
            throw new ArgumentException(
                "State objects must match the scanline object capacity.",
                nameof(state)
            );
        }

        if ((uint)state.ObjectCount > _objects.Length)
        {
            throw new ArgumentException(
                "State object count must be within the scanline object capacity.",
                nameof(state)
            );
        }

        if (state.ObjectHeight is not (PpuObjectAttributes.Size8 or PpuObjectAttributes.Size16))
        {
            throw new ArgumentException(
                "State object height must be 8 or 16 pixels.",
                nameof(state)
            );
        }

        ulong selectedObjectIndices = 0;
        for (var objectSlot = 0; objectSlot < state.ObjectCount; objectSlot++)
        {
            var objectIndex = objects[objectSlot].Index;
            if ((uint)objectIndex >= PpuObjectAttributes.ObjectCount)
            {
                throw new ArgumentException(
                    "State object indices must identify OAM entries.",
                    nameof(state)
                );
            }

            var objectIndexBit = 1UL << objectIndex;
            if ((selectedObjectIndices & objectIndexBit) != 0)
            {
                throw new ArgumentException(
                    "State selected objects must not duplicate OAM entries.",
                    nameof(state)
                );
            }

            selectedObjectIndices |= objectIndexBit;
        }
    }

    internal void RestoreState(ScanlineObjectSelectorState state)
    {
        ValidateState(state);
        state.Objects.CopyTo(_objects, 0);
        _objectCount = state.ObjectCount;
        _selected = state.Selected;
        ObjectHeight = state.ObjectHeight;
    }

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
        if (priorityMode is ObjectPriorityMode.LowerXWins)
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

    internal int CalculatePenaltyDots(byte scrollXLowBits)
    {
        Span<ScanlineObject> objects = stackalloc ScanlineObject[_objectCount];
        Objects.CopyTo(objects);
        objects.Sort(
            static (x, y) =>
            {
                var xComparison = x.X.CompareTo(y.X);
                return xComparison != 0 ? xComparison : x.Index.CompareTo(y.Index);
            }
        );

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

    private static int GetObjectTileIndex(byte objectX, byte scrollXLowBits)
    {
        var pixel = objectX - PpuObjectAttributes.XScreenOffset + scrollXLowBits;
        return (pixel >> 3) & (PpuTileData.TilesPerMapRow - 1);
    }
}

internal readonly record struct ScanlineObject(int Index, byte X, byte Y, byte Tile, byte Flags);

internal readonly record struct ScanlineObjectSelectorState(
    ScanlineObject[] Objects,
    int ObjectCount,
    bool Selected,
    int ObjectHeight
);
