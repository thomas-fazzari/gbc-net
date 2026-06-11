using GbcNet.Core.Apu.Profiles;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Dma.Policies;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu.Engines;
using GbcNet.Core.Sm83;

namespace GbcNet.Core.Hardware.Profiles;

/// <summary>
/// Selects model-specific component profiles, policies, and boot hand-off state.
/// </summary>
internal interface IHardwareProfile
{
    /// <summary>
    /// Physical hardware model represented by this profile.
    /// </summary>
    HardwareModel Model { get; }

    /// <summary>
    /// Number of physical 8 KiB VRAM banks available through the CPU-visible VRAM window.
    /// </summary>
    int VideoRamBankCount { get; }

    /// <summary>
    /// Number of physical 4 KiB WRAM banks available to the model.
    /// </summary>
    int WorkRamBankCount { get; }

    /// <summary>
    /// Indicates whether the CPU-visible CGB WRAM bank register is enabled.
    /// </summary>
    bool IsWorkRamBankRegisterEnabled { get; }

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

    /// <summary>
    /// Seeds CPU and hardware registers after skipping the boot ROM.
    /// </summary>
    void ApplyPostBootState(Cartridge cartridge, Cpu cpu, MemoryBus bus);
}
