using GbcNet.Core.Dma;
using GbcNet.Core.Ppu;
using GbcNet.Core.Ppu.Strategies;

namespace GbcNet.Core.Hardware.Strategies;

/// <summary>
/// Selects DMG implementations for model-specific hardware behavior.
/// </summary>
internal sealed class DmgHardwareStrategy : IHardwareStrategy
{
    public IPpuTimingStrategy CreatePpuTimingStrategy() => new DmgPpuTimingStrategy();

    public IDmaBusConflictPolicy CreateDmaBusConflictPolicy() => new DmgDmaBusConflictPolicy();
}
