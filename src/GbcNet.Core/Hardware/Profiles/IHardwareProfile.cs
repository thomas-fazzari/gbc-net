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
    /// Indicates whether the CPU-visible CGB VRAM bank register is enabled.
    /// </summary>
    bool IsVideoRamBankRegisterEnabled { get; }

    /// <summary>
    /// Number of physical 4 KiB WRAM banks available to the model.
    /// </summary>
    int WorkRamBankCount { get; }

    /// <summary>
    /// Indicates whether the CPU-visible CGB speed switch register is enabled.
    /// </summary>
    bool IsKey1RegisterEnabled { get; }

    /// <summary>
    /// Indicates whether the CPU-visible CGB WRAM bank register is enabled.
    /// </summary>
    bool IsWorkRamBankRegisterEnabled { get; }

    /// <summary>
    /// Indicates whether the CPU-visible CGB color palette RAM registers are enabled.
    /// </summary>
    bool IsColorPaletteRamEnabled { get; }

    /// <summary>
    /// Indicates whether the CPU-visible CGB color palette index registers are enabled.
    /// </summary>
    bool IsColorPaletteIndexRegisterEnabled { get; }

    /// <summary>
    /// Indicates whether the CPU-visible CGB object priority mode register is enabled.
    /// </summary>
    bool IsObjectPriorityModeRegisterEnabled { get; }

    /// <summary>
    /// Indicates whether the CPU-visible CGB VRAM DMA registers are enabled.
    /// </summary>
    bool IsVideoRamDmaRegisterEnabled { get; }

    /// <summary>
    /// Indicates whether the CGB hardware undocumented registers at FF72, FF73, and FF75 are enabled.
    /// </summary>
    bool IsCgbHardwareMiscRegisterEnabled { get; }

    /// <summary>
    /// Indicates whether the CGB-mode-only undocumented register at FF74 is enabled.
    /// </summary>
    bool IsCgbUndocumentedFf74RegisterEnabled { get; }

    /// <summary>
    /// Creates the LCD/PPU engine for this hardware model.
    /// </summary>
    IPpuEngine CreatePpuEngine();

    /// <summary>
    /// Creates the OAM DMA transfer policy for this hardware model.
    /// </summary>
    ITransferPolicy CreateOamDmaTransferPolicy();

    /// <summary>
    /// Creates the APU profile for this hardware model.
    /// </summary>
    IApuHardwareProfile CreateApuHardwareProfile();

    /// <summary>
    /// Seeds CPU and hardware registers after skipping the boot ROM.
    /// </summary>
    void ApplyPostBootState(Cartridge cartridge, Cpu cpu, MemoryBus bus);
}
