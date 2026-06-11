using System.Runtime.CompilerServices;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Hardware.Profiles;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests.Sm83;

internal static class CpuTestFactory
{
    private static readonly ConditionalWeakTable<Cpu, MemoryBus> Buses = [];

    public static Cpu CreateCpu(
        Action<byte[]>? configure = null,
        Action? tickMachineCycle = null,
        IHardwareProfile? hardwareProfile = null
    )
    {
        Cartridge cartridge = ResultAssertions.AssertSuccess(
            Cartridge.Load(TestRomFactory.Create(configure))
        );
        var bus = new MemoryBus(cartridge, hardwareProfile ?? DmgHardwareProfile.Instance);
        var cpu = new Cpu(bus, tickMachineCycle);
        Buses.Add(cpu, bus);
        return cpu;
    }

    public static MemoryBus GetBus(Cpu cpu)
    {
        return Buses.TryGetValue(cpu, out MemoryBus? bus)
            ? bus
            : throw new InvalidOperationException("CPU was not created by the CPU test factory.");
    }
}
