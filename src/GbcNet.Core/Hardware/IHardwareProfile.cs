using GbcNet.Core.Apu;
using GbcNet.Core.Dma;
using GbcNet.Core.Ppu;

namespace GbcNet.Core.Hardware;

/// <summary>
/// Selects model-specific component profiles and policies.
/// </summary>
internal interface IHardwareProfile
{
    /// <summary>
    /// Creates the LCD/PPU engine for this hardware model.
    /// </summary>
    IPpuEngine CreatePpuEngine();

    /// <summary>
    /// Creates the OAM DMA transfer policy for this hardware model.
    /// </summary>
    IDmaTransferPolicy CreateDmaTransferPolicy();

    /// <summary>
    /// Creates the APU profile for this hardware model.
    /// </summary>
    IApuHardwareProfile CreateApuHardwareProfile();
}
