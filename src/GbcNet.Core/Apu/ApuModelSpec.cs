namespace GbcNet.Core.Apu;

/// <summary>
/// Provides model-specific APU register visibility, DIV-APU timing edges, and output filter behavior.
/// </summary>
internal readonly record struct ApuModelSpec
{
    private const ushort DivApuNormalSpeedFallingEdgeMask = 1 << 12;

    private ApuModelSpec(
        bool isPcmOutputRegisterEnabled,
        int outputClockHz,
        double highPassChargeFactorPerTCycle,
        ushort divApuDoubleSpeedFallingEdgeMask
    )
    {
        IsPcmOutputRegisterEnabled = isPcmOutputRegisterEnabled;
        OutputClockHz = outputClockHz;
        HighPassChargeFactorPerTCycle = highPassChargeFactorPerTCycle;
        DivApuDoubleSpeedFallingEdgeMask = divApuDoubleSpeedFallingEdgeMask;
    }

    /// <summary>
    /// DMG APU register visibility, normal-speed DIV-APU timing, and analog high-pass filter behavior.
    /// </summary>
    public static ApuModelSpec Dmg { get; } =
        new(
            isPcmOutputRegisterEnabled: false,
            outputClockHz: 4_194_304,
            highPassChargeFactorPerTCycle: 0.999958,
            divApuDoubleSpeedFallingEdgeMask: DivApuNormalSpeedFallingEdgeMask
        );

    /// <summary>
    /// CGB APU register visibility, double-speed DIV-APU timing, and analog high-pass filter behavior.
    /// </summary>
    public static ApuModelSpec Cgb { get; } =
        new(
            isPcmOutputRegisterEnabled: true,
            outputClockHz: 4_194_304,
            highPassChargeFactorPerTCycle: 0.998943,
            divApuDoubleSpeedFallingEdgeMask: 1 << 13
        );

    /// <summary>
    /// SGB APU register visibility, faster NTSC clock, and DMG-style analog high-pass filter behavior.
    /// </summary>
    public static ApuModelSpec Sgb { get; } =
        new(
            isPcmOutputRegisterEnabled: false,
            outputClockHz: 4_295_454,
            highPassChargeFactorPerTCycle: 0.999958,
            divApuDoubleSpeedFallingEdgeMask: DivApuNormalSpeedFallingEdgeMask
        );

    /// <summary>
    /// Indicates whether CGB PCM output registers FF76-FF77 are enabled.
    /// </summary>
    public bool IsPcmOutputRegisterEnabled { get; }

    /// <summary>
    /// Source clock used by the fixed-rate output sample scheduler.
    /// </summary>
    public int OutputClockHz { get; }

    /// <summary>
    /// Per-T-cycle HPF charge factor used by the analog output conditioner.
    /// </summary>
    public double HighPassChargeFactorPerTCycle { get; }

    /// <summary>
    /// System-counter falling-edge bit that clocks DIV-APU in CGB double-speed mode.
    /// </summary>
    public ushort DivApuDoubleSpeedFallingEdgeMask { get; }

    /// <summary>
    /// Returns the HPF charge factor for the requested output sample rate.
    /// </summary>
    public double GetOutputHighPassChargeFactor(int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        return Math.Pow(HighPassChargeFactorPerTCycle, OutputClockHz / (double)sampleRate);
    }

    /// <summary>
    /// Applies APU read masks for CPU-visible unused and write-only bits.
    /// </summary>
    public static byte ApplyRegisterReadMask(ushort address, byte value) =>
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

    /// <summary>
    /// Returns the system-counter falling-edge bit that clocks DIV-APU for current speed mode.
    /// </summary>
    public ushort GetDivApuFallingEdgeMask(bool cgbDoubleSpeed) =>
        cgbDoubleSpeed ? DivApuDoubleSpeedFallingEdgeMask : DivApuNormalSpeedFallingEdgeMask;
}
