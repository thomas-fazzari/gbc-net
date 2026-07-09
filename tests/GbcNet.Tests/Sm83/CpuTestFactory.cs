// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests.Sm83;

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
}
