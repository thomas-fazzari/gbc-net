// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using System.Numerics;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Core.Hardware.Profiles;

/// <summary>
/// SGB1 register state after boot ROM execution.
/// </summary>
internal static class SgbPostBootState
{
    private const byte Accumulator = 0x01;
    private const byte Flags = 0x00;
    private const ushort RegisterBc = 0x0014;
    private const ushort RegisterDe = 0x0000;
    private const ushort AudioMasterControlRegister = 0xFF26;
    private const ushort GlobalChecksumHighAddress = 0x014E;
    private const ushort GlobalChecksumLowAddress = 0x014F;
    private const ushort RegisterHl = 0xC060;
    private const ushort DividerCounterBase = 0xD874;
    private const int DividerCounterCyclesPerSetGlobalChecksumBit = 4;

    private static readonly PostBootHardwareRegisterState[] _sgbRegisterStates =
    [
        new(AddressMap.JoypadRegister, 0xFF),
        new(AudioMasterControlRegister, 0xF0),
    ];

    public static void Apply(Cartridge cartridge, Cpu cpu, MemoryBus bus)
    {
        DmgPostBootState.Apply(cartridge, cpu, bus);
        PostBootState.SetCpuRegisters(cpu.Registers, CreateCpuRegisterState());
        bus.Clock.SetCounter(CreateDividerCounter(cartridge));
        PostBootState.SetHardwareRegisterStates(bus, _sgbRegisterStates);
    }

    private static PostBootCpuRegisterState CreateCpuRegisterState() =>
        new(
            Accumulator,
            Flags,
            RegisterBc,
            RegisterDe,
            RegisterHl,
            AddressMap.CartridgeEntryPointAddress,
            AddressMap.HighRamEnd
        );

    private static ushort CreateDividerCounter(Cartridge cartridge)
    {
        var globalChecksum = (ushort)(
            (cartridge.ReadRom(GlobalChecksumHighAddress) << 8)
            | cartridge.ReadRom(GlobalChecksumLowAddress)
        );
        var checksumCycles =
            BitOperations.PopCount(globalChecksum) * DividerCounterCyclesPerSetGlobalChecksumBit;

        return (ushort)(DividerCounterBase - checksumCycles);
    }
}
