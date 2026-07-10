// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Mooneye;

[Collection<RomTestSuite>]
public sealed class MooneyeAcceptanceRomTests
{
    private const string RomDirectory = "RomTesting/Resources/Mooneye/acceptance";
    private const int MaxMachineCycles = 20_000_000;

    private static readonly string[] _romRelativePaths =
    [
        "add_sp_e_timing.gb",
        "bits/mem_oam.gb",
        "bits/reg_f.gb",
        "bits/unused_hwio-GS.gb",
        "boot_div-dmgABCmgb.gb",
        "boot_hwio-dmgABCmgb.gb",
        "boot_regs-dmgABC.gb",
        "call_cc_timing.gb",
        "call_cc_timing2.gb",
        "call_timing.gb",
        "call_timing2.gb",
        "di_timing-GS.gb",
        "div_timing.gb",
        "ei_sequence.gb",
        "ei_timing.gb",
        "halt_ime0_ei.gb",
        "halt_ime0_nointr_timing.gb",
        "halt_ime1_timing.gb",
        "halt_ime1_timing2-GS.gb",
        "ppu/hblank_ly_scx_timing-GS.gb",
        "if_ie_registers.gb",
        "interrupts/ie_push.gb",
        "intr_timing.gb",
        "instr/daa.gb",
        "jp_cc_timing.gb",
        "jp_timing.gb",
        "ld_hl_sp_e_timing.gb",
        "ppu/lcdon_timing-GS.gb",
        "ppu/lcdon_write_timing-GS.gb",
        "oam_dma/basic.gb",
        "oam_dma/reg_read.gb",
        "oam_dma/sources-GS.gb",
        "oam_dma_restart.gb",
        "oam_dma_start.gb",
        "oam_dma_timing.gb",
        "pop_timing.gb",
        "ppu/intr_1_2_timing-GS.gb",
        "ppu/intr_2_0_timing.gb",
        "ppu/intr_2_mode0_timing.gb",
        "ppu/intr_2_mode0_timing_sprites.gb",
        "ppu/intr_2_mode3_timing.gb",
        "ppu/intr_2_oam_ok_timing.gb",
        "ppu/stat_irq_blocking.gb",
        "ppu/stat_lyc_onoff.gb",
        "ppu/vblank_stat_intr-GS.gb",
        "push_timing.gb",
        "rapid_di_ei.gb",
        "ret_cc_timing.gb",
        "ret_timing.gb",
        "reti_intr_timing.gb",
        "reti_timing.gb",
        "rst_timing.gb",
        "serial/boot_sclk_align-dmgABCmgb.gb",
        "timer/div_write.gb",
        "timer/rapid_toggle.gb",
        "timer/tim00.gb",
        "timer/tim00_div_trigger.gb",
        "timer/tim01.gb",
        "timer/tim01_div_trigger.gb",
        "timer/tim10.gb",
        "timer/tim10_div_trigger.gb",
        "timer/tim11.gb",
        "timer/tim11_div_trigger.gb",
        "timer/tima_reload.gb",
        "timer/tima_write_reloading.gb",
        "timer/tma_write_reloading.gb",
    ];

    private static readonly RomTestRunner.RomSuite _roms = RomTestRunner.CreateSuite(
        _romRelativePaths,
        RomDirectory,
        MaxMachineCycles,
        RomTestProtocol.Mooneye
    );

    public static TheoryData<string> RomRelativePathRows => _roms.Rows;

    [Theory]
    [MemberData(nameof(RomRelativePathRows))]
    public void AcceptanceRomPasses(string relativePath) =>
        RomTestAssertions.AssertPassed(_roms.Results, relativePath);
}
