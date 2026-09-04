// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Unit.Sm83;

/// <summary>
/// Creates an SM83 CPU with a ROM-backed memory bus for instruction tests.
/// </summary>
internal static class CpuTestFactory
{
    /// <summary>
    /// Creates a CPU using a generated test ROM and the DMG profile by default.
    /// </summary>
    /// <param name="configure">
    /// An optional callback that edits the ROM before its checksum is written.
    /// </param>
    /// <param name="tickMachineCycle">An optional callback invoked once per CPU M-cycle.</param>
    /// <param name="profile">The hardware profile, or the DMG profile when omitted.</param>
    public static Cpu CreateCpu(
        Action<byte[]>? configure = null,
        Action? tickMachineCycle = null,
        IHardwareProfile? profile = null
    ) => CreateCpuWithBus(configure, tickMachineCycle, profile).Cpu;

    /// <summary>
    /// Creates a CPU and exposes its bus for arranging memory and checking side effects.
    /// </summary>
    /// <param name="configure">
    /// An optional callback that edits the ROM before its checksum is written.
    /// </param>
    /// <param name="tickMachineCycle">An optional callback invoked once per CPU M-cycle.</param>
    /// <param name="profile">The hardware profile, or the DMG profile when omitted.</param>
    public static (Cpu Cpu, MemoryBus Bus) CreateCpuWithBus(
        Action<byte[]>? configure = null,
        Action? tickMachineCycle = null,
        IHardwareProfile? profile = null
    )
    {
        var cartridge = TestRomFactory.LoadCartridge(configure);
        var bus = new MemoryBus(cartridge, profile ?? DmgHardwareProfile.Instance);
        return (new Cpu(bus, tickMachineCycle), bus);
    }

    /// <summary>
    /// Creates a CPU and bus around an existing cartridge.
    /// </summary>
    /// <param name="cartridge">The cartridge exposed through the new memory bus.</param>
    /// <param name="profile">The hardware profile, or the DMG profile when omitted.</param>
    /// <param name="tickMachineCycle">An optional callback invoked once per CPU M-cycle.</param>
    public static (Cpu Cpu, MemoryBus Bus) CreateCpuWithBus(
        Cartridge cartridge,
        IHardwareProfile? profile = null,
        Action? tickMachineCycle = null
    )
    {
        var bus = new MemoryBus(cartridge, profile ?? DmgHardwareProfile.Instance);
        return (new Cpu(bus, tickMachineCycle), bus);
    }
}
