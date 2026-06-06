using GbcNet.Core.Apu;
using GbcNet.Core.Apu.Strategies;
using GbcNet.Core.Dma;
using GbcNet.Core.Dma.Strategies;
using GbcNet.Core.Ppu;
using GbcNet.Core.Ppu.Engines;

namespace GbcNet.Core.Hardware.Strategies;

/// <summary>
/// Selects DMG implementations for model-specific hardware behavior.
/// </summary>
internal sealed class DmgHardwareStrategy : IHardwareStrategy
{
    public IPpuEngine CreatePpuEngine() => new DmgPpuEngine();

    public IDmaTransferStrategy CreateDmaTransferStrategy() => new DmgDmaTransferStrategy();

    public IApuHardwareStrategy CreateApuHardwareStrategy() => new DmgApuHardwareStrategy();
}
