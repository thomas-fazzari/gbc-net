// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Blargg;

[Collection<RomTestSuite>]
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

    private static readonly RomTestRunner.RomSuite _roms = RomTestRunner.CreateSuite(
        _romFileNames,
        RomDirectory,
        MaxMachineCycles
    );

    public static TheoryData<string> RomFileNameRows => _roms.Rows;

    [Theory]
    [MemberData(nameof(RomFileNameRows))]
    public void MemoryTiming2RomPasses(string fileName) =>
        RomTestAssertions.AssertPassed(_roms.Results, fileName);
}
