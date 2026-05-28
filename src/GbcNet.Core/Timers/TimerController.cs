using GbcNet.Core.Interrupts;

namespace GbcNet.Core.Timers;

/// <summary>
/// Emulates the DMG divider and programmable timer registers.
/// </summary>
internal sealed class TimerController(InterruptController interrupts)
{
    /// <summary>
    /// DIV exposes bits 15-8 of the internal 16-bit system counter.
    /// </summary>
    private const int DividerVisibleShift = 8;

    /// <summary>
    /// Unused TAC bits read back as set on DMG hardware.
    /// </summary>
    private const byte TimerControlReadMask = 0xF8;

    /// <summary>
    /// TAC stores only enable and clock select bits.
    /// </summary>
    private const byte TimerControlWriteMask = 0x07;

    /// <summary>
    /// TAC bit 2 enables TIMA increments.
    /// </summary>
    private const byte TimerEnableMask = 0x04;

    /// <summary>
    /// TAC bits 1-0 select which divider bit clocks TIMA.
    /// </summary>
    private const byte TimerClockSelectMask = 0x03;

    /// <summary>
    /// TAC clock select 00: divider bit 9 falls every 1024 T-cycles.
    /// </summary>
    private const ushort ClockSelect00BitMask = 1 << 9;

    /// <summary>
    /// TAC clock select 01: divider bit 3 falls every 16 T-cycles.
    /// </summary>
    private const ushort ClockSelect01BitMask = 1 << 3;

    /// <summary>
    /// TAC clock select 10: divider bit 5 falls every 64 T-cycles.
    /// </summary>
    private const ushort ClockSelect10BitMask = 1 << 5;

    /// <summary>
    /// TAC clock select 11: divider bit 7 falls every 256 T-cycles.
    /// </summary>
    private const ushort ClockSelect11BitMask = 1 << 7;

    private ushort _systemCounter;
    private byte _timerControl;

    /// <summary>
    /// TIMA register at FF05, incremented by the selected timer clock.
    /// </summary>
    public byte TimerCounter { get; set; }

    /// <summary>
    /// TMA register at FF06, reloaded into TIMA when TIMA overflows.
    /// </summary>
    public byte TimerModulo { get; set; }

    /// <summary>
    /// Reads DIV as the high byte of the internal system counter.
    /// </summary>
    public byte ReadDivider() => (byte)(_systemCounter >> DividerVisibleShift);

    /// <summary>
    /// Resets the internal system counter, making DIV read as zero.
    /// </summary>
    public void ResetDivider()
    {
        _systemCounter = 0;
    }

    /// <summary>
    /// Sets the internal divider counter when skipping boot ROM execution.
    /// </summary>
    internal void SetDivider(byte value)
    {
        _systemCounter = (ushort)(value << DividerVisibleShift);
    }

    /// <summary>
    /// Reads TAC with unused bits set.
    /// </summary>
    public byte ReadTimerControl() => (byte)(_timerControl | TimerControlReadMask);

    /// <summary>
    /// Sets TAC enable and clock select bits.
    /// </summary>
    internal void SetTimerControl(byte value)
    {
        _timerControl = (byte)(value & TimerControlWriteMask);
    }

    /// <summary>
    /// Advances the divider and timer by the specified number of T-cycles.
    /// </summary>
    public void Tick(int tCycles)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tCycles);

        for (int cycle = 0; cycle < tCycles; cycle++)
        {
            TickOneTCycle();
        }
    }

    private void TickOneTCycle()
    {
        ushort previousCounter = _systemCounter;
        _systemCounter = unchecked((ushort)(_systemCounter + 1));

        if ((_timerControl & TimerEnableMask) != 0 && HasSelectedTimerBitFallen(previousCounter))
        {
            IncrementTimerCounter();
        }
    }

    private bool HasSelectedTimerBitFallen(ushort previousCounter)
    {
        ushort selectedBitMask = (_timerControl & TimerClockSelectMask) switch
        {
            0b00 => ClockSelect00BitMask,
            0b01 => ClockSelect01BitMask,
            0b10 => ClockSelect10BitMask,
            _ => ClockSelect11BitMask,
        };

        return (previousCounter & selectedBitMask) != 0 && (_systemCounter & selectedBitMask) == 0;
    }

    private void IncrementTimerCounter()
    {
        if (TimerCounter is byte.MaxValue)
        {
            TimerCounter = TimerModulo;
            interrupts.Request(InterruptSource.Timer);
            return;
        }

        TimerCounter++;
    }
}
