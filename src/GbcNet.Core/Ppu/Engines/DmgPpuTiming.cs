namespace GbcNet.Core.Ppu.Engines;

/// <summary>
/// Owns DMG scanline dot timing, LY progression, and CPU-visible STAT mode timing.
/// </summary>
internal sealed class DmgPpuTiming
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
    /// Current dot within the scanline.
    /// </summary>
    public int LineDots { get; private set; }

    /// <summary>
    /// DMG LY register value advanced by the scanline sequencer.
    /// </summary>
    public byte LcdYCoordinate { get; private set; }

    /// <summary>
    /// CPU-visible DMG STAT mode, including the four-dot visible-line startup delay.
    /// </summary>
    public PpuMode StatusMode { get; private set; }

    /// <summary>
    /// Indicates that timing is on the shortened first scanline after LCD enable.
    /// </summary>
    public bool FirstScanlineAfterLcdEnable { get; private set; }

    /// <summary>
    /// Starts timing at the shortened first scanline after LCD enable.
    /// </summary>
    public void EnableLcd()
    {
        LineDots = 0;
        FirstScanlineAfterLcdEnable = true;
        StatusMode = PpuMode.HBlank;
    }

    /// <summary>
    /// Resets timing to the LCD-disabled register state.
    /// </summary>
    public void DisableLcd()
    {
        LineDots = 0;
        LcdYCoordinate = 0;
        StatusMode = PpuMode.HBlank;
        FirstScanlineAfterLcdEnable = false;
    }

    /// <summary>
    /// Seeds STAT mode from a CPU-visible STAT register value.
    /// </summary>
    public void SetStatusState(byte value)
    {
        StatusMode = (PpuMode)(value & PpuStatusRegister.ModeMask);
    }

    /// <summary>
    /// Seeds LY from a CPU-visible LY register value.
    /// </summary>
    public void SetLcdYCoordinateState(byte value)
    {
        LcdYCoordinate = value;
    }

    /// <summary>
    /// Advances scanline dots by a positive elapsed interval.
    /// </summary>
    public void AdvanceDots(int elapsedDots)
    {
        LineDots += elapsedDots;
    }

    /// <summary>
    /// Advances to the next LY and starts a new scanline.
    /// </summary>
    public byte AdvanceScanline()
    {
        LineDots = 0;
        FirstScanlineAfterLcdEnable = false;
        LcdYCoordinate =
            LcdYCoordinate == PpuGeometry.LastScanline ? (byte)0 : (byte)(LcdYCoordinate + 1);
        return LcdYCoordinate;
    }

    /// <summary>
    /// Returns the total dots in the current scanline.
    /// </summary>
    public int GetCurrentScanlineDots() =>
        FirstScanlineAfterLcdEnable ? FirstScanlineAfterLcdEnableDots : PpuGeometry.ScanlineDots;

    /// <summary>
    /// Returns the dot where rendering starts on the current scanline.
    /// </summary>
    public int GetCurrentDrawingStartDots() =>
        FirstScanlineAfterLcdEnable ? OamScanDots : NormalScanlineDrawingStartDots;

    /// <summary>
    /// Returns the dot where rendering ends on the current scanline, including dynamic penalties.
    /// </summary>
    public int GetCurrentDrawingEndDots(
        int scrollPenaltyDots,
        int windowPenaltyDots,
        int objectPenaltyDots
    )
    {
        int drawingEndDots = FirstScanlineAfterLcdEnable
            ? FirstScanlineAfterLcdEnableDrawingEndDots
            : NormalScanlineDrawingEndDots;

        return drawingEndDots + scrollPenaltyDots + windowPenaltyDots + objectPenaltyDots;
    }

    /// <summary>
    /// Returns the next dot boundary where timing, access blocking, or STAT mode can change.
    /// </summary>
    public int GetNextTimingBoundary(int drawingEndDots)
    {
        int scanlineDots = GetCurrentScanlineDots();
        if (LcdYCoordinate >= PpuGeometry.VBlankStartLine)
        {
            return scanlineDots;
        }

        if (FirstScanlineAfterLcdEnable)
        {
            if (LineDots < OamScanDots)
            {
                return OamScanDots;
            }

            return LineDots < drawingEndDots ? drawingEndDots : scanlineDots;
        }

        if (LineDots < NormalScanlineStatusModeDelayDots)
        {
            return NormalScanlineStatusModeDelayDots;
        }

        if (LineDots < OamScanDots)
        {
            return OamScanDots;
        }

        int drawingStartDots = GetCurrentDrawingStartDots();
        if (LineDots < drawingStartDots)
        {
            return drawingStartDots;
        }

        return LineDots < drawingEndDots ? drawingEndDots : scanlineDots;
    }

    /// <summary>
    /// Indicates whether the current interval is in Mode 3 rendering ownership.
    /// </summary>
    public bool IsRenderingInterval(int drawingStartDots, int drawingEndDots) =>
        LcdYCoordinate < PpuGeometry.VBlankStartLine
        && LineDots >= drawingStartDots
        && LineDots < drawingEndDots;

    /// <summary>
    /// Refreshes and returns the CPU-visible STAT mode for the current dot.
    /// </summary>
    public PpuMode RefreshStatusMode(int drawingEndDots)
    {
        StatusMode = CalculateMode(drawingEndDots);
        return StatusMode;
    }

    /// <summary>
    /// Indicates that object selection is past the OAM scan dot threshold.
    /// </summary>
    public bool HasReachedOamScanEnd => LineDots >= OamScanDots;

    /// <summary>
    /// Indicates whether LY=LYC comparison is active on the current dot.
    /// </summary>
    public bool IsLycCompareActiveOnCurrentDot() =>
        StatusMode is PpuMode.VBlank
        || LcdYCoordinate >= PpuGeometry.VBlankStartLine
        || FirstScanlineAfterLcdEnable
        || LineDots >= NormalScanlineStatusModeDelayDots;

    /// <summary>
    /// Indicates that CPU VRAM reads are blocked by current timing ownership.
    /// </summary>
    public bool IsCpuVideoRamReadBlocked(int drawingEndDots) =>
        LcdYCoordinate < PpuGeometry.VBlankStartLine
        && LineDots >= OamScanDots
        && LineDots < drawingEndDots;

    /// <summary>
    /// Indicates that CPU VRAM writes are blocked by current timing ownership.
    /// </summary>
    public bool IsCpuVideoRamWriteBlocked(int drawingStartDots, int drawingEndDots) =>
        LcdYCoordinate < PpuGeometry.VBlankStartLine
        && LineDots >= drawingStartDots
        && LineDots < drawingEndDots;

    /// <summary>
    /// Indicates that CPU OAM reads are blocked by current timing ownership.
    /// </summary>
    public bool IsCpuObjectAttributeMemoryReadBlocked(int drawingEndDots) =>
        LcdYCoordinate < PpuGeometry.VBlankStartLine
        && (
            FirstScanlineAfterLcdEnable
                ? IsCpuVideoRamReadBlocked(drawingEndDots)
                : LineDots < drawingEndDots
        );

    /// <summary>
    /// Indicates that CPU OAM writes are blocked by current timing ownership.
    /// </summary>
    public bool IsCpuObjectAttributeMemoryWriteBlocked(int drawingStartDots, int drawingEndDots) =>
        LcdYCoordinate < PpuGeometry.VBlankStartLine
        && (
            FirstScanlineAfterLcdEnable
                ? IsCpuVideoRamWriteBlocked(drawingStartDots, drawingEndDots)
                : LineDots is >= NormalScanlineStatusModeDelayDots and < OamScanDots
                    || (LineDots >= drawingStartDots && LineDots < drawingEndDots)
        );

    private PpuMode CalculateMode(int drawingEndDots)
    {
        if (LcdYCoordinate >= PpuGeometry.VBlankStartLine)
        {
            return PpuMode.VBlank;
        }

        if (!FirstScanlineAfterLcdEnable)
        {
            return LineDots switch
            {
                < NormalScanlineStatusModeDelayDots => PpuMode.HBlank,
                < NormalScanlineDrawingStartDots => PpuMode.OamScan,
                _ when LineDots < drawingEndDots => PpuMode.Drawing,
                _ => PpuMode.HBlank,
            };
        }

        if (LcdYCoordinate == 0 && LineDots < OamScanDots)
        {
            return PpuMode.HBlank;
        }

        return LineDots < drawingEndDots ? PpuMode.Drawing : PpuMode.HBlank;
    }
}
