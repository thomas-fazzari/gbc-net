using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Blargg;

[Collection<RomTestingGroup>]
public sealed class BlarggInstructionTimingRomTests
{
    private const string RomPath = "RomTesting/Resources/Blargg/instr_timing/instr_timing.gb";
    private const int MaxMachineCycles = 50_000_000;

    [Fact]
    public void InstructionTimingRomPasses()
    {
        byte[] rom = File.ReadAllBytes(RomPath);

        RomTestResult result = RomTestRunner.Run(rom, MaxMachineCycles);

        Assert.Equal(RomTestStatus.Passed, result.Status);
    }
}
