// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Tests.Unit.RomTesting.Utils;

namespace GbcNet.Tests.Unit.RomTesting.Blargg;

[Collection<RomTestCollectionDefinition>]
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

    private static readonly RomSuite _roms = RomTestRunner.CreateSuite(
        _romFileNames,
        RomDirectory,
        MaxMachineCycles
    );

    public static TheoryData<string> RomFileNameRows => _roms.Rows;

    [Theory]
    [MemberData(nameof(RomFileNameRows))]
    public void MemoryTimingRomPasses(string fileName) =>
        RomTestAssertions.AssertPassed(_roms.Results, fileName);
}
