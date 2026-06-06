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
    private const int InitialVolumeShift = 4;
    private const int MaxLength = 64;
    private const byte MaxVolume = 15;

    private int _lengthCounter;
    private byte _envelopePeriod;
    private int _envelopeTimer;
    private bool _lengthEnabled;
    private bool _envelopeIncreases;

    public bool IsActive { get; private set; }

    public byte Volume { get; private set; }

    public void WriteLength(byte value)
    {
        // Hardware stores an initial length
        // This counter stores remaining ticks until expiry
        _lengthCounter = MaxLength - (value & InitialLengthMask);
    }

    public void WriteEnvelope(byte value)
    {
        if ((value & DacEnableMask) == 0)
        {
            IsActive = false;
        }
    }

    public void WriteControl(byte value, byte envelope)
    {
        _lengthEnabled = (value & LengthEnableMask) != 0;

        if ((value & TriggerMask) == 0)
        {
            return;
        }

        if (_lengthCounter == 0)
        {
            _lengthCounter = MaxLength;
        }

        Volume = (byte)(envelope >> InitialVolumeShift);

        _envelopePeriod = (byte)(envelope & EnvelopePeriodMask);
        _envelopeTimer = _envelopePeriod;
        _envelopeIncreases = (envelope & EnvelopeIncreaseMask) != 0;

        IsActive = (envelope & DacEnableMask) != 0;
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

    public void PowerOff()
    {
        _lengthCounter = 0;
        _envelopePeriod = 0;
        _envelopeTimer = 0;
        _lengthEnabled = false;
        _envelopeIncreases = false;
        IsActive = false;
        Volume = 0;
    }
}
