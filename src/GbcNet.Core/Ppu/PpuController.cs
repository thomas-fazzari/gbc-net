using GbcNet.Core.Memory;

namespace GbcNet.Core.Ppu;

/// <summary>
/// Stores CPU-visible LCD/PPU register state.
/// </summary>
internal sealed class PpuController
{
    /// <summary>
    /// STAT bit 7 reads back as set.
    /// </summary>
    private const byte StatusReadMask = 0x80;

    /// <summary>
    /// STAT bits 6-3 select LCD interrupt sources and are writable by the CPU.
    /// </summary>
    private const byte StatusInterruptSelectMask = 0x78;

    /// <summary>
    /// STAT bit 2 is set when LY equals LYC.
    /// </summary>
    private const byte StatusLycEqualsLyMask = 0x04;

    /// <summary>
    /// STAT bits 1-0 expose the current PPU mode.
    /// </summary>
    private const byte StatusModeMask = 0x03;

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
    private bool _lycEqualsLy;

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
    public byte ReadRegister(ushort address)
    {
        return address switch
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
    }

    /// <summary>
    /// Writes an LCD/PPU register as the CPU sees it.
    /// </summary>
    public void WriteRegister(ushort address, byte value)
    {
        switch (address)
        {
            case AddressMap.LcdStatusRegister:
                _statusInterruptSelect = (byte)(value & StatusInterruptSelectMask);
                return;
            case AddressMap.LcdYCoordinateRegister:
                return;
            default:
                SetReadWriteRegister(address, value);
                return;
        }
    }

    /// <summary>
    /// Seeds an LCD/PPU register without modeling a CPU bus write.
    /// </summary>
    internal void SetRegisterState(ushort address, byte value)
    {
        switch (address)
        {
            case AddressMap.LcdStatusRegister:
                _statusInterruptSelect = (byte)(value & StatusInterruptSelectMask);
                _lycEqualsLy = (value & StatusLycEqualsLyMask) != 0;
                _statusMode = (byte)(value & StatusModeMask);
                return;
            case AddressMap.LcdYCoordinateRegister:
                _lcdYCoordinate = value;
                return;
            default:
                SetReadWriteRegister(address, value);
                return;
        }
    }

    private byte ReadStatus()
    {
        byte lycEqualsLy = _lycEqualsLy ? StatusLycEqualsLyMask : (byte)0;
        return (byte)(StatusReadMask | _statusInterruptSelect | lycEqualsLy | _statusMode);
    }

    private void SetReadWriteRegister(ushort address, byte value)
    {
        switch (address)
        {
            case AddressMap.LcdControlRegister:
                _control = value;
                return;
            case AddressMap.ScrollYRegister:
                _scrollY = value;
                return;
            case AddressMap.ScrollXRegister:
                _scrollX = value;
                return;
            case AddressMap.LcdYCompareRegister:
                _lcdYCompare = value;
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

    private static ArgumentOutOfRangeException CreateUnsupportedRegisterException(ushort address) =>
        new(nameof(address), address, "Address must target an LCD/PPU register.");
}
