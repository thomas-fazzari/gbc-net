// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// CGB LCD engine for BG/window/OBJ RGB555 rendering with CGB tile attributes.
/// </summary>
internal sealed class CgbPpuEngine() : PpuEngineBase(Rgb555BytesPerPixel, LcdPixelFormat.Rgb555Le)
{
    private const byte AttributePaletteMask = 0x07;
    private const byte AttributeTileBankMask = 0x08;
    private const byte AttributeXFlipMask = 0x20;
    private const byte AttributeYFlipMask = 0x40;
    private const byte AttributeBackgroundPriorityMask = 0x80;
    private const int Rgb555BytesPerPixel = 2;

    private readonly byte[] _backgroundColorFifo = new byte[BackgroundFifoCapacity];
    private readonly byte[] _backgroundAttributeFifo = new byte[BackgroundFifoCapacity];
    private readonly CgbObjectLayer _objects = new();
    private byte _fetcherTileId;
    private byte _fetcherTileAttributes;

    public override IPpuEngineState CaptureState() =>
        new CgbPpuEngineState(
            CapturePpuEngineBaseState(),
            [.. _backgroundColorFifo],
            [.. _backgroundAttributeFifo],
            _objects.CaptureState(),
            _fetcherTileId,
            _fetcherTileAttributes
        );

    public override void ValidateState(IPpuEngineState state)
    {
        if (state is not CgbPpuEngineState cgbState)
        {
            throw new ArgumentException(
                "PPU engine state must be for the CGB engine.",
                nameof(state)
            );
        }

        ValidateCgbState(cgbState);
    }

    public override void RestoreState(IPpuEngineState state)
    {
        if (state is not CgbPpuEngineState cgbState)
        {
            throw new ArgumentException(
                "PPU engine state must be for the CGB engine.",
                nameof(state)
            );
        }

        ValidateCgbState(cgbState);
        RestorePpuEngineBaseState(cgbState.Common);
        cgbState.BackgroundColorFifo.CopyTo(_backgroundColorFifo, 0);
        cgbState.BackgroundAttributeFifo.CopyTo(_backgroundAttributeFifo, 0);
        _objects.RestoreState(cgbState.Objects);
        _fetcherTileId = cgbState.FetchedTileId;
        _fetcherTileAttributes = cgbState.FetchedTileAttributes;
    }

    private void ValidateCgbState(CgbPpuEngineState state)
    {
        ValidatePpuEngineBaseState(state.Common);
        _objects.ValidateState(state.Objects);
        ValidateBackgroundFifo(state.BackgroundColorFifo, nameof(state));
        ValidateBackgroundFifo(state.BackgroundAttributeFifo, nameof(state));
    }

    private static void ValidateBackgroundFifo(byte[]? fifo, string parameterName)
    {
        if (fifo is null || fifo.Length != BackgroundFifoCapacity)
        {
            throw new ArgumentException(
                "Background FIFO length must match the engine FIFO capacity.",
                parameterName
            );
        }
    }

    protected override int ObjectPenaltyDots => _objects.PenaltyDots;

    protected override bool RequestsMode2InterruptBeforeVBlank => true;

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
        (inputs.LcdControl & PpuLcdControlRegister.WindowEnableMask) != 0;

    internal override void FetchTileMapEntry(PpuEngineInputs inputs, ushort tileMapAddress)
    {
        _fetcherTileId = inputs.VideoRam.ReadBank(bank: 0, tileMapAddress);
        _fetcherTileAttributes = inputs.VideoRam.ReadBank(bank: 1, tileMapAddress);
    }

    internal override byte ReadTileDataByte(PpuEngineInputs inputs, bool highByte)
    {
        var tileBank = (_fetcherTileAttributes & AttributeTileBankMask) == 0 ? 0 : 1;
        return inputs.VideoRam.ReadBank(tileBank, GetTileDataAddress(inputs, highByte));
    }

    internal override bool TryPushFetchedTileRow()
    {
        if (BgWindowFetcher.BackgroundFifoCount > PpuTileData.TileSizePixels)
        {
            return false;
        }

        var xFlip = (_fetcherTileAttributes & AttributeXFlipMask) != 0;
        for (var pixel = 0; pixel < PpuTileData.TileSizePixels; pixel++)
        {
            PushBackgroundPixel(
                PpuTileData.DecodeColorId(
                    BgWindowFetcher.FetchedTileDataLow,
                    BgWindowFetcher.FetchedTileDataHigh,
                    xFlip ? pixel : 7 - pixel
                ),
                _fetcherTileAttributes
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

        PopBackgroundPixel(out var colorId, out var attributes);
        if (BgWindowFetcher.ShouldDiscardPixel())
        {
            return;
        }

        WriteRgb555Pixel(MixPixel(colorId, attributes, inputs));
        RenderedPixels++;
    }

    internal override void ClearFetchedTileMapEntry()
    {
        _fetcherTileId = 0;
        _fetcherTileAttributes = 0;
    }

    private ushort GetTileDataAddress(PpuEngineInputs inputs, bool highByte)
    {
        var tileLine = BgWindowFetcher.GetFetcherY(LcdYCoordinate) & TileLineMask;
        if ((_fetcherTileAttributes & AttributeYFlipMask) != 0)
        {
            tileLine = PpuTileData.TileSizePixels - 1 - tileLine;
        }

        return BackgroundWindowFetcher.GetBackgroundTileDataAddress(
            inputs,
            _fetcherTileId,
            tileLine,
            highByte
        );
    }

    private ushort MixPixel(
        byte backgroundColorId,
        byte backgroundAttributes,
        PpuEngineInputs inputs
    )
    {
        var objectPixel = _objects.SelectPixel(RenderedPixels, LcdYCoordinate, inputs);
        if (
            objectPixel is null
            || BackgroundCoversObject(
                backgroundColorId,
                backgroundAttributes,
                objectPixel.Value,
                inputs.LcdControl
            )
        )
        {
            return ResolveBackgroundColor(backgroundColorId, backgroundAttributes, inputs);
        }

        return ResolveObjectColor(objectPixel.Value, inputs);
    }

    private static bool BackgroundCoversObject(
        byte backgroundColorId,
        byte backgroundAttributes,
        CgbObjectPixel objectPixel,
        byte lcdControl
    ) =>
        backgroundColorId != 0
        && (lcdControl & PpuLcdControlRegister.BackgroundWindowEnableOrPriorityMask) != 0
        && (
            (backgroundAttributes & AttributeBackgroundPriorityMask) != 0
            || objectPixel.HasBackgroundPriority
        );

    private static ushort ResolveBackgroundColor(
        byte backgroundColorId,
        byte backgroundAttributes,
        PpuEngineInputs inputs
    ) =>
        inputs.BackgroundPaletteRam.ReadRgb555Color(
            backgroundAttributes & AttributePaletteMask,
            backgroundColorId
        );

    private static ushort ResolveObjectColor(CgbObjectPixel objectPixel, PpuEngineInputs inputs) =>
        inputs.ObjectPaletteRam.ReadRgb555Color(objectPixel.PaletteIndex, objectPixel.ColorId);

    private void WriteRgb555Pixel(ushort color)
    {
        var frameOffset =
            ((LcdYCoordinate * PpuGeometry.FrameWidth) + RenderedPixels) * Rgb555BytesPerPixel;
        FrameBuffer[frameOffset] = (byte)color;
        FrameBuffer[frameOffset + 1] = (byte)(color >> 8);
    }

    private void PushBackgroundPixel(byte colorId, byte attributes)
    {
        var writeIndex = BgWindowFetcher.BackgroundFifoWriteIndex;
        _backgroundColorFifo[writeIndex] = colorId;
        _backgroundAttributeFifo[writeIndex] = attributes;
        BgWindowFetcher.CommitBackgroundFifoPush();
    }

    private void PopBackgroundPixel(out byte colorId, out byte attributes)
    {
        var readIndex = BgWindowFetcher.BackgroundFifoReadIndex;
        colorId = _backgroundColorFifo[readIndex];
        attributes = _backgroundAttributeFifo[readIndex];
        BgWindowFetcher.CommitBackgroundFifoPop();
    }
}

internal sealed record CgbPpuEngineState(
    PpuEngineBaseState Common,
    byte[] BackgroundColorFifo,
    byte[] BackgroundAttributeFifo,
    CgbObjectLayerState Objects,
    byte FetchedTileId,
    byte FetchedTileAttributes
) : IPpuEngineState;
