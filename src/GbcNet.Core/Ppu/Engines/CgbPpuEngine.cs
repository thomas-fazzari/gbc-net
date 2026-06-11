namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// CGB LCD engine for LY, STAT mode, CPU video-memory access, and BG/window RGB555 frames.
/// </summary>
internal sealed class CgbPpuEngine : IPpuEngine
{
    private const byte ScrollXLowBitsMask = 0x07;
    private const int TileLineMask = PpuTileData.TileSizePixels - 1;
    private const byte AttributePaletteMask = 0x07;
    private const byte AttributeTileBankMask = 0x08;
    private const byte AttributeXFlipMask = 0x20;
    private const byte AttributeYFlipMask = 0x40;
    private const int Rgb555BytesPerPixel = 2;
    private const int WindowStartupPenaltyDots = 6;
    private const byte MaxVisibleWindowX = 166;
    private const int WindowXScreenOffset = 7;

    private readonly PpuTiming _timing = new();
    private readonly byte[] _frameBuffer = new byte[
        PpuGeometry.FrameWidth * PpuGeometry.FrameHeight * Rgb555BytesPerPixel
    ];
    private readonly byte[] _backgroundColorFifo = new byte[16];
    private readonly byte[] _backgroundAttributeFifo = new byte[16];
    private byte _latchedScrollX;
    private byte _latchedScrollY;
    private int _windowPenaltyDots;
    private int _windowLine;
    private int _activeWindowLine;
    private int _backgroundFifoStart;
    private int _backgroundFifoCount;
    private int _fetcherStepDots;
    private int _fetcherTileX;
    private int _discardedPixels;
    private int _renderedPixels;
    private byte _fetcherTileId;
    private byte _fetcherTileAttributes;
    private byte _fetcherTileDataLow;
    private byte _fetcherTileDataHigh;
    private bool _statInterruptLine;
    private bool _renderingScanline;
    private bool _windowYCondition;
    private bool _windowActiveThisLine;
    private bool _renderCurrentFrame = true;
    private BackgroundFetcherStep _fetcherStep;
    private PixelFetcherSource _fetcherSource;
    private PpuMode _statInterruptMode;

    public byte LcdYCoordinate => _timing.LcdYCoordinate;

    public bool LycEqualsLy { get; private set; } = true;

    public PpuMode StatusMode => _timing.StatusMode;

    public bool IsCpuVideoRamReadBlocked =>
        _timing.IsCpuVideoRamReadBlocked(GetCurrentDrawingEndDots());

    public bool IsCpuVideoRamWriteBlocked =>
        _timing.IsCpuVideoRamWriteBlocked(
            _timing.GetCurrentDrawingStartDots(),
            GetCurrentDrawingEndDots()
        );

    public bool IsCpuObjectAttributeMemoryReadBlocked =>
        _timing.IsCpuObjectAttributeMemoryReadBlocked(GetCurrentDrawingEndDots());

    public bool IsCpuObjectAttributeMemoryWriteBlocked =>
        _timing.IsCpuObjectAttributeMemoryWriteBlocked(
            _timing.GetCurrentDrawingStartDots(),
            GetCurrentDrawingEndDots()
        );

    public PpuEngineTickResult Tick(int tCycles, PpuEngineInputs inputs, bool renderFrame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tCycles);

        if (tCycles == 0)
        {
            return new PpuEngineTickResult(PpuInterruptRequest.None, CompletedFrame: null);
        }

        PpuInterruptRequest requests = RefreshPpuState(inputs, requestInterrupt: true);
        LcdFrame? completedFrame = null;

        int remainingCycles = tCycles;
        while (remainingCycles > 0)
        {
            int scanlineDots = _timing.GetCurrentScanlineDots();
            int drawingStartDots = _timing.GetCurrentDrawingStartDots();
            int drawingEndDots = GetCurrentDrawingEndDots();
            int nextBoundary = _timing.GetNextTimingBoundary(drawingEndDots);
            int elapsedCycles = Math.Min(remainingCycles, nextBoundary - _timing.LineDots);
            if (_timing.IsRenderingInterval(drawingStartDots, drawingEndDots))
            {
                if (_renderCurrentFrame)
                {
                    TickRenderer(elapsedCycles, inputs);
                }
                else
                {
                    TickVideoTimingOnly(inputs);
                }
            }

            _timing.AdvanceDots(elapsedCycles);
            remainingCycles -= elapsedCycles;

            if (_timing.LineDots == scanlineDots)
            {
                PpuEngineTickResult result = AdvanceScanline(inputs, renderFrame);
                requests |= result.Interrupts;
                completedFrame ??= result.CompletedFrame;
                continue;
            }

            requests |= RefreshPpuState(inputs, requestInterrupt: true);
        }

        return new PpuEngineTickResult(requests, completedFrame);
    }

    public PpuInterruptRequest EnableLcd(PpuEngineInputs inputs, bool renderFrame)
    {
        _timing.EnableLcd();
        LatchScroll(inputs);
        RefreshWindowYCondition(inputs);
        ResetRenderer();
        _renderCurrentFrame = renderFrame;
        _statInterruptMode = PpuMode.HBlank;

        bool oldLycEqualsLy = LycEqualsLy;
        RefreshLycEqualsLy(inputs.LcdYCompare);

        if (!ShouldSuppressStableLycInterrupt(oldLycEqualsLy, inputs.StatusInterruptSelect))
        {
            return RefreshStatInterruptLine(
                inputs.StatusInterruptSelect,
                lcdEnabled: true,
                requestInterrupt: true
            );
        }

        RefreshStatInterruptLine(
            inputs.StatusInterruptSelect,
            lcdEnabled: true,
            requestInterrupt: false
        );

        return PpuInterruptRequest.None;
    }

    public void DisableLcd()
    {
        _timing.DisableLcd();
        _latchedScrollX = 0;
        _latchedScrollY = 0;
        _windowLine = 0;
        _activeWindowLine = 0;
        _windowYCondition = false;
        _renderCurrentFrame = true;
        ResetRenderer();
        _statInterruptMode = PpuMode.HBlank;
        _statInterruptLine = false;
    }

    public PpuInterruptRequest WriteStatusInterruptSelect(
        PpuEngineInputs inputs,
        bool lcdEnabled
    ) => RefreshStatInterruptLine(inputs.StatusInterruptSelect, lcdEnabled, requestInterrupt: true);

    public PpuInterruptRequest WriteLycCompare(PpuEngineInputs inputs, bool lcdEnabled)
    {
        if (!lcdEnabled)
        {
            return PpuInterruptRequest.None;
        }

        RefreshLycEqualsLy(inputs.LcdYCompare);
        return RefreshStatInterruptLine(
            inputs.StatusInterruptSelect,
            lcdEnabled: true,
            requestInterrupt: true
        );
    }

    public void SetStatusState(byte value, PpuEngineInputs inputs, bool lcdEnabled)
    {
        LycEqualsLy = (value & PpuStatusRegister.LycEqualsLyMask) != 0;
        _timing.SetStatusState(value);
        _statInterruptMode = StatusMode;
        RefreshStatInterruptLine(inputs.StatusInterruptSelect, lcdEnabled, requestInterrupt: false);
    }

    public void SetLcdYCoordinateState(byte value, PpuEngineInputs inputs, bool lcdEnabled)
    {
        _timing.SetLcdYCoordinateState(value);
        if (LcdYCoordinate < PpuGeometry.VBlankStartLine)
        {
            LatchScroll(inputs);
            RefreshWindowYCondition(inputs);
        }
        else
        {
            _windowPenaltyDots = 0;
        }

        RefreshLycEqualsLy(inputs.LcdYCompare);
        RefreshStatInterruptLine(inputs.StatusInterruptSelect, lcdEnabled, requestInterrupt: false);
    }

    public void SetLycCompareState(PpuEngineInputs inputs, bool lcdEnabled)
    {
        RefreshLycEqualsLy(inputs.LcdYCompare);
        RefreshStatInterruptLine(inputs.StatusInterruptSelect, lcdEnabled, requestInterrupt: false);
    }

    private int GetCurrentDrawingEndDots() =>
        _timing.GetCurrentDrawingEndDots(
            LatchedScrollXLowBits,
            _windowPenaltyDots,
            objectPenaltyDots: 0
        );

    private PpuEngineTickResult AdvanceScanline(PpuEngineInputs inputs, bool renderFrame)
    {
        _timing.AdvanceScanline();
        PpuInterruptRequest requests = PpuInterruptRequest.None;
        LcdFrame? completedFrame = null;

        bool shouldRequestMode2Interrupt =
            !_statInterruptLine
            && (inputs.StatusInterruptSelect & PpuStatusRegister.Mode2InterruptSelectMask) != 0;

        switch (LcdYCoordinate)
        {
            case < PpuGeometry.VBlankStartLine:
                if (LcdYCoordinate == 0)
                {
                    _renderCurrentFrame = renderFrame;
                }

                LatchScroll(inputs);
                RefreshWindowYCondition(inputs);
                ResetRenderer();
                break;
            case PpuGeometry.VBlankStartLine:
                _windowYCondition = false;
                _windowLine = 0;
                ResetRenderer();
                requests |= PpuInterruptRequest.VBlank;
                if (_renderCurrentFrame)
                {
                    completedFrame = new LcdFrame(
                        PpuGeometry.FrameWidth,
                        PpuGeometry.FrameHeight,
                        LcdPixelFormat.Rgb555Le,
                        _frameBuffer
                    );
                }

                break;
        }

        if (shouldRequestMode2Interrupt && LcdYCoordinate is 0 or PpuGeometry.VBlankStartLine)
        {
            requests |= PpuInterruptRequest.Lcd;
        }

        requests |= RefreshPpuState(inputs, requestInterrupt: true);

        return new PpuEngineTickResult(requests, completedFrame);
    }

    private PpuInterruptRequest RefreshPpuState(PpuEngineInputs inputs, bool requestInterrupt)
    {
        _statInterruptMode = _timing.RefreshStatusMode(GetCurrentDrawingEndDots());
        RefreshLycEqualsLy(inputs.LcdYCompare);

        return RefreshStatInterruptLine(
            inputs.StatusInterruptSelect,
            lcdEnabled: true,
            requestInterrupt
        );
    }

    private byte LatchedScrollXLowBits => (byte)(_latchedScrollX & ScrollXLowBitsMask);

    private void LatchScroll(PpuEngineInputs inputs)
    {
        _latchedScrollX = inputs.ScrollX;
        _latchedScrollY = inputs.ScrollY;
    }

    private void TickVideoTimingOnly(PpuEngineInputs inputs)
    {
        if (_renderingScanline)
        {
            return;
        }

        BeginRenderingScanline();
        if (CanStartWindow(inputs))
        {
            StartWindow();
        }
    }

    private void TickRenderer(int dots, PpuEngineInputs inputs)
    {
        if (!_renderingScanline)
        {
            BeginRenderingScanline();
        }

        for (int dot = 0; dot < dots && _renderedPixels < PpuGeometry.FrameWidth; dot++)
        {
            TryStartWindow(inputs);
            AdvanceBackgroundFetcher(inputs);
            TryRenderPixel(inputs);
        }
    }

    private void BeginRenderingScanline()
    {
        ResetRenderer();
        _renderingScanline = true;
    }

    private void ResetRenderer()
    {
        _discardedPixels = 0;
        _renderedPixels = 0;
        _windowPenaltyDots = 0;
        _windowActiveThisLine = false;
        ClearBackgroundFetcher(PixelFetcherSource.Background);
        _renderingScanline = false;
    }

    private void AdvanceBackgroundFetcher(PpuEngineInputs inputs)
    {
        switch (_fetcherStep)
        {
            case BackgroundFetcherStep.GetTile:
                TickGetTile(inputs);
                return;
            case BackgroundFetcherStep.GetTileDataLow:
                TickGetTileDataLow(inputs);
                return;
            case BackgroundFetcherStep.GetTileDataHigh:
                TickGetTileDataHigh(inputs);
                return;
            case BackgroundFetcherStep.Sleep:
                TickSleep();
                return;
            case BackgroundFetcherStep.Push:
                TickPush();
                return;
            default:
                throw new InvalidOperationException("Unknown BG fetcher step.");
        }
    }

    private void TickGetTile(PpuEngineInputs inputs)
    {
        _fetcherStepDots++;
        if (_fetcherStepDots < 2)
        {
            return;
        }

        ushort tileMapAddress = GetTileMapAddress(inputs);
        _fetcherTileId = inputs.VideoRam.ReadBank(bank: 0, tileMapAddress);
        _fetcherTileAttributes = inputs.VideoRam.ReadBank(bank: 1, tileMapAddress);
        MoveToFetcherStep(BackgroundFetcherStep.GetTileDataLow);
    }

    private void TickGetTileDataLow(PpuEngineInputs inputs)
    {
        _fetcherStepDots++;
        if (_fetcherStepDots < 2)
        {
            return;
        }

        _fetcherTileDataLow = ReadTileDataByte(inputs, highByte: false);
        MoveToFetcherStep(BackgroundFetcherStep.GetTileDataHigh);
    }

    private void TickGetTileDataHigh(PpuEngineInputs inputs)
    {
        _fetcherStepDots++;
        if (_fetcherStepDots < 2)
        {
            return;
        }

        _fetcherTileDataHigh = ReadTileDataByte(inputs, highByte: true);
        if (TryPushFetchedTileRow())
        {
            CompleteFetchedTileRow();
            return;
        }

        MoveToFetcherStep(BackgroundFetcherStep.Sleep);
    }

    private byte ReadTileDataByte(PpuEngineInputs inputs, bool highByte)
    {
        int tileBank = (_fetcherTileAttributes & AttributeTileBankMask) == 0 ? 0 : 1;
        return inputs.VideoRam.ReadBank(tileBank, GetTileDataAddress(inputs, highByte));
    }

    private void TickSleep()
    {
        if (TryPushFetchedTileRow())
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

    private void TickPush()
    {
        if (!TryPushFetchedTileRow())
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

    private bool TryPushFetchedTileRow()
    {
        if (_backgroundFifoCount > PpuTileData.TileSizePixels)
        {
            return false;
        }

        bool xFlip = (_fetcherTileAttributes & AttributeXFlipMask) != 0;
        for (int pixel = 0; pixel < PpuTileData.TileSizePixels; pixel++)
        {
            int bit = xFlip ? pixel : 7 - pixel;
            byte colorId = (byte)(
                (((_fetcherTileDataHigh >> bit) & 0x01) << 1)
                | ((_fetcherTileDataLow >> bit) & 0x01)
            );
            PushBackgroundPixel(colorId, _fetcherTileAttributes);
        }

        return true;
    }

    private void TryRenderPixel(PpuEngineInputs inputs)
    {
        if (_backgroundFifoCount == 0 || _renderedPixels == PpuGeometry.FrameWidth)
        {
            return;
        }

        byte colorId = PopBackgroundColorId();
        byte attributes = PopBackgroundAttributes();
        if (_discardedPixels < LatchedScrollXLowBits)
        {
            _discardedPixels++;
            return;
        }

        ushort color = inputs.BackgroundPaletteRam.ReadRgb555Color(
            attributes & AttributePaletteMask,
            colorId
        );
        int frameOffset =
            ((LcdYCoordinate * PpuGeometry.FrameWidth) + _renderedPixels) * Rgb555BytesPerPixel;
        _frameBuffer[frameOffset] = (byte)color;
        _frameBuffer[frameOffset + 1] = (byte)(color >> 8);
        _renderedPixels++;
    }

    private void TryStartWindow(PpuEngineInputs inputs)
    {
        if (
            !CanStartWindow(inputs)
            || _renderedPixels < Math.Max(0, inputs.WindowX - WindowXScreenOffset)
        )
        {
            return;
        }

        StartWindow();
        ClearBackgroundFetcher(PixelFetcherSource.Window);
    }

    private bool CanStartWindow(PpuEngineInputs inputs) =>
        !_windowActiveThisLine
        && _windowYCondition
        && inputs.WindowX <= MaxVisibleWindowX
        && (inputs.LcdControl & PpuLcdControlRegister.WindowEnableMask) != 0;

    private void StartWindow()
    {
        _windowPenaltyDots += WindowStartupPenaltyDots;
        _windowActiveThisLine = true;
        _activeWindowLine = _windowLine;
        _windowLine++;
    }

    private void RefreshWindowYCondition(PpuEngineInputs inputs)
    {
        if (
            (inputs.LcdControl & PpuLcdControlRegister.WindowEnableMask) != 0
            && LcdYCoordinate == inputs.WindowY
        )
        {
            _windowYCondition = true;
        }
    }

    private void ClearBackgroundFetcher(PixelFetcherSource source)
    {
        _backgroundFifoStart = 0;
        _backgroundFifoCount = 0;
        _fetcherStepDots = 0;
        _fetcherTileX = 0;
        _fetcherTileId = 0;
        _fetcherTileAttributes = 0;
        _fetcherTileDataLow = 0;
        _fetcherTileDataHigh = 0;
        _fetcherStep = BackgroundFetcherStep.GetTile;
        _fetcherSource = source;
    }

    private ushort GetTileMapAddress(PpuEngineInputs inputs)
    {
        bool isWindow = _fetcherSource == PixelFetcherSource.Window;

        byte tileMapSelectMask = isWindow
            ? PpuLcdControlRegister.WindowTileMapSelectMask
            : PpuLcdControlRegister.BackgroundTileMapSelectMask;

        ushort tileMapStart =
            (inputs.LcdControl & tileMapSelectMask) == 0
                ? PpuTileData.TileMap0Start
                : PpuTileData.TileMap1Start;

        int tileY = GetFetcherY() / PpuTileData.TileSizePixels;
        int tileX = GetFetcherTileX();

        return (ushort)(tileMapStart + (tileY * PpuTileData.TilesPerMapRow) + tileX);
    }

    private ushort GetTileDataAddress(PpuEngineInputs inputs, bool highByte)
    {
        int tileLine = GetFetcherY() & TileLineMask;
        if ((_fetcherTileAttributes & AttributeYFlipMask) != 0)
        {
            tileLine = PpuTileData.TileSizePixels - 1 - tileLine;
        }

        int byteOffset = (tileLine * PpuTileData.TileRowBytes) + (highByte ? 1 : 0);

        int tileAddress =
            (inputs.LcdControl & PpuLcdControlRegister.BackgroundWindowTileDataSelectMask) == 0
                ? PpuTileData.SignedTileDataBase
                    + ((sbyte)_fetcherTileId * PpuTileData.TileDataBytes)
                : PpuTileData.UnsignedTileDataStart + (_fetcherTileId * PpuTileData.TileDataBytes);

        return (ushort)(tileAddress + byteOffset);
    }

    private int GetFetcherY() =>
        _fetcherSource == PixelFetcherSource.Window
            ? _activeWindowLine
            : (_latchedScrollY + LcdYCoordinate) & 0xFF;

    private int GetFetcherTileX() =>
        _fetcherSource == PixelFetcherSource.Window
            ? _fetcherTileX
            : ((_latchedScrollX / PpuTileData.TileSizePixels) + _fetcherTileX)
                & (PpuTileData.TilesPerMapRow - 1);

    private void PushBackgroundPixel(byte colorId, byte attributes)
    {
        int writeIndex =
            (_backgroundFifoStart + _backgroundFifoCount) % _backgroundColorFifo.Length;
        _backgroundColorFifo[writeIndex] = colorId;
        _backgroundAttributeFifo[writeIndex] = attributes;
        _backgroundFifoCount++;
    }

    private byte PopBackgroundColorId() => _backgroundColorFifo[_backgroundFifoStart];

    private byte PopBackgroundAttributes()
    {
        byte attributes = _backgroundAttributeFifo[_backgroundFifoStart];
        _backgroundFifoStart = (_backgroundFifoStart + 1) % _backgroundColorFifo.Length;
        _backgroundFifoCount--;
        return attributes;
    }

    private void MoveToFetcherStep(BackgroundFetcherStep fetcherStep)
    {
        _fetcherStep = fetcherStep;
        _fetcherStepDots = 0;
    }

    private void RefreshLycEqualsLy(byte lcdYCompare)
    {
        LycEqualsLy = _timing.IsLycCompareActiveOnCurrentDot() && LcdYCoordinate == lcdYCompare;
    }

    private PpuInterruptRequest RefreshStatInterruptLine(
        byte statusInterruptSelect,
        bool lcdEnabled,
        bool requestInterrupt
    )
    {
        bool statInterruptLine = IsStatInterruptLineAsserted(statusInterruptSelect, lcdEnabled);

        bool requestLcdInterrupt = requestInterrupt && !_statInterruptLine && statInterruptLine;

        _statInterruptLine = statInterruptLine;

        return requestLcdInterrupt ? PpuInterruptRequest.Lcd : PpuInterruptRequest.None;
    }

    private bool IsStatInterruptLineAsserted(byte statusInterruptSelect, bool lcdEnabled)
    {
        if (!lcdEnabled)
        {
            return false;
        }

        return (
                statusInterruptSelect & PpuStatusRegister.GetInterruptSelectMask(_statInterruptMode)
            ) != 0
            || (
                LycEqualsLy
                && (statusInterruptSelect & PpuStatusRegister.LycEqualsLyInterruptSelectMask) != 0
            );
    }

    private bool ShouldSuppressStableLycInterrupt(
        bool oldLycEqualsLy,
        byte statusInterruptSelect
    ) =>
        oldLycEqualsLy
        && LycEqualsLy
        && (statusInterruptSelect & PpuStatusRegister.LycEqualsLyInterruptSelectMask) != 0
        && (statusInterruptSelect & PpuStatusRegister.Mode0InterruptSelectMask) == 0;
}
