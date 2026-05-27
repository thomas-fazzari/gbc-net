using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests.Sm83;

internal static class CpuTestFactory
{
    public static Cpu CreateCpu(Action<byte[]>? configure = null)
    {
        Cartridge cartridge = ResultAssertions.AssertSuccess(
            Cartridge.Load(TestRomFactory.Create(configure))
        );
        return new Cpu(new MemoryBus(cartridge));
    }
}
