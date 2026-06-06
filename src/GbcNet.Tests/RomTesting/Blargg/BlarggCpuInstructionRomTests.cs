using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Blargg;

[Collection<RomTestingGroup>]
public sealed class BlarggCpuInstructionRomTests
{
    private const string RomDirectory = "RomTesting/Resources/Blargg/cpu_instrs/individual";
    private const int MaxMachineCycles = 20_000_000;

    [Theory]
    [InlineData("01-special.gb")]
    [InlineData("02-interrupts.gb")]
    [InlineData("03-op sp,hl.gb")]
    [InlineData("04-op r,imm.gb")]
    [InlineData("05-op rp.gb")]
    [InlineData("06-ld r,r.gb")]
    [InlineData("07-jr,jp,call,ret,rst.gb")]
    [InlineData("08-misc instrs.gb")]
    [InlineData("09-op r,r.gb")]
    [InlineData("10-bit ops.gb")]
    [InlineData("11-op a,(hl).gb")]
    public void CpuInstructionRomPasses(string fileName)
    {
        byte[] rom = File.ReadAllBytes(Path.Combine(RomDirectory, fileName));

        RomTestResult result = RomTestRunner.Run(rom, MaxMachineCycles);

        RomTestAssertions.AssertPassed(result);
    }
}
