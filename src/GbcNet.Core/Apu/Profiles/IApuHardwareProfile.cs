namespace GbcNet.Core.Apu.Profiles;

/// <summary>
/// Provides model-specific APU register masks and timing edges.
/// </summary>
internal interface IApuHardwareProfile
{
    /// <summary>
    /// Applies model-specific read masks for CPU-visible APU registers.
    /// </summary>
    byte ApplyRegisterReadMask(ushort address, byte value);

    /// <summary>
    /// Returns the system-counter falling-edge bit that clocks DIV-APU for current speed mode.
    /// </summary>
    ushort GetDivApuFallingEdgeMask(bool cgbDoubleSpeed);
}
