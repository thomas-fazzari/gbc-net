// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu.Channels;
using GbcNet.Core.Apu.Components;

namespace GbcNet.Core.Apu;

/// <summary>
/// Stores CPU-visible Audio Processing Unit registers and delegates hardware-specific behavior.
/// </summary>
internal sealed class ApuController(ApuModelSpec modelSpec)
{
    private const ushort RegisterStart = 0xFF10;
    private const ushort RegisterEnd = 0xFF26;

    // FF15 and FF1F sit inside the APU address range but are not real audio registers
    private const ushort UnmappedAudioAddressFf15 = 0xFF15;
    private const ushort UnmappedAudioAddressFf1F = 0xFF1F;

    private const ushort Pcm12Register = 0xFF76;
    private const ushort Pcm34Register = 0xFF77;

    private const ushort Channel1SweepRegister = 0xFF10;
    private const ushort Channel1LengthRegister = 0xFF11;
    private const ushort Channel1EnvelopeRegister = 0xFF12;
    private const ushort Channel1PeriodLowRegister = 0xFF13;
    private const ushort Channel1PeriodHighControlRegister = 0xFF14;

    private const ushort Channel2LengthRegister = 0xFF16;
    private const ushort Channel2EnvelopeRegister = 0xFF17;
    private const ushort Channel2PeriodLowRegister = 0xFF18;
    private const ushort Channel2PeriodHighControlRegister = 0xFF19;

    private const ushort Channel3DacRegister = 0xFF1A;
    private const ushort Channel3LengthRegister = 0xFF1B;
    private const ushort Channel3OutputLevelRegister = 0xFF1C;
    private const ushort Channel3PeriodLowRegister = 0xFF1D;
    private const ushort Channel3PeriodHighControlRegister = 0xFF1E;

    private const ushort Channel4LengthRegister = 0xFF20;
    private const ushort Channel4EnvelopeRegister = 0xFF21;
    private const ushort Channel4FrequencyRegister = 0xFF22;
    private const ushort Channel4ControlRegister = 0xFF23;

    private const ushort MasterVolumeRegister = 0xFF24;
    private const ushort SoundPanningRegister = 0xFF25;
    private const ushort AudioMasterControlRegister = 0xFF26;
    private const byte AudioMasterWritableMask = 0x80;
    private const byte EnvelopeDacEnableMask = 0xF8;
    private const byte WaveDacEnableMask = 0x80;

    private const byte AudioChannelStatusMask = 0x0F;
    private const byte AudioChannel1StatusMask = 0x01;
    private const byte AudioChannel2StatusMask = 0x02;
    private const byte AudioChannel3StatusMask = 0x04;
    private const byte AudioChannel4StatusMask = 0x08;

    private const byte Channel1RightRouteMask = 0x01;
    private const byte Channel2RightRouteMask = 0x02;
    private const byte Channel3RightRouteMask = 0x04;
    private const byte Channel4RightRouteMask = 0x08;
    private const byte Channel1LeftRouteMask = 0x10;
    private const byte Channel2LeftRouteMask = 0x20;
    private const byte Channel3LeftRouteMask = 0x40;
    private const byte Channel4LeftRouteMask = 0x80;

    private const byte RightVolumeMask = 0x07;
    private const byte LeftVolumeMask = 0x70;
    private const byte PeriodHighMask = 0x07;
    private const byte TriggerMask = 0x80;
    private const int LeftVolumeShift = 4;
    private const byte DivApuStepMask = 0x07;

    private readonly byte[] _registers = new byte[RegisterEnd - RegisterStart + 1];
    private readonly PulseChannel _channel1 = new();
    private readonly Channel1Sweep _channel1Sweep = new();
    private readonly PulseChannel _channel2 = new();
    private readonly WaveChannel _channel3 = new();
    private readonly NoiseChannel _channel4 = new();
    private readonly SampleBuffer<ApuStereoSample> _sampleBuffer = new(modelSpec.OutputClockHz);
    private readonly ApuOutputFilter _outputFilter = new(
        modelSpec.GetOutputHighPassChargeFactor(ApuSampleTiming.DefaultSampleRate)
    );

    /// <summary>
    /// Current DIV-APU frame sequencer step, advanced at 512 Hz.
    /// </summary>
    internal byte DivApuStep { get; private set; }

    internal ushort Channel1Period => _channel1.Period;

    internal byte Channel2Volume => _channel2.Volume;
    internal byte Channel2DigitalOutput => _channel2.DigitalOutput;
    internal byte Channel3DigitalOutput => _channel3.DigitalOutput;

    internal byte Channel4Volume => _channel4.Volume;
    internal byte Channel4DigitalOutput => _channel4.DigitalOutput;

    /// <summary>
    /// True for CPU-visible APU register addresses FF10-FF3F, excluding unmapped FF15 and FF1F.
    /// </summary>
    internal static bool ContainsRegister(ushort address) =>
        address
            is (
                    >= RegisterStart
                    and <= RegisterEnd
                    and not UnmappedAudioAddressFf15
                    and not UnmappedAudioAddressFf1F
                )
                or (>= WaveChannel.WaveRamStart and <= WaveChannel.WaveRamEnd)
                or Pcm12Register
                or Pcm34Register;

    /// <summary>
    /// Applies system-counter falling edges that clock DIV-APU timing.
    /// </summary>
    internal ApuFrameSequencerEvents TickSystemCounter(ApuTickInputs inputs)
    {
        if (
            (
                inputs.SystemCounterFallingEdges
                & modelSpec.GetDivApuFallingEdgeMask(inputs.CgbDoubleSpeed)
            ) == 0
        )
        {
            return default;
        }

        DivApuStep = (byte)((DivApuStep + 1) & DivApuStepMask);
        // Events are based on the frame sequencer step reached after this DIV-APU tick
        var events = new ApuFrameSequencerEvents(
            LengthClock: DivApuStep is 1 or 3 or 5 or 7,
            SweepClock: DivApuStep is 3 or 7,
            EnvelopeClock: DivApuStep is 7
        );

        if (events.LengthClock)
        {
            _channel1.ClockLength();
            _channel2.ClockLength();
            _channel3.ClockLength();
            _channel4.ClockLength();

            UpdateChannelStatus(AudioChannel1StatusMask, _channel1.IsActive);
            UpdateChannelStatus(AudioChannel2StatusMask, _channel2.IsActive);
            UpdateChannelStatus(AudioChannel3StatusMask, _channel3.IsActive);
            UpdateChannelStatus(AudioChannel4StatusMask, _channel4.IsActive);
        }

        if (events.SweepClock)
        {
            var sweepResult = _channel1Sweep.Clock();
            if (sweepResult.PeriodChanged)
            {
                _channel1.SetPeriod(sweepResult.Period);
                _registers[Channel1PeriodLowRegister - RegisterStart] = (byte)sweepResult.Period;
                _registers[Channel1PeriodHighControlRegister - RegisterStart] = (byte)(
                    (
                        _registers[Channel1PeriodHighControlRegister - RegisterStart]
                        & ~PeriodHighMask
                    ) | (sweepResult.Period >> 8)
                );
            }

            if (sweepResult.Overflowed)
            {
                _channel1.Disable();
                UpdateChannelStatus(AudioChannel1StatusMask, isActive: false);
            }
        }

        if (!events.EnvelopeClock)
        {
            return events;
        }

        _channel1.ClockEnvelope();
        _channel2.ClockEnvelope();
        _channel4.ClockEnvelope();

        return events;
    }

    /// <summary>
    /// Advances channel period timers and buffers fixed-rate APU samples by elapsed T-cycles.
    /// </summary>
    internal void Tick(int tCycles)
    {
        _channel1.Tick(tCycles);
        _channel2.Tick(tCycles);
        _channel3.Tick(tCycles);
        _channel4.Tick(tCycles);

        for (var samplesDue = _sampleBuffer.Tick(tCycles); samplesDue > 0; samplesDue--)
        {
            _sampleBuffer.Add(_outputFilter.Filter(GetAnalogStereoSample(), IsAnyDacEnabled()));
        }
    }

    private ApuAnalogStereoSample GetAnalogStereoSample()
    {
        var masterVolume = _registers[MasterVolumeRegister - RegisterStart];
        var panning = _registers[SoundPanningRegister - RegisterStart];

        var leftVolume = ((masterVolume & LeftVolumeMask) >> LeftVolumeShift) + 1;
        double leftInput = 0;
        var rightVolume = (masterVolume & RightVolumeMask) + 1;
        double rightInput = 0;

        if ((panning & Channel1LeftRouteMask) != 0)
        {
            leftInput += DigitalToAnalog(_channel1.DigitalOutput, _channel1.DacEnabled);
        }

        if ((panning & Channel2LeftRouteMask) != 0)
        {
            leftInput += DigitalToAnalog(_channel2.DigitalOutput, _channel2.DacEnabled);
        }

        if ((panning & Channel3LeftRouteMask) != 0)
        {
            leftInput += DigitalToAnalog(_channel3.DigitalOutput, _channel3.DacEnabled);
        }

        if ((panning & Channel4LeftRouteMask) != 0)
        {
            leftInput += DigitalToAnalog(_channel4.DigitalOutput, _channel4.DacEnabled);
        }

        if ((panning & Channel1RightRouteMask) != 0)
        {
            rightInput += DigitalToAnalog(_channel1.DigitalOutput, _channel1.DacEnabled);
        }

        if ((panning & Channel2RightRouteMask) != 0)
        {
            rightInput += DigitalToAnalog(_channel2.DigitalOutput, _channel2.DacEnabled);
        }

        if ((panning & Channel3RightRouteMask) != 0)
        {
            rightInput += DigitalToAnalog(_channel3.DigitalOutput, _channel3.DacEnabled);
        }

        if ((panning & Channel4RightRouteMask) != 0)
        {
            rightInput += DigitalToAnalog(_channel4.DigitalOutput, _channel4.DacEnabled);
        }

        return new ApuAnalogStereoSample(leftInput * leftVolume, rightInput * rightVolume);
    }

    private static double DigitalToAnalog(byte digitalOutput, bool dacEnabled) =>
        dacEnabled ? 1.0 - (digitalOutput / 7.5) : 0.0;

    /// <summary>
    /// Drains buffered fixed-rate APU samples.
    /// </summary>
    internal int DrainBufferedSamples(Span<ApuStereoSample> destination) =>
        _sampleBuffer.Drain(destination);

    /// <summary>
    /// Reads an APU register with hardware-specific unused and write-only bits applied.
    /// </summary>
    public byte ReadRegister(ushort address) =>
        address switch
        {
            >= WaveChannel.WaveRamStart and <= WaveChannel.WaveRamEnd => _channel3.ReadWaveRam(
                address
            ),
            Pcm12Register => modelSpec.IsPcmOutputRegisterEnabled
                ? (byte)((_channel2.DigitalOutput << 4) | _channel1.DigitalOutput)
                : (byte)0xFF,
            Pcm34Register => modelSpec.IsPcmOutputRegisterEnabled
                ? (byte)((_channel4.DigitalOutput << 4) | _channel3.DigitalOutput)
                : (byte)0xFF,
            _ => ApuModelSpec.ApplyRegisterReadMask(address, _registers[address - RegisterStart]),
        };

    /// <summary>
    /// Writes an APU register, respecting NR52 power state and read-only channel status bits.
    /// </summary>
    public void WriteRegister(ushort address, byte value)
    {
        // Wave RAM and NR52 bypass normal APU power gating
        switch (address)
        {
            case >= WaveChannel.WaveRamStart and <= WaveChannel.WaveRamEnd:
                _channel3.WriteWaveRam(address, value);
                return;
            case Pcm12Register:
            case Pcm34Register:
                return;
            case AudioMasterControlRegister when (value & AudioMasterWritableMask) == 0:
                // NR52 power-off clears APU registers and silences active channels, but not Wave RAM
                Array.Clear(_registers);
                _channel1.PowerOff();
                _channel2.PowerOff();
                _channel3.PowerOff();
                _channel4.PowerOff();
                _channel1Sweep.PowerOff();
                return;
            case AudioMasterControlRegister:
                _registers[AudioMasterControlRegister - RegisterStart] = (byte)(
                    (
                        _registers[AudioMasterControlRegister - RegisterStart]
                        & AudioChannelStatusMask
                    ) | AudioMasterWritableMask
                );
                return;
        }

        if ((_registers[AudioMasterControlRegister - RegisterStart] & AudioMasterWritableMask) == 0)
        {
            return;
        }

        // Keep write-only register bits latched for later trigger side effects
        _registers[address - RegisterStart] = value;

        switch (address)
        {
            // CH1: pulse plus sweep overflow handling
            case Channel1SweepRegister:
                _channel1Sweep.WriteRegister(value);
                return;

            case Channel1LengthRegister:
                _channel1.WriteLength(value);
                return;

            case Channel1EnvelopeRegister:
                _channel1.WriteEnvelope(value);
                UpdateChannelStatus(AudioChannel1StatusMask, _channel1.IsActive);
                return;

            case Channel1PeriodLowRegister:
                _channel1.WritePeriodLow(value);
                return;

            case Channel1PeriodHighControlRegister:
                _channel1.WriteControl(value, _registers[Channel1EnvelopeRegister - RegisterStart]);
                if ((value & TriggerMask) != 0)
                {
                    var triggerSweepResult = _channel1Sweep.Trigger(_channel1.Period);
                    if (triggerSweepResult.Overflowed)
                    {
                        _channel1.Disable();
                    }
                }

                UpdateChannelStatus(AudioChannel1StatusMask, _channel1.IsActive);
                return;

            // CH2: pulse without sweep
            case Channel2LengthRegister:
                _channel2.WriteLength(value);
                return;

            case Channel2EnvelopeRegister:
                _channel2.WriteEnvelope(value);
                UpdateChannelStatus(AudioChannel2StatusMask, _channel2.IsActive);
                return;

            case Channel2PeriodLowRegister:
                _channel2.WritePeriodLow(value);
                return;

            case Channel2PeriodHighControlRegister:
                _channel2.WriteControl(value, _registers[Channel2EnvelopeRegister - RegisterStart]);
                UpdateChannelStatus(AudioChannel2StatusMask, _channel2.IsActive);
                return;

            // CH3: wave channel, including DAC-controlled activity
            case Channel3DacRegister:
                _channel3.WriteDac(value);
                UpdateChannelStatus(AudioChannel3StatusMask, _channel3.IsActive);
                return;

            case Channel3LengthRegister:
                _channel3.WriteLength(value);
                return;

            case Channel3OutputLevelRegister:
                _channel3.WriteOutputLevel(value);
                return;

            case Channel3PeriodLowRegister:
                _channel3.WritePeriodLow(value);
                return;

            case Channel3PeriodHighControlRegister:
                _channel3.WriteControl(value);
                UpdateChannelStatus(AudioChannel3StatusMask, _channel3.IsActive);
                return;

            // CH4: noise channel, including envelope and LFSR timing
            case Channel4LengthRegister:
                _channel4.WriteLength(value);
                return;

            case Channel4EnvelopeRegister:
                _channel4.WriteEnvelope(value);
                UpdateChannelStatus(AudioChannel4StatusMask, _channel4.IsActive);
                return;

            case Channel4FrequencyRegister:
                _channel4.WriteFrequency(value);
                return;

            case Channel4ControlRegister:
                _channel4.WriteControl(value, _registers[Channel4EnvelopeRegister - RegisterStart]);
                UpdateChannelStatus(AudioChannel4StatusMask, _channel4.IsActive);
                return;
        }
    }

    private bool IsAnyDacEnabled() =>
        (_registers[Channel1EnvelopeRegister - RegisterStart] & EnvelopeDacEnableMask) != 0
        || (_registers[Channel2EnvelopeRegister - RegisterStart] & EnvelopeDacEnableMask) != 0
        || (_registers[Channel3DacRegister - RegisterStart] & WaveDacEnableMask) != 0
        || (_registers[Channel4EnvelopeRegister - RegisterStart] & EnvelopeDacEnableMask) != 0;

    private void UpdateChannelStatus(byte statusMask, bool isActive)
    {
        if (isActive)
        {
            _registers[AudioMasterControlRegister - RegisterStart] |= statusMask;
            return;
        }

        _registers[AudioMasterControlRegister - RegisterStart] &= unchecked((byte)~statusMask);
    }

    /// <summary>
    /// Seeds an APU register without applying CPU write-only restrictions.
    /// </summary>
    internal void SetRegisterState(ushort address, byte value)
    {
        if (address is >= WaveChannel.WaveRamStart and <= WaveChannel.WaveRamEnd)
        {
            _channel3.SetWaveRamState(address, value);
            return;
        }

        _registers[address - RegisterStart] = value;
    }

    internal ApuControllerState CaptureState() =>
        new(
            (byte[])_registers.Clone(),
            DivApuStep,
            _channel1.CaptureState(),
            _channel1Sweep.CaptureState(),
            _channel2.CaptureState(),
            _channel3.CaptureState(),
            _channel4.CaptureState(),
            _sampleBuffer.CaptureState(),
            _outputFilter.CaptureState()
        );

    internal void ValidateState(ApuControllerState state)
    {
        if (
            state.Registers is null
            || state.Registers.Length != _registers.Length
            || state.Registers[UnmappedAudioAddressFf15 - RegisterStart] != 0
            || state.Registers[UnmappedAudioAddressFf1F - RegisterStart] != 0
        )
        {
            throw new ArgumentException(
                "APU register state must contain only mapped registers.",
                nameof(state)
            );
        }

        if (state.DivApuStep > DivApuStepMask)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        _channel1.ValidateState(state.Channel1);
        _channel1Sweep.ValidateState(state.Channel1Sweep);
        _channel2.ValidateState(state.Channel2);
        _channel3.ValidateState(state.Channel3);
        _channel4.ValidateState(state.Channel4);
        _sampleBuffer.ValidateState(state.SampleBuffer);
        ApuOutputFilter.ValidateState(state.OutputFilter);
    }

    internal void RestoreState(ApuControllerState state)
    {
        ValidateState(state);
        state.Registers.CopyTo(_registers, 0);
        DivApuStep = state.DivApuStep;
        _channel1.RestoreState(state.Channel1);
        _channel1Sweep.RestoreState(state.Channel1Sweep);
        _channel2.RestoreState(state.Channel2);
        _channel3.RestoreState(state.Channel3);
        _channel4.RestoreState(state.Channel4);
        _sampleBuffer.RestoreState(state.SampleBuffer);
        _outputFilter.RestoreState(state.OutputFilter);
    }
}

internal readonly record struct ApuControllerState(
    byte[] Registers,
    byte DivApuStep,
    PulseChannelState Channel1,
    Channel1SweepState Channel1Sweep,
    PulseChannelState Channel2,
    WaveChannelState Channel3,
    NoiseChannelState Channel4,
    SampleBufferState<ApuStereoSample> SampleBuffer,
    ApuOutputFilterState OutputFilter
);
