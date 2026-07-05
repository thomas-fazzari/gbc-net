// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu.Engines;
using GbcNet.Core.Sm83;

namespace GbcNet.Core.Hardware.Profiles;

/// <summary>
/// Provides SGB model-specific hardware behavior with high-level SNES-side feature emulation.
/// </summary>
internal sealed class SgbHardwareProfile : IHardwareProfile
{
    public static SgbHardwareProfile Instance { get; } = new();

    private SgbHardwareProfile() { }

    public HardwareModel Model => HardwareModel.Sgb;

    public int VideoRamBankCount => 1;

    public bool IsVideoRamBankRegisterEnabled => false;

    public int WorkRamBankCount => 2;

    public bool IsKey1RegisterEnabled => false;

    public bool IsSerialHighSpeedClockEnabled => false;

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

    public ushort MapOamDmaSourceAddress(ushort sourceAddress) =>
        DmgHardwareProfile.MapOamDmaSourceAddressCore(sourceAddress);

    public bool IsCpuAddressBlockedByOamDma(ushort address, ushort sourceAddress) =>
        DmgHardwareProfile.IsCpuAddressBlockedByOamDmaCore(address, sourceAddress);

    public ApuModelSpec CreateApuModelSpec() => ApuModelSpec.Sgb;

    public void ApplyPostBootState(Cartridge cartridge, Cpu cpu, MemoryBus bus)
    {
        DmgPostBootState.Apply(cartridge, cpu, bus, registerBc: 0x0014);
    }
}
