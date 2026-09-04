// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// CGB object layer state for one selected scanline.
/// </summary>
internal sealed class CgbObjectLayer : ObjectLayerBase<CgbObjectPixel>
{
    internal CgbObjectLayerState CaptureState() => new(_selector.CaptureState(), PenaltyDots);

    internal void ValidateState(CgbObjectLayerState state)
    {
        _selector.ValidateState(state.Selector);

        if (state.PenaltyDots < 0)
        {
            throw new ArgumentException("State penalty dots must be nonnegative.", nameof(state));
        }
    }

    internal void RestoreState(CgbObjectLayerState state)
    {
        ValidateState(state);
        _selector.RestoreState(state.Selector);
        PenaltyDots = state.PenaltyDots;
    }

    protected override ObjectPriorityMode ResolvePriorityMode(PpuEngineInputs inputs) =>
        inputs.ObjectPriorityMode;

    protected override CgbObjectPixel CreateObjectPixel(
        ScanlineObject scanlineObject,
        byte colorId
    ) =>
        new(
            colorId,
            (byte)(scanlineObject.Flags & PpuObjectAttributes.CgbPaletteMask),
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
        var bank = (scanlineObject.Flags & PpuObjectAttributes.CgbTileBankMask) == 0 ? 0 : 1;
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

internal readonly record struct CgbObjectLayerState(
    ScanlineObjectSelectorState Selector,
    int PenaltyDots
);
