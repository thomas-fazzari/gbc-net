using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting;

[Collection(RomTestingGroup.Name)]
public sealed class BlarggCpuInstructionRomTests
{
    private const int MaxMachineCycles = 5_000_000;

    [Fact]
    public void SpecialInstructionsPass()
    {
        byte[] rom = File.ReadAllBytes(
            "RomTesting/Resources/Blargg/cpu_instrs/individual/01-special.gb"
        );

        RomTestResult result = RomTestRunner.Run(rom, MaxMachineCycles);

        Assert.Equal(RomTestStatus.Passed, result.Status);
    }
}
