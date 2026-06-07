namespace GbcNet.Core.Apu.Components;

/// <summary>
/// Shared volume envelope and DAC-enable register logic.
/// </summary>
internal sealed class VolumeEnvelope
{
    private const byte DacEnableMask = 0xF8;
    private const byte EnvelopePeriodMask = 0x07;
    private const byte EnvelopeIncreaseMask = 0x08;
    private const int InitialVolumeShift = 4;
    private const byte MaxVolume = 15;

    private byte _period;
    private int _timer;
    private bool _increases;

    /// <summary>
    /// Whether the channel DAC is enabled by the NRx2 register value.
    /// </summary>
    public bool DacEnabled { get; private set; }

    /// <summary>
    /// Current 4-bit envelope volume.
    /// </summary>
    public byte Volume { get; private set; }

    /// <summary>
    /// Latches DAC enable from an NRx2 write.
    /// </summary>
    public void WriteRegister(byte value)
    {
        DacEnabled = (value & DacEnableMask) != 0;
    }

    /// <summary>
    /// Reloads initial volume, pace, and direction on channel trigger.
    /// </summary>
    public void Trigger(byte envelopeRegister)
    {
        Volume = (byte)(envelopeRegister >> InitialVolumeShift);
        _period = (byte)(envelopeRegister & EnvelopePeriodMask);
        _timer = _period;
        _increases = (envelopeRegister & EnvelopeIncreaseMask) != 0;
        DacEnabled = (envelopeRegister & DacEnableMask) != 0;
    }

    /// <summary>
    /// Applies one 64 Hz envelope clock if the configured pace expires.
    /// </summary>
    public void Clock()
    {
        if (_period == 0)
        {
            return;
        }

        _timer--;
        if (_timer != 0)
        {
            return;
        }

        _timer = _period;

        switch (_increases)
        {
            case true when Volume < MaxVolume:
                Volume++;
                break;
            case false when Volume > 0:
                Volume--;
                break;
        }
    }

    /// <summary>
    /// Clears envelope and DAC state on APU power-off.
    /// </summary>
    public void PowerOff()
    {
        _period = 0;
        _timer = 0;
        _increases = false;
        DacEnabled = false;
        Volume = 0;
    }
}
