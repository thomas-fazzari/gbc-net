using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Blargg;

[Collection<RomTestingGroup>]
public sealed class BlarggMemoryTiming2RomTests
{
    private const string RomDirectory = "RomTesting/Resources/Blargg/mem_timing-2";
    private const int MaxMachineCycles = 50_000_000;

    [Theory]
    [InlineData("mem_timing.gb")]
    [InlineData("rom_singles/01-read_timing.gb")]
    [InlineData("rom_singles/02-write_timing.gb")]
    [InlineData("rom_singles/03-modify_timing.gb")]
    public void MemoryTiming2RomPasses(string fileName)
    {
        byte[] rom = File.ReadAllBytes(Path.Combine(RomDirectory, fileName));

        RomTestResult result = RomTestRunner.Run(rom, MaxMachineCycles);

        Assert.Equal(RomTestStatus.Passed, result.Status);
    }
}
