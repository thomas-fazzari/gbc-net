// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu.Engines;

internal interface IDmgPixelOutput
{
    static abstract int BytesPerPixel { get; }

    static abstract LcdPixelFormat PixelFormat { get; }

    static abstract void WritePixel(
        byte[] frameBuffer,
        int pixelIndex,
        byte backgroundColorId,
        DmgObjectPixel? objectPixel,
        PpuEngineInputs inputs
    );
}

/// <summary>
/// Shared DMG pixel-rule renderer used by DMG hardware and CGB DMG compatibility output.
/// </summary>
/// <remarks>
/// The <paramref name="requestsMode2InterruptBeforeVBlank"/> flag selects the CGB-compat
/// early Mode 2 STAT interrupt, and <paramref name="stateWrapper"/> wraps the captured
/// state in the profile-specific <see cref="IPpuEngineState"/> subtype for JSON discrimination.
/// </remarks>
internal sealed class DmgPixelRulesPpuEngine<TPixelOutput>(
    bool requestsMode2InterruptBeforeVBlank,
    Func<DmgPixelRulesPpuEngineState, IPpuEngineState> stateWrapper
) : PpuEngineBase(TPixelOutput.BytesPerPixel, TPixelOutput.PixelFormat)
    where TPixelOutput : struct, IDmgPixelOutput
{
    public override IPpuEngineState CaptureState() =>
        stateWrapper(CaptureDmgPixelRulesPpuEngineState());

    public override void ValidateState(IPpuEngineState state)
    {
        if (state is not IDmgPixelRulesState dmgState)
        {
            throw new ArgumentException(
                "PPU engine state must be for a DMG pixel-rules engine.",
                nameof(state)
            );
        }

        ValidateDmgPixelRulesPpuEngineState(dmgState.PixelRules);
    }

    public override void RestoreState(IPpuEngineState state)
    {
        if (state is not IDmgPixelRulesState dmgState)
        {
            throw new ArgumentException(
                "PPU engine state must be for a DMG pixel-rules engine.",
                nameof(state)
            );
        }

        RestoreDmgPixelRulesPpuEngineState(dmgState.PixelRules);
    }

    protected override bool RequestsMode2InterruptBeforeVBlank =>
        requestsMode2InterruptBeforeVBlank;

    protected override int Mode2InterruptLeadDots =>
        requestsMode2InterruptBeforeVBlank ? CgbPpuEngine.Mode2InterruptLeadDotsValue : 0;

    private readonly byte[] _backgroundFifo = new byte[BackgroundFifoCapacity];
    private readonly DmgObjectLayer _objects = new();
    private byte _fetcherTileId;

    private DmgPixelRulesPpuEngineState CaptureDmgPixelRulesPpuEngineState() =>
        new(
            CapturePpuEngineBaseState(),
            [.. _backgroundFifo],
            _objects.CaptureState(),
            _fetcherTileId
        );

    private void ValidateDmgPixelRulesPpuEngineState(DmgPixelRulesPpuEngineState state)
    {
        ValidatePpuEngineBaseState(state.Common);
        _objects.ValidateState(state.Objects);

        if (state.BackgroundFifo is null || state.BackgroundFifo.Length != _backgroundFifo.Length)
        {
            throw new ArgumentException(
                "Background FIFO length must match the engine FIFO capacity.",
                nameof(state)
            );
        }
    }

    private void RestoreDmgPixelRulesPpuEngineState(DmgPixelRulesPpuEngineState state)
    {
        ValidateDmgPixelRulesPpuEngineState(state);
        RestorePpuEngineBaseState(state.Common);
        state.BackgroundFifo.CopyTo(_backgroundFifo, 0);
        _objects.RestoreState(state.Objects);
        _fetcherTileId = state.FetchedTileId;
    }

    protected override int ObjectPenaltyDots => _objects.PenaltyDots;

    protected override void EnsureObjectsSelected(PpuEngineInputs inputs)
    {
        _objects.EnsureSelected(
            inputs,
            LcdYCoordinate,
            Timing.HasReachedOamScanEnd,
            BgWindowFetcher.LatchedScrollXLowBits
        );
    }

    protected override void ClearObjects()
    {
        _objects.Clear();
    }

    internal override bool IsWindowEnabled(PpuEngineInputs inputs) =>
        (
            inputs.LcdControl
            & (
                PpuLcdControlRegister.BackgroundWindowEnableOrPriorityMask
                | PpuLcdControlRegister.WindowEnableMask
            )
        )
        == (
            PpuLcdControlRegister.BackgroundWindowEnableOrPriorityMask
            | PpuLcdControlRegister.WindowEnableMask
        );

    internal override void FetchTileMapEntry(PpuEngineInputs inputs, ushort tileMapAddress)
    {
        _fetcherTileId = inputs.VideoRam.Read(tileMapAddress);
    }

    internal override byte ReadTileDataByte(PpuEngineInputs inputs, bool highByte) =>
        inputs.VideoRam.Read(GetTileDataAddress(inputs, highByte));

    internal override bool TryPushFetchedTileRow()
    {
        if (BgWindowFetcher.BackgroundFifoCount > PpuTileData.TileSizePixels)
        {
            return false;
        }

        for (var pixel = 0; pixel < PpuTileData.TileSizePixels; pixel++)
        {
            PushBackgroundPixel(
                PpuTileData.DecodeColorId(
                    BgWindowFetcher.FetchedTileDataLow,
                    BgWindowFetcher.FetchedTileDataHigh,
                    7 - pixel
                )
            );
        }

        return true;
    }

    protected override void TryRenderPixel(PpuEngineInputs inputs)
    {
        if (BgWindowFetcher.BackgroundFifoCount == 0 || RenderedPixels == PpuGeometry.FrameWidth)
        {
            return;
        }

        var colorId = PopBackgroundPixel();
        if (BgWindowFetcher.ShouldDiscardPixel())
        {
            return;
        }

        TPixelOutput.WritePixel(
            FrameBuffer,
            (LcdYCoordinate * PpuGeometry.FrameWidth) + RenderedPixels,
            DmgPixelRules.ApplyBackgroundEnable(colorId, inputs.LcdControl),
            _objects.SelectPixel(RenderedPixels, LcdYCoordinate, inputs),
            inputs
        );
        RenderedPixels++;
    }

    internal override void ClearFetchedTileMapEntry()
    {
        _fetcherTileId = 0;
    }

    private ushort GetTileDataAddress(PpuEngineInputs inputs, bool highByte) =>
        BackgroundWindowFetcher.GetBackgroundTileDataAddress(
            inputs,
            _fetcherTileId,
            BgWindowFetcher.GetFetcherY(LcdYCoordinate) & TileLineMask,
            highByte
        );

    private void PushBackgroundPixel(byte colorId)
    {
        _backgroundFifo[BgWindowFetcher.BackgroundFifoWriteIndex] = colorId;
        BgWindowFetcher.CommitBackgroundFifoPush();
    }

    private byte PopBackgroundPixel()
    {
        var colorId = _backgroundFifo[BgWindowFetcher.BackgroundFifoReadIndex];
        BgWindowFetcher.CommitBackgroundFifoPop();
        return colorId;
    }
}

internal readonly record struct DmgShadePixelOutput : IDmgPixelOutput
{
    public static int BytesPerPixel => 1;

    public static LcdPixelFormat PixelFormat => LcdPixelFormat.DmgShadeIndex8;

    public static void WritePixel(
        byte[] frameBuffer,
        int pixelIndex,
        byte backgroundColorId,
        DmgObjectPixel? objectPixel,
        PpuEngineInputs inputs
    )
    {
        frameBuffer[pixelIndex] = DmgPixelRules.ResolveShade(
            backgroundColorId,
            objectPixel,
            inputs
        );
    }
}

internal readonly record struct CgbDmgCompatibilityPixelOutput : IDmgPixelOutput
{
    private const int Rgb555BytesPerPixel = 2;

    public static int BytesPerPixel => Rgb555BytesPerPixel;

    public static LcdPixelFormat PixelFormat => LcdPixelFormat.Rgb555Le;

    public static void WritePixel(
        byte[] frameBuffer,
        int pixelIndex,
        byte backgroundColorId,
        DmgObjectPixel? objectPixel,
        PpuEngineInputs inputs
    )
    {
        var color = DmgPixelRules.ResolveCgbDmgCompatibilityColor(
            backgroundColorId,
            objectPixel,
            inputs
        );
        var frameOffset = pixelIndex * Rgb555BytesPerPixel;
        frameBuffer[frameOffset] = (byte)color;
        frameBuffer[frameOffset + 1] = (byte)(color >> 8);
    }
}

internal static class DmgPixelRules
{
    public static byte ApplyBackgroundEnable(byte backgroundColorId, byte lcdControl) =>
        (lcdControl & PpuLcdControlRegister.BackgroundWindowEnableOrPriorityMask) == 0
            ? (byte)0
            : backgroundColorId;

    public static byte ResolveShade(
        byte backgroundColorId,
        DmgObjectPixel? objectPixel,
        PpuEngineInputs inputs
    )
    {
        if (
            objectPixel is null
            || BackgroundCoversObject(backgroundColorId, objectPixel.Value, inputs.LcdControl)
        )
        {
            return ApplyPalette(backgroundColorId, inputs.BackgroundPalette);
        }

        return ApplyPalette(
            objectPixel.Value.ColorId,
            objectPixel.Value.UsesPalette1 ? inputs.ObjectPalette1 : inputs.ObjectPalette0
        );
    }

    public static ushort ResolveCgbDmgCompatibilityColor(
        byte backgroundColorId,
        DmgObjectPixel? objectPixel,
        PpuEngineInputs inputs
    )
    {
        if (
            objectPixel is null
            || BackgroundCoversObject(backgroundColorId, objectPixel.Value, inputs.LcdControl)
        )
        {
            return inputs.BackgroundPaletteRam.ReadRgb555Color(
                0,
                ApplyPalette(backgroundColorId, inputs.BackgroundPalette)
            );
        }

        var usesPalette1 = objectPixel.Value.UsesPalette1;
        return inputs.ObjectPaletteRam.ReadRgb555Color(
            usesPalette1 ? 1 : 0,
            ApplyPalette(
                objectPixel.Value.ColorId,
                usesPalette1 ? inputs.ObjectPalette1 : inputs.ObjectPalette0
            )
        );
    }

    private static bool BackgroundCoversObject(
        byte backgroundColorId,
        DmgObjectPixel objectPixel,
        byte lcdControl
    ) =>
        objectPixel.HasBackgroundPriority
        && backgroundColorId != 0
        && (lcdControl & PpuLcdControlRegister.BackgroundWindowEnableOrPriorityMask) != 0;

    private static byte ApplyPalette(byte colorId, byte palette) =>
        (byte)((palette >> (colorId * 2)) & 0x03);
}

internal readonly record struct DmgPixelRulesPpuEngineState(
    PpuEngineBaseState Common,
    byte[] BackgroundFifo,
    DmgObjectLayerState Objects,
    byte FetchedTileId
);

/// <summary>
/// Marker for DMG pixel-rules engine state wrappers, exposing the inner <see cref="DmgPixelRulesPpuEngineState"/>.
/// </summary>
internal interface IDmgPixelRulesState
{
    DmgPixelRulesPpuEngineState PixelRules { get; }
}

internal sealed record DmgPpuEngineState(DmgPixelRulesPpuEngineState PixelRules)
    : IDmgPixelRulesState,
        IPpuEngineState;

internal sealed record CgbDmgCompatibilityPpuEngineState(DmgPixelRulesPpuEngineState PixelRules)
    : IDmgPixelRulesState,
        IPpuEngineState;
