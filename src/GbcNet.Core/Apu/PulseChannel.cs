namespace GbcNet.Core.Apu;

/// <summary>
/// Shared pulse-channel state for DAC power and channel trigger/active behavior.
/// </summary>
internal sealed class PulseChannel
{
    private const byte DacEnableMask = 0xF8;

    public bool IsActive { get; private set; }

    public void WriteEnvelope(byte value)
    {
        if ((value & DacEnableMask) == 0)
        {
            IsActive = false;
        }
    }

    public void Trigger(byte envelope)
    {
        IsActive = (envelope & DacEnableMask) != 0;
    }

    public void PowerOff()
    {
        IsActive = false;
    }
}
