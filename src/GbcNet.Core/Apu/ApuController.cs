namespace GbcNet.Core.Apu;

/// <summary>
/// Stores CPU-visible Audio Processing Unit registers and delegates hardware-specific behavior.
/// </summary>
internal sealed class ApuController(IApuHardwareProfile hardwareProfile)
{
    private const ushort RegisterStart = 0xFF10;
    private const ushort RegisterEnd = 0xFF26;

    // FF15 and FF1F sit inside the APU address range but are not real audio registers
    private const ushort UnmappedAudioAddressFf15 = 0xFF15;
    private const ushort UnmappedAudioAddressFf1F = 0xFF1F;

    private const ushort Channel1SweepRegister = 0xFF10;
    private const ushort Channel1LengthRegister = 0xFF11;
    private const ushort Channel1EnvelopeRegister = 0xFF12;
    private const ushort Channel1PeriodLowRegister = 0xFF13;
    private const ushort Channel1PeriodHighControlRegister = 0xFF14;

    private const ushort Channel2LengthRegister = 0xFF16;
    private const ushort Channel2EnvelopeRegister = 0xFF17;
    private const ushort Channel2PeriodLowRegister = 0xFF18;
    private const ushort Channel2PeriodHighControlRegister = 0xFF19;

    private const ushort MasterVolumeRegister = 0xFF24;
    private const ushort SoundPanningRegister = 0xFF25;
    private const ushort AudioMasterControlRegister = 0xFF26;
    private const byte AudioMasterWritableMask = 0x80;
    private const byte AudioChannelStatusMask = 0x0F;
    private const byte AudioChannel1StatusMask = 0x01;
    private const byte AudioChannel2StatusMask = 0x02;

    private const byte Channel1RightRouteMask = 0x01;
    private const byte Channel2RightRouteMask = 0x02;
    private const byte Channel1LeftRouteMask = 0x10;
    private const byte Channel2LeftRouteMask = 0x20;

    private const byte RightVolumeMask = 0x07;
    private const byte LeftVolumeMask = 0x70;
    private const byte PeriodHighMask = 0x07;
    private const byte TriggerMask = 0x80;
    private const int LeftVolumeShift = 4;
    private const byte DivApuStepMask = 0x07;

    private readonly byte[] _registers = new byte[RegisterEnd - RegisterStart + 1];
    private readonly PulseChannel _channel1 = new();
    private readonly PulseChannel _channel2 = new();
    private readonly Channel1Sweep _channel1Sweep = new();

    /// <summary>
    /// Current DIV-APU frame sequencer step, advanced at 512 Hz.
    /// </summary>
    internal byte DivApuStep { get; private set; }

    internal byte Channel2Volume => _channel2.Volume;

    internal byte Channel2DigitalOutput => _channel2.DigitalOutput;

    internal ushort Channel1Period => _channel1.Period;

    /// <summary>
    /// Returns whether an address is owned by the APU register block.
    /// </summary>
    internal static bool ContainsRegister(ushort address) =>
        address
            is >= RegisterStart
                and <= RegisterEnd
                and not UnmappedAudioAddressFf15
                and not UnmappedAudioAddressFf1F;

    /// <summary>
    /// Applies system-counter falling edges that clock DIV-APU timing.
    /// </summary>
    internal ApuFrameSequencerEvents TickSystemCounter(ApuTickInputs inputs)
    {
        if (
            (
                inputs.SystemCounterFallingEdges
                & hardwareProfile.GetDivApuFallingEdgeMask(inputs.CgbDoubleSpeed)
            ) == 0
        )
        {
            return default;
        }

        DivApuStep = (byte)((DivApuStep + 1) & DivApuStepMask);
        // Events are based on the frame sequencer step reached after this DIV-APU tick
        var events = new ApuFrameSequencerEvents(
            Length: DivApuStep is 1 or 3 or 5 or 7,
            Sweep: DivApuStep is 3 or 7,
            Envelope: DivApuStep is 7
        );

        if (events.Length)
        {
            _channel1.ClockLength();
            _channel2.ClockLength();
            if (!_channel1.IsActive)
            {
                _registers[AudioMasterControlRegister - RegisterStart] &= unchecked(
                    (byte)~AudioChannel1StatusMask
                );
            }

            if (!_channel2.IsActive)
            {
                _registers[AudioMasterControlRegister - RegisterStart] &= unchecked(
                    (byte)~AudioChannel2StatusMask
                );
            }
        }

        if (events.Sweep)
        {
            Channel1SweepResult sweepResult = _channel1Sweep.Clock();
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
                _registers[AudioMasterControlRegister - RegisterStart] &= unchecked(
                    (byte)~AudioChannel1StatusMask
                );
            }
        }

        if (events.Envelope)
        {
            _channel1.ClockEnvelope();
            _channel2.ClockEnvelope();
        }

        return events;
    }

    /// <summary>
    /// Advances channel period timers by elapsed T-cycles.
    /// </summary>
    internal void Tick(int tCycles)
    {
        _channel1.Tick(tCycles);
        _channel2.Tick(tCycles);
    }

    /// <summary>
    /// Returns the current pulse-channel stereo mix using NR50 and NR51 routing.
    /// </summary>
    internal ApuStereoSample GetStereoSample()
    {
        byte masterVolume = _registers[MasterVolumeRegister - RegisterStart];
        byte panning = _registers[SoundPanningRegister - RegisterStart];
        int leftInput = 0;
        int rightInput = 0;

        if ((panning & Channel1LeftRouteMask) != 0)
        {
            leftInput += _channel1.DigitalOutput;
        }

        if ((panning & Channel2LeftRouteMask) != 0)
        {
            leftInput += _channel2.DigitalOutput;
        }

        if ((panning & Channel1RightRouteMask) != 0)
        {
            rightInput += _channel1.DigitalOutput;
        }

        if ((panning & Channel2RightRouteMask) != 0)
        {
            rightInput += _channel2.DigitalOutput;
        }

        return new ApuStereoSample(
            Left: leftInput * (((masterVolume & LeftVolumeMask) >> LeftVolumeShift) + 1),
            Right: rightInput * ((masterVolume & RightVolumeMask) + 1)
        );
    }

    /// <summary>
    /// Reads an APU register with hardware-specific unused and write-only bits applied.
    /// </summary>
    public byte ReadRegister(ushort address) =>
        hardwareProfile.ApplyRegisterReadMask(address, _registers[address - RegisterStart]);

    /// <summary>
    /// Writes an APU register, respecting NR52 power state and read-only channel status bits.
    /// </summary>
    public void WriteRegister(ushort address, byte value)
    {
        if (address is AudioMasterControlRegister)
        {
            if ((value & AudioMasterWritableMask) == 0)
            {
                // NR52 power-off clears APU registers and silences active channels, but not Wave RAM
                Array.Clear(_registers);
                _channel1.PowerOff();
                _channel2.PowerOff();
                _channel1Sweep.PowerOff();
                return;
            }

            _registers[AudioMasterControlRegister - RegisterStart] = (byte)(
                (_registers[AudioMasterControlRegister - RegisterStart] & AudioChannelStatusMask)
                | AudioMasterWritableMask
            );
            return;
        }

        if ((_registers[AudioMasterControlRegister - RegisterStart] & AudioMasterWritableMask) == 0)
        {
            return;
        }

        _registers[address - RegisterStart] = value;

        switch (address)
        {
            case Channel1SweepRegister:
                _channel1Sweep.WriteRegister(value);
                return;

            case Channel1LengthRegister:
                _channel1.WriteLength(value);
                return;

            case Channel1EnvelopeRegister:
                _channel1.WriteEnvelope(value);
                if (!_channel1.IsActive)
                {
                    _registers[AudioMasterControlRegister - RegisterStart] &= unchecked(
                        (byte)~AudioChannel1StatusMask
                    );
                }
                return;

            case Channel1PeriodLowRegister:
                _channel1.WritePeriodLow(value);
                return;

            case Channel1PeriodHighControlRegister:
                _channel1.WriteControl(value, _registers[Channel1EnvelopeRegister - RegisterStart]);
                if ((value & TriggerMask) != 0)
                {
                    Channel1SweepResult triggerSweepResult = _channel1Sweep.Trigger(
                        _channel1.Period
                    );
                    if (triggerSweepResult.Overflowed)
                    {
                        _channel1.Disable();
                    }
                }

                if (_channel1.IsActive)
                {
                    _registers[AudioMasterControlRegister - RegisterStart] |=
                        AudioChannel1StatusMask;
                }
                else
                {
                    _registers[AudioMasterControlRegister - RegisterStart] &= unchecked(
                        (byte)~AudioChannel1StatusMask
                    );
                }
                return;

            case Channel2LengthRegister:
                _channel2.WriteLength(value);
                return;

            case Channel2EnvelopeRegister:
                _channel2.WriteEnvelope(value);
                if (!_channel2.IsActive)
                {
                    _registers[AudioMasterControlRegister - RegisterStart] &= unchecked(
                        (byte)~AudioChannel2StatusMask
                    );
                }
                return;

            case Channel2PeriodLowRegister:
                _channel2.WritePeriodLow(value);
                return;

            case Channel2PeriodHighControlRegister:
                _channel2.WriteControl(value, _registers[Channel2EnvelopeRegister - RegisterStart]);
                if (_channel2.IsActive)
                {
                    _registers[AudioMasterControlRegister - RegisterStart] |=
                        AudioChannel2StatusMask;
                }
                else
                {
                    _registers[AudioMasterControlRegister - RegisterStart] &= unchecked(
                        (byte)~AudioChannel2StatusMask
                    );
                }
                return;
        }
    }

    /// <summary>
    /// Seeds an APU register without applying CPU write-only restrictions.
    /// </summary>
    internal void SetRegisterState(ushort address, byte value)
    {
        _registers[address - RegisterStart] = value;
    }
}
