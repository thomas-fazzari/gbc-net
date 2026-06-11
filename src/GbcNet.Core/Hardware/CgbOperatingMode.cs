namespace GbcNet.Core.Hardware;

/// <summary>
/// Operating mode selected by CGB boot compatibility rules.
/// </summary>
internal enum CgbOperatingMode
{
    /// <summary>
    /// Full CGB mode for CGB-aware cartridges.
    /// </summary>
    Cgb = 0,

    /// <summary>
    /// CGB hardware running a DMG-only cartridge in compatibility mode.
    /// </summary>
    DmgCompatibility = 1,
}
