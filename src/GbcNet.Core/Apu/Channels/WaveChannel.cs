// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu.Components;

namespace GbcNet.Core.Apu.Channels;

/// <summary>
/// CH3 wave channel state, including CPU-visible Wave RAM.
/// </summary>
internal sealed class WaveChannel(bool usesMonochromeWaveRamAccess)
{
    internal const ushort WaveRamStart = 0xFF30;
    internal const ushort WaveRamEnd = 0xFF3F;

    private const byte DacEnableMask = 0x80;
    private const byte LengthEnableMask = 0x40;
    private const byte TriggerMask = 0x80;
    private const byte OutputLevelMask = 0x60;
    private const byte PeriodHighMask = 0x07;

    private const int OutputLevelShift = 5;
    private const int PeriodHighShift = 8;
    private const int WavePeriodClockTCycles = 2;
    private const int PeriodReloadBase = 2048;
    private const int SampleIndexMask = 0x1F;
    private const byte OutputLevelFull = 1;
    private const byte OutputLevelHalf = 2;
    private const byte OutputLevelQuarter = 3;

    private readonly byte[] _waveRam = new byte[16];
    private readonly LengthCounter _length = new(256);
    private int _periodTimer;
    private int _tCycleAccumulator;
    private byte _outputLevel;
    private byte _sampleIndex;
    private byte _sampleBuffer;
    private bool _waveRamAccessWindowOpen;

    /// <summary>
    /// Whether CH3 generation is currently active and reported through NR52 bit 2.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Whether the channel DAC is enabled.
    /// </summary>
    public bool DacEnabled { get; private set; }

    /// <summary>
    /// Current 11-bit wave period latched from NR33/NR34.
    /// </summary>
    public ushort Period { get; private set; }

    /// <summary>
    /// Current CH3 digital output after NR32 shifting.
    /// </summary>
    public byte DigitalOutput =>
        IsActive
            ? _outputLevel switch
            {
                OutputLevelFull => _sampleBuffer,
                OutputLevelHalf => (byte)(_sampleBuffer >> 1),
                OutputLevelQuarter => (byte)(_sampleBuffer >> 2),
                _ => (byte)0,
            }
            : (byte)0;

    /// <summary>
    /// Reads CPU-visible Wave RAM, applying the active-channel lock.
    /// </summary>
    public byte ReadWaveRam(ushort address)
    {
        if (!IsActive)
        {
            return _waveRam[address - WaveRamStart];
        }

        return usesMonochromeWaveRamAccess && !_waveRamAccessWindowOpen
            ? (byte)0xFF
            : _waveRam[_sampleIndex >> 1];
    }

    /// <summary>
    /// Writes CPU-visible Wave RAM, applying the active-channel lock.
    /// </summary>
    public void WriteWaveRam(ushort address, byte value)
    {
        if (IsActive)
        {
            if (!usesMonochromeWaveRamAccess || _waveRamAccessWindowOpen)
            {
                _waveRam[_sampleIndex >> 1] = value;
            }

            return;
        }

        _waveRam[address - WaveRamStart] = value;
    }

    /// <summary>
    /// Seeds Wave RAM without applying CPU active-channel access restrictions.
    /// </summary>
    public void SetWaveRamState(ushort address, byte value)
    {
        _waveRam[address - WaveRamStart] = value;
    }

    /// <summary>
    /// Applies NR30 DAC enable; disabling the DAC also disables CH3.
    /// </summary>
    public void WriteDac(byte value)
    {
        DacEnabled = (value & DacEnableMask) != 0;
        if (!DacEnabled)
        {
            IsActive = false;
            _waveRamAccessWindowOpen = false;
        }
    }

    /// <summary>
    /// Loads NR31 initial length into the 256-step length counter.
    /// </summary>
    public void WriteLength(byte value)
    {
        _length.WriteInitialLength(value);
    }

    /// <summary>
    /// Latches NR32 output level bits.
    /// </summary>
    public void WriteOutputLevel(byte value)
    {
        _outputLevel = (byte)((value & OutputLevelMask) >> OutputLevelShift);
    }

    /// <summary>
    /// Latches NR33 period low bits.
    /// </summary>
    public void WritePeriodLow(byte value)
    {
        Period = (ushort)((Period & 0x700) | value);
    }

    /// <summary>
    /// Applies NR34 period high, length enable, and trigger side effects.
    /// </summary>
    public void WriteControl(byte value, ApuLengthWriteContext context)
    {
        Period = (ushort)((Period & 0xFF) | ((value & PeriodHighMask) << PeriodHighShift));
        var triggered = (value & TriggerMask) != 0;
        if (_length.WriteControl((value & LengthEnableMask) != 0, triggered, context))
        {
            IsActive = false;
            _waveRamAccessWindowOpen = false;
        }

        if (!triggered)
        {
            return;
        }

        if (usesMonochromeWaveRamAccess && _waveRamAccessWindowOpen)
        {
            CorruptWaveRamOnRetrigger();
        }

        _periodTimer = PeriodReloadBase - Period;
        _tCycleAccumulator = 0;
        _sampleIndex = 0;
        _waveRamAccessWindowOpen = false;
        IsActive = DacEnabled;
    }

    /// <summary>
    /// Advances CH3 period timing by elapsed T-cycles.
    /// </summary>
    public void Tick(int tCycles)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tCycles);

        if (!IsActive)
        {
            return;
        }

        _tCycleAccumulator += tCycles;

        while (_tCycleAccumulator >= WavePeriodClockTCycles)
        {
            _tCycleAccumulator -= WavePeriodClockTCycles;
            _waveRamAccessWindowOpen = false;
            _periodTimer--;

            if (_periodTimer > 0)
            {
                continue;
            }

            _periodTimer = PeriodReloadBase - Period;
            _sampleIndex = (byte)((_sampleIndex + 1) & SampleIndexMask);
            var sampleByte = _waveRam[_sampleIndex >> 1];
            _sampleBuffer =
                (_sampleIndex & 1) == 0 ? (byte)(sampleByte >> 4) : (byte)(sampleByte & 0x0F);
            _waveRamAccessWindowOpen = usesMonochromeWaveRamAccess;
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
            _waveRamAccessWindowOpen = false;
        }
    }

    /// <summary>
    /// Clears CH3 internal state without clearing Wave RAM.
    /// </summary>
    public void PowerOff()
    {
        _length.PowerOff();
        _periodTimer = 0;
        _tCycleAccumulator = 0;
        Period = 0;
        _outputLevel = 0;
        _sampleIndex = 0;
        _sampleBuffer = 0;
        _waveRamAccessWindowOpen = false;
        DacEnabled = false;
        IsActive = false;
    }

    internal WaveChannelState CaptureState() =>
        new(
            (byte[])_waveRam.Clone(),
            _length.CaptureState(),
            _periodTimer,
            _tCycleAccumulator,
            _outputLevel,
            _sampleIndex,
            _sampleBuffer,
            _waveRamAccessWindowOpen,
            IsActive,
            DacEnabled,
            Period
        );

    internal void ValidateState(WaveChannelState state)
    {
        if (state.WaveRam is null || state.WaveRam.Length != _waveRam.Length)
        {
            throw new ArgumentException(
                "Wave RAM state must contain exactly 16 bytes.",
                nameof(state)
            );
        }

        _length.ValidateState(state.Length);

        if (state.PeriodTimer is < 0 or > PeriodReloadBase)
        {
            throw new ArgumentException(
                "Wave channel period timer must be between 0 and 2048.",
                nameof(state)
            );
        }

        if (state.TCycleAccumulator is < 0 or >= WavePeriodClockTCycles)
        {
            throw new ArgumentException(
                "Wave channel T-cycle accumulator must be less than two.",
                nameof(state)
            );
        }

        if (
            state.OutputLevel > 3
            || state.SampleIndex > SampleIndexMask
            || state.SampleBuffer > 0x0F
            || (state.WaveRamAccessWindowOpen && (!usesMonochromeWaveRamAccess || !state.IsActive))
            || (
                state.WaveRamAccessWindowOpen
                && state.PeriodTimer != PeriodReloadBase - state.Period
            )
            || state.Period > 0x7FF
        )
        {
            throw new ArgumentException(
                "Wave channel state contains an invalid register value.",
                nameof(state)
            );
        }

        if (
            state.IsActive
            && (!state.DacEnabled || state.Length.Counter == 0 || state.PeriodTimer == 0)
        )
        {
            throw new ArgumentException(
                "An active wave channel requires its DAC, length counter, and period timer.",
                nameof(state)
            );
        }

        if (state.PeriodTimer == 0 && state.TCycleAccumulator != 0)
        {
            throw new ArgumentException(
                "An uninitialized wave period timer cannot retain T-cycles.",
                nameof(state)
            );
        }
    }

    internal void RestoreState(WaveChannelState state)
    {
        ValidateState(state);
        state.WaveRam.CopyTo(_waveRam, 0);
        _length.RestoreState(state.Length);
        _periodTimer = state.PeriodTimer;
        _tCycleAccumulator = state.TCycleAccumulator;
        _outputLevel = state.OutputLevel;
        _sampleIndex = state.SampleIndex;
        _sampleBuffer = state.SampleBuffer;
        _waveRamAccessWindowOpen = state.WaveRamAccessWindowOpen;
        IsActive = state.IsActive;
        DacEnabled = state.DacEnabled;
        Period = state.Period;
    }

    private void CorruptWaveRamOnRetrigger()
    {
        var currentByteIndex = _sampleIndex >> 1;
        if (currentByteIndex < 4)
        {
            _waveRam[0] = _waveRam[currentByteIndex];
            return;
        }

        Array.Copy(_waveRam, currentByteIndex & ~3, _waveRam, 0, 4);
    }
}

internal readonly record struct WaveChannelState(
    byte[] WaveRam,
    LengthCounterState Length,
    int PeriodTimer,
    int TCycleAccumulator,
    byte OutputLevel,
    byte SampleIndex,
    byte SampleBuffer,
    bool WaveRamAccessWindowOpen,
    bool IsActive,
    bool DacEnabled,
    ushort Period
);
