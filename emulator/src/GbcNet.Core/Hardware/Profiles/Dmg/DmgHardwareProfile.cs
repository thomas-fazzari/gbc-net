// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Apu;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using GbcNet.Core.Ppu.Engines;
using GbcNet.Core.Sm83;

namespace GbcNet.Core.Hardware.Profiles;

/// <summary>
/// Provides DMG model-specific hardware behavior.
/// </summary>
internal sealed class DmgHardwareProfile : IHardwareProfile
{
    public static DmgHardwareProfile Instance { get; } = new();

    private DmgHardwareProfile() { }

    private const ushort HighSourceMirrorMask = 0xDFFF;

    private enum OamDmaBus
    {
        Main = 0,
        Video = 1,
    }

    public HardwareModel Model => HardwareModel.Dmg;

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
        MapOamDmaSourceAddressCore(sourceAddress);

    public bool IsCpuAddressBlockedByOamDma(ushort address, ushort sourceAddress) =>
        IsCpuAddressBlockedByOamDmaCore(address, sourceAddress);

    internal static ushort MapOamDmaSourceAddressCore(ushort sourceAddress) =>
        sourceAddress >= AddressMap.EchoRamStart
            ? (ushort)(sourceAddress & HighSourceMirrorMask)
            : sourceAddress;

    internal static bool IsCpuAddressBlockedByOamDmaCore(ushort address, ushort sourceAddress)
    {
        if (address >= AddressMap.ObjectAttributeMemoryStart)
        {
            return address <= AddressMap.ObjectAttributeMemoryEnd;
        }

        return GetOamDmaBus(address) == GetOamDmaBus(sourceAddress);
    }

    private static OamDmaBus GetOamDmaBus(ushort address) =>
        address is >= AddressMap.VideoRamStart and <= AddressMap.VideoRamEnd
            ? OamDmaBus.Video
            : OamDmaBus.Main;

    public ApuModelSpec CreateApuModelSpec() => ApuModelSpec.Dmg;

    public void ApplyPostBootState(Cartridge cartridge, Cpu cpu, MemoryBus bus)
    {
        DmgPostBootState.Apply(cartridge, cpu, bus);
    }
}
