using GbcNet.Core.Hardware;
using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Mooneye;

[Collection<RomTestingGroup>]
public sealed class MooneyeCgbRomTests
{
    private const string RomDirectory = "RomTesting/Resources/Mooneye";
    private const int MaxMachineCycles = 20_000_000;

    private static readonly string[] _romRelativePaths =
    [
        "misc/bits/unused_hwio-C.gb",
        "misc/boot_div-cgbABCDE.gb",
        "misc/boot_hwio-C.gb",
        "misc/boot_regs-cgb.gb",
        "misc/ppu/vblank_stat_intr-C.gb",
        "acceptance/ppu/stat_lyc_onoff.gb",
        "acceptance/ppu/intr_2_0_timing.gb",
        "acceptance/ppu/intr_2_mode0_timing.gb",
        "acceptance/ppu/intr_2_mode0_timing_sprites.gb",
        "acceptance/ppu/intr_2_mode3_timing.gb",
        "acceptance/ppu/intr_2_oam_ok_timing.gb",
        "acceptance/ppu/stat_irq_blocking.gb",
        "acceptance/timer/div_write.gb",
        "acceptance/timer/rapid_toggle.gb",
        "acceptance/timer/tim00.gb",
        "acceptance/timer/tim00_div_trigger.gb",
        "acceptance/timer/tim01.gb",
        "acceptance/timer/tim01_div_trigger.gb",
        "acceptance/timer/tim10.gb",
        "acceptance/timer/tim10_div_trigger.gb",
        "acceptance/timer/tim11.gb",
        "acceptance/timer/tim11_div_trigger.gb",
        "acceptance/timer/tima_reload.gb",
        "acceptance/timer/tima_write_reloading.gb",
        "acceptance/timer/tma_write_reloading.gb",
    ];

    public static TheoryData<string> RomRelativePathRows =>
        RomTestRunner.CreateTheoryData(_romRelativePaths);

    private static readonly Lazy<IReadOnlyDictionary<string, RomTestResult>> _results = new(() =>
        RomTestRunner.RunAll(
            _romRelativePaths,
            relativePath =>
            {
                var rom = File.ReadAllBytes(Path.Combine(RomDirectory, relativePath));

                return RomTestRunner.Run(
                    rom,
                    MaxMachineCycles,
                    RomTestProtocol.Mooneye,
                    HardwareModel.Cgb
                );
            }
        )
    );

    [Theory]
    [MemberData(nameof(RomRelativePathRows))]
    public void CgbRomPasses(string relativePath) =>
        RomTestAssertions.AssertPassed(_results.Value, relativePath);
}
