// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Apu.Channels;

/// <summary>
/// CH1 sweep result for one trigger or sweep clock.
/// </summary>
internal readonly record struct Channel1SweepResult(
    bool Overflowed,
    bool PeriodChanged,
    ushort Period
);

/// <summary>
/// CH1 period sweep shadow state and 128 Hz sweep clock behavior.
/// </summary>
internal sealed class Channel1Sweep
{
    private const byte PaceMask = 0x70;
    private const byte DirectionSubtractMask = 0x08;
    private const byte ShiftMask = 0x07;
    private const int PaceShift = 4;
    private const byte DisabledPaceReload = 8;
    private const ushort MaxPeriod = 0x07FF;

    private byte _register;
    private byte _timer;
    private ushort _shadowPeriod;
    private bool _enabled;
    private bool _subtractionCalculated;

    public bool WriteRegister(byte value)
    {
        var previousPace = GetPace(_register);
        var disablesAfterSubtraction =
            _subtractionCalculated
            && (_register & DirectionSubtractMask) != 0
            && (value & DirectionSubtractMask) == 0;
        _register = value;
        var pace = GetPace(value);
        if (previousPace == 0 && pace != 0)
        {
            _timer = pace;
        }

        return disablesAfterSubtraction;
    }

    public Channel1SweepResult Trigger(ushort period)
    {
        var pace = GetPace(_register);
        var shift = (byte)(_register & ShiftMask);
        _shadowPeriod = period;
        _timer = GetTimerReload(pace);
        _enabled = pace != 0 || shift != 0;
        _subtractionCalculated = false;
        return shift == 0 ? default : GetOverflowCheckResult(period: _shadowPeriod);
    }

    public Channel1SweepResult Clock()
    {
        if (!_enabled)
        {
            return default;
        }

        _timer--;
        if (_timer != 0)
        {
            return default;
        }

        var pace = GetPace(_register);
        _timer = GetTimerReload(pace);
        if (pace == 0)
        {
            return default;
        }

        if ((_register & ShiftMask) == 0)
        {
            return default;
        }

        var result = GetOverflowCheckResult(_shadowPeriod);
        if (result.Overflowed)
        {
            return result;
        }

        _shadowPeriod = result.Period;
        var secondCheck = GetOverflowCheckResult(_shadowPeriod);
        return new Channel1SweepResult(
            Overflowed: secondCheck.Overflowed,
            PeriodChanged: true,
            Period: result.Period
        );
    }

    public void PowerOff()
    {
        _register = 0;
        _timer = 0;
        _shadowPeriod = 0;
        _enabled = false;
        _subtractionCalculated = false;
    }

    internal Channel1SweepState CaptureState() =>
        new(_register, _timer, _shadowPeriod, _enabled, _subtractionCalculated);

    internal static void ValidateState(Channel1SweepState state)
    {
        if (state.ShadowPeriod > MaxPeriod)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
    }

    internal void RestoreState(Channel1SweepState state)
    {
        ValidateState(state);
        _register = state.Register;
        _timer = state.Timer;
        _shadowPeriod = state.ShadowPeriod;
        _enabled = state.Enabled;
        _subtractionCalculated = state.SubtractionCalculated;
    }

    private Channel1SweepResult GetOverflowCheckResult(ushort period)
    {
        var delta = period >> (_register & ShiftMask);
        var subtract = (_register & DirectionSubtractMask) != 0;
        _subtractionCalculated |= subtract;
        var nextPeriod = subtract ? period - delta : period + delta;
        return nextPeriod > MaxPeriod
            ? new Channel1SweepResult(Overflowed: true, PeriodChanged: false, Period: 0)
            : new Channel1SweepResult(
                Overflowed: false,
                PeriodChanged: true,
                Period: (ushort)nextPeriod
            );
    }

    private static byte GetPace(byte register) => (byte)((register & PaceMask) >> PaceShift);

    private static byte GetTimerReload(byte pace) => pace == 0 ? DisabledPaceReload : pace;
}

internal readonly record struct Channel1SweepState(
    byte Register,
    byte Timer,
    ushort ShadowPeriod,
    bool Enabled,
    bool SubtractionCalculated
);
