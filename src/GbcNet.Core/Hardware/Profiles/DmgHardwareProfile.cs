using GbcNet.Core.Apu.Profiles;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Dma.Policies;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu.Engines;
using GbcNet.Core.Sm83;

namespace GbcNet.Core.Hardware.Profiles;

/// <summary>
/// Selects DMG implementations for model-specific hardware behavior.
/// </summary>
internal sealed class DmgHardwareProfile : IHardwareProfile
{
    public static DmgHardwareProfile Instance { get; } = new();

    private DmgHardwareProfile() { }

    public HardwareModel Model => HardwareModel.Dmg;

    public IPpuEngine CreatePpuEngine() => new DmgPpuEngine();

    public IDmaTransferPolicy CreateDmaTransferPolicy() => new DmgDmaTransferPolicy();

    public IApuHardwareProfile CreateApuHardwareProfile() => new DmgApuHardwareProfile();

    public void ApplyPostBootState(Cartridge cartridge, Cpu cpu, MemoryBus bus)
    {
        DmgPostBootState.Apply(cartridge, cpu, bus);
    }
}
