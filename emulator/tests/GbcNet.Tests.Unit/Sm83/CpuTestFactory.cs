// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;

namespace GbcNet.Tests.Unit.Sm83;

internal static class CpuTestFactory
{
    public static Cpu CreateCpu(
        Action<byte[]>? configure = null,
        Action? tickMachineCycle = null,
        IHardwareProfile? profile = null
    ) => CreateCpuWithBus(configure, tickMachineCycle, profile).Cpu;

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
