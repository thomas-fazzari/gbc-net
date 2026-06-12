namespace GbcNet.Core.Hardware;

/// <summary>
/// Game Boy hardware model to emulate.
/// </summary>
public enum HardwareModel
{
    /// <summary>
    /// Original monochrome Game Boy hardware.
    /// </summary>
    Dmg = 0,

    /// <summary>
    /// Game Boy Color hardware using the common retail CGB ABC/DE timing and boot behavior.
    /// </summary>
    /// <remarks>
    /// Early CGB0 differences are modeled separately only when explicit revision support exists.
    /// </remarks>
    Cgb = 1,
}
