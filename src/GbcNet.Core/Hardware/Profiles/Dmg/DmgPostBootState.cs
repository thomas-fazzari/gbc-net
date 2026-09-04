// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Core.Hardware.Profiles;

/// <summary>
/// DMG register state after boot ROM execution.
/// </summary>
internal static class DmgPostBootState
{
    private const ushort DividerCounter = 0xABCC;
    private const byte Accumulator = 0x01;
    private const byte FlagsBase = (byte)CpuFlag.Zero;
    private const byte FlagsChecksumNonZero = (byte)(CpuFlag.HalfCarry | CpuFlag.Carry);
    private const ushort RegisterBc = 0x0013;
    private const ushort RegisterDe = 0x00D8;
    private const ushort RegisterHl = 0x014D;

    private static readonly PostBootHardwareRegisterState[] _registerStatesBeforeAudio =
    [
        new(AddressMap.JoypadRegister, 0xCF),
        new(AddressMap.SerialTransferDataRegister, 0x00),
        new(AddressMap.SerialTransferControlRegister, 0x7E),
        new(AddressMap.TimerCounterRegister, 0x00),
        new(AddressMap.TimerModuloRegister, 0x00),
        new(AddressMap.TimerControlRegister, 0x00),
        new(AddressMap.InterruptFlagRegister, 0x01),
    ];

    private static readonly PostBootHardwareRegisterState[] _registerStatesAfterAudio =
    [
        new(AddressMap.LcdControlRegister, 0x91),
        new(AddressMap.LcdStatusRegister, 0x85),
        new(AddressMap.ScrollYRegister, 0x00),
        new(AddressMap.ScrollXRegister, 0x00),
        new(AddressMap.LcdYCoordinateRegister, 0x00),
        new(AddressMap.LcdYCompareRegister, 0x00),
        new(AddressMap.DmaRegister, 0xFF),
        new(AddressMap.BackgroundPaletteRegister, 0xFC),
        new(AddressMap.WindowYRegister, 0x00),
        new(AddressMap.WindowXRegister, 0x00),
        new(AddressMap.InterruptEnableRegister, 0x00),
    ];

    public static void Apply(Cartridge cartridge, Cpu cpu, MemoryBus bus)
    {
        Apply(cpu, bus, CreateCpuRegisterState(cartridge, RegisterBc), DividerCounter);
    }

    /// <summary>
    /// Applies the shared DMG/SGB hardware register state: CPU registers, divider, pre-audio, audio, and post-audio registers.
    /// SGB passes its own CPU register state and divider so the
    /// init runs once, then applies its two SGB-specific overrides afterward.
    /// </summary>
    internal static void Apply(
        Cpu cpu,
        MemoryBus bus,
        PostBootCpuRegisterState cpuRegisterState,
        ushort dividerCounter
    )
    {
        PostBootState.SetCpuRegisters(cpu.Registers, cpuRegisterState);
        bus.Clock.SetCounter(dividerCounter);
        PostBootState.SetHardwareRegisterStates(bus, _registerStatesBeforeAudio);
        PostBootState.ApplyAudioRegisters(bus);
        PostBootState.SetHardwareRegisterStates(bus, _registerStatesAfterAudio);
    }

    private static PostBootCpuRegisterState CreateCpuRegisterState(
        Cartridge cartridge,
        ushort registerBc
    ) =>
        new(
            Accumulator,
            cartridge.Header.HeaderChecksum is 0x00
                ? FlagsBase
                : (byte)(FlagsBase | FlagsChecksumNonZero),
            registerBc,
            RegisterDe,
            RegisterHl,
            AddressMap.CartridgeEntryPointAddress,
            AddressMap.HighRamEnd
        );
}
