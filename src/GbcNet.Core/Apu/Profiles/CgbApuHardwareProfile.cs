namespace GbcNet.Core.Apu.Profiles;

/// <summary>
/// CGB APU register masks, double-speed DIV-APU edge selection, and analog high-pass filter behavior.
/// </summary>
internal sealed class CgbApuHardwareProfile : IApuHardwareProfile
{
    private const ushort DivApuNormalSpeedFallingEdgeMask = 1 << 12;
    private const ushort DivApuDoubleSpeedFallingEdgeMask = 1 << 13;
    private const double HighPassChargeFactorPerTCycle = 0.998943;

    public int OutputClockHz => 4_194_304;

    public double GetOutputHighPassChargeFactor(int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        return Math.Pow(HighPassChargeFactorPerTCycle, OutputClockHz / (double)sampleRate);
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

    public ushort GetDivApuFallingEdgeMask(bool cgbDoubleSpeed) =>
        cgbDoubleSpeed ? DivApuDoubleSpeedFallingEdgeMask : DivApuNormalSpeedFallingEdgeMask;
}
