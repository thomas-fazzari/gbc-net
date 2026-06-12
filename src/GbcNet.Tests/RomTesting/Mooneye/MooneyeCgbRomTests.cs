using GbcNet.Core.Hardware;
using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Mooneye;

[Collection<RomTestingGroup>]
public sealed class MooneyeCgbRomTests
{
    private const string RomDirectory = "RomTesting/Resources/Mooneye/misc";
    private const int MaxMachineCycles = 20_000_000;

    private static readonly string[] _romRelativePaths =
    [
        "bits/unused_hwio-C.gb",
        "ppu/vblank_stat_intr-C.gb",
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
