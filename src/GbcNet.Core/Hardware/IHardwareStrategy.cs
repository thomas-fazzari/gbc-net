using GbcNet.Core.Dma;
using GbcNet.Core.Ppu;

namespace GbcNet.Core.Hardware;

/// <summary>
/// Selects model-specific component strategies and policies.
/// </summary>
internal interface IHardwareStrategy
{
    /// <summary>
    /// Creates the LCD timing strategy for this hardware model.
    /// </summary>
    IPpuTimingStrategy CreatePpuTimingStrategy();

    /// <summary>
    /// Creates the OAM DMA transfer strategy for this hardware model.
    /// </summary>
    IDmaTransferStrategy CreateDmaTransferStrategy();
}
