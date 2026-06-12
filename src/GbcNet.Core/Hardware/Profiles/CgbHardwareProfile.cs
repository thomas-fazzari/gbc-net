using GbcNet.Core.Apu.Profiles;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Dma.Policies;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu.Engines;
using GbcNet.Core.Sm83;

namespace GbcNet.Core.Hardware.Profiles;

/// <summary>
/// Selects CGB hardware components and records the boot-selected operating mode.
/// </summary>
internal sealed class CgbHardwareProfile(CgbOperatingMode operatingMode) : IHardwareProfile
{
    public CgbOperatingMode OperatingMode { get; } =
        operatingMode switch
        {
            CgbOperatingMode.Cgb or CgbOperatingMode.DmgCompatibility => operatingMode,
            _ => throw new ArgumentOutOfRangeException(
                nameof(operatingMode),
                operatingMode,
                "Unsupported CGB operating mode."
            ),
        };

    public HardwareModel Model => HardwareModel.Cgb;

    public int VideoRamBankCount => OperatingMode is CgbOperatingMode.Cgb ? 2 : 1;

    public int WorkRamBankCount => 8;

    public bool IsKey1RegisterEnabled => OperatingMode is CgbOperatingMode.Cgb;

    public bool IsWorkRamBankRegisterEnabled => OperatingMode is CgbOperatingMode.Cgb;

    public bool IsColorPaletteRamEnabled => OperatingMode is CgbOperatingMode.Cgb;

    public bool IsObjectPriorityModeRegisterEnabled => OperatingMode is CgbOperatingMode.Cgb;

    public bool IsVideoRamDmaRegisterEnabled => OperatingMode is CgbOperatingMode.Cgb;

    public IPpuEngine CreatePpuEngine() =>
        OperatingMode is CgbOperatingMode.Cgb
            ? new CgbPpuEngine()
            : new CgbDmgCompatibilityPpuEngine();

    public ITransferPolicy CreateOamDmaTransferPolicy() => new CgbOamDmaTransferPolicy();

    public IApuHardwareProfile CreateApuHardwareProfile() => new CgbApuHardwareProfile();

    public void ApplyPostBootState(Cartridge cartridge, Cpu cpu, MemoryBus bus)
    {
        CgbPostBootState.Apply(OperatingMode, cartridge, cpu, bus);
    }
}
