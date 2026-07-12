// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu.Engines;

internal readonly record struct DmgObjectLayerState(
    ScanlineObjectSelectorState Selector,
    int PenaltyDots
);

/// <summary>
/// DMG object layer state for one selected scanline.
/// </summary>
internal sealed class DmgObjectLayer
{
    private readonly ScanlineObjectSelector _selector = new();

    /// <summary>
    /// Additional Mode 3 dots caused by OBJ fetches on the selected scanline.
    /// </summary>
    public int PenaltyDots { get; private set; }

    internal DmgObjectLayerState CaptureState() => new(_selector.CaptureState(), PenaltyDots);

    internal void ValidateState(DmgObjectLayerState state)
    {
        _selector.ValidateState(state.Selector);

        if (state.PenaltyDots < 0)
        {
            throw new ArgumentException("State penalty dots must be nonnegative.", nameof(state));
        }
    }

    internal void RestoreState(DmgObjectLayerState state)
    {
        ValidateState(state);
        _selector.RestoreState(state.Selector);
        PenaltyDots = state.PenaltyDots;
    }

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
                ObjectPriorityMode.LowerXWins
            )
        )
        {
            PenaltyDots = _selector.CalculatePenaltyDots(scrollXLowBits);
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
