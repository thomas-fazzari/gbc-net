// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Tests.Unit.RomTesting.Utils;

namespace GbcNet.Tests.Unit.RomTesting.Blargg;

public sealed class BlarggMemoryTiming2RomTests
{
    private const string RomDirectory = "RomTesting/Resources/Blargg/mem_timing-2";
    private const int MaxMachineCycles = 50_000_000;

    public static TheoryData<string> RomFileNameRows =>
        [
            "mem_timing.gb",
            "rom_singles/01-read_timing.gb",
            "rom_singles/02-write_timing.gb",
            "rom_singles/03-modify_timing.gb",
        ];

    [Theory]
    [MemberData(nameof(RomFileNameRows))]
    public void MemoryTiming2RomPasses(string fileName)
    {
        var rom = File.ReadAllBytes(Path.Combine(RomDirectory, fileName));
        var result = RomTestRunner.Run(rom, MaxMachineCycles);

        RomTestAssertions.AssertPassed(result);
    }
}
