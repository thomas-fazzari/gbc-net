// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// DMG object layer state for one selected scanline.
/// </summary>
internal sealed class DmgObjectLayer : ObjectLayerBase<DmgObjectPixel>
{
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

    protected override ObjectPriorityMode ResolvePriorityMode(PpuEngineInputs inputs) =>
        ObjectPriorityMode.LowerXWins;

    protected override DmgObjectPixel CreateObjectPixel(
        ScanlineObject scanlineObject,
        byte colorId
    ) =>
        new(
            colorId,
            (scanlineObject.Flags & PpuObjectAttributes.DmgPalette1Mask) != 0,
            (scanlineObject.Flags & PpuObjectAttributes.BackgroundPriorityMask) != 0
        );

    protected override void ReadObjectTileRow(
        PpuEngineInputs inputs,
        ScanlineObject scanlineObject,
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

internal readonly record struct DmgObjectLayerState(
    ScanlineObjectSelectorState Selector,
    int PenaltyDots
);
