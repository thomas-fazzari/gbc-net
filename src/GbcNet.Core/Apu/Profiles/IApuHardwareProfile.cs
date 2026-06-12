namespace GbcNet.Core.Apu.Profiles;

/// <summary>
/// Provides model-specific APU register masks and timing edges.
/// </summary>
internal interface IApuHardwareProfile
{
    /// <summary>
    /// Indicates whether CGB PCM output registers FF76-FF77 are enabled.
    /// </summary>
    bool IsPcmOutputRegisterEnabled { get; }

    /// <summary>
    /// Applies model-specific read masks for CPU-visible APU registers.
    /// </summary>
    byte ApplyRegisterReadMask(ushort address, byte value);

    /// <summary>
    /// Returns the system-counter falling-edge bit that clocks DIV-APU for current speed mode.
    /// </summary>
    ushort GetDivApuFallingEdgeMask(bool cgbDoubleSpeed);

    /// <summary>
    /// Source clock used by the fixed-rate output sample scheduler.
    /// </summary>
    int OutputClockHz { get; }

    /// <summary>
    /// Returns the HPF charge factor for the requested output sample rate.
    /// </summary>
    double GetOutputHighPassChargeFactor(int sampleRate);
}
