// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu.Components;

namespace GbcNet.Core.Apu.Channels;

/// <summary>
/// Shared pulse-channel state for DAC power and channel trigger/active behavior.
/// </summary>
internal sealed class PulseChannel
{
    private const byte InitialLengthMask = 0x3F;
    private const byte LengthEnableMask = 0x40;
    private const byte TriggerMask = 0x80;
    private const byte PeriodHighMask = 0x07;

    private const int DutyShift = 6;
    private const int PeriodHighShift = 8;
    private const int PulsePeriodClockTCycles = 4;
    private const int MaxLength = 64;
    private const int PeriodReloadBase = 2048;

    /// <summary>
    /// Four 8-step pulse duty waveforms, indexed by duty * 8 + duty step.
    /// </summary>
    // csharpier-ignore-start
    private static readonly byte[] _dutyPatterns =
    [
        0, 0, 0, 0, 0, 0, 0, 1,
        1, 0, 0, 0, 0, 0, 0, 1,
        1, 0, 0, 0, 0, 1, 1, 1,
        0, 1, 1, 1, 1, 1, 1, 0,
    ];
    // csharpier-ignore-end

    private readonly LengthCounter _length = new(MaxLength);
    private readonly VolumeEnvelope _envelope = new();

    private int _periodTimer;
    private int _tCycleAccumulator;
    private byte _duty;
    private byte _dutyStep;
    private bool _suppressInitialOutput;

    /// <summary>
    /// Whether pulse generation is active and reported through NR52.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Whether the channel DAC is enabled.
    /// </summary>
    public bool DacEnabled => _envelope.DacEnabled;

    /// <summary>
    /// Current envelope volume.
    /// </summary>
    public byte Volume => _envelope.Volume;

    /// <summary>
    /// Current 11-bit pulse period.
    /// </summary>
    public ushort Period { get; private set; }

    /// <summary>
    /// Current pulse digital output after duty and envelope logic.
    /// </summary>
    public byte DigitalOutput =>
        IsActive && !_suppressInitialOutput && _dutyPatterns[(_duty * 8) + _dutyStep] != 0
            ? _envelope.Volume
            : (byte)0;

    /// <summary>
    /// Applies NRx1 length and duty bits.
    /// </summary>
    public void WriteLength(byte value)
    {
        _length.WriteInitialLength((byte)(value & InitialLengthMask));
        _duty = (byte)(value >> DutyShift);
    }

    /// <summary>
    /// Latches NRx3 period low bits.
    /// </summary>
    public void WritePeriodLow(byte value)
    {
        Period = (ushort)((Period & 0x700) | value);
    }

    /// <summary>
    /// Updates period from CH1 sweep write-back.
    /// </summary>
    public void SetPeriod(ushort period)
    {
        Period = (ushort)(period & 0x07FF);
    }

    /// <summary>
    /// Applies NRx2 envelope and DAC-enable side effects.
    /// </summary>
    public void WriteEnvelope(byte value)
    {
        _envelope.WriteRegister(value);

        if (_envelope.DacEnabled)
        {
            return;
        }

        IsActive = false;
        _suppressInitialOutput = false;
    }

    /// <summary>
    /// Applies NRx4 period high, length enable, and trigger side effects.
    /// </summary>
    public void WriteControl(byte value, byte envelope)
    {
        Period = (ushort)((Period & 0xFF) | ((value & PeriodHighMask) << PeriodHighShift));
        _length.SetEnabled((value & LengthEnableMask) != 0);

        if ((value & TriggerMask) == 0)
        {
            return;
        }

        _length.TriggerReloadIfExpired();
        _periodTimer = PeriodReloadBase - Period;
        _tCycleAccumulator = 0;
        _envelope.Trigger(envelope);
        _suppressInitialOutput = !IsActive && _envelope.DacEnabled;
        IsActive = _envelope.DacEnabled;
    }

    /// <summary>
    /// Advances pulse period timing by elapsed T-cycles.
    /// </summary>
    public void Tick(int tCycles)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tCycles);

        _tCycleAccumulator += tCycles;

        while (_tCycleAccumulator >= PulsePeriodClockTCycles)
        {
            _tCycleAccumulator -= PulsePeriodClockTCycles;
            _periodTimer--;

            if (_periodTimer > 0)
            {
                continue;
            }

            _periodTimer = PeriodReloadBase - Period;
            _dutyStep = (byte)((_dutyStep + 1) & 0x07);
            _suppressInitialOutput = false;
        }
    }

    /// <summary>
    /// Clocks the length counter from the DIV-APU frame sequencer.
    /// </summary>
    public void ClockLength()
    {
        if (_length.Clock())
        {
            IsActive = false;
        }
    }

    /// <summary>
    /// Clocks the volume envelope from the DIV-APU frame sequencer.
    /// </summary>
    public void ClockEnvelope()
    {
        if (IsActive)
        {
            _envelope.Clock();
        }
    }

    /// <summary>
    /// Forces the channel inactive.
    /// </summary>
    public void Disable()
    {
        IsActive = false;
        _suppressInitialOutput = false;
    }

    /// <summary>
    /// Clears pulse channel state on APU power-off.
    /// </summary>
    public void PowerOff()
    {
        _length.PowerOff();
        _envelope.PowerOff();
        _periodTimer = 0;
        _tCycleAccumulator = 0;
        Period = 0;
        _duty = 0;
        _dutyStep = 0;
        _suppressInitialOutput = false;
        IsActive = false;
    }

    internal PulseChannelState CaptureState() =>
        new(
            _length.CaptureState(),
            _envelope.CaptureState(),
            _periodTimer,
            _tCycleAccumulator,
            _duty,
            _dutyStep,
            _suppressInitialOutput,
            IsActive,
            Period
        );

    internal void ValidateState(PulseChannelState state)
    {
        _length.ValidateState(state.Length);
        VolumeEnvelope.ValidateState(state.Envelope);

        if (
            state.PeriodTimer < 0
            || state.PeriodTimer > PeriodReloadBase
            || state.Duty > 0x03
            || state.DutyStep > 0x07
            || state.Period >= PeriodReloadBase
        )
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
    }

    internal void RestoreState(PulseChannelState state)
    {
        ValidateState(state);
        _length.RestoreState(state.Length);
        _envelope.RestoreState(state.Envelope);
        _periodTimer = state.PeriodTimer;
        _tCycleAccumulator = state.TCycleAccumulator;
        _duty = state.Duty;
        _dutyStep = state.DutyStep;
        _suppressInitialOutput = state.SuppressInitialOutput;
        IsActive = state.IsActive;
        Period = state.Period;
    }
}

internal readonly record struct PulseChannelState(
    LengthCounterState Length,
    VolumeEnvelopeState Envelope,
    int PeriodTimer,
    int TCycleAccumulator,
    byte Duty,
    byte DutyStep,
    bool SuppressInitialOutput,
    bool IsActive,
    ushort Period
);
