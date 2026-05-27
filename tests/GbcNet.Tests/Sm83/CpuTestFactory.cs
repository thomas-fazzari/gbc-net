using FluentResults;
using GbcNet.Core.Cartridges;
using GbcNet.Core.Memory;
using GbcNet.Core.Sm83;
using GbcNet.Tests.Cartridges;

namespace GbcNet.Tests.Sm83;

internal static class CpuTestFactory
{
    public static Cpu CreateCpu(Action<byte[]>? configure = null)
    {
        Result<Cartridge> cartridge = Cartridge.Load(TestRomFactory.Create(configure));
        Assert.True(cartridge.IsSuccess, DescribeErrors(cartridge.Errors));
        return new Cpu(new MemoryBus(cartridge.Value));
    }

    private static string DescribeErrors(IReadOnlyList<IError> errors) =>
        string.Join(Environment.NewLine, errors.Select(error => error.Message));
}
