// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu.Engines;
using GbcNet.Core.Sm83;

namespace GbcNet.Core.Hardware.Profiles;

/// <summary>
/// Provides retail CGB ABC/DE hardware behavior and records the boot-selected operating mode.
/// </summary>
internal sealed class CgbHardwareProfile(CgbOperatingMode operatingMode) : IHardwareProfile
{
    public CgbOperatingMode OperatingMode { get; } = operatingMode;

    private enum OamDmaBus
    {
        Cartridge = 0,
        Video = 1,
        WorkRam = 2,
        None = 3,
    }

    public HardwareModel Model => HardwareModel.Cgb;

    public int VideoRamBankCount => OperatingMode is CgbOperatingMode.Cgb ? 2 : 1;

    public bool IsVideoRamBankRegisterEnabled => true;

    public int WorkRamBankCount => 8;

    public bool IsKey1RegisterEnabled => OperatingMode is CgbOperatingMode.Cgb;

    public bool IsSerialHighSpeedClockEnabled => OperatingMode is CgbOperatingMode.Cgb;

    public bool TicksTimerOnTacDisableWhenInputHigh => false;

    public bool TicksTimerOnTacEnableWhenInputHigh => true;

    public bool IsWorkRamBankRegisterEnabled => OperatingMode is CgbOperatingMode.Cgb;

    public bool IsColorPaletteRamEnabled => OperatingMode is CgbOperatingMode.Cgb;

    public bool IsColorPaletteIndexRegisterEnabled => true;

    public bool IsObjectPriorityModeRegisterEnabled => OperatingMode is CgbOperatingMode.Cgb;

    public bool HasDmgStatWriteInterruptQuirk => false;

    public bool IsVideoRamDmaRegisterEnabled => OperatingMode is CgbOperatingMode.Cgb;

    public bool IsCgbHardwareMiscRegisterEnabled => true;

    public bool IsCgbUndocumentedFf74RegisterEnabled => OperatingMode is CgbOperatingMode.Cgb;

    public PpuEngineBase CreatePpuEngine() =>
        OperatingMode is CgbOperatingMode.Cgb
            ? new CgbPpuEngine()
            : new DmgPixelRulesPpuEngine<CgbDmgCompatibilityPixelOutput>(
                usesCgbWindowBehavior: true,
                requestsMode2InterruptBeforeVBlank: true,
                stateWrapper: static s => new CgbDmgCompatibilityPpuEngineState(s)
            );

    public ushort MapOamDmaSourceAddress(ushort sourceAddress) =>
        OamDmaMapping.MapSourceAddress(sourceAddress);

    public bool IsCpuAddressBlockedByOamDma(ushort address, ushort sourceAddress)
    {
        if (address >= AddressMap.ObjectAttributeMemoryStart)
        {
            return address <= AddressMap.ObjectAttributeMemoryEnd;
        }

        var sourceBus = GetOamDmaBus(OamDmaMapping.MapSourceAddress(sourceAddress));
        return sourceBus is not OamDmaBus.None && GetOamDmaBus(address) == sourceBus;
    }

    private static OamDmaBus GetOamDmaBus(ushort address) =>
        address switch
        {
            <= AddressMap.RomEnd => OamDmaBus.Cartridge,
            <= AddressMap.VideoRamEnd => OamDmaBus.Video,
            <= AddressMap.ExternalRamEnd => OamDmaBus.Cartridge,
            <= AddressMap.EchoRamEnd => OamDmaBus.WorkRam,
            _ => OamDmaBus.None,
        };

    public ApuModelSpec CreateApuModelSpec() => ApuModelSpec.Cgb;

    public void ApplyPostBootState(Cartridge cartridge, Cpu cpu, MemoryBus bus)
    {
        CgbPostBootState.Apply(OperatingMode, cartridge, cpu, bus);
    }
}
