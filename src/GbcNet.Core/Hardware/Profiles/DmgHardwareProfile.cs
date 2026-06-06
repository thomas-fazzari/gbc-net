using GbcNet.Core.Apu;
using GbcNet.Core.Apu.Profiles;
using GbcNet.Core.Dma;
using GbcNet.Core.Dma.Policies;
using GbcNet.Core.Ppu;
using GbcNet.Core.Ppu.Engines;

namespace GbcNet.Core.Hardware.Profiles;

/// <summary>
/// Selects DMG implementations for model-specific hardware behavior.
/// </summary>
internal sealed class DmgHardwareProfile : IHardwareProfile
{
    public IPpuEngine CreatePpuEngine() => new DmgPpuEngine();

    public IDmaTransferPolicy CreateDmaTransferPolicy() => new DmgDmaTransferPolicy();

    public IApuHardwareProfile CreateApuHardwareProfile() => new DmgApuHardwareProfile();
}
