namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// DMG LCD engine for LY, STAT mode, STAT interrupt line, CPU video-memory access, and frames.
/// </summary>
internal sealed class DmgPpuEngine : IPpuEngine
{
    /// <summary>
    /// The first scanline after enabling DMG LCD is four dots shorter.
    /// </summary>
    private const int FirstScanlineAfterLcdEnableDots = 452;

    /// <summary>
    /// Mode 2 lasts 80 dots on visible scanlines.
    /// </summary>
    private const int OamScanDots = 80;

    /// <summary>
    /// First DMG scanline after LCD enable enters HBlank after the minimum Mode 3 duration.
    /// </summary>
    private const int FirstScanlineAfterLcdEnableDrawingEndDots = OamScanDots + 172;

    /// <summary>
    /// STAT mode changes lag the start of normal visible scanlines by four dots on DMG.
    /// </summary>
    private const int NormalScanlineStatusModeDelayDots = 4;

    /// <summary>
    /// Normal visible scanlines enter Mode 3 after OAM scan plus the STAT mode delay.
    /// </summary>
    private const int NormalScanlineDrawingStartDots =
        OamScanDots + NormalScanlineStatusModeDelayDots;

    /// <summary>
    /// Minimal DMG Mode 3 end point for normal visible scanlines.
    /// </summary>
    private const int NormalScanlineDrawingEndDots = OamScanDots + 176;

    /// <summary>
    /// SCX low bits are latched at visible scanline start for the initial pixel shift.
    /// </summary>
    private const byte ScrollXLowBitsMask = 0x07;

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

    private int _lineDots;
    private readonly byte[] _frameBuffer = new byte[
        PpuGeometry.FrameWidth * PpuGeometry.FrameHeight
    ];
    private readonly byte[] _backgroundFifo = new byte[16];
    private readonly DmgObjectLayer _objects = new();
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
    private byte _fetcherTileDataLow;
    private byte _fetcherTileDataHigh;
    private bool _statInterruptLine;
    private bool _firstScanlineAfterLcdEnable;
    private bool _renderingScanline;
    private bool _windowYCondition;
    private bool _windowActiveThisLine;
    private BackgroundFetcherStep _fetcherStep;
    private PixelFetcherSource _fetcherSource;

    /// <summary>
    /// DMG LY register value advanced by the scanline sequencer.
    /// </summary>
    public byte LcdYCoordinate { get; private set; }

    /// <summary>
    /// DMG STAT coincidence flag derived from LY and LYC.
    /// </summary>
    public bool LycEqualsLy { get; private set; } = true;

    /// <summary>
    /// CPU-visible DMG STAT mode, including the four-dot visible-line startup delay.
    /// </summary>
    public PpuMode StatusMode { get; private set; }

    private PpuMode _statInterruptMode;

    /// <summary>
    /// Indicates that DMG VRAM reads are blocked by Mode 3 drawing ownership.
    /// </summary>
    public bool IsCpuVideoRamReadBlocked =>
        LcdYCoordinate < PpuGeometry.VBlankStartLine
        && _lineDots >= OamScanDots
        && _lineDots < GetCurrentDrawingEndDots();

    /// <summary>
    /// Indicates that DMG VRAM writes are blocked by Mode 3 drawing ownership.
    /// </summary>
    public bool IsCpuVideoRamWriteBlocked =>
        LcdYCoordinate < PpuGeometry.VBlankStartLine
        && _lineDots >= GetCurrentDrawingStartDots()
        && _lineDots < GetCurrentDrawingEndDots();

    /// <summary>
    /// Indicates that DMG OAM reads are blocked by OAM scan or drawing ownership.
    /// </summary>
    public bool IsCpuObjectAttributeMemoryReadBlocked =>
        LcdYCoordinate < PpuGeometry.VBlankStartLine
        && (
            _firstScanlineAfterLcdEnable
                ? IsCpuVideoRamReadBlocked
                : _lineDots < GetCurrentDrawingEndDots()
        );

    /// <summary>
    /// Indicates that DMG OAM writes are blocked by OAM scan or drawing ownership.
    /// </summary>
    public bool IsCpuObjectAttributeMemoryWriteBlocked =>
        LcdYCoordinate < PpuGeometry.VBlankStartLine
        && (
            _firstScanlineAfterLcdEnable
                ? IsCpuVideoRamWriteBlocked
                : _lineDots is >= NormalScanlineStatusModeDelayDots and < OamScanDots
                    || (
                        _lineDots >= NormalScanlineDrawingStartDots
                        && _lineDots < GetCurrentDrawingEndDots()
                    )
        );

    /// <summary>
    /// Advances DMG LCD timing, renderer FIFO state, STAT interrupts, and VBlank frame completion.
    /// </summary>
    public PpuEngineTickResult Tick(int tCycles, PpuEngineInputs inputs)
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
            int scanlineDots = GetCurrentScanlineDots();
            int drawingStartDots = GetCurrentDrawingStartDots();
            int drawingEndDots = GetCurrentDrawingEndDots();
            int nextBoundary = GetNextTimingBoundary(
                scanlineDots,
                drawingStartDots,
                drawingEndDots
            );
            int elapsedCycles = Math.Min(remainingCycles, nextBoundary - _lineDots);
            if (IsRenderingInterval(drawingStartDots, drawingEndDots))
            {
                TickRenderer(elapsedCycles, inputs);
            }

            _lineDots += elapsedCycles;
            remainingCycles -= elapsedCycles;

            if (_lineDots == scanlineDots)
            {
                PpuEngineTickResult result = AdvanceScanline(inputs);
                requests |= result.Interrupts;
                completedFrame ??= result.CompletedFrame;
                continue;
            }

            requests |= RefreshPpuState(inputs, requestInterrupt: true);
        }

        return new PpuEngineTickResult(requests, completedFrame);
    }

    /// <summary>
    /// Starts DMG LCD timing at the shortened first scanline after LCD enable.
    /// </summary>
    public PpuInterruptRequest EnableLcd(PpuEngineInputs inputs)
    {
        _lineDots = 0;
        LatchScroll(inputs);
        RefreshWindowYCondition(inputs);
        _objects.Clear();
        ResetRenderer();
        _firstScanlineAfterLcdEnable = true;
        StatusMode = PpuMode.HBlank;
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

    /// <summary>
    /// Stops DMG LCD timing and resets LY, STAT mode, renderer, and STAT interrupt line state.
    /// </summary>
    public void DisableLcd()
    {
        _lineDots = 0;
        _latchedScrollX = 0;
        _latchedScrollY = 0;
        _windowLine = 0;
        _activeWindowLine = 0;
        _windowYCondition = false;
        _objects.Clear();
        ResetRenderer();
        LcdYCoordinate = 0;
        StatusMode = PpuMode.HBlank;
        _statInterruptMode = PpuMode.HBlank;
        _firstScanlineAfterLcdEnable = false;
        _statInterruptLine = false;
    }

    /// <summary>
    /// Applies DMG STAT interrupt line edge behavior after STAT interrupt select bits change.
    /// </summary>
    public PpuInterruptRequest WriteStatusInterruptSelect(
        PpuEngineInputs inputs,
        bool lcdEnabled
    ) => RefreshStatInterruptLine(inputs.StatusInterruptSelect, lcdEnabled, requestInterrupt: true);

    /// <summary>
    /// Applies DMG LYC write comparison and STAT interrupt line edge behavior.
    /// </summary>
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

    /// <summary>
    /// Seeds DMG STAT state when constructing post-boot or saved hardware state.
    /// </summary>
    public void SetStatusState(byte value, PpuEngineInputs inputs, bool lcdEnabled)
    {
        LycEqualsLy = (value & PpuStatusRegister.LycEqualsLyMask) != 0;
        StatusMode = (PpuMode)(value & PpuStatusRegister.ModeMask);
        _statInterruptMode = StatusMode;
        RefreshStatInterruptLine(inputs.StatusInterruptSelect, lcdEnabled, requestInterrupt: false);
    }

    /// <summary>
    /// Seeds DMG LY state and refreshes scanline-local rendering selectors.
    /// </summary>
    public void SetLcdYCoordinateState(byte value, PpuEngineInputs inputs, bool lcdEnabled)
    {
        LcdYCoordinate = value;
        if (LcdYCoordinate < PpuGeometry.VBlankStartLine)
        {
            LatchScroll(inputs);
            RefreshWindowYCondition(inputs);
            _objects.Clear();
        }
        else
        {
            _objects.Clear();
            _windowPenaltyDots = 0;
        }

        RefreshLycEqualsLy(inputs.LcdYCompare);
        RefreshStatInterruptLine(inputs.StatusInterruptSelect, lcdEnabled, requestInterrupt: false);
    }

    /// <summary>
    /// Seeds DMG LYC comparison state without requesting a CPU-visible STAT edge.
    /// </summary>
    public void SetLycCompareState(PpuEngineInputs inputs, bool lcdEnabled)
    {
        RefreshLycEqualsLy(inputs.LcdYCompare);
        RefreshStatInterruptLine(inputs.StatusInterruptSelect, lcdEnabled, requestInterrupt: false);
    }

    private int GetCurrentScanlineDots() =>
        _firstScanlineAfterLcdEnable ? FirstScanlineAfterLcdEnableDots : PpuGeometry.ScanlineDots;

    private int GetCurrentDrawingEndDots()
    {
        int drawingEndDots = _firstScanlineAfterLcdEnable
            ? FirstScanlineAfterLcdEnableDrawingEndDots
            : NormalScanlineDrawingEndDots;

        return drawingEndDots + LatchedScrollXLowBits + _windowPenaltyDots + _objects.PenaltyDots;
    }

    private int GetNextTimingBoundary(int scanlineDots, int drawingStartDots, int drawingEndDots)
    {
        if (LcdYCoordinate >= PpuGeometry.VBlankStartLine)
        {
            return scanlineDots;
        }

        if (_firstScanlineAfterLcdEnable)
        {
            if (_lineDots < OamScanDots)
            {
                return OamScanDots;
            }

            return _lineDots < drawingEndDots ? drawingEndDots : scanlineDots;
        }

        ReadOnlySpan<int> thresholds =
        [
            NormalScanlineStatusModeDelayDots,
            OamScanDots,
            drawingStartDots,
            drawingEndDots,
            scanlineDots,
        ];

        foreach (int threshold in thresholds)
        {
            if (_lineDots < threshold)
            {
                return threshold;
            }
        }

        return scanlineDots;
    }

    private PpuEngineTickResult AdvanceScanline(PpuEngineInputs inputs)
    {
        _lineDots = 0;
        _firstScanlineAfterLcdEnable = false;
        PpuInterruptRequest requests = PpuInterruptRequest.None;
        LcdFrame? completedFrame = null;

        bool shouldRequestMode2Interrupt =
            !_statInterruptLine
            && (inputs.StatusInterruptSelect & PpuStatusRegister.Mode2InterruptSelectMask) != 0;

        LcdYCoordinate =
            LcdYCoordinate == PpuGeometry.LastScanline ? (byte)0 : (byte)(LcdYCoordinate + 1);
        switch (LcdYCoordinate)
        {
            case < PpuGeometry.VBlankStartLine:
                LatchScroll(inputs);
                RefreshWindowYCondition(inputs);
                _objects.Clear();
                ResetRenderer();
                break;
            case PpuGeometry.VBlankStartLine:
                _objects.Clear();
                _windowYCondition = false;
                _windowLine = 0;
                ResetRenderer();
                requests |= PpuInterruptRequest.VBlank;
                completedFrame = new LcdFrame(
                    PpuGeometry.FrameWidth,
                    PpuGeometry.FrameHeight,
                    LcdPixelFormat.DmgShadeIndex8,
                    _frameBuffer
                );
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
        _objects.EnsureSelected(
            inputs,
            LcdYCoordinate,
            _lineDots >= OamScanDots,
            LatchedScrollXLowBits
        );
        StatusMode = CalculateMode();
        _statInterruptMode = StatusMode;
        RefreshLycEqualsLy(inputs.LcdYCompare);

        return RefreshStatInterruptLine(
            inputs.StatusInterruptSelect,
            lcdEnabled: true,
            requestInterrupt
        );
    }

    private PpuMode CalculateMode()
    {
        if (LcdYCoordinate >= PpuGeometry.VBlankStartLine)
        {
            return PpuMode.VBlank;
        }

        if (!_firstScanlineAfterLcdEnable)
        {
            return _lineDots switch
            {
                < NormalScanlineStatusModeDelayDots => PpuMode.HBlank,
                < NormalScanlineDrawingStartDots => PpuMode.OamScan,
                _ when _lineDots < GetCurrentDrawingEndDots() => PpuMode.Drawing,
                _ => PpuMode.HBlank,
            };
        }

        if (LcdYCoordinate == 0 && _lineDots < OamScanDots)
        {
            return PpuMode.HBlank;
        }

        return _lineDots < GetCurrentDrawingEndDots() ? PpuMode.Drawing : PpuMode.HBlank;
    }

    private int GetCurrentDrawingStartDots() =>
        _firstScanlineAfterLcdEnable ? OamScanDots : NormalScanlineDrawingStartDots;

    private byte LatchedScrollXLowBits => (byte)(_latchedScrollX & ScrollXLowBitsMask);

    private void LatchScroll(PpuEngineInputs inputs)
    {
        _latchedScrollX = inputs.ScrollX;
        _latchedScrollY = inputs.ScrollY;
    }

    private bool IsRenderingInterval(int drawingStartDots, int drawingEndDots) =>
        LcdYCoordinate < PpuGeometry.VBlankStartLine
        && _lineDots >= drawingStartDots
        && _lineDots < drawingEndDots;

    private void TickRenderer(int dots, PpuEngineInputs inputs)
    {
        _objects.EnsureSelected(
            inputs,
            LcdYCoordinate,
            _lineDots >= OamScanDots,
            LatchedScrollXLowBits
        );

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

        _fetcherTileId = inputs.VideoRam.Read(GetTileMapAddress(inputs));
        MoveToFetcherStep(BackgroundFetcherStep.GetTileDataLow);
    }

    private void TickGetTileDataLow(PpuEngineInputs inputs)
    {
        _fetcherStepDots++;
        if (_fetcherStepDots < 2)
        {
            return;
        }

        _fetcherTileDataLow = inputs.VideoRam.Read(GetTileDataAddress(inputs, highByte: false));
        MoveToFetcherStep(BackgroundFetcherStep.GetTileDataHigh);
    }

    private void TickGetTileDataHigh(PpuEngineInputs inputs)
    {
        _fetcherStepDots++;
        if (_fetcherStepDots < 2)
        {
            return;
        }

        _fetcherTileDataHigh = inputs.VideoRam.Read(GetTileDataAddress(inputs, highByte: true));
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

    private bool TryPushFetchedTileRow()
    {
        if (_backgroundFifoCount > PpuTileData.TileSizePixels)
        {
            return false;
        }

        for (int pixel = 0; pixel < PpuTileData.TileSizePixels; pixel++)
        {
            int bit = 7 - pixel;
            byte colorId = (byte)(
                (((_fetcherTileDataHigh >> bit) & 0x01) << 1)
                | ((_fetcherTileDataLow >> bit) & 0x01)
            );
            PushBackgroundPixel(colorId);
        }

        return true;
    }

    private void TryRenderPixel(PpuEngineInputs inputs)
    {
        if (_backgroundFifoCount == 0 || _renderedPixels == PpuGeometry.FrameWidth)
        {
            return;
        }

        byte colorId = PopBackgroundPixel();
        if (_discardedPixels < LatchedScrollXLowBits)
        {
            _discardedPixels++;
            return;
        }

        if ((inputs.LcdControl & PpuLcdControlRegister.BackgroundWindowEnableOrPriorityMask) == 0)
        {
            colorId = 0;
        }

        _frameBuffer[(LcdYCoordinate * PpuGeometry.FrameWidth) + _renderedPixels] = MixPixel(
            colorId,
            inputs
        );
        _renderedPixels++;
    }

    private byte MixPixel(byte backgroundColorId, PpuEngineInputs inputs)
    {
        DmgObjectPixel? objectPixel = _objects.SelectPixel(_renderedPixels, LcdYCoordinate, inputs);

        if (
            objectPixel is null
            || (
                objectPixel.Value.HasBackgroundPriority
                && backgroundColorId != 0
                && (inputs.LcdControl & PpuLcdControlRegister.BackgroundWindowEnableOrPriorityMask)
                    != 0
            )
        )
        {
            return ApplyPalette(backgroundColorId, inputs.BackgroundPalette);
        }

        byte objectPalette = objectPixel.Value.UsesPalette1
            ? inputs.ObjectPalette1
            : inputs.ObjectPalette0;

        return ApplyPalette(objectPixel.Value.ColorId, objectPalette);
    }

    private void TryStartWindow(PpuEngineInputs inputs)
    {
        if (
            _windowActiveThisLine
            || !_windowYCondition
            || inputs.WindowX > MaxVisibleWindowX
            || (
                inputs.LcdControl
                & (
                    PpuLcdControlRegister.BackgroundWindowEnableOrPriorityMask
                    | PpuLcdControlRegister.WindowEnableMask
                )
            )
                != (
                    PpuLcdControlRegister.BackgroundWindowEnableOrPriorityMask
                    | PpuLcdControlRegister.WindowEnableMask
                )
            || _renderedPixels < Math.Max(0, inputs.WindowX - WindowXScreenOffset)
        )
        {
            return;
        }

        _windowPenaltyDots += WindowStartupPenaltyDots;
        _windowActiveThisLine = true;
        _activeWindowLine = _windowLine;
        _windowLine++;
        ClearBackgroundFetcher(PixelFetcherSource.Window);
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
        int tileLine = GetFetcherY() & ScrollXLowBitsMask;
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

    private void PushBackgroundPixel(byte colorId)
    {
        int writeIndex = (_backgroundFifoStart + _backgroundFifoCount) % _backgroundFifo.Length;
        _backgroundFifo[writeIndex] = colorId;
        _backgroundFifoCount++;
    }

    private byte PopBackgroundPixel()
    {
        byte colorId = _backgroundFifo[_backgroundFifoStart];
        _backgroundFifoStart = (_backgroundFifoStart + 1) % _backgroundFifo.Length;
        _backgroundFifoCount--;
        return colorId;
    }

    private static byte ApplyPalette(byte colorId, byte palette) =>
        (byte)((palette >> (colorId * 2)) & 0x03);

    private void MoveToFetcherStep(BackgroundFetcherStep fetcherStep)
    {
        _fetcherStep = fetcherStep;
        _fetcherStepDots = 0;
    }

    private void RefreshLycEqualsLy(byte lcdYCompare)
    {
        LycEqualsLy = IsLycCompareActiveOnCurrentDot() && LcdYCoordinate == lcdYCompare;
    }

    private bool IsLycCompareActiveOnCurrentDot() =>
        StatusMode is PpuMode.VBlank
        || LcdYCoordinate >= PpuGeometry.VBlankStartLine
        || _firstScanlineAfterLcdEnable
        || _lineDots >= NormalScanlineStatusModeDelayDots;

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
