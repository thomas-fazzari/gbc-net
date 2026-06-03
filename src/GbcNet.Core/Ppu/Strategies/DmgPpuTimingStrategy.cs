using System.Runtime.InteropServices;
using GbcNet.Core.Memory;

namespace GbcNet.Core.Ppu.Strategies;

/// <summary>
/// DMG LCD timing model for LY, STAT mode, STAT interrupt line, and CPU video-memory access.
/// </summary>
internal sealed class DmgPpuTimingStrategy : IPpuTimingStrategy
{
    /// <summary>
    /// One PPU scanline is 456 dots.
    /// </summary>
    private const int ScanlineDots = 456;

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
    /// First LY value in VBlank.
    /// </summary>
    private const byte VBlankStartLine = 144;

    /// <summary>
    /// LY wraps after line 153.
    /// </summary>
    private const byte LastScanline = 153;

    /// <summary>
    /// SCX low bits are latched at visible scanline start for the initial pixel shift.
    /// </summary>
    private const byte ScrollXLowBitsMask = 0x07;

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

    /// <summary>
    /// Visible scanlines span 32 BG tiles horizontally.
    /// </summary>
    private const int VisibleTileCount = 32;

    private int _lineDots;
    private byte _latchedScrollXLowBits;
    private int _latchedObjectPenaltyDots;
    private bool _statInterruptLine;
    private bool _firstScanlineAfterLcdEnable;

    public byte LcdYCoordinate { get; private set; }

    public bool LycEqualsLy { get; private set; } = true;

    public PpuMode StatusMode { get; private set; }

    private PpuMode _statInterruptMode;

    public bool IsCpuVideoRamReadBlocked =>
        LcdYCoordinate < VBlankStartLine
        && _lineDots >= OamScanDots
        && _lineDots < GetCurrentDrawingEndDots();

    public bool IsCpuVideoRamWriteBlocked =>
        LcdYCoordinate < VBlankStartLine
        && _lineDots >= GetCurrentDrawingStartDots()
        && _lineDots < GetCurrentDrawingEndDots();

    public bool IsCpuObjectAttributeMemoryReadBlocked =>
        LcdYCoordinate < VBlankStartLine
        && (
            _firstScanlineAfterLcdEnable
                ? IsCpuVideoRamReadBlocked
                : _lineDots < GetCurrentDrawingEndDots()
        );

    public bool IsCpuObjectAttributeMemoryWriteBlocked =>
        LcdYCoordinate < VBlankStartLine
        && (
            _firstScanlineAfterLcdEnable
                ? IsCpuVideoRamWriteBlocked
                : _lineDots is >= NormalScanlineStatusModeDelayDots and < OamScanDots
                    || (
                        _lineDots >= NormalScanlineDrawingStartDots
                        && _lineDots < GetCurrentDrawingEndDots()
                    )
        );

    public PpuInterruptRequest Tick(int tCycles, PpuTimingInputs inputs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tCycles);

        if (tCycles == 0)
        {
            return PpuInterruptRequest.None;
        }

        PpuInterruptRequest requests = RefreshPpuState(inputs, requestInterrupt: true);

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
            _lineDots += elapsedCycles;
            remainingCycles -= elapsedCycles;

            if (_lineDots == scanlineDots)
            {
                requests |= AdvanceScanline(inputs);
                continue;
            }

            requests |= RefreshPpuState(inputs, requestInterrupt: true);
        }

        return requests;
    }

    public PpuInterruptRequest EnableLcd(PpuTimingInputs inputs)
    {
        _lineDots = 0;
        _latchedScrollXLowBits = (byte)(inputs.ScrollX & ScrollXLowBitsMask);
        _latchedObjectPenaltyDots = CalculateObjectPenaltyDots(inputs);
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
        _latchedScrollXLowBits = 0;
        _latchedObjectPenaltyDots = 0;
        LcdYCoordinate = 0;
        StatusMode = PpuMode.HBlank;
        _statInterruptMode = PpuMode.HBlank;
        _firstScanlineAfterLcdEnable = false;
        _statInterruptLine = false;
    }

    public PpuInterruptRequest WriteStatusInterruptSelect(
        PpuTimingInputs inputs,
        bool lcdEnabled
    ) => RefreshStatInterruptLine(inputs.StatusInterruptSelect, lcdEnabled, requestInterrupt: true);

    public PpuInterruptRequest WriteLycCompare(PpuTimingInputs inputs, bool lcdEnabled)
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

    public void SetStatusState(byte value, PpuTimingInputs inputs, bool lcdEnabled)
    {
        LycEqualsLy = (value & PpuStatusRegister.LycEqualsLyMask) != 0;
        StatusMode = (PpuMode)(value & PpuStatusRegister.ModeMask);
        _statInterruptMode = StatusMode;
        RefreshStatInterruptLine(inputs.StatusInterruptSelect, lcdEnabled, requestInterrupt: false);
    }

    public void SetLcdYCoordinateState(byte value, PpuTimingInputs inputs, bool lcdEnabled)
    {
        LcdYCoordinate = value;
        if (LcdYCoordinate < VBlankStartLine)
        {
            _latchedScrollXLowBits = (byte)(inputs.ScrollX & ScrollXLowBitsMask);
            _latchedObjectPenaltyDots = CalculateObjectPenaltyDots(inputs);
        }
        else
        {
            _latchedObjectPenaltyDots = 0;
        }

        RefreshLycEqualsLy(inputs.LcdYCompare);
        RefreshStatInterruptLine(inputs.StatusInterruptSelect, lcdEnabled, requestInterrupt: false);
    }

    public void SetLycCompareState(PpuTimingInputs inputs, bool lcdEnabled)
    {
        RefreshLycEqualsLy(inputs.LcdYCompare);
        RefreshStatInterruptLine(inputs.StatusInterruptSelect, lcdEnabled, requestInterrupt: false);
    }

    private int GetCurrentScanlineDots() =>
        _firstScanlineAfterLcdEnable ? FirstScanlineAfterLcdEnableDots : ScanlineDots;

    private int GetCurrentDrawingEndDots()
    {
        int drawingEndDots = _firstScanlineAfterLcdEnable
            ? FirstScanlineAfterLcdEnableDrawingEndDots
            : NormalScanlineDrawingEndDots;

        return drawingEndDots + _latchedScrollXLowBits + _latchedObjectPenaltyDots;
    }

    private int GetNextTimingBoundary(int scanlineDots, int drawingStartDots, int drawingEndDots)
    {
        if (LcdYCoordinate >= VBlankStartLine)
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

    private PpuInterruptRequest AdvanceScanline(PpuTimingInputs inputs)
    {
        _lineDots = 0;
        _firstScanlineAfterLcdEnable = false;
        PpuInterruptRequest requests = PpuInterruptRequest.None;

        bool shouldRequestMode2Interrupt =
            !_statInterruptLine
            && (inputs.StatusInterruptSelect & PpuStatusRegister.Mode2InterruptSelectMask) != 0;

        LcdYCoordinate = LcdYCoordinate == LastScanline ? (byte)0 : (byte)(LcdYCoordinate + 1);
        switch (LcdYCoordinate)
        {
            case < VBlankStartLine:
                _latchedScrollXLowBits = (byte)(inputs.ScrollX & ScrollXLowBitsMask);
                _latchedObjectPenaltyDots = CalculateObjectPenaltyDots(inputs);
                break;
            case VBlankStartLine:
                _latchedObjectPenaltyDots = 0;
                requests |= PpuInterruptRequest.VBlank;
                break;
        }

        if (shouldRequestMode2Interrupt && LcdYCoordinate is 0 or VBlankStartLine)
        {
            requests |= PpuInterruptRequest.Lcd;
        }

        requests |= RefreshPpuState(inputs, requestInterrupt: true);

        return requests;
    }

    private PpuInterruptRequest RefreshPpuState(PpuTimingInputs inputs, bool requestInterrupt)
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
        if (LcdYCoordinate >= VBlankStartLine)
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

    private int CalculateObjectPenaltyDots(PpuTimingInputs inputs)
    {
        if (LcdYCoordinate >= VBlankStartLine || (inputs.LcdControl & ObjectEnableMask) == 0)
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

            int tileIndex = GetObjectTileIndex(firstObject.X, _latchedScrollXLowBits);
            int sessionEnd = index + 1;
            while (
                sessionEnd < objects.Length
                && objects[sessionEnd].X < FirstFullyHiddenRightObjectX
                && GetObjectTileIndex(objects[sessionEnd].X, _latchedScrollXLowBits) == tileIndex
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

    private int SelectScanlineObjects(PpuTimingInputs inputs, Span<ScanlineObject> objects)
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
        return (pixel >> 3) & (VisibleTileCount - 1);
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
        || LcdYCoordinate >= VBlankStartLine
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
}
