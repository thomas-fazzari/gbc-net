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

    private const ushort Channel2LengthRegister = 0xFF16;
    private const ushort Channel2EnvelopeRegister = 0xFF17;
    private const ushort Channel2PeriodLowRegister = 0xFF18;
    private const ushort Channel2PeriodHighControlRegister = 0xFF19;
    private const ushort MasterVolumeRegister = 0xFF24;
    private const ushort SoundPanningRegister = 0xFF25;
    private const ushort AudioMasterControlRegister = 0xFF26;
    private const byte AudioMasterWritableMask = 0x80;
    private const byte AudioChannelStatusMask = 0x0F;
    private const byte AudioChannel2StatusMask = 0x02;
    private const byte Channel2RightRouteMask = 0x02;
    private const byte Channel2LeftRouteMask = 0x20;
    private const byte RightVolumeMask = 0x07;
    private const byte LeftVolumeMask = 0x70;
    private const int LeftVolumeShift = 4;
    private const byte DivApuStepMask = 0x07;

    private readonly byte[] _registers = new byte[RegisterEnd - RegisterStart + 1];
    private readonly PulseChannel _channel2 = new();

    /// <summary>
    /// Current DIV-APU frame sequencer step, advanced at 512 Hz.
    /// </summary>
    internal byte DivApuStep { get; private set; }

    internal byte Channel2Volume => _channel2.Volume;

    internal byte Channel2DigitalOutput => _channel2.DigitalOutput;

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
            _channel2.ClockLength();
            if (!_channel2.IsActive)
            {
                _registers[AudioMasterControlRegister - RegisterStart] &= unchecked(
                    (byte)~AudioChannel2StatusMask
                );
            }
        }

        if (events.Envelope)
        {
            _channel2.ClockEnvelope();
        }

        return events;
    }

    /// <summary>
    /// Advances channel period timers by elapsed T-cycles.
    /// </summary>
    internal void Tick(int tCycles)
    {
        _channel2.Tick(tCycles);
    }

    /// <summary>
    /// Returns the current CH2-only stereo mix using NR50 and NR51 routing.
    /// </summary>
    internal ApuStereoSample GetStereoSample()
    {
        byte masterVolume = _registers[MasterVolumeRegister - RegisterStart];
        byte panning = _registers[SoundPanningRegister - RegisterStart];
        byte channel2Output = _channel2.DigitalOutput;

        return new ApuStereoSample(
            Left: (panning & Channel2LeftRouteMask) != 0
                ? channel2Output * (((masterVolume & LeftVolumeMask) >> LeftVolumeShift) + 1)
                : 0,
            Right: (panning & Channel2RightRouteMask) != 0
                ? channel2Output * ((masterVolume & RightVolumeMask) + 1)
                : 0
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
                _channel2.PowerOff();
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
