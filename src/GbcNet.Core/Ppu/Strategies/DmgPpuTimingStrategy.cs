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

    private int _lineDots;
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

    public bool IsCpuObjectAttributeMemoryWriteBlocked
    {
        get
        {
            if (LcdYCoordinate >= VBlankStartLine)
            {
                return false;
            }

            if (_firstScanlineAfterLcdEnable)
            {
                return IsCpuVideoRamWriteBlocked;
            }

            return _lineDots
                is (>= NormalScanlineStatusModeDelayDots and < OamScanDots)
                    or (>= NormalScanlineDrawingStartDots and < NormalScanlineDrawingEndDots);
        }
    }

    public PpuInterruptRequest Tick(int tCycles, byte lcdYCompare, byte statusInterruptSelect)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tCycles);

        if (tCycles == 0)
        {
            return PpuInterruptRequest.None;
        }

        PpuInterruptRequest requests = RefreshPpuState(
            lcdYCompare,
            statusInterruptSelect,
            requestInterrupt: true
        );

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
                requests |= AdvanceScanline(lcdYCompare, statusInterruptSelect);
                continue;
            }

            requests |= RefreshPpuState(lcdYCompare, statusInterruptSelect, requestInterrupt: true);
        }

        return requests;
    }

    public PpuInterruptRequest EnableLcd(byte lcdYCompare, byte statusInterruptSelect)
    {
        _lineDots = 0;
        _firstScanlineAfterLcdEnable = true;
        StatusMode = PpuMode.HBlank;
        _statInterruptMode = PpuMode.HBlank;

        bool oldLycEqualsLy = LycEqualsLy;
        RefreshLycEqualsLy(lcdYCompare);

        if (ShouldSuppressStableLycInterrupt(oldLycEqualsLy, statusInterruptSelect))
        {
            RefreshStatInterruptLine(
                statusInterruptSelect,
                lcdEnabled: true,
                requestInterrupt: false
            );
            return PpuInterruptRequest.None;
        }

        return RefreshStatInterruptLine(
            statusInterruptSelect,
            lcdEnabled: true,
            requestInterrupt: true
        );
    }

    public void DisableLcd()
    {
        _lineDots = 0;
        LcdYCoordinate = 0;
        StatusMode = PpuMode.HBlank;
        _statInterruptMode = PpuMode.HBlank;
        _firstScanlineAfterLcdEnable = false;
        _statInterruptLine = false;
    }

    public PpuInterruptRequest WriteStatusInterruptSelect(
        byte statusInterruptSelect,
        bool lcdEnabled
    ) => RefreshStatInterruptLine(statusInterruptSelect, lcdEnabled, requestInterrupt: true);

    public PpuInterruptRequest WriteLycCompare(
        byte lcdYCompare,
        byte statusInterruptSelect,
        bool lcdEnabled
    )
    {
        if (!lcdEnabled)
        {
            return PpuInterruptRequest.None;
        }

        RefreshLycEqualsLy(lcdYCompare);
        return RefreshStatInterruptLine(
            statusInterruptSelect,
            lcdEnabled: true,
            requestInterrupt: true
        );
    }

    public void SetStatusState(byte value, byte statusInterruptSelect, bool lcdEnabled)
    {
        LycEqualsLy = (value & PpuStatusRegister.LycEqualsLyMask) != 0;
        StatusMode = (PpuMode)(value & PpuStatusRegister.ModeMask);
        _statInterruptMode = StatusMode;
        RefreshStatInterruptLine(statusInterruptSelect, lcdEnabled, requestInterrupt: false);
    }

    public void SetLcdYCoordinateState(
        byte value,
        byte lcdYCompare,
        byte statusInterruptSelect,
        bool lcdEnabled
    )
    {
        LcdYCoordinate = value;
        RefreshLycEqualsLy(lcdYCompare);
        RefreshStatInterruptLine(statusInterruptSelect, lcdEnabled, requestInterrupt: false);
    }

    public void SetLycCompareState(byte lcdYCompare, byte statusInterruptSelect, bool lcdEnabled)
    {
        RefreshLycEqualsLy(lcdYCompare);
        RefreshStatInterruptLine(statusInterruptSelect, lcdEnabled, requestInterrupt: false);
    }

    private int GetCurrentScanlineDots() =>
        _firstScanlineAfterLcdEnable ? FirstScanlineAfterLcdEnableDots : ScanlineDots;

    private int GetCurrentDrawingEndDots() =>
        _firstScanlineAfterLcdEnable
            ? FirstScanlineAfterLcdEnableDrawingEndDots
            : NormalScanlineDrawingEndDots;

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

        if (_lineDots < NormalScanlineStatusModeDelayDots)
        {
            return NormalScanlineStatusModeDelayDots;
        }

        if (_lineDots < OamScanDots)
        {
            return OamScanDots;
        }

        if (_lineDots < drawingStartDots)
        {
            return drawingStartDots;
        }

        return _lineDots < drawingEndDots ? drawingEndDots : scanlineDots;
    }

    private PpuInterruptRequest AdvanceScanline(byte lcdYCompare, byte statusInterruptSelect)
    {
        _lineDots = 0;
        _firstScanlineAfterLcdEnable = false;
        PpuInterruptRequest requests = PpuInterruptRequest.None;

        if (LcdYCoordinate == LastScanline)
        {
            LcdYCoordinate = 0;
        }
        else
        {
            LcdYCoordinate++;
            if (LcdYCoordinate == VBlankStartLine)
            {
                requests |= PpuInterruptRequest.VBlank;
            }
        }

        requests |= RefreshPpuState(lcdYCompare, statusInterruptSelect, requestInterrupt: true);

        return requests;
    }

    private PpuInterruptRequest RefreshPpuState(
        byte lcdYCompare,
        byte statusInterruptSelect,
        bool requestInterrupt
    )
    {
        StatusMode = CalculateMode();
        _statInterruptMode = StatusMode;
        RefreshLycEqualsLy(lcdYCompare);
        return RefreshStatInterruptLine(statusInterruptSelect, lcdEnabled: true, requestInterrupt);
    }

    private PpuMode CalculateMode()
    {
        if (LcdYCoordinate >= VBlankStartLine)
        {
            return PpuMode.VBlank;
        }

        if (_firstScanlineAfterLcdEnable && LcdYCoordinate == 0 && _lineDots < OamScanDots)
        {
            return PpuMode.HBlank;
        }

        if (_firstScanlineAfterLcdEnable)
        {
            return _lineDots < FirstScanlineAfterLcdEnableDrawingEndDots
                ? PpuMode.Drawing
                : PpuMode.HBlank;
        }

        return _lineDots switch
        {
            < NormalScanlineStatusModeDelayDots => PpuMode.HBlank,
            < NormalScanlineDrawingStartDots => PpuMode.OamScan,
            _ when _lineDots < GetCurrentDrawingEndDots() => PpuMode.Drawing,
            _ => PpuMode.HBlank,
        };
    }

    private int GetCurrentDrawingStartDots() =>
        _firstScanlineAfterLcdEnable ? OamScanDots : NormalScanlineDrawingStartDots;

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
}
