using GbcNet.Core.Interrupts;
using GbcNet.Core.Memory;

namespace GbcNet.Core.Ppu;

/// <summary>
/// Stores CPU-visible LCD/PPU register state and advances minimal DMG PPU timing.
/// </summary>
internal sealed class PpuController(InterruptController interrupts)
{
    /// <summary>
    /// LCDC bit 7 enables LCD/PPU timing.
    /// </summary>
    private const byte LcdEnableMask = 0x80;

    /// <summary>
    /// STAT bit 7 reads back as set.
    /// </summary>
    private const byte StatusReadMask = 0x80;

    /// <summary>
    /// STAT bits 6-3 select LCD interrupt sources and are writable by the CPU.
    /// </summary>
    private const byte StatusInterruptSelectMask = 0x78;

    /// <summary>
    /// STAT bit 3 enables Mode 0 LCD STAT interrupts.
    /// </summary>
    private const byte StatusMode0InterruptSelectMask = 0x08;

    /// <summary>
    /// STAT bit 4 enables Mode 1 LCD STAT interrupts.
    /// </summary>
    private const byte StatusMode1InterruptSelectMask = 0x10;

    /// <summary>
    /// STAT bit 5 enables Mode 2 LCD STAT interrupts.
    /// </summary>
    private const byte StatusMode2InterruptSelectMask = 0x20;

    /// <summary>
    /// STAT bit 6 enables LYC=LY LCD STAT interrupts.
    /// </summary>
    private const byte StatusLycEqualsLyInterruptSelectMask = 0x40;

    /// <summary>
    /// STAT bit 2 is set when LY equals LYC.
    /// </summary>
    private const byte StatusLycEqualsLyMask = 0x04;

    /// <summary>
    /// STAT bits 1-0 expose the current PPU mode.
    /// </summary>
    private const byte StatusModeMask = 0x03;

    /// <summary>
    /// STAT mode 0, horizontal blank.
    /// </summary>
    private const byte ModeHBlank = 0;

    /// <summary>
    /// STAT mode 1, vertical blank.
    /// </summary>
    private const byte ModeVBlank = 1;

    /// <summary>
    /// STAT mode 2, OAM scan.
    /// </summary>
    private const byte ModeOamScan = 2;

    /// <summary>
    /// STAT mode 3, drawing pixels.
    /// </summary>
    private const byte ModeDrawing = 3;

    /// <summary>
    /// One PPU scanline is 456 dots.
    /// </summary>
    private const int ScanlineDots = 456;

    /// <summary>
    /// Mode 2 lasts 80 dots on visible scanlines.
    /// </summary>
    private const int OamScanDots = 80;

    /// <summary>
    /// Fixed minimal Mode 3 length used until pixel fetch penalties are modeled.
    /// </summary>
    private const int DrawingEndDots = OamScanDots + 172;

    /// <summary>
    /// First LY value in VBlank.
    /// </summary>
    private const byte VBlankStartLine = 144;

    /// <summary>
    /// LY wraps after line 153.
    /// </summary>
    private const byte LastScanline = 153;

    private byte _control;
    private byte _statusInterruptSelect;
    private byte _statusMode;
    private byte _scrollY;
    private byte _scrollX;
    private byte _lcdYCoordinate;
    private byte _lcdYCompare;
    private byte _backgroundPalette;
    private byte _objectPalette0;
    private byte _objectPalette1;
    private byte _windowY;
    private byte _windowX;
    private int _lineDots;
    private bool _lycEqualsLy = true;
    private bool _statInterruptLine;
    private bool _firstScanlineAfterLcdEnable;

    /// <summary>
    /// Returns whether an address is owned by the LCD/PPU register block.
    /// </summary>
    internal static bool ContainsRegister(ushort address) =>
        address
            is >= AddressMap.LcdControlRegister
                and <= AddressMap.WindowXRegister
                and not AddressMap.DmaRegister;

    /// <summary>
    /// Reads an LCD/PPU register at FF40-FF45 or FF47-FF4B.
    /// </summary>
    public byte ReadRegister(ushort address) =>
        address switch
        {
            AddressMap.LcdControlRegister => _control,
            AddressMap.LcdStatusRegister => ReadStatus(),
            AddressMap.ScrollYRegister => _scrollY,
            AddressMap.ScrollXRegister => _scrollX,
            AddressMap.LcdYCoordinateRegister => _lcdYCoordinate,
            AddressMap.LcdYCompareRegister => _lcdYCompare,
            AddressMap.BackgroundPaletteRegister => _backgroundPalette,
            AddressMap.ObjectPalette0Register => _objectPalette0,
            AddressMap.ObjectPalette1Register => _objectPalette1,
            AddressMap.WindowYRegister => _windowY,
            AddressMap.WindowXRegister => _windowX,
            _ => throw CreateUnsupportedRegisterException(address),
        };

    /// <summary>
    /// Indicates whether LCD/PPU timing is running.
    /// </summary>
    public bool IsLcdEnabled => (_control & LcdEnableMask) != 0;

    /// <summary>
    /// Indicates whether the CPU can access VRAM at 8000-9FFF in the current PPU mode.
    /// </summary>
    public bool CanCpuAccessVideoRam => !IsLcdEnabled || _statusMode != ModeDrawing;

    /// <summary>
    /// Indicates whether the CPU can access OAM at FE00-FE9F in the current PPU mode.
    /// </summary>
    public bool CanCpuAccessObjectAttributeMemory =>
        !IsLcdEnabled || _statusMode is ModeHBlank or ModeVBlank;

    /// <summary>
    /// Writes an LCD/PPU register as the CPU sees it.
    /// </summary>
    public void WriteRegister(ushort address, byte value)
    {
        switch (address)
        {
            case AddressMap.LcdControlRegister:
                WriteLcdControl(value);
                return;
            case AddressMap.LcdStatusRegister:
                _statusInterruptSelect = (byte)(value & StatusInterruptSelectMask);
                RefreshStatInterruptLine(requestInterrupt: true);
                return;
            case AddressMap.LcdYCoordinateRegister:
                return;
            case AddressMap.LcdYCompareRegister:
                _lcdYCompare = value;
                if (!IsLcdEnabled)
                {
                    return;
                }
                RefreshLycEqualsLy();
                RefreshStatInterruptLine(requestInterrupt: true);
                return;
            default:
                SetReadWriteRegister(address, value);
                return;
        }
    }

    /// <summary>
    /// Advances LCD/PPU timing by elapsed dots.
    /// </summary>
    public void Tick(int tCycles)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tCycles);

        if (!IsLcdEnabled || tCycles == 0)
        {
            return;
        }

        RefreshPpuState(requestStatInterrupt: true);

        int remainingCycles = tCycles;
        while (remainingCycles > 0)
        {
            int nextBoundary = GetNextTimingBoundary();
            int elapsedCycles = Math.Min(remainingCycles, nextBoundary - _lineDots);
            _lineDots += elapsedCycles;
            remainingCycles -= elapsedCycles;

            if (_lineDots == ScanlineDots)
            {
                AdvanceScanline();
                continue;
            }

            RefreshPpuState(requestStatInterrupt: true);
        }
    }

    /// <summary>
    /// Seeds an LCD/PPU register without modeling a CPU bus write.
    /// </summary>
    internal void SetRegisterState(ushort address, byte value)
    {
        switch (address)
        {
            case AddressMap.LcdControlRegister:
                _control = value;
                return;
            case AddressMap.LcdStatusRegister:
                _statusInterruptSelect = (byte)(value & StatusInterruptSelectMask);
                _lycEqualsLy = (value & StatusLycEqualsLyMask) != 0;
                _statusMode = (byte)(value & StatusModeMask);
                RefreshStatInterruptLine(requestInterrupt: false);
                return;
            case AddressMap.LcdYCoordinateRegister:
                _lcdYCoordinate = value;
                RefreshLycEqualsLy();
                RefreshStatInterruptLine(requestInterrupt: false);
                return;
            case AddressMap.LcdYCompareRegister:
                _lcdYCompare = value;
                RefreshLycEqualsLy();
                RefreshStatInterruptLine(requestInterrupt: false);
                return;
            default:
                SetReadWriteRegister(address, value);
                return;
        }
    }

    private byte ReadStatus()
    {
        byte lycEqualsLy = _lycEqualsLy ? StatusLycEqualsLyMask : (byte)0;
        byte mode = IsLcdEnabled ? _statusMode : ModeHBlank;
        return (byte)(StatusReadMask | _statusInterruptSelect | lycEqualsLy | mode);
    }

    private void SetReadWriteRegister(ushort address, byte value)
    {
        switch (address)
        {
            case AddressMap.ScrollYRegister:
                _scrollY = value;
                return;
            case AddressMap.ScrollXRegister:
                _scrollX = value;
                return;
            case AddressMap.BackgroundPaletteRegister:
                _backgroundPalette = value;
                return;
            case AddressMap.ObjectPalette0Register:
                _objectPalette0 = value;
                return;
            case AddressMap.ObjectPalette1Register:
                _objectPalette1 = value;
                return;
            case AddressMap.WindowYRegister:
                _windowY = value;
                return;
            case AddressMap.WindowXRegister:
                _windowX = value;
                return;
            default:
                throw CreateUnsupportedRegisterException(address);
        }
    }

    private void WriteLcdControl(byte value)
    {
        bool wasEnabled = IsLcdEnabled;
        _control = value;

        if (wasEnabled && !IsLcdEnabled)
        {
            ResetLcdTiming();
            return;
        }

        if (!wasEnabled && IsLcdEnabled)
        {
            StartLcdTiming();
        }
    }

    private int GetNextTimingBoundary()
    {
        if (_lcdYCoordinate >= VBlankStartLine)
        {
            return ScanlineDots;
        }

        return _lineDots switch
        {
            < OamScanDots => OamScanDots,
            < DrawingEndDots => DrawingEndDots,
            _ => ScanlineDots,
        };
    }

    private void AdvanceScanline()
    {
        _lineDots = 0;
        _firstScanlineAfterLcdEnable = false;

        if (_lcdYCoordinate == LastScanline)
        {
            _lcdYCoordinate = 0;
        }
        else
        {
            _lcdYCoordinate++;
            if (_lcdYCoordinate == VBlankStartLine)
            {
                interrupts.Request(InterruptSource.VBlank);
            }
        }

        RefreshPpuState(requestStatInterrupt: true);
    }

    private void ResetLcdTiming()
    {
        _lineDots = 0;
        _lcdYCoordinate = 0;
        _statusMode = ModeHBlank;
        _firstScanlineAfterLcdEnable = false;
        RefreshStatInterruptLine(requestInterrupt: false);
    }

    private void StartLcdTiming()
    {
        _lineDots = 0;
        _firstScanlineAfterLcdEnable = true;
        _statusMode = ModeHBlank;

        bool oldLycEqualsLy = _lycEqualsLy;
        RefreshLycEqualsLy();

        bool shouldSuppressStableLycInterrupt =
            oldLycEqualsLy
            && _lycEqualsLy
            && (_statusInterruptSelect & StatusLycEqualsLyInterruptSelectMask) != 0
            && (_statusInterruptSelect & StatusMode0InterruptSelectMask) == 0;

        if (shouldSuppressStableLycInterrupt)
        {
            _statInterruptLine = IsStatInterruptLineAsserted();
            return;
        }

        RefreshStatInterruptLine(requestInterrupt: true);
    }

    private void RefreshPpuState(bool requestStatInterrupt)
    {
        _statusMode = CalculateMode();
        RefreshLycEqualsLy();
        RefreshStatInterruptLine(requestStatInterrupt);
    }

    private byte CalculateMode()
    {
        if (!IsLcdEnabled)
        {
            return ModeHBlank;
        }

        if (_lcdYCoordinate >= VBlankStartLine)
        {
            return ModeVBlank;
        }

        if (_firstScanlineAfterLcdEnable && _lcdYCoordinate == 0 && _lineDots < OamScanDots)
        {
            return ModeHBlank;
        }

        return _lineDots switch
        {
            < OamScanDots => ModeOamScan,
            < DrawingEndDots => ModeDrawing,
            _ => ModeHBlank,
        };
    }

    private void RefreshLycEqualsLy()
    {
        _lycEqualsLy = _lcdYCoordinate == _lcdYCompare;
    }

    private void RefreshStatInterruptLine(bool requestInterrupt)
    {
        bool statInterruptLine = IsStatInterruptLineAsserted();

        if (requestInterrupt && !_statInterruptLine && statInterruptLine)
        {
            interrupts.Request(InterruptSource.Lcd);
        }

        _statInterruptLine = statInterruptLine;
    }

    private bool IsStatInterruptLineAsserted()
    {
        if (!IsLcdEnabled)
        {
            return false;
        }

        return (_statusInterruptSelect & GetCurrentModeInterruptSelectMask()) != 0
            || (
                _lycEqualsLy && (_statusInterruptSelect & StatusLycEqualsLyInterruptSelectMask) != 0
            );
    }

    private byte GetCurrentModeInterruptSelectMask() =>
        _statusMode switch
        {
            ModeHBlank => StatusMode0InterruptSelectMask,
            ModeVBlank => StatusMode1InterruptSelectMask,
            ModeOamScan => StatusMode2InterruptSelectMask,
            _ => 0,
        };

    private static ArgumentOutOfRangeException CreateUnsupportedRegisterException(ushort address) =>
        new(nameof(address), address, "Address must target an LCD/PPU register.");
}
