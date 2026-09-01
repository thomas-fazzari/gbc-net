// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// Shared OAM selection, penalty tracking, and scanline pixel-pick loop for DMG and CGB
/// object layers. Subclasses supply the strategy hooks that differ between models:
/// priority mode, pixel creation, and banked VRAM reads.
/// </summary>
internal abstract class ObjectLayerBase<TPixel>
    where TPixel : struct
{
    private protected readonly ScanlineObjectSelector _selector = new();

    /// <summary>
    /// Additional Mode 3 dots caused by OBJ fetches on the selected scanline.
    /// </summary>
    public int PenaltyDots { get; protected set; }

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
                ResolvePriorityMode(inputs)
            )
        )
        {
            PenaltyDots = _selector.CalculatePenaltyDots(scrollXLowBits);
        }
    }

    /// <summary>
    /// Selects the frontmost non-transparent OBJ pixel for a screen X position.
    /// </summary>
    public TPixel? SelectPixel(int screenX, byte lcdYCoordinate, PpuEngineInputs inputs)
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

    protected abstract ObjectPriorityMode ResolvePriorityMode(PpuEngineInputs inputs);

    protected abstract TPixel CreateObjectPixel(ScanlineObject scanlineObject, byte colorId);

    protected abstract void ReadObjectTileRow(
        PpuEngineInputs inputs,
        ScanlineObject scanlineObject,
        ushort tileRowAddress,
        out byte lowByte,
        out byte highByte
    );

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
            scanlineObject,
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
}
