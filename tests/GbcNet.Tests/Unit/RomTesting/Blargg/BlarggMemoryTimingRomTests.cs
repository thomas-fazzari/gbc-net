// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Tests.Unit.RomTesting.Utils;

namespace GbcNet.Tests.Unit.RomTesting.Blargg;

public sealed class BlarggMemoryTimingRomTests
{
    private const string RomDirectory = "RomTesting/Resources/Blargg/mem_timing";
    private const int MaxMachineCycles = 50_000_000;

    public static TheoryData<string> RomFileNameRows =>
        [
            "mem_timing.gb",
            "individual/01-read_timing.gb",
            "individual/02-write_timing.gb",
            "individual/03-modify_timing.gb",
        ];

    [Theory]
    [MemberData(nameof(RomFileNameRows))]
    public void MemoryTimingRomPasses(string fileName)
    {
        var rom = File.ReadAllBytes(Path.Combine(RomDirectory, fileName));
        var result = RomTestRunner.Run(rom, MaxMachineCycles);

        RomTestAssertions.AssertPassed(result);
    }
}
