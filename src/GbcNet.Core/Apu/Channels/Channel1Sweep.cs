// Copyright (C) 2026 thomas-fazzari
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
    private const ushort MaxPeriod = 0x07FF;

    private byte _register;
    private byte _timer;
    private ushort _shadowPeriod;
    private bool _enabled;

    public void WriteRegister(byte value)
    {
        _register = value;
    }

    public Channel1SweepResult Trigger(ushort period)
    {
        var pace = (byte)((_register & PaceMask) >> PaceShift);
        var shift = (byte)(_register & ShiftMask);
        _shadowPeriod = period;
        _timer = pace;
        _enabled = pace != 0 || shift != 0;
        return shift == 0 ? default : GetOverflowCheckResult(period: _shadowPeriod);
    }

    public Channel1SweepResult Clock()
    {
        var pace = (byte)((_register & PaceMask) >> PaceShift);
        if (!_enabled || pace == 0)
        {
            return default;
        }

        _timer--;
        if (_timer != 0)
        {
            return default;
        }

        _timer = pace;
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
    }

    private Channel1SweepResult GetOverflowCheckResult(ushort period)
    {
        var delta = period >> (_register & ShiftMask);
        var nextPeriod = (_register & DirectionSubtractMask) == 0 ? period + delta : period - delta;
        return nextPeriod > MaxPeriod
            ? new Channel1SweepResult(Overflowed: true, PeriodChanged: false, Period: 0)
            : new Channel1SweepResult(
                Overflowed: false,
                PeriodChanged: true,
                Period: (ushort)nextPeriod
            );
    }
}
