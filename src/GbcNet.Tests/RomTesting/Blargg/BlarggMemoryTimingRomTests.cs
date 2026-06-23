using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Blargg;

[Collection<RomTestSuite>]
public sealed class BlarggMemoryTimingRomTests
{
    private const string RomDirectory = "RomTesting/Resources/Blargg/mem_timing";
    private const int MaxMachineCycles = 50_000_000;

    private static readonly string[] _romFileNames =
    [
        "mem_timing.gb",
        "individual/01-read_timing.gb",
        "individual/02-write_timing.gb",
        "individual/03-modify_timing.gb",
    ];

    public static TheoryData<string> RomFileNameRows =>
        RomTestRunner.CreateTheoryData(_romFileNames);

    private static readonly Lazy<IReadOnlyDictionary<string, RomTestResult>> _results = new(() =>
        RomTestRunner.RunAll(
            _romFileNames,
            fileName =>
            {
                var rom = File.ReadAllBytes(Path.Combine(RomDirectory, fileName));
                return RomTestRunner.Run(rom, MaxMachineCycles);
            }
        )
    );

    [Theory]
    [MemberData(nameof(RomFileNameRows))]
    public void MemoryTimingRomPasses(string fileName) =>
        RomTestAssertions.AssertPassed(_results.Value, fileName);
}
