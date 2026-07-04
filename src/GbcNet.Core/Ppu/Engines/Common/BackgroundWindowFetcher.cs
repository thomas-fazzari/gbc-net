// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu.Engines;

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
