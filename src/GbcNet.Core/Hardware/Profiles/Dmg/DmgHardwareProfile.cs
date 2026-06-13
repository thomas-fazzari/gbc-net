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

    public int VideoRamBankCount => 1;

    public bool IsVideoRamBankRegisterEnabled => false;

    public int WorkRamBankCount => 2;

    public bool IsKey1RegisterEnabled => false;

    public bool TicksTimerOnTacDisableWhenInputHigh => true;

    public bool TicksTimerOnTacEnableWhenInputHigh => false;

    public bool IsWorkRamBankRegisterEnabled => false;

    public bool IsColorPaletteRamEnabled => false;

    public bool IsColorPaletteIndexRegisterEnabled => false;

    public bool IsObjectPriorityModeRegisterEnabled => false;

    public bool IsVideoRamDmaRegisterEnabled => false;

    public bool IsCgbHardwareMiscRegisterEnabled => false;

    public bool IsCgbUndocumentedFf74RegisterEnabled => false;

    public IPpuEngine CreatePpuEngine() => new DmgPpuEngine();

    public ITransferPolicy CreateOamDmaTransferPolicy() => new DmgOamDmaTransferPolicy();

    public IApuHardwareProfile CreateApuHardwareProfile() => new DmgApuHardwareProfile();

    public void ApplyPostBootState(Cartridge cartridge, Cpu cpu, MemoryBus bus)
    {
        DmgPostBootState.Apply(cartridge, cpu, bus);
    }
}
