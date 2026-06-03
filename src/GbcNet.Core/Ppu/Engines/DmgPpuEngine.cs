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
    /// LCDC bit that enables BG and Window display on DMG.
    /// </summary>
    private const byte BackgroundEnableMask = 0x01;

    /// <summary>
    /// LCDC bit that selects the BG tile map at 9C00-9FFF.
    /// </summary>
    private const byte BackgroundTileMapSelectMask = 0x08;

    /// <summary>
    /// LCDC bit that selects unsigned BG tile addressing at 8000-8FFF.
    /// </summary>
    private const byte BackgroundTileDataSelectMask = 0x10;

    /// <summary>
    /// LCDC bit that enables OBJ display.
    /// </summary>
    private const byte ObjectEnableMask = 0x02;

    /// <summary>
    /// LCDC bit that selects 8x16 OBJ mode instead of 8x8.
    /// </summary>
    private const byte ObjectSizeMask = 0x04;

    /// <summary>
    /// DMG OAM contains 40 OBJ entries.
    /// </summary>
    private const int ObjectCount = 40;

    /// <summary>
    /// OAM scan can select at most 10 OBJ entries for a scanline.
    /// </summary>
    private const int MaxObjectsPerScanline = 10;

    /// <summary>
    /// Each OBJ entry contains Y, X, tile, and flags.
    /// </summary>
    private const int ObjectAttributeSize = 4;

    /// <summary>
    /// Y coordinate byte offset inside one OBJ entry.
    /// </summary>
    private const int ObjectYCoordinateOffset = 0;

    /// <summary>
    /// X coordinate byte offset inside one OBJ entry.
    /// </summary>
    private const int ObjectXCoordinateOffset = 1;

    /// <summary>
    /// OAM stores OBJ Y as screen Y plus 16.
    /// </summary>
    private const int ObjectYScreenOffset = 16;

    /// <summary>
    /// DMG OBJ size when LCDC bit 2 is clear.
    /// </summary>
    private const int ObjectSize8 = 8;

    /// <summary>
    /// DMG OBJ size when LCDC bit 2 is set.
    /// </summary>
    private const int ObjectSize16 = 16;

    /// <summary>
    /// OBJ fetch adds a six-dot minimum Mode 3 penalty.
    /// </summary>
    private const int ObjectBasePenaltyDots = 6;

    /// <summary>
    /// OBJ with X>=168 is fully hidden on the right side.
    /// </summary>
    private const byte FirstFullyHiddenRightObjectX = 168;

    /// <summary>
    /// OBJ X is stored as screen X plus eight.
    /// </summary>
    private const int ObjectXScreenOffset = 8;

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

    private int _lineDots;
    private readonly byte[] _frameBuffer = new byte[
        PpuGeometry.FrameWidth * PpuGeometry.FrameHeight
    ];
    private readonly byte[] _backgroundFifo = new byte[16];
    private byte _latchedScrollX;
    private byte _latchedScrollY;
    private int _latchedObjectPenaltyDots;
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
    private BackgroundFetcherStep _fetcherStep;

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
        _latchedObjectPenaltyDots = CalculateObjectPenaltyDots(inputs);
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
        _latchedObjectPenaltyDots = 0;
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
            _latchedObjectPenaltyDots = CalculateObjectPenaltyDots(inputs);
        }
        else
        {
            _latchedObjectPenaltyDots = 0;
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

        return drawingEndDots + LatchedScrollXLowBits + _latchedObjectPenaltyDots;
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
                _latchedObjectPenaltyDots = CalculateObjectPenaltyDots(inputs);
                ResetRenderer();
                break;
            case PpuGeometry.VBlankStartLine:
                _latchedObjectPenaltyDots = 0;
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
        if (!_renderingScanline)
        {
            BeginRenderingScanline();
        }

        for (int dot = 0; dot < dots && _renderedPixels < PpuGeometry.FrameWidth; dot++)
        {
            AdvanceBackgroundFetcher(inputs);
            TryRenderBackgroundPixel(inputs);
        }
    }

    private void BeginRenderingScanline()
    {
        ResetRenderer();
        _renderingScanline = true;
    }

    private void ResetRenderer()
    {
        _backgroundFifoStart = 0;
        _backgroundFifoCount = 0;
        _fetcherStepDots = 0;
        _fetcherTileX = 0;
        _discardedPixels = 0;
        _renderedPixels = 0;
        _fetcherTileId = 0;
        _fetcherTileDataLow = 0;
        _fetcherTileDataHigh = 0;
        _fetcherStep = BackgroundFetcherStep.GetTile;
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

        _fetcherTileId = inputs.VideoRam.Read(GetBackgroundTileMapAddress(inputs));
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

    private void TryRenderBackgroundPixel(PpuEngineInputs inputs)
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

        if ((inputs.LcdControl & BackgroundEnableMask) == 0)
        {
            colorId = 0;
        }

        _frameBuffer[(LcdYCoordinate * PpuGeometry.FrameWidth) + _renderedPixels] =
            ApplyBackgroundPalette(colorId, inputs.BackgroundPalette);
        _renderedPixels++;
    }

    private ushort GetBackgroundTileMapAddress(PpuEngineInputs inputs)
    {
        ushort tileMapStart =
            (inputs.LcdControl & BackgroundTileMapSelectMask) == 0
                ? PpuTileData.TileMap0Start
                : PpuTileData.TileMap1Start;
        int backgroundY = (_latchedScrollY + LcdYCoordinate) & 0xFF;
        int tileY = backgroundY / PpuTileData.TileSizePixels;
        int tileX =
            ((_latchedScrollX / PpuTileData.TileSizePixels) + _fetcherTileX)
            & (PpuTileData.TilesPerMapRow - 1);
        return (ushort)(tileMapStart + (tileY * PpuTileData.TilesPerMapRow) + tileX);
    }

    private ushort GetTileDataAddress(PpuEngineInputs inputs, bool highByte)
    {
        int backgroundY = (_latchedScrollY + LcdYCoordinate) & 0xFF;
        int tileLine = backgroundY & ScrollXLowBitsMask;
        int byteOffset = (tileLine * PpuTileData.TileRowBytes) + (highByte ? 1 : 0);
        int tileAddress =
            (inputs.LcdControl & BackgroundTileDataSelectMask) == 0
                ? PpuTileData.SignedTileDataBase
                    + ((sbyte)_fetcherTileId * PpuTileData.TileDataBytes)
                : PpuTileData.UnsignedTileDataStart + (_fetcherTileId * PpuTileData.TileDataBytes);

        return (ushort)(tileAddress + byteOffset);
    }

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

    private static byte ApplyBackgroundPalette(byte colorId, byte backgroundPalette) =>
        (byte)((backgroundPalette >> (colorId * 2)) & 0x03);

    private void MoveToFetcherStep(BackgroundFetcherStep fetcherStep)
    {
        _fetcherStep = fetcherStep;
        _fetcherStepDots = 0;
    }

    private int CalculateObjectPenaltyDots(PpuEngineInputs inputs)
    {
        if (
            LcdYCoordinate >= PpuGeometry.VBlankStartLine
            || (inputs.LcdControl & ObjectEnableMask) == 0
        )
        {
            return 0;
        }

        Span<ScanlineObject> objects = stackalloc ScanlineObject[MaxObjectsPerScanline];
        int objectCount = SelectScanlineObjects(inputs, objects);
        objects = objects[..objectCount];
        objects.Sort(
            static (left, right) =>
            {
                int xComparison = left.X.CompareTo(right.X);
                return xComparison != 0 ? xComparison : left.Index.CompareTo(right.Index);
            }
        );

        int penaltyDots = 0;
        int index = 0;
        while (index < objects.Length)
        {
            ScanlineObject firstObject = objects[index];
            if (firstObject.X >= FirstFullyHiddenRightObjectX)
            {
                index++;
                continue;
            }

            int tileIndex = GetObjectTileIndex(firstObject.X, LatchedScrollXLowBits);
            int sessionEnd = index + 1;
            while (
                sessionEnd < objects.Length
                && objects[sessionEnd].X < FirstFullyHiddenRightObjectX
                && GetObjectTileIndex(objects[sessionEnd].X, LatchedScrollXLowBits) == tileIndex
            )
            {
                sessionEnd++;
            }

            penaltyDots += GetObjectSessionStartupDots(firstObject.X);
            penaltyDots += (sessionEnd - index - 1) * ObjectBasePenaltyDots;

            for (int laterIndex = sessionEnd; laterIndex < objects.Length; laterIndex++)
            {
                if (objects[laterIndex].X >= FirstFullyHiddenRightObjectX)
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

    private int SelectScanlineObjects(PpuEngineInputs inputs, Span<ScanlineObject> objects)
    {
        int objectHeight = (inputs.LcdControl & ObjectSizeMask) == 0 ? ObjectSize8 : ObjectSize16;
        int selectedCount = 0;

        for (int objectIndex = 0; objectIndex < ObjectCount; objectIndex++)
        {
            ushort objectAddress = (ushort)(
                AddressMap.ObjectAttributeMemoryStart + (objectIndex * ObjectAttributeSize)
            );
            byte objectY = inputs.ObjectAttributeMemory.Read(
                (ushort)(objectAddress + ObjectYCoordinateOffset)
            );
            int objectTop = objectY - ObjectYScreenOffset;
            if (objectTop > LcdYCoordinate || objectTop + objectHeight <= LcdYCoordinate)
            {
                continue;
            }

            objects[selectedCount] = new(
                objectIndex,
                inputs.ObjectAttributeMemory.Read((ushort)(objectAddress + ObjectXCoordinateOffset))
            );
            selectedCount++;

            if (selectedCount == MaxObjectsPerScanline)
            {
                return selectedCount;
            }
        }

        return selectedCount;
    }

    private static int GetObjectTileIndex(byte objectX, byte scrollXLowBits)
    {
        int pixel = objectX - ObjectXScreenOffset + scrollXLowBits;
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
    private readonly record struct ScanlineObject(int Index, byte X);

    private enum BackgroundFetcherStep
    {
        GetTile = 0,
        GetTileDataLow = 1,
        GetTileDataHigh = 2,
        Sleep = 3,
        Push = 4,
    }
}
