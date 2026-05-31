using GbcNet.Core.Interrupts;
using GbcNet.Core.Memory;

namespace GbcNet.Core.Ppu;

/// <summary>
/// Stores CPU-visible LCD/PPU registers and delegates model-specific timing.
/// </summary>
internal sealed class PpuController(
    InterruptController interrupts,
    IPpuTimingStrategy timingStrategy
)
{
    /// <summary>
    /// LCDC bit 7 enables LCD/PPU timing.
    /// </summary>
    private const byte LcdEnableMask = 0x80;

    private byte _control;
    private byte _statusInterruptSelect;
    private byte _scrollY;
    private byte _scrollX;
    private byte _lcdYCompare;
    private byte _backgroundPalette;
    private byte _objectPalette0;
    private byte _objectPalette1;
    private byte _windowY;
    private byte _windowX;

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
            AddressMap.LcdYCoordinateRegister => timingStrategy.LcdYCoordinate,
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
    public bool CanCpuAccessVideoRam => !IsLcdEnabled || timingStrategy.CanCpuAccessVideoRam;

    /// <summary>
    /// Indicates whether the CPU can access OAM at FE00-FE9F in the current PPU mode.
    /// </summary>
    public bool CanCpuAccessObjectAttributeMemory =>
        !IsLcdEnabled || timingStrategy.CanCpuAccessObjectAttributeMemory;

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
                _statusInterruptSelect = (byte)(value & PpuStatusRegister.InterruptSelectMask);
                RequestInterrupts(
                    timingStrategy.WriteStatusInterruptSelect(_statusInterruptSelect, IsLcdEnabled)
                );
                return;
            case AddressMap.LcdYCoordinateRegister:
                return;
            case AddressMap.LcdYCompareRegister:
                _lcdYCompare = value;
                RequestInterrupts(
                    timingStrategy.WriteLycCompare(
                        _lcdYCompare,
                        _statusInterruptSelect,
                        IsLcdEnabled
                    )
                );
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

        RequestInterrupts(timingStrategy.Tick(tCycles, _lcdYCompare, _statusInterruptSelect));
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
                _statusInterruptSelect = (byte)(value & PpuStatusRegister.InterruptSelectMask);
                timingStrategy.SetStatusState(value, _statusInterruptSelect, IsLcdEnabled);
                return;
            case AddressMap.LcdYCoordinateRegister:
                timingStrategy.SetLcdYCoordinateState(
                    value,
                    _lcdYCompare,
                    _statusInterruptSelect,
                    IsLcdEnabled
                );
                return;
            case AddressMap.LcdYCompareRegister:
                _lcdYCompare = value;
                timingStrategy.SetLycCompareState(
                    _lcdYCompare,
                    _statusInterruptSelect,
                    IsLcdEnabled
                );
                return;
            default:
                SetReadWriteRegister(address, value);
                return;
        }
    }

    private byte ReadStatus()
    {
        byte lycEqualsLy = timingStrategy.LycEqualsLy ? PpuStatusRegister.LycEqualsLyMask : (byte)0;
        byte mode = IsLcdEnabled ? (byte)timingStrategy.StatusMode : (byte)PpuMode.HBlank;

        return (byte)(PpuStatusRegister.ReadMask | _statusInterruptSelect | lycEqualsLy | mode);
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
            timingStrategy.DisableLcd();
            return;
        }

        if (!wasEnabled && IsLcdEnabled)
        {
            RequestInterrupts(timingStrategy.EnableLcd(_lcdYCompare, _statusInterruptSelect));
        }
    }

    private void RequestInterrupts(PpuInterruptRequest requests)
    {
        if ((requests & PpuInterruptRequest.VBlank) is not PpuInterruptRequest.None)
        {
            interrupts.Request(InterruptSource.VBlank);
        }

        if ((requests & PpuInterruptRequest.Lcd) is not PpuInterruptRequest.None)
        {
            interrupts.Request(InterruptSource.Lcd);
        }
    }

    private static ArgumentOutOfRangeException CreateUnsupportedRegisterException(ushort address) =>
        new(nameof(address), address, "Address must target an LCD/PPU register.");
}
