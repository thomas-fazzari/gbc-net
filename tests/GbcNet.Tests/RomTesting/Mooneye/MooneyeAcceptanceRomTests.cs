using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Mooneye;

[Collection<RomTestingGroup>]
public sealed class MooneyeAcceptanceRomTests
{
    private const string RomDirectory = "RomTesting/Resources/Mooneye/acceptance";
    private const int MaxMachineCycles = 20_000_000;

    [Theory]
    [InlineData("bits/reg_f.gb")]
    [InlineData("ei_sequence.gb")]
    [InlineData("ei_timing.gb")]
    [InlineData("halt_ime0_ei.gb")]
    [InlineData("if_ie_registers.gb")]
    [InlineData("interrupts/ie_push.gb")]
    [InlineData("instr/daa.gb")]
    [InlineData("rapid_di_ei.gb")]
    [InlineData("reti_intr_timing.gb")]
    [InlineData("reti_timing.gb")]
    public void AcceptanceRomPasses(string relativePath)
    {
        byte[] rom = File.ReadAllBytes(Path.Combine(RomDirectory, relativePath));

        RomTestResult result = RomTestRunner.Run(rom, MaxMachineCycles, RomTestProtocol.Mooneye);

        Assert.True(result.Status is RomTestStatus.Passed, result.ToFailureMessage());
    }
}
