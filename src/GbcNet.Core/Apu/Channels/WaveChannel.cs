namespace GbcNet.Core.Apu.Channels;

/// <summary>
/// CH3 wave channel state, including CPU-visible Wave RAM.
/// </summary>
internal sealed class WaveChannel
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
    private const int MaxLength = 256;
    private const int PeriodReloadBase = 2048;
    private const int SampleIndexMask = 0x1F;

    private readonly byte[] _waveRam = new byte[16];

    private int _lengthCounter;
    private int _periodTimer;
    private int _tCycleAccumulator;
    private byte _outputLevel;
    private byte _sampleIndex;
    private byte _sampleBuffer;
    private bool _lengthEnabled;
    private bool _dacEnabled;

    /// <summary>
    /// Whether CH3 generation is currently active and reported through NR52 bit 2.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Whether the channel DAC is enabled.
    /// </summary>
    public bool DacEnabled => _dacEnabled;

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
                1 => _sampleBuffer,
                2 => (byte)(_sampleBuffer >> 1),
                3 => (byte)(_sampleBuffer >> 2),
                _ => (byte)0,
            }
            : (byte)0;

    /// <summary>
    /// Reads CPU-visible Wave RAM, applying the active-channel lock.
    /// </summary>
    public byte ReadWaveRam(ushort address) =>
        IsActive ? (byte)0xFF : _waveRam[address - WaveRamStart];

    /// <summary>
    /// Writes CPU-visible Wave RAM, applying the active-channel lock.
    /// </summary>
    public void WriteWaveRam(ushort address, byte value)
    {
        if (IsActive)
        {
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
        _dacEnabled = (value & DacEnableMask) != 0;
        if (!_dacEnabled)
        {
            IsActive = false;
        }
    }

    /// <summary>
    /// Loads NR31 initial length into the 256-step length counter.
    /// </summary>
    public void WriteLength(byte value)
    {
        _lengthCounter = MaxLength - value;
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
    public void WriteControl(byte value)
    {
        Period = (ushort)((Period & 0xFF) | ((value & PeriodHighMask) << PeriodHighShift));
        _lengthEnabled = (value & LengthEnableMask) != 0;

        if ((value & TriggerMask) == 0)
        {
            return;
        }

        if (_lengthCounter == 0)
        {
            _lengthCounter = MaxLength;
        }

        _periodTimer = PeriodReloadBase - Period;
        _tCycleAccumulator = 0;
        _sampleIndex = 0;
        IsActive = _dacEnabled;
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
            _periodTimer--;

            if (_periodTimer > 0)
            {
                continue;
            }

            _periodTimer = PeriodReloadBase - Period;
            _sampleIndex = (byte)((_sampleIndex + 1) & SampleIndexMask);
            byte sampleByte = _waveRam[_sampleIndex >> 1];
            _sampleBuffer =
                (_sampleIndex & 1) == 0 ? (byte)(sampleByte >> 4) : (byte)(sampleByte & 0x0F);
        }
    }

    /// <summary>
    /// Clocks the length counter from the DIV-APU frame sequencer.
    /// </summary>
    public void ClockLength()
    {
        if (!_lengthEnabled || _lengthCounter == 0)
        {
            return;
        }

        _lengthCounter--;
        if (_lengthCounter == 0)
        {
            IsActive = false;
        }
    }

    /// <summary>
    /// Clears CH3 internal state without clearing Wave RAM.
    /// </summary>
    public void PowerOff()
    {
        _lengthCounter = 0;
        _periodTimer = 0;
        _tCycleAccumulator = 0;
        Period = 0;
        _outputLevel = 0;
        _sampleIndex = 0;
        _sampleBuffer = 0;
        _lengthEnabled = false;
        _dacEnabled = false;
        IsActive = false;
    }
}
