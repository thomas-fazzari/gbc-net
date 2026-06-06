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
    private const int MaxLength = 64;

    private int _lengthCounter;
    private bool _lengthEnabled;

    public bool IsActive { get; private set; }

    public void WriteLength(byte value)
    {
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

    public void PowerOff()
    {
        _lengthCounter = 0;
        _lengthEnabled = false;
        IsActive = false;
    }
}
