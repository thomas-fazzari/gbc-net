using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Blargg;

[Collection<RomTestingGroup>]
public sealed class BlarggMemoryTiming2RomTests
{
    private const string RomDirectory = "RomTesting/Resources/Blargg/mem_timing-2";
    private const int MaxMachineCycles = 50_000_000;

    private static readonly string[] _romFileNames =
    [
        "mem_timing.gb",
        "rom_singles/01-read_timing.gb",
        "rom_singles/02-write_timing.gb",
        "rom_singles/03-modify_timing.gb",
    ];

    public static TheoryData<string> RomFileNameRows =>
        RomTestRunner.CreateTheoryData(_romFileNames);

    private static readonly Lazy<IReadOnlyDictionary<string, RomTestResult>> _results = new(() =>
        RomTestRunner.RunAll(
            _romFileNames,
            fileName =>
            {
                byte[] rom = File.ReadAllBytes(Path.Combine(RomDirectory, fileName));
                return RomTestRunner.Run(rom, MaxMachineCycles);
            }
        )
    );

    [Theory]
    [MemberData(nameof(RomFileNameRows))]
    public void MemoryTiming2RomPasses(string fileName) =>
        RomTestAssertions.AssertPassed(_results.Value, fileName);
}
