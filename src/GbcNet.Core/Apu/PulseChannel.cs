namespace GbcNet.Core.Apu;

/// <summary>
/// Shared pulse-channel state for DAC power and channel trigger/active behavior.
/// </summary>
internal sealed class PulseChannel
{
    private const byte DacEnableMask = 0xF8;
    private const byte InitialLengthMask = 0x3F;
    private const byte LengthEnableMask = 0x40;
    private const byte TriggerMask = 0x80;
    private const byte EnvelopePeriodMask = 0x07;
    private const byte EnvelopeIncreaseMask = 0x08;
    private const byte PeriodHighMask = 0x07;

    private const int InitialVolumeShift = 4;
    private const int DutyShift = 6;
    private const int PeriodHighShift = 8;
    private const int PulsePeriodClockTCycles = 4;
    private const int MaxLength = 64;
    private const int PeriodReloadBase = 2048;

    private const byte MaxVolume = 15;

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

    private int _lengthCounter;
    private byte _envelopePeriod;
    private int _envelopeTimer;
    private int _periodTimer;
    private int _tCycleAccumulator;
    private byte _duty;
    private byte _dutyStep;
    private bool _lengthEnabled;
    private bool _envelopeIncreases;
    private bool _suppressInitialOutput;

    public bool IsActive { get; private set; }

    public byte Volume { get; private set; }

    public ushort Period { get; private set; }

    public byte DigitalOutput =>
        IsActive && !_suppressInitialOutput && _dutyPatterns[(_duty * 8) + _dutyStep] != 0
            ? Volume
            : (byte)0;

    public void WriteLength(byte value)
    {
        // Hardware stores an initial length
        // This counter stores remaining ticks until expiry
        _lengthCounter = MaxLength - (value & InitialLengthMask);
        _duty = (byte)(value >> DutyShift);
    }

    public void WritePeriodLow(byte value)
    {
        Period = (ushort)((Period & 0x700) | value);
    }

    public void SetPeriod(ushort period)
    {
        Period = (ushort)(period & 0x07FF);
    }

    public void WriteEnvelope(byte value)
    {
        if ((value & DacEnableMask) != 0)
        {
            return;
        }

        IsActive = false;
        _suppressInitialOutput = false;
    }

    public void WriteControl(byte value, byte envelope)
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

        Volume = (byte)(envelope >> InitialVolumeShift);

        _envelopePeriod = (byte)(envelope & EnvelopePeriodMask);
        _envelopeTimer = _envelopePeriod;
        _envelopeIncreases = (envelope & EnvelopeIncreaseMask) != 0;

        bool isDacEnabled = (envelope & DacEnableMask) != 0;
        _suppressInitialOutput = !IsActive && isDacEnabled;
        IsActive = isDacEnabled;
    }

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

    public void ClockEnvelope()
    {
        if (!IsActive || _envelopePeriod == 0)
        {
            return;
        }

        _envelopeTimer--;
        if (_envelopeTimer != 0)
        {
            return;
        }

        _envelopeTimer = _envelopePeriod;

        switch (_envelopeIncreases)
        {
            case true when Volume < MaxVolume:
                Volume++;
                break;
            case false when Volume > 0:
                Volume--;
                break;
        }
    }

    public void Disable()
    {
        IsActive = false;
        _suppressInitialOutput = false;
    }

    public void PowerOff()
    {
        _lengthCounter = 0;
        _envelopePeriod = 0;
        _envelopeTimer = 0;
        _periodTimer = 0;
        _tCycleAccumulator = 0;
        Period = 0;
        _duty = 0;
        _dutyStep = 0;
        _lengthEnabled = false;
        _envelopeIncreases = false;
        _suppressInitialOutput = false;
        IsActive = false;
        Volume = 0;
    }
}
