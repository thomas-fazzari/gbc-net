using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Mooneye;

[Collection<RomTestingGroup>]
public sealed class MooneyeAcceptanceRomTests
{
    private const string RomDirectory = "RomTesting/Resources/Mooneye/acceptance";
    private const int MaxMachineCycles = 20_000_000;

    [Theory]
    [InlineData("add_sp_e_timing.gb")]
    [InlineData("bits/mem_oam.gb")]
    [InlineData("bits/reg_f.gb")]
    [InlineData("bits/unused_hwio-GS.gb")]
    [InlineData("boot_div-dmgABCmgb.gb")]
    [InlineData("boot_hwio-dmgABCmgb.gb")]
    [InlineData("boot_regs-dmgABC.gb")]
    [InlineData("call_cc_timing.gb")]
    [InlineData("call_cc_timing2.gb")]
    [InlineData("call_timing.gb")]
    [InlineData("call_timing2.gb")]
    [InlineData("di_timing-GS.gb")]
    [InlineData("div_timing.gb")]
    [InlineData("ei_sequence.gb")]
    [InlineData("ei_timing.gb")]
    [InlineData("halt_ime0_ei.gb")]
    [InlineData("halt_ime0_nointr_timing.gb")]
    [InlineData("halt_ime1_timing.gb")]
    [InlineData("halt_ime1_timing2-GS.gb")]
    [InlineData("ppu/hblank_ly_scx_timing-GS.gb")]
    [InlineData("if_ie_registers.gb")]
    [InlineData("interrupts/ie_push.gb")]
    [InlineData("intr_timing.gb")]
    [InlineData("instr/daa.gb")]
    [InlineData("jp_cc_timing.gb")]
    [InlineData("jp_timing.gb")]
    [InlineData("ld_hl_sp_e_timing.gb")]
    [InlineData("ppu/lcdon_timing-GS.gb")]
    [InlineData("ppu/lcdon_write_timing-GS.gb")]
    [InlineData("oam_dma/basic.gb")]
    [InlineData("oam_dma/reg_read.gb")]
    [InlineData("oam_dma/sources-GS.gb")]
    [InlineData("oam_dma_restart.gb")]
    [InlineData("oam_dma_start.gb")]
    [InlineData("oam_dma_timing.gb")]
    [InlineData("pop_timing.gb")]
    [InlineData("ppu/intr_1_2_timing-GS.gb")]
    [InlineData("ppu/intr_2_0_timing.gb")]
    [InlineData("ppu/intr_2_mode0_timing.gb")]
    [InlineData("ppu/intr_2_mode3_timing.gb")]
    [InlineData("ppu/intr_2_oam_ok_timing.gb")]
    [InlineData("ppu/stat_irq_blocking.gb")]
    [InlineData("ppu/stat_lyc_onoff.gb")]
    [InlineData("ppu/vblank_stat_intr-GS.gb")]
    [InlineData("push_timing.gb")]
    [InlineData("rapid_di_ei.gb")]
    [InlineData("ret_cc_timing.gb")]
    [InlineData("ret_timing.gb")]
    [InlineData("reti_intr_timing.gb")]
    [InlineData("reti_timing.gb")]
    [InlineData("rst_timing.gb")]
    [InlineData("timer/div_write.gb")]
    [InlineData("timer/rapid_toggle.gb")]
    [InlineData("timer/tim00.gb")]
    [InlineData("timer/tim00_div_trigger.gb")]
    [InlineData("timer/tim01.gb")]
    [InlineData("timer/tim01_div_trigger.gb")]
    [InlineData("timer/tim10.gb")]
    [InlineData("timer/tim10_div_trigger.gb")]
    [InlineData("timer/tim11.gb")]
    [InlineData("timer/tim11_div_trigger.gb")]
    [InlineData("timer/tima_reload.gb")]
    [InlineData("timer/tima_write_reloading.gb")]
    [InlineData("timer/tma_write_reloading.gb")]
    public void AcceptanceRomPasses(string relativePath)
    {
        byte[] rom = File.ReadAllBytes(Path.Combine(RomDirectory, relativePath));

        RomTestResult result = RomTestRunner.Run(rom, MaxMachineCycles, RomTestProtocol.Mooneye);

        Assert.True(result.Status is RomTestStatus.Passed, result.ToFailureMessage());
    }
}
