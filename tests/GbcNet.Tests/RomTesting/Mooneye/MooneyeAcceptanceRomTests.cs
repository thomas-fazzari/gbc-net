using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Mooneye;

[Collection<RomTestingGroup>]
public sealed class MooneyeAcceptanceRomTests
{
    private const string RomDirectory = "RomTesting/Resources/Mooneye/acceptance";
    private const int MaxMachineCycles = 20_000_000;

    [Theory]
    [InlineData("add_sp_e_timing.gb")]
    [InlineData("bits/reg_f.gb")]
    [InlineData("call_cc_timing.gb")]
    [InlineData("call_cc_timing2.gb")]
    [InlineData("call_timing.gb")]
    [InlineData("call_timing2.gb")]
    [InlineData("ei_sequence.gb")]
    [InlineData("ei_timing.gb")]
    [InlineData("halt_ime0_ei.gb")]
    [InlineData("if_ie_registers.gb")]
    [InlineData("interrupts/ie_push.gb")]
    [InlineData("instr/daa.gb")]
    [InlineData("jp_cc_timing.gb")]
    [InlineData("jp_timing.gb")]
    [InlineData("ld_hl_sp_e_timing.gb")]
    [InlineData("pop_timing.gb")]
    [InlineData("push_timing.gb")]
    [InlineData("rapid_di_ei.gb")]
    [InlineData("ret_cc_timing.gb")]
    [InlineData("ret_timing.gb")]
    [InlineData("reti_intr_timing.gb")]
    [InlineData("reti_timing.gb")]
    [InlineData("rst_timing.gb")]
    public void AcceptanceRomPasses(string relativePath)
    {
        byte[] rom = File.ReadAllBytes(Path.Combine(RomDirectory, relativePath));

        RomTestResult result = RomTestRunner.Run(rom, MaxMachineCycles, RomTestProtocol.Mooneye);

        Assert.True(result.Status is RomTestStatus.Passed, result.ToFailureMessage());
    }
}
