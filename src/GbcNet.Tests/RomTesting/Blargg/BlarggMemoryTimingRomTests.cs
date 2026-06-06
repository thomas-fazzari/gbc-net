using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Blargg;

[Collection<RomTestingGroup>]
public sealed class BlarggMemoryTimingRomTests
{
    private const string RomDirectory = "RomTesting/Resources/Blargg/mem_timing";
    private const int MaxMachineCycles = 50_000_000;

    [Theory]
    [InlineData("mem_timing.gb")]
    [InlineData("individual/01-read_timing.gb")]
    [InlineData("individual/02-write_timing.gb")]
    [InlineData("individual/03-modify_timing.gb")]
    public void MemoryTimingRomPasses(string fileName)
    {
        byte[] rom = File.ReadAllBytes(Path.Combine(RomDirectory, fileName));

        RomTestResult result = RomTestRunner.Run(rom, MaxMachineCycles);

        RomTestAssertions.AssertPassed(result);
    }
}
