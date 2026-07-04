// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu;
using GbcNet.Core.Interrupts;
using GbcNet.Core.Serial;
using GbcNet.Core.Timers;

namespace GbcNet.Core.Clock;

/// <summary>
/// Owns the shared system counter and dispatches DIV falling-edge effects.
/// </summary>
internal sealed class ClockController
{
    // Pan Docs: KEY1-armed STOP pauses the CPU for 2050 M-cycles while DIV is frozen.
    private const int SpeedSwitchPauseDuration = 2050;

    private const byte Key1SwitchArmedMask = 0x01;
    private const byte Key1ReadMask = 0x7E;
    private const byte Key1CurrentSpeedMask = 0x80;
    private const byte DisabledRegisterValue = 0xFF;

    private readonly SystemCounter _systemCounter = new();
    private readonly SerialController _serial;
    private readonly ApuController _apu;

    private readonly bool _isKey1RegisterEnabled;
    private bool _speedSwitchArmed;
    private int _speedSwitchPauseCycles;

    public ClockController(
        InterruptController interrupts,
        SerialController serial,
        ApuController apu,
        bool isKey1RegisterEnabled = false,
        bool ticksTimerOnTacDisableWhenInputHigh = true,
        bool ticksTimerOnTacEnableWhenInputHigh = false
    )
    {
        _serial = serial;
        _apu = apu;
        _isKey1RegisterEnabled = isKey1RegisterEnabled;
        Timers = new TimerController(
            interrupts,
            _systemCounter,
            ticksTimerOnTacDisableWhenInputHigh,
            ticksTimerOnTacEnableWhenInputHigh
        );
    }

    /// <summary>
    /// Programmable timer registers clocked from the shared system counter.
    /// </summary>
    public TimerController Timers { get; }

    /// <summary>
    /// Indicates that CGB CPU double-speed mode is active.
    /// </summary>
    public bool CgbDoubleSpeed { get; private set; }

    /// <summary>
    /// Remaining CGB speed-switch pause duration in CPU machine cycles.
    /// </summary>
    public int SpeedSwitchPauseCycles => _speedSwitchPauseCycles;

    /// <summary>
    /// Number of PPU/APU T-cycles elapsed per CPU machine cycle at the current speed.
    /// </summary>
    public int VideoAndAudioTCyclesPerMachineCycle =>
        CgbDoubleSpeed
            ? HardwareTiming.DoubleSpeedMachineCycleTCycles
            : HardwareTiming.MachineCycleTCycles;

    /// <summary>
    /// Reads the CPU-visible DIV register value.
    /// </summary>
    public byte ReadDivider() => _systemCounter.ReadDivider();

    /// <summary>
    /// Reads KEY1 with current speed, armed bit, and unused bits set.
    /// </summary>
    public byte ReadKey1()
    {
        if (!_isKey1RegisterEnabled)
        {
            return DisabledRegisterValue;
        }

        var value = Key1ReadMask;
        if (CgbDoubleSpeed)
        {
            value |= Key1CurrentSpeedMask;
        }

        if (_speedSwitchArmed)
        {
            value |= Key1SwitchArmedMask;
        }

        return value;
    }

    /// <summary>
    /// Writes KEY1; only bit 0 is CPU-writable.
    /// </summary>
    public void WriteKey1(byte value)
    {
        if (_isKey1RegisterEnabled)
        {
            _speedSwitchArmed = (value & Key1SwitchArmedMask) != 0;
        }
    }

    /// <summary>
    /// Seeds KEY1 without modeling a CPU write.
    /// </summary>
    internal void SetKey1State(byte value)
    {
        if (!_isKey1RegisterEnabled)
        {
            return;
        }

        CgbDoubleSpeed = (value & Key1CurrentSpeedMask) != 0;
        _speedSwitchArmed = (value & Key1SwitchArmedMask) != 0;
    }

    /// <summary>
    /// Applies a CGB speed switch requested by STOP, if armed, and starts the hardware pause.
    /// </summary>
    public bool TryStartSpeedSwitch()
    {
        if (!_isKey1RegisterEnabled || !_speedSwitchArmed)
        {
            return false;
        }

        ResetDivider();
        _speedSwitchArmed = false;
        CgbDoubleSpeed = !CgbDoubleSpeed;
        _speedSwitchPauseCycles = SpeedSwitchPauseDuration;
        return true;
    }

    /// <summary>
    /// Consumes one CGB speed-switch pause machine cycle without advancing the system counter.
    /// </summary>
    public bool TryStepSpeedSwitchPause()
    {
        if (_speedSwitchPauseCycles == 0)
        {
            return false;
        }

        _speedSwitchPauseCycles--;
        return true;
    }

    /// <summary>
    /// Advances the system counter one CPU machine cycle and dispatches falling-edge events.
    /// </summary>
    public void TickMachineCycle()
    {
        Timers.AdvanceOverflowReload();
        DispatchSystemCounterFallingEdges(_systemCounter.AdvanceMachineCycle());
    }

    /// <summary>
    /// Clears DIV as a CPU write would, including falling-edge side effects.
    /// </summary>
    public void ResetDivider()
    {
        DispatchSystemCounterFallingEdges(_systemCounter.Reset());
    }

    /// <summary>
    /// Seeds the visible divider state without CPU write side effects.
    /// </summary>
    internal void SetDivider(byte value)
    {
        _systemCounter.SetDivider(value);
        _serial.SetMasterClockStateFromCounter(_systemCounter.DividerCounter);
    }

    /// <summary>
    /// Seeds the raw system counter state without CPU write side effects.
    /// </summary>
    internal void SetCounter(ushort value)
    {
        _systemCounter.SetCounter(value);
        _serial.SetMasterClockStateFromCounter(_systemCounter.DividerCounter);
    }

    private void DispatchSystemCounterFallingEdges(ushort fallingEdges)
    {
        Timers.TickSystemCounter(fallingEdges);
        _serial.TickSystemCounter(fallingEdges);
        _apu.TickSystemCounter(new ApuTickInputs(fallingEdges, CgbDoubleSpeed));
    }
}
