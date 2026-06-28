using GbcNet.Core.Interrupts;
using GbcNet.Core.Sgb;

namespace GbcNet.Core.Joypad;

/// <summary>
/// Emulates the DMG P1/JOYP button matrix register.
/// </summary>
internal sealed class JoypadController(InterruptController interrupts, SgbController? sgb = null)
{
    /// <summary>
    /// JOYP bits 7-6 read back as set on DMG hardware.
    /// </summary>
    private const byte HighBitsReadMask = 0xC0;

    /// <summary>
    /// JOYP bits 5-4 select action and direction button groups.
    /// </summary>
    private const byte SelectBitsMask = 0x30;

    /// <summary>
    /// JOYP bit 5 is cleared by the CPU to select action buttons.
    /// </summary>
    private const byte ActionButtonsNotSelectedBit = 0x20;

    /// <summary>
    /// JOYP bit 4 is cleared by the CPU to select direction buttons.
    /// </summary>
    private const byte DirectionButtonsNotSelectedBit = 0x10;

    /// <summary>
    /// JOYP bits 0-3 read as set when no selected button is pressed.
    /// </summary>
    private const byte ReleasedLowNibble = 0x0F;

    /// <summary>
    /// Stored pressed-state bits for direction buttons.
    /// </summary>
    private const byte DirectionButtonMask = 0x0F;

    /// <summary>
    /// Number of bits between matching direction and action buttons in storage.
    /// </summary>
    private const int ActionButtonShift = 4;

    private byte _selectedGroups = SelectBitsMask;
    private byte _pressedButtons;

    /// <summary>
    /// Reads JOYP with selected pressed buttons pulled low.
    /// </summary>
    public byte Read()
    {
        var lowNibble = ReadLowNibble();
        var selectedLowNibble = sgb?.ReadLowNibble(_selectedGroups, lowNibble) ?? lowNibble;
        return (byte)(HighBitsReadMask | _selectedGroups | selectedLowNibble);
    }

    /// <summary>
    /// Indicates that at least one selected button line is pulled low.
    /// </summary>
    internal bool HasSelectedLineLow => ReadLowNibble() != ReleasedLowNibble;

    /// <summary>
    /// Writes JOYP selection bits, ignoring read-only button state bits.
    /// </summary>
    public void Write(byte value, bool requestInterruptOnTransition)
    {
        var previousValue = Read();
        var previousSelectedGroups = _selectedGroups;
        _selectedGroups = (byte)(value & SelectBitsMask);

        if (requestInterruptOnTransition)
        {
            sgb?.Write(value, previousSelectedGroups);
        }

        if (requestInterruptOnTransition)
        {
            RequestInterruptOnHighToLowTransition(previousValue, Read());
        }
    }

    /// <summary>
    /// Sets a button state and requests Joypad interrupt on visible high-to-low transitions.
    /// </summary>
    public void SetButtonState(JoypadButton button, bool pressed)
    {
        var previousValue = Read();
        var buttonMask = GetButtonMask(button);
        _pressedButtons = pressed
            ? (byte)(_pressedButtons | buttonMask)
            : (byte)(_pressedButtons & ~buttonMask);
        RequestInterruptOnHighToLowTransition(previousValue, Read());
    }

    private byte ReadLowNibble()
    {
        byte pressedLowNibble = 0;
        if ((_selectedGroups & DirectionButtonsNotSelectedBit) == 0)
        {
            pressedLowNibble = (byte)(_pressedButtons & DirectionButtonMask);
        }

        if ((_selectedGroups & ActionButtonsNotSelectedBit) == 0)
        {
            pressedLowNibble = (byte)(pressedLowNibble | (_pressedButtons >> ActionButtonShift));
        }

        return (byte)(ReleasedLowNibble & ~pressedLowNibble);
    }

    private void RequestInterruptOnHighToLowTransition(byte previousValue, byte currentValue)
    {
        var newlyPressedVisibleBits = (byte)(previousValue & ~currentValue & ReleasedLowNibble);

        if (newlyPressedVisibleBits != 0)
        {
            interrupts.Request(InterruptSource.Joypad);
        }
    }

    private static byte GetButtonMask(JoypadButton button)
    {
        if ((uint)button > (uint)JoypadButton.Start)
        {
            throw new ArgumentOutOfRangeException(nameof(button), button, message: null);
        }

        return (byte)(1 << (int)button);
    }
}
