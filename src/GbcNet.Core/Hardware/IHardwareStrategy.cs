using GbcNet.Core.Dma;
using GbcNet.Core.Ppu;

namespace GbcNet.Core.Hardware;

/// <summary>
/// Selects model-specific component strategies and policies.
/// </summary>
internal interface IHardwareStrategy
{
    /// <summary>
    /// Creates the LCD/PPU engine for this hardware model.
    /// </summary>
    IPpuEngine CreatePpuEngine();

    /// <summary>
    /// Creates the OAM DMA transfer strategy for this hardware model.
    /// </summary>
    IDmaTransferStrategy CreateDmaTransferStrategy();
}
