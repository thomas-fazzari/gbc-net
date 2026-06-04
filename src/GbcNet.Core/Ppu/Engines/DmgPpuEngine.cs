using System.Runtime.InteropServices;
using GbcNet.Core.Memory;

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
    /// OBJ fetch adds a six-dot minimum Mode 3 penalty.
    /// </summary>
    private const int ObjectBasePenaltyDots = 6;

    /// <summary>
    /// Startup penalty for an object fetch session beginning at X mod 8 equal to 0 or 1.
    /// </summary>
    private const int SlowObjectSessionStartupDots = 8;

    /// <summary>
    /// Startup penalty for an object fetch session beginning at X mod 8 equal to 2 or 3.
    /// </summary>
    private const int NormalObjectSessionStartupDots = 6;

    /// <summary>
    /// Startup penalty for an object fetch session beginning at X mod 8 equal to 4, 5, 6, or 7.
    /// </summary>
    private const int FastObjectSessionStartupDots = 4;

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
    private readonly ScanlineObject[] _scanlineObjects = new ScanlineObject[
        PpuObjectAttributes.MaxObjectsPerScanline
    ];
    private byte _latchedScrollX;
    private byte _latchedScrollY;
    private int _latchedObjectPenaltyDots;
    private int _windowPenaltyDots;
    private int _windowLine;
    private int _activeWindowLine;
    private int _backgroundFifoStart;
    private int _backgroundFifoCount;
    private int _fetcherStepDots;
    private int _fetcherTileX;
    private int _discardedPixels;
    private int _renderedPixels;
    private int _scanlineObjectCount;
    private int _scanlineObjectHeight = PpuObjectAttributes.Size8;
    private byte _fetcherTileId;
    private byte _fetcherTileDataLow;
    private byte _fetcherTileDataHigh;
    private bool _statInterruptLine;
    private bool _firstScanlineAfterLcdEnable;
    private bool _renderingScanline;
    private bool _scanlineObjectsSelected;
    private bool _windowYCondition;
    private bool _windowActiveThisLine;
    private BackgroundFetcherStep _fetcherStep;
    private PixelFetcherSource _fetcherSource;

    public byte LcdYCoordinate { get; private set; }

    public bool LycEqualsLy { get; private set; } = true;

    public PpuMode StatusMode { get; private set; }

    private PpuMode _statInterruptMode;

    public bool IsCpuVideoRamReadBlocked =>
        LcdYCoordinate < PpuGeometry.VBlankStartLine
        && _lineDots >= OamScanDots
        && _lineDots < GetCurrentDrawingEndDots();

    public bool IsCpuVideoRamWriteBlocked =>
        LcdYCoordinate < PpuGeometry.VBlankStartLine
        && _lineDots >= GetCurrentDrawingStartDots()
        && _lineDots < GetCurrentDrawingEndDots();

    public bool IsCpuObjectAttributeMemoryReadBlocked =>
        LcdYCoordinate < PpuGeometry.VBlankStartLine
        && (
            _firstScanlineAfterLcdEnable
                ? IsCpuVideoRamReadBlocked
                : _lineDots < GetCurrentDrawingEndDots()
        );

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

    public PpuInterruptRequest EnableLcd(PpuEngineInputs inputs)
    {
        _lineDots = 0;
        LatchScroll(inputs);
        RefreshWindowYCondition(inputs);
        ClearScanlineObjects();
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

    public void DisableLcd()
    {
        _lineDots = 0;
        _latchedScrollX = 0;
        _latchedScrollY = 0;
        _windowLine = 0;
        _activeWindowLine = 0;
        _windowYCondition = false;
        ClearScanlineObjects();
        ResetRenderer();
        LcdYCoordinate = 0;
        StatusMode = PpuMode.HBlank;
        _statInterruptMode = PpuMode.HBlank;
        _firstScanlineAfterLcdEnable = false;
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
        StatusMode = (PpuMode)(value & PpuStatusRegister.ModeMask);
        _statInterruptMode = StatusMode;
        RefreshStatInterruptLine(inputs.StatusInterruptSelect, lcdEnabled, requestInterrupt: false);
    }

    public void SetLcdYCoordinateState(byte value, PpuEngineInputs inputs, bool lcdEnabled)
    {
        LcdYCoordinate = value;
        if (LcdYCoordinate < PpuGeometry.VBlankStartLine)
        {
            LatchScroll(inputs);
            RefreshWindowYCondition(inputs);
            ClearScanlineObjects();
        }
        else
        {
            ClearScanlineObjects();
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

    private int GetCurrentScanlineDots() =>
        _firstScanlineAfterLcdEnable ? FirstScanlineAfterLcdEnableDots : PpuGeometry.ScanlineDots;

    private int GetCurrentDrawingEndDots()
    {
        int drawingEndDots = _firstScanlineAfterLcdEnable
            ? FirstScanlineAfterLcdEnableDrawingEndDots
            : NormalScanlineDrawingEndDots;

        return drawingEndDots
            + LatchedScrollXLowBits
            + _windowPenaltyDots
            + _latchedObjectPenaltyDots;
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
                ClearScanlineObjects();
                ResetRenderer();
                break;
            case PpuGeometry.VBlankStartLine:
                ClearScanlineObjects();
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
        EnsureScanlineObjectsSelected(inputs);
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
        EnsureScanlineObjectsSelected(inputs);

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
        ObjectPixel? objectPixel = SelectObjectPixel(_renderedPixels, inputs);

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

    private ObjectPixel? SelectObjectPixel(int screenX, PpuEngineInputs inputs)
    {
        if ((inputs.LcdControl & PpuLcdControlRegister.ObjectEnableMask) == 0)
        {
            return null;
        }

        foreach (ScanlineObject scanlineObject in _scanlineObjects.AsSpan(0, _scanlineObjectCount))
        {
            int objectLeft = scanlineObject.X - PpuObjectAttributes.XScreenOffset;
            if (screenX < objectLeft || screenX >= objectLeft + PpuTileData.TileSizePixels)
            {
                continue;
            }

            byte colorId = ReadObjectColorId(scanlineObject, screenX, inputs);
            if (colorId == 0)
            {
                continue;
            }

            return new ObjectPixel(
                colorId,
                (scanlineObject.Flags & PpuObjectAttributes.DmgPalette1Mask) != 0,
                (scanlineObject.Flags & PpuObjectAttributes.BackgroundPriorityMask) != 0
            );
        }

        return null;
    }

    private byte ReadObjectColorId(
        ScanlineObject scanlineObject,
        int screenX,
        PpuEngineInputs inputs
    )
    {
        int objectLine = LcdYCoordinate - (scanlineObject.Y - PpuObjectAttributes.YScreenOffset);

        if ((scanlineObject.Flags & PpuObjectAttributes.YFlipMask) != 0)
        {
            objectLine = _scanlineObjectHeight - 1 - objectLine;
        }

        byte tileId =
            _scanlineObjectHeight == PpuObjectAttributes.Size16
                ? (byte)((scanlineObject.Tile & 0xFE) | (objectLine / PpuTileData.TileSizePixels))
                : scanlineObject.Tile;
        int tileLine = objectLine & ScrollXLowBitsMask;
        int tileAddress =
            PpuTileData.UnsignedTileDataStart
            + (tileId * PpuTileData.TileDataBytes)
            + (tileLine * PpuTileData.TileRowBytes);
        int pixel = screenX - (scanlineObject.X - PpuObjectAttributes.XScreenOffset);
        int bit = (scanlineObject.Flags & PpuObjectAttributes.XFlipMask) == 0 ? 7 - pixel : pixel;
        byte lowByte = inputs.VideoRam.Read((ushort)tileAddress);
        byte highByte = inputs.VideoRam.Read((ushort)(tileAddress + 1));

        return (byte)((((highByte >> bit) & 0x01) << 1) | ((lowByte >> bit) & 0x01));
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

    private int CalculateObjectPenaltyDots()
    {
        if (LcdYCoordinate >= PpuGeometry.VBlankStartLine || _scanlineObjectCount == 0)
        {
            return 0;
        }

        ReadOnlySpan<ScanlineObject> objects = _scanlineObjects.AsSpan(0, _scanlineObjectCount);

        int penaltyDots = 0;
        int index = 0;
        while (index < objects.Length)
        {
            ScanlineObject firstObject = objects[index];
            if (firstObject.X >= PpuObjectAttributes.FirstFullyHiddenRightX)
            {
                index++;
                continue;
            }

            int tileIndex = GetObjectTileIndex(firstObject.X, LatchedScrollXLowBits);
            int sessionEnd = index + 1;
            while (
                sessionEnd < objects.Length
                && objects[sessionEnd].X < PpuObjectAttributes.FirstFullyHiddenRightX
                && GetObjectTileIndex(objects[sessionEnd].X, LatchedScrollXLowBits) == tileIndex
            )
            {
                sessionEnd++;
            }

            penaltyDots += GetObjectSessionStartupDots(firstObject.X);
            penaltyDots += (sessionEnd - index - 1) * ObjectBasePenaltyDots;

            for (int laterIndex = sessionEnd; laterIndex < objects.Length; laterIndex++)
            {
                if (objects[laterIndex].X >= PpuObjectAttributes.FirstFullyHiddenRightX)
                {
                    continue;
                }

                penaltyDots += GetObjectSessionShutdownDots(objects[sessionEnd - 1].X);
                break;
            }

            index = sessionEnd;
        }

        return penaltyDots;
    }

    private void EnsureScanlineObjectsSelected(PpuEngineInputs inputs)
    {
        if (
            _scanlineObjectsSelected
            || LcdYCoordinate >= PpuGeometry.VBlankStartLine
            || _lineDots < OamScanDots
        )
        {
            return;
        }

        SelectScanlineObjects(inputs);
        _latchedObjectPenaltyDots = CalculateObjectPenaltyDots();
        _scanlineObjectsSelected = true;
    }

    private void ClearScanlineObjects()
    {
        _scanlineObjectCount = 0;
        _scanlineObjectHeight = PpuObjectAttributes.Size8;
        _latchedObjectPenaltyDots = 0;
        _scanlineObjectsSelected = false;
    }

    private void SelectScanlineObjects(PpuEngineInputs inputs)
    {
        if (
            LcdYCoordinate >= PpuGeometry.VBlankStartLine
            || (inputs.LcdControl & PpuLcdControlRegister.ObjectEnableMask) == 0
        )
        {
            _scanlineObjectCount = 0;
            _scanlineObjectHeight = PpuObjectAttributes.Size8;
            return;
        }

        _scanlineObjectHeight = GetObjectHeight(inputs.LcdControl);
        _scanlineObjectCount = 0;
        for (int objectIndex = 0; objectIndex < PpuObjectAttributes.ObjectCount; objectIndex++)
        {
            ushort objectAddress = (ushort)(
                AddressMap.ObjectAttributeMemoryStart
                + (objectIndex * PpuObjectAttributes.AttributeSize)
            );
            byte objectY = inputs.ObjectAttributeMemory.Read(
                (ushort)(objectAddress + PpuObjectAttributes.YCoordinateOffset)
            );
            int objectTop = objectY - PpuObjectAttributes.YScreenOffset;

            if (objectTop > LcdYCoordinate || objectTop + _scanlineObjectHeight <= LcdYCoordinate)
            {
                continue;
            }

            _scanlineObjects[_scanlineObjectCount] = new(
                objectIndex,
                inputs.ObjectAttributeMemory.Read(
                    (ushort)(objectAddress + PpuObjectAttributes.XCoordinateOffset)
                ),
                objectY,
                inputs.ObjectAttributeMemory.Read(
                    (ushort)(objectAddress + PpuObjectAttributes.TileIndexOffset)
                ),
                inputs.ObjectAttributeMemory.Read(
                    (ushort)(objectAddress + PpuObjectAttributes.FlagsOffset)
                )
            );

            _scanlineObjectCount++;

            if (_scanlineObjectCount == PpuObjectAttributes.MaxObjectsPerScanline)
            {
                break;
            }
        }

        _scanlineObjects
            .AsSpan(0, _scanlineObjectCount)
            .Sort(
                static (x, y) =>
                {
                    int xComparison = x.X.CompareTo(y.X);
                    return xComparison != 0 ? xComparison : x.Index.CompareTo(y.Index);
                }
            );
    }

    private static int GetObjectHeight(byte lcdControl) =>
        (lcdControl & PpuLcdControlRegister.ObjectSizeMask) == 0
            ? PpuObjectAttributes.Size8
            : PpuObjectAttributes.Size16;

    private static int GetObjectTileIndex(byte objectX, byte scrollXLowBits)
    {
        int pixel = objectX - PpuObjectAttributes.XScreenOffset + scrollXLowBits;
        return (pixel >> 3) & (PpuTileData.TilesPerMapRow - 1);
    }

    private static int GetObjectSessionStartupDots(byte objectX) =>
        (objectX & ScrollXLowBitsMask) switch
        {
            <= 1 => SlowObjectSessionStartupDots,
            <= 3 => NormalObjectSessionStartupDots,
            _ => FastObjectSessionStartupDots,
        };

    private static int GetObjectSessionShutdownDots(byte objectX) =>
        (objectX & ScrollXLowBitsMask) switch
        {
            0 or 2 or 4 => 3,
            _ => 2,
        };

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

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct ScanlineObject(int Index, byte X, byte Y, byte Tile, byte Flags);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct ObjectPixel(
        byte ColorId,
        bool UsesPalette1,
        bool HasBackgroundPriority
    );
}
