// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Apu.Components;

/// <summary>
/// Shared APU length counter that disables a channel when enabled and expired.
/// </summary>
internal sealed class LengthCounter(int maxLength)
{
    private int _counter;
    private bool _enabled;

    /// <summary>
    /// Loads the hardware initial length as remaining ticks until expiry.
    /// </summary>
    public void WriteInitialLength(byte value)
    {
        _counter = maxLength - value;
    }

    /// <summary>
    /// Applies NRx4 length enable and trigger timing against the next frame-sequencer step.
    /// </summary>
    public bool WriteControl(bool enabled, bool triggered, ApuLengthWriteContext context)
    {
        var wasEnabled = _enabled;
        _enabled = enabled;

        var expiredWithoutTrigger = false;
        if (!context.NextStepClocksLength && !wasEnabled && enabled && _counter != 0)
        {
            _counter--;
            expiredWithoutTrigger = _counter == 0 && !triggered;
        }

        if (!triggered || _counter != 0)
        {
            return expiredWithoutTrigger;
        }

        _counter = maxLength;
        if (!context.NextStepClocksLength && enabled)
        {
            _counter--;
        }

        return false;
    }

    /// <summary>
    /// Clocks length once and returns true when this tick expired it.
    /// </summary>
    public bool Clock()
    {
        if (!_enabled || _counter == 0)
        {
            return false;
        }

        _counter--;
        return _counter == 0;
    }

    /// <summary>
    /// Clears length state on APU power-off.
    /// </summary>
    public void PowerOff()
    {
        _counter = 0;
        _enabled = false;
    }

    internal LengthCounterState CaptureState() => new(_counter, _enabled);

    internal void ValidateState(LengthCounterState state)
    {
        if (state.Counter < 0 || state.Counter > maxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
    }

    internal void RestoreState(LengthCounterState state)
    {
        ValidateState(state);
        _counter = state.Counter;
        _enabled = state.Enabled;
    }
}

internal readonly record struct LengthCounterState(int Counter, bool Enabled);

internal readonly record struct ApuLengthWriteContext(bool NextStepClocksLength);
