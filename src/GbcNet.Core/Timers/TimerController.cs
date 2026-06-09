using GbcNet.Core.Clock;
using GbcNet.Core.Interrupts;

namespace GbcNet.Core.Timers;

/// <summary>
/// Emulates the DMG divider and programmable timer registers.
/// </summary>
internal sealed class TimerController(InterruptController interrupts, SystemCounter systemCounter)
{
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

    private byte _timerCounter;
    private byte _timerControl;
    private TimerReloadState _reloadState;

    /// <summary>
    /// TIMA register at FF05, incremented by the selected timer clock.
    /// </summary>
    public byte TimerCounter
    {
        get => _reloadState is TimerReloadState.Reloading ? (byte)0 : _timerCounter;
        internal set
        {
            _timerCounter = value;
            _reloadState = TimerReloadState.Running;
        }
    }

    /// <summary>
    /// TMA register at FF06, reloaded into TIMA when TIMA overflows.
    /// </summary>
    public byte TimerModulo { get; internal set; }

    /// <summary>
    /// Reads TAC with unused bits set.
    /// </summary>
    public byte ReadTimerControl() => (byte)(_timerControl | TimerControlReadMask);

    /// <summary>
    /// Writes TIMA, cancelling a pending overflow reload unless the reload already happened.
    /// </summary>
    internal void WriteTimerCounter(byte value)
    {
        if (_reloadState is TimerReloadState.Reloaded)
        {
            return;
        }

        _timerCounter = value;
        _reloadState = TimerReloadState.Running;
    }

    /// <summary>
    /// Writes TMA, updating TIMA too during the delayed reload window.
    /// </summary>
    internal void WriteTimerModulo(byte value)
    {
        TimerModulo = value;

        if (_reloadState is not TimerReloadState.Running)
        {
            _timerCounter = value;
        }
    }

    /// <summary>
    /// Writes TAC and applies the DMG falling-edge tick caused by changing timer input.
    /// </summary>
    internal void WriteTimerControl(byte value)
    {
        byte newTimerControl = (byte)(value & TimerControlWriteMask);
        bool wasTimerInputHigh = IsTimerInputHigh(_timerControl, systemCounter.Value);
        bool isTimerInputHigh = IsTimerInputHigh(newTimerControl, systemCounter.Value);
        _timerControl = newTimerControl;

        // TAC writes can create a synthetic falling edge without the system counter ticking
        if (wasTimerInputHigh && !isTimerInputHigh)
        {
            IncrementTimerCounter();
        }
    }

    /// <summary>
    /// Seeds TAC without triggering CPU write timing effects.
    /// </summary>
    internal void SetTimerControlState(byte value)
    {
        _timerControl = (byte)(value & TimerControlWriteMask);
    }

    /// <summary>
    /// Advances the delayed TIMA overflow reload pipeline by one CPU machine cycle.
    /// </summary>
    public void AdvanceReloadPipeline()
    {
        switch (_reloadState)
        {
            // The interrupt is delayed one M-cycle after TIMA overflows and reloads from TMA
            case TimerReloadState.Reloading:
                interrupts.Request(InterruptSource.Timer);
                _reloadState = TimerReloadState.Reloaded;
                return;
            // CPU writes to TIMA during the reload M-cycle are ignored, then normal writes resume
            case TimerReloadState.Reloaded:
                _reloadState = TimerReloadState.Running;
                return;
        }
    }

    /// <summary>
    /// Applies falling edges produced by the shared divider counter.
    /// </summary>
    public void TickSystemCounter(ushort fallingEdges)
    {
        if (
            (_timerControl & TimerEnableMask) != 0
            && (fallingEdges & GetClockBitMask(_timerControl)) != 0
        )
        {
            IncrementTimerCounter();
        }
    }

    private static bool IsTimerInputHigh(byte timerControl, ushort systemCounter) =>
        (timerControl & TimerEnableMask) != 0
        && (systemCounter & GetClockBitMask(timerControl)) != 0;

    private static ushort GetClockBitMask(byte timerControl) =>
        (timerControl & TimerClockSelectMask) switch
        {
            0b00 => ClockSelect00BitMask,
            0b01 => ClockSelect01BitMask,
            0b10 => ClockSelect10BitMask,
            _ => ClockSelect11BitMask,
        };

    private void IncrementTimerCounter()
    {
        _timerCounter++;

        if (_timerCounter is not 0)
        {
            return;
        }

        _timerCounter = TimerModulo;
        _reloadState = TimerReloadState.Reloading;
    }

    private enum TimerReloadState
    {
        Running = 0,
        Reloading = 1,
        Reloaded = 2,
    }
}
