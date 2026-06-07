namespace GbcNet.Core.Apu.Profiles;

/// <summary>
/// DMG APU register masks and timing source selection.
/// </summary>
internal sealed class DmgApuHardwareProfile : IApuHardwareProfile
{
    private const ushort DivApuNormalSpeedFallingEdgeMask = 1 << 12;
    private const double DmgHighPassChargeFactorPerTCycle = 0.999958;

    public int OutputClockHz => 4_194_304;

    public double GetOutputHighPassChargeFactor(int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        return Math.Pow(DmgHighPassChargeFactorPerTCycle, OutputClockHz / (double)sampleRate);
    }

    public byte ApplyRegisterReadMask(ushort address, byte value) =>
        address switch
        {
            0xFF10 => (byte)(value | 0x80),
            0xFF11 => (byte)(value | 0x3F),
            0xFF13 or 0xFF18 or 0xFF1B or 0xFF1D or 0xFF20 => 0xFF,
            0xFF14 or 0xFF19 or 0xFF1E or 0xFF23 => (byte)(value | 0xBF),
            0xFF16 => (byte)(value | 0x3F),
            0xFF1A => (byte)(value | 0x7F),
            0xFF1C => (byte)(value | 0x9F),
            0xFF26 => (byte)((value & 0x8F) | 0x70),
            _ => value,
        };

    public ushort GetDivApuFallingEdgeMask(bool cgbDoubleSpeed) => DivApuNormalSpeedFallingEdgeMask;
}
