// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu.Engines;

internal readonly record struct BackgroundWindowFetcherState(
    byte LatchedScrollX,
    byte LatchedScrollY,
    int WindowLine,
    int ActiveWindowLine,
    int FetcherStepDots,
    int FetcherTileX,
    int DiscardedPixels,
    bool WindowYCondition,
    bool WindowActiveThisLine,
    BackgroundFetcherStep FetcherStep,
    PixelFetcherSource FetcherSource,
    int WindowPenaltyDots,
    int BackgroundFifoCount,
    int BackgroundFifoReadIndex,
    byte FetchedTileDataLow,
    byte FetchedTileDataHigh
);

/// <summary>
/// Owns BG/window fetch sequencing, window startup state, and background FIFO cursor state.
/// </summary>
internal sealed class BackgroundWindowFetcher
{
    private const byte ScrollXLowBitsMask = 0x07;
    private const int BackgroundFifoCapacity = PpuTileData.TileSizePixels * 2;
    private const int WindowStartupPenaltyDots = 6;
    private const byte MaxVisibleWindowX = 166;
    private const int WindowXScreenOffset = 7;

    private byte _latchedScrollX;
    private byte _latchedScrollY;
    private int _windowLine;
    private int _activeWindowLine;
    private int _fetcherStepDots;
    private int _fetcherTileX;
    private int _discardedPixels;
    private bool _windowYCondition;
    private bool _windowActiveThisLine;
    private BackgroundFetcherStep _fetcherStep;
    private PixelFetcherSource _fetcherSource;

    internal byte LatchedScrollXLowBits => (byte)(_latchedScrollX & ScrollXLowBitsMask);

    internal int WindowPenaltyDots { get; private set; }

    internal int BackgroundFifoCount { get; private set; }

    internal int BackgroundFifoReadIndex { get; private set; }

    internal int BackgroundFifoWriteIndex =>
        (BackgroundFifoReadIndex + BackgroundFifoCount) % BackgroundFifoCapacity;

    internal byte FetchedTileDataLow { get; private set; }

    internal byte FetchedTileDataHigh { get; private set; }

    internal BackgroundWindowFetcherState CaptureState() =>
        new(
            _latchedScrollX,
            _latchedScrollY,
            _windowLine,
            _activeWindowLine,
            _fetcherStepDots,
            _fetcherTileX,
            _discardedPixels,
            _windowYCondition,
            _windowActiveThisLine,
            _fetcherStep,
            _fetcherSource,
            WindowPenaltyDots,
            BackgroundFifoCount,
            BackgroundFifoReadIndex,
            FetchedTileDataLow,
            FetchedTileDataHigh
        );

    internal static void ValidateState(BackgroundWindowFetcherState state)
    {
        if (state.WindowLine is < 0 or > PpuGeometry.FrameHeight)
        {
            throw new ArgumentException(
                "Window line must be within the visible frame.",
                nameof(state)
            );
        }

        if (state.ActiveWindowLine is < 0 or >= PpuGeometry.FrameHeight)
        {
            throw new ArgumentException(
                "Active window line must be within the visible frame.",
                nameof(state)
            );
        }

        if (state.FetcherStepDots is < 0 or >= 2)
        {
            throw new ArgumentException(
                "Fetcher step dots must be within a two-dot fetch step.",
                nameof(state)
            );
        }

        if (state.FetcherTileX is < 0 or >= PpuTileData.TilesPerMapRow)
        {
            throw new ArgumentException(
                "Fetcher tile X must be within a tile map row.",
                nameof(state)
            );
        }

        if (state.DiscardedPixels is < 0 or > ScrollXLowBitsMask)
        {
            throw new ArgumentException(
                "Discarded pixels must be within the scroll offset.",
                nameof(state)
            );
        }

        if (state.WindowPenaltyDots is not (0 or WindowStartupPenaltyDots))
        {
            throw new ArgumentException("Window penalty dots are invalid.", nameof(state));
        }

        if (state.BackgroundFifoCount is < 0 or > BackgroundFifoCapacity)
        {
            throw new ArgumentException(
                "Background FIFO count must be within its capacity.",
                nameof(state)
            );
        }

        if (state.BackgroundFifoReadIndex is < 0 or >= BackgroundFifoCapacity)
        {
            throw new ArgumentException(
                "Background FIFO read index must be within its capacity.",
                nameof(state)
            );
        }

        if (
            state.FetcherStep
            is not (
                BackgroundFetcherStep.GetTile
                or BackgroundFetcherStep.GetTileDataLow
                or BackgroundFetcherStep.GetTileDataHigh
                or BackgroundFetcherStep.Sleep
                or BackgroundFetcherStep.Push
            )
        )
        {
            throw new ArgumentException("Fetcher step is invalid.", nameof(state));
        }

        if (state.FetcherSource is not (PixelFetcherSource.Background or PixelFetcherSource.Window))
        {
            throw new ArgumentException("Fetcher source is invalid.", nameof(state));
        }
    }

    internal void RestoreState(BackgroundWindowFetcherState state)
    {
        ValidateState(state);
        _latchedScrollX = state.LatchedScrollX;
        _latchedScrollY = state.LatchedScrollY;
        _windowLine = state.WindowLine;
        _activeWindowLine = state.ActiveWindowLine;
        _fetcherStepDots = state.FetcherStepDots;
        _fetcherTileX = state.FetcherTileX;
        _discardedPixels = state.DiscardedPixels;
        _windowYCondition = state.WindowYCondition;
        _windowActiveThisLine = state.WindowActiveThisLine;
        _fetcherStep = state.FetcherStep;
        _fetcherSource = state.FetcherSource;
        WindowPenaltyDots = state.WindowPenaltyDots;
        BackgroundFifoCount = state.BackgroundFifoCount;
        BackgroundFifoReadIndex = state.BackgroundFifoReadIndex;
        FetchedTileDataLow = state.FetchedTileDataLow;
        FetchedTileDataHigh = state.FetchedTileDataHigh;
    }

    internal static ushort GetBackgroundTileDataAddress(
        PpuEngineInputs inputs,
        byte tileId,
        int tileLine,
        bool highByte
    )
    {
        var byteOffset = (tileLine * PpuTileData.TileRowBytes) + (highByte ? 1 : 0);

        var tileAddress =
            (inputs.LcdControl & PpuLcdControlRegister.BackgroundWindowTileDataSelectMask) == 0
                ? PpuTileData.SignedTileDataBase + ((sbyte)tileId * PpuTileData.TileDataBytes)
                : PpuTileData.UnsignedTileDataStart + (tileId * PpuTileData.TileDataBytes);

        return (ushort)(tileAddress + byteOffset);
    }

    internal int GetFetcherY(byte lcdYCoordinate) =>
        _fetcherSource == PixelFetcherSource.Window
            ? _activeWindowLine
            : (_latchedScrollY + lcdYCoordinate) & 0xFF;

    internal void LatchScroll(PpuEngineInputs inputs)
    {
        _latchedScrollX = inputs.ScrollX;
        _latchedScrollY = inputs.ScrollY;
    }

    internal void RefreshWindowYCondition(PpuEngineInputs inputs, byte lcdYCoordinate)
    {
        if (
            (inputs.LcdControl & PpuLcdControlRegister.WindowEnableMask) != 0
            && lcdYCoordinate == inputs.WindowY
        )
        {
            _windowYCondition = true;
        }
    }

    internal void ResetForLcdDisable()
    {
        _latchedScrollX = 0;
        _latchedScrollY = 0;
        _windowLine = 0;
        _activeWindowLine = 0;
        _windowYCondition = false;
    }

    internal void ResetForVBlank()
    {
        _windowYCondition = false;
        _windowLine = 0;
    }

    internal void ResetRenderer(PpuEngineBase engine)
    {
        _discardedPixels = 0;
        WindowPenaltyDots = 0;
        _windowActiveThisLine = false;
        ClearBackgroundFetcher(PixelFetcherSource.Background, engine);
    }

    internal void ClearWindowPenalty()
    {
        WindowPenaltyDots = 0;
    }

    internal void Advance(PpuEngineInputs inputs, byte lcdYCoordinate, PpuEngineBase engine)
    {
        switch (_fetcherStep)
        {
            case BackgroundFetcherStep.GetTile:
                TickGetTile(inputs, lcdYCoordinate, engine);
                return;
            case BackgroundFetcherStep.GetTileDataLow:
                TickGetTileDataLow(inputs, engine);
                return;
            case BackgroundFetcherStep.GetTileDataHigh:
                TickGetTileDataHigh(inputs, engine);
                return;
            case BackgroundFetcherStep.Sleep:
                TickSleep(engine);
                return;
            case BackgroundFetcherStep.Push:
                TickPush(engine);
                return;
            default:
                throw new InvalidOperationException("Unknown BG fetcher step.");
        }
    }

    internal void TryStartWindow(PpuEngineInputs inputs, int renderedPixels, PpuEngineBase engine)
    {
        if (
            !CanStartWindow(inputs, engine)
            || renderedPixels < Math.Max(0, inputs.WindowX - WindowXScreenOffset)
        )
        {
            return;
        }

        StartWindow();
        ClearBackgroundFetcher(PixelFetcherSource.Window, engine);
    }

    internal void TryStartWindowForTimingOnly(PpuEngineInputs inputs, PpuEngineBase engine)
    {
        if (CanStartWindow(inputs, engine))
        {
            StartWindow();
        }
    }

    internal bool ShouldDiscardPixel()
    {
        if (_discardedPixels >= LatchedScrollXLowBits)
        {
            return false;
        }

        _discardedPixels++;
        return true;
    }

    internal void CommitBackgroundFifoPush()
    {
        BackgroundFifoCount++;
    }

    internal void CommitBackgroundFifoPop()
    {
        BackgroundFifoReadIndex = (BackgroundFifoReadIndex + 1) % BackgroundFifoCapacity;
        BackgroundFifoCount--;
    }

    private void TickGetTile(PpuEngineInputs inputs, byte lcdYCoordinate, PpuEngineBase engine)
    {
        _fetcherStepDots++;
        if (_fetcherStepDots < 2)
        {
            return;
        }

        engine.FetchTileMapEntry(inputs, GetTileMapAddress(inputs, lcdYCoordinate));
        MoveToFetcherStep(BackgroundFetcherStep.GetTileDataLow);
    }

    private void TickGetTileDataLow(PpuEngineInputs inputs, PpuEngineBase engine)
    {
        _fetcherStepDots++;
        if (_fetcherStepDots < 2)
        {
            return;
        }

        FetchedTileDataLow = engine.ReadTileDataByte(inputs, highByte: false);
        MoveToFetcherStep(BackgroundFetcherStep.GetTileDataHigh);
    }

    private void TickGetTileDataHigh(PpuEngineInputs inputs, PpuEngineBase engine)
    {
        _fetcherStepDots++;
        if (_fetcherStepDots < 2)
        {
            return;
        }

        FetchedTileDataHigh = engine.ReadTileDataByte(inputs, highByte: true);
        if (engine.TryPushFetchedTileRow())
        {
            CompleteFetchedTileRow();
            return;
        }

        MoveToFetcherStep(BackgroundFetcherStep.Sleep);
    }

    private void TickSleep(PpuEngineBase engine)
    {
        if (engine.TryPushFetchedTileRow())
        {
            CompleteFetchedTileRow();
            return;
        }

        _fetcherStepDots++;
        if (_fetcherStepDots == 2)
        {
            MoveToFetcherStep(BackgroundFetcherStep.Push);
        }
    }

    private void TickPush(PpuEngineBase engine)
    {
        if (!engine.TryPushFetchedTileRow())
        {
            return;
        }

        CompleteFetchedTileRow();
    }

    private void CompleteFetchedTileRow()
    {
        _fetcherTileX = (_fetcherTileX + 1) & (PpuTileData.TilesPerMapRow - 1);
        MoveToFetcherStep(BackgroundFetcherStep.GetTile);
    }

    private ushort GetTileMapAddress(PpuEngineInputs inputs, byte lcdYCoordinate)
    {
        var isWindow = _fetcherSource == PixelFetcherSource.Window;

        var tileMapSelectMask = isWindow
            ? PpuLcdControlRegister.WindowTileMapSelectMask
            : PpuLcdControlRegister.BackgroundTileMapSelectMask;

        var tileMapStart =
            (inputs.LcdControl & tileMapSelectMask) == 0
                ? PpuTileData.TileMap0Start
                : PpuTileData.TileMap1Start;

        var tileY = GetFetcherY(lcdYCoordinate) / PpuTileData.TileSizePixels;
        var tileX = GetFetcherTileX();

        return (ushort)(tileMapStart + (tileY * PpuTileData.TilesPerMapRow) + tileX);
    }

    private bool CanStartWindow(PpuEngineInputs inputs, PpuEngineBase engine) =>
        !_windowActiveThisLine
        && _windowYCondition
        && inputs.WindowX <= MaxVisibleWindowX
        && engine.IsWindowEnabled(inputs);

    private void StartWindow()
    {
        WindowPenaltyDots += WindowStartupPenaltyDots;
        _windowActiveThisLine = true;
        _activeWindowLine = _windowLine;
        _windowLine++;
    }

    private void ClearBackgroundFetcher(PixelFetcherSource source, PpuEngineBase engine)
    {
        BackgroundFifoReadIndex = 0;
        BackgroundFifoCount = 0;
        _fetcherStepDots = 0;
        _fetcherTileX = 0;
        FetchedTileDataLow = 0;
        FetchedTileDataHigh = 0;
        engine.ClearFetchedTileMapEntry();
        _fetcherStep = BackgroundFetcherStep.GetTile;
        _fetcherSource = source;
    }

    private int GetFetcherTileX() =>
        _fetcherSource == PixelFetcherSource.Window
            ? _fetcherTileX
            : ((_latchedScrollX / PpuTileData.TileSizePixels) + _fetcherTileX)
                & (PpuTileData.TilesPerMapRow - 1);

    private void MoveToFetcherStep(BackgroundFetcherStep fetcherStep)
    {
        _fetcherStep = fetcherStep;
        _fetcherStepDots = 0;
    }
}
