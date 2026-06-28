using GbcNet.Core.Cartridges;

namespace GbcNet.Core.Hardware.Profiles;

/// <summary>
/// Selects the hardware profile for the requested physical model and loaded cartridge.
/// </summary>
internal static class HardwareProfileFactory
{
    public static IHardwareProfile Create(
        HardwareModel hardwareModel,
        CartridgeHeader cartridgeHeader
    )
    {
        ArgumentNullException.ThrowIfNull(cartridgeHeader);

        return (hardwareModel, cartridgeHeader.CgbSupport) switch
        {
            (HardwareModel.Dmg, CgbSupport.None or CgbSupport.Enhanced) =>
                DmgHardwareProfile.Instance,

            (HardwareModel.Dmg, CgbSupport.Required) => throw new NotSupportedException(
                "CGB-required cartridges cannot run on DMG hardware."
            ),

            (HardwareModel.Cgb, CgbSupport.None) => new CgbHardwareProfile(
                CgbOperatingMode.DmgCompatibility
            ),

            (HardwareModel.Cgb, CgbSupport.Enhanced or CgbSupport.Required) =>
                new CgbHardwareProfile(CgbOperatingMode.Cgb),

            (HardwareModel.Sgb, CgbSupport.None or CgbSupport.Enhanced) =>
                SgbHardwareProfile.Instance,

            (HardwareModel.Sgb, CgbSupport.Required) => throw new NotSupportedException(
                "CGB-required cartridges cannot run on SGB hardware."
            ),

            (_, CgbSupport.None or CgbSupport.Enhanced or CgbSupport.Required) =>
                throw new ArgumentOutOfRangeException(
                    nameof(hardwareModel),
                    hardwareModel,
                    "Unsupported hardware model."
                ),

            _ => throw new ArgumentOutOfRangeException(
                nameof(cartridgeHeader),
                cartridgeHeader.CgbSupport,
                "Unsupported cartridge CGB support value."
            ),
        };
    }
}
