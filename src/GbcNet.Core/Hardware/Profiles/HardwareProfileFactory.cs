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

        return hardwareModel switch
        {
            HardwareModel.Dmg => DmgHardwareProfile.Instance,
            _ => throw new ArgumentOutOfRangeException(
                nameof(hardwareModel),
                hardwareModel,
                "Unsupported hardware model."
            ),
        };
    }
}
