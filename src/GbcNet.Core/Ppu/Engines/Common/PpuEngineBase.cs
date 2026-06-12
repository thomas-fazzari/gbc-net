namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// Shared LCD timing, STAT, window, and background fetch sequencing for the current PPU engines.
/// </summary>
internal abstract class PpuEngineBase(int frameBufferBytesPerPixel, LcdPixelFormat framePixelFormat)
    : IPpuEngine
{
    private const byte ScrollXLowBitsMask = 0x07;
    protected const int TileLineMask = PpuTileData.TileSizePixels - 1;
    protected const int BackgroundFifoCapacity = PpuTileData.TileSizePixels * 2;

    /// <summary>
    /// Window startup resets the BG fetcher and adds six dots to Mode 3.
    /// </summary>
    private const int WindowStartupPenaltyDots = 6;

    /// <summary>
    /// WX values up to 166 can make the Window visible.
    /// </summary>
    private const byte MaxVisibleWindowX = 166;

    /// <summary>
    /// WX stores the Window X coordinate plus seven.
    /// </summary>
    private const int WindowXScreenOffset = 7;

    /// <summary>
    /// CGB raises the LY=144 Mode 2 STAT interrupt one M-cycle before VBlank.
    /// </summary>
    private const int CgbMode2VBlankInterruptLeadDots = 4;

    private byte _latchedScrollX;
    private byte _latchedScrollY;
    private int _windowPenaltyDots;
    private int _windowLine;
    private int _activeWindowLine;
    private int _fetcherStepDots;
    private int _fetcherTileX;
    private int _discardedPixels;
    private bool _statInterruptLine;
    private bool _renderingScanline;
    private bool _windowYCondition;
    private bool _windowActiveThisLine;
    private bool _renderCurrentFrame = true;
    private BackgroundFetcherStep _fetcherStep;
    private PixelFetcherSource _fetcherSource;
    private PpuMode _statInterruptMode;

    public byte LcdYCoordinate => Timing.LcdYCoordinate;

    public bool LycEqualsLy { get; private set; } = true;

    public PpuMode StatusMode => Timing.StatusMode;

    public bool IsCpuVideoRamReadBlocked =>
        Timing.IsCpuVideoRamReadBlocked(GetCurrentDrawingEndDots());

    public bool IsCpuVideoRamWriteBlocked =>
        Timing.IsCpuVideoRamWriteBlocked(
            Timing.GetCurrentDrawingStartDots(),
            GetCurrentDrawingEndDots()
        );

    public bool IsCpuObjectAttributeMemoryReadBlocked =>
        Timing.IsCpuObjectAttributeMemoryReadBlocked(GetCurrentDrawingEndDots());

    public bool IsCpuObjectAttributeMemoryWriteBlocked =>
        Timing.IsCpuObjectAttributeMemoryWriteBlocked(
            Timing.GetCurrentDrawingStartDots(),
            GetCurrentDrawingEndDots()
        );

    protected byte[] FrameBuffer { get; } =
        new byte[PpuGeometry.FrameWidth * PpuGeometry.FrameHeight * frameBufferBytesPerPixel];

    protected PpuTiming Timing { get; } = new();

    protected byte LatchedScrollXLowBits => (byte)(_latchedScrollX & ScrollXLowBitsMask);

    protected int RenderedPixels { get; private set; }

    protected int BackgroundFifoCount { get; private set; }

    protected int BackgroundFifoReadIndex { get; private set; }

    protected int BackgroundFifoWriteIndex =>
        (BackgroundFifoReadIndex + BackgroundFifoCount) % BackgroundFifoCapacity;

    protected byte FetchedTileDataLow { get; private set; }

    protected byte FetchedTileDataHigh { get; private set; }

    protected abstract int ObjectPenaltyDots { get; }

    protected virtual bool RequestsMode2InterruptBeforeVBlank => false;

    public PpuEngineTickResult Tick(int tCycles, PpuEngineInputs inputs, bool renderFrame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tCycles);

        if (tCycles == 0)
        {
            return new PpuEngineTickResult(
                PpuInterruptRequest.None,
                CompletedFrame: null,
                EnteredVisibleHBlank: false
            );
        }

        var requests = RefreshPpuState(inputs, requestInterrupt: true);
        LcdFrame? completedFrame = null;
        var enteredVisibleHBlank = false;
        var remainingCycles = tCycles;
        while (remainingCycles > 0)
        {
            var scanlineDots = Timing.GetCurrentScanlineDots();
            var drawingStartDots = Timing.GetCurrentDrawingStartDots();
            var drawingEndDots = GetCurrentDrawingEndDots();
            var nextBoundary = Timing.GetNextTimingBoundary(drawingEndDots);
            var cgbMode2VBlankInterruptDots = scanlineDots - CgbMode2VBlankInterruptLeadDots;
            if (
                RequestsMode2InterruptBeforeVBlank
                && LcdYCoordinate == PpuGeometry.VBlankStartLine - 1
                && (inputs.StatusInterruptSelect & PpuStatusRegister.Mode2InterruptSelectMask) != 0
                && !_statInterruptLine
                && Timing.LineDots < cgbMode2VBlankInterruptDots
                && nextBoundary > cgbMode2VBlankInterruptDots
            )
            {
                nextBoundary = cgbMode2VBlankInterruptDots;
            }

            var elapsedCycles = Math.Min(remainingCycles, nextBoundary - Timing.LineDots);
            if (Timing.IsRenderingInterval(drawingStartDots, drawingEndDots))
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

            Timing.AdvanceDots(elapsedCycles);
            remainingCycles -= elapsedCycles;

            if (Timing.LineDots == scanlineDots)
            {
                var result = AdvanceScanline(inputs, renderFrame);
                requests |= result.Interrupts;
                completedFrame ??= result.CompletedFrame;
                enteredVisibleHBlank |= result.EnteredVisibleHBlank;
                continue;
            }

            var previousMode = StatusMode;
            var previousLcdYCoordinate = LcdYCoordinate;
            requests |= RefreshPpuState(inputs, requestInterrupt: true);
            if (
                RequestsMode2InterruptBeforeVBlank
                && LcdYCoordinate == PpuGeometry.VBlankStartLine - 1
                && Timing.LineDots == cgbMode2VBlankInterruptDots
                && (inputs.StatusInterruptSelect & PpuStatusRegister.Mode2InterruptSelectMask) != 0
                && !_statInterruptLine
            )
            {
                requests |= PpuInterruptRequest.Lcd;
            }

            enteredVisibleHBlank |=
                previousLcdYCoordinate < PpuGeometry.VBlankStartLine
                && previousMode is PpuMode.Drawing
                && StatusMode is PpuMode.HBlank;
        }

        return new PpuEngineTickResult(requests, completedFrame, enteredVisibleHBlank);
    }

    public PpuInterruptRequest EnableLcd(PpuEngineInputs inputs, bool renderFrame)
    {
        Timing.EnableLcd();
        LatchScroll(inputs);
        RefreshWindowYCondition(inputs);
        ClearObjects();
        ResetRenderer();
        _renderCurrentFrame = renderFrame;
        _statInterruptMode = PpuMode.HBlank;

        var oldLycEqualsLy = LycEqualsLy;
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
        Timing.DisableLcd();
        _latchedScrollX = 0;
        _latchedScrollY = 0;
        _windowLine = 0;
        _activeWindowLine = 0;
        _windowYCondition = false;
        _renderCurrentFrame = true;
        ClearObjects();
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
        Timing.SetStatusState(value);
        _statInterruptMode = StatusMode;
        RefreshStatInterruptLine(inputs.StatusInterruptSelect, lcdEnabled, requestInterrupt: false);
    }

    public void SetLcdYCoordinateState(byte value, PpuEngineInputs inputs, bool lcdEnabled)
    {
        Timing.SetLcdYCoordinateState(value);
        if (LcdYCoordinate < PpuGeometry.VBlankStartLine)
        {
            LatchScroll(inputs);
            RefreshWindowYCondition(inputs);
            ClearObjects();
        }
        else
        {
            _windowPenaltyDots = 0;
            ClearObjects();
        }

        RefreshLycEqualsLy(inputs.LcdYCompare);
        RefreshStatInterruptLine(inputs.StatusInterruptSelect, lcdEnabled, requestInterrupt: false);
    }

    public void SetLycCompareState(PpuEngineInputs inputs, bool lcdEnabled)
    {
        RefreshLycEqualsLy(inputs.LcdYCompare);
        RefreshStatInterruptLine(inputs.StatusInterruptSelect, lcdEnabled, requestInterrupt: false);
    }

    protected abstract void EnsureObjectsSelected(PpuEngineInputs inputs);

    protected abstract void ClearObjects();

    protected abstract bool IsWindowEnabled(PpuEngineInputs inputs);

    protected abstract void FetchTileMapEntry(PpuEngineInputs inputs, ushort tileMapAddress);

    protected abstract byte ReadTileDataByte(PpuEngineInputs inputs, bool highByte);

    protected abstract bool TryPushFetchedTileRow();

    protected abstract void TryRenderPixel(PpuEngineInputs inputs);

    protected abstract void ClearFetchedTileMapEntry();

    protected ushort GetTileMapAddress(PpuEngineInputs inputs)
    {
        var isWindow = _fetcherSource == PixelFetcherSource.Window;

        var tileMapSelectMask = isWindow
            ? PpuLcdControlRegister.WindowTileMapSelectMask
            : PpuLcdControlRegister.BackgroundTileMapSelectMask;

        var tileMapStart =
            (inputs.LcdControl & tileMapSelectMask) == 0
                ? PpuTileData.TileMap0Start
                : PpuTileData.TileMap1Start;

        var tileY = GetFetcherY() / PpuTileData.TileSizePixels;
        var tileX = GetFetcherTileX();

        return (ushort)(tileMapStart + (tileY * PpuTileData.TilesPerMapRow) + tileX);
    }

    protected static ushort GetBackgroundTileDataAddress(
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

    protected int GetFetcherY() =>
        _fetcherSource == PixelFetcherSource.Window
            ? _activeWindowLine
            : (_latchedScrollY + LcdYCoordinate) & 0xFF;

    protected void CommitBackgroundFifoPush()
    {
        BackgroundFifoCount++;
    }

    protected void CommitBackgroundFifoPop()
    {
        BackgroundFifoReadIndex = (BackgroundFifoReadIndex + 1) % BackgroundFifoCapacity;
        BackgroundFifoCount--;
    }

    protected bool ShouldDiscardPixel()
    {
        if (_discardedPixels >= LatchedScrollXLowBits)
        {
            return false;
        }

        _discardedPixels++;
        return true;
    }

    protected void AdvanceRenderedPixel()
    {
        RenderedPixels++;
    }

    private LcdFrame CreateCompletedFrame() =>
        new(PpuGeometry.FrameWidth, PpuGeometry.FrameHeight, framePixelFormat, FrameBuffer);

    private int GetCurrentDrawingEndDots() =>
        Timing.GetCurrentDrawingEndDots(
            LatchedScrollXLowBits,
            _windowPenaltyDots,
            ObjectPenaltyDots
        );

    private PpuEngineTickResult AdvanceScanline(PpuEngineInputs inputs, bool renderFrame)
    {
        Timing.AdvanceScanline();
        var requests = PpuInterruptRequest.None;
        LcdFrame? completedFrame = null;

        var shouldRequestMode2Interrupt =
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
                ClearObjects();
                ResetRenderer();
                break;

            case PpuGeometry.VBlankStartLine:
                ClearObjects();
                _windowYCondition = false;
                _windowLine = 0;
                ResetRenderer();
                requests |= PpuInterruptRequest.VBlank;
                if (_renderCurrentFrame)
                {
                    completedFrame = CreateCompletedFrame();
                }
                break;
        }

        if (shouldRequestMode2Interrupt && LcdYCoordinate is 0 or PpuGeometry.VBlankStartLine)
        {
            requests |= PpuInterruptRequest.Lcd;
        }

        requests |= RefreshPpuState(inputs, requestInterrupt: true);

        return new PpuEngineTickResult(requests, completedFrame, EnteredVisibleHBlank: false);
    }

    private PpuInterruptRequest RefreshPpuState(PpuEngineInputs inputs, bool requestInterrupt)
    {
        EnsureObjectsSelected(inputs);
        _statInterruptMode = Timing.RefreshStatusMode(GetCurrentDrawingEndDots());

        RefreshLycEqualsLy(inputs.LcdYCompare);
        return RefreshStatInterruptLine(
            inputs.StatusInterruptSelect,
            lcdEnabled: true,
            requestInterrupt
        );
    }

    private void LatchScroll(PpuEngineInputs inputs)
    {
        _latchedScrollX = inputs.ScrollX;
        _latchedScrollY = inputs.ScrollY;
    }

    private void TickVideoTimingOnly(PpuEngineInputs inputs)
    {
        EnsureObjectsSelected(inputs);

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
        EnsureObjectsSelected(inputs);

        if (!_renderingScanline)
        {
            BeginRenderingScanline();
        }

        for (var dot = 0; dot < dots && RenderedPixels < PpuGeometry.FrameWidth; dot++)
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
        RenderedPixels = 0;
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

        FetchTileMapEntry(inputs, GetTileMapAddress(inputs));
        MoveToFetcherStep(BackgroundFetcherStep.GetTileDataLow);
    }

    private void TickGetTileDataLow(PpuEngineInputs inputs)
    {
        _fetcherStepDots++;
        if (_fetcherStepDots < 2)
        {
            return;
        }

        FetchedTileDataLow = ReadTileDataByte(inputs, highByte: false);
        MoveToFetcherStep(BackgroundFetcherStep.GetTileDataHigh);
    }

    private void TickGetTileDataHigh(PpuEngineInputs inputs)
    {
        _fetcherStepDots++;
        if (_fetcherStepDots < 2)
        {
            return;
        }

        FetchedTileDataHigh = ReadTileDataByte(inputs, highByte: true);
        if (TryPushFetchedTileRow())
        {
            CompleteFetchedTileRow();
            return;
        }

        MoveToFetcherStep(BackgroundFetcherStep.Sleep);
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

    private void TryStartWindow(PpuEngineInputs inputs)
    {
        if (
            !CanStartWindow(inputs)
            || RenderedPixels < Math.Max(0, inputs.WindowX - WindowXScreenOffset)
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
        && IsWindowEnabled(inputs);

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
        BackgroundFifoReadIndex = 0;
        BackgroundFifoCount = 0;
        _fetcherStepDots = 0;
        _fetcherTileX = 0;
        FetchedTileDataLow = 0;
        FetchedTileDataHigh = 0;
        ClearFetchedTileMapEntry();
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

    private void RefreshLycEqualsLy(byte lcdYCompare)
    {
        LycEqualsLy = Timing.IsLycCompareActiveOnCurrentDot() && LcdYCoordinate == lcdYCompare;
    }

    private PpuInterruptRequest RefreshStatInterruptLine(
        byte statusInterruptSelect,
        bool lcdEnabled,
        bool requestInterrupt
    )
    {
        var statInterruptLine = IsStatInterruptLineAsserted(statusInterruptSelect, lcdEnabled);

        var requestLcdInterrupt = requestInterrupt && !_statInterruptLine && statInterruptLine;

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
