// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Blargg;

[Collection<RomTestSuite>]
public sealed class BlarggDmgSoundRomTests
{
    private const string RomDirectory = "RomTesting/Resources/Blargg/dmg_sound";
    private const int MaxMachineCycles = 100_000_000;

    private static readonly string[] _romFileNames =
    [
        "01-registers.gb",
        "02-len ctr.gb",
        "03-trigger.gb",
        "04-sweep.gb",
        "05-sweep details.gb",
        "06-overflow on trigger.gb",
        "07-len sweep period sync.gb",
        "08-len ctr during power.gb",
        "09-wave read while on.gb",
        "10-wave trigger while on.gb",
        "11-regs after power.gb",
        "12-wave write while on.gb",
    ];

    private static readonly RomTestRunner.RomSuite _roms = RomTestRunner.CreateSuite(
        _romFileNames,
        RomDirectory,
        MaxMachineCycles
    );

    public static TheoryData<string> RomFileNameRows => _roms.Rows;

    [Theory]
    [MemberData(nameof(RomFileNameRows))]
    public void DmgSoundRomPasses(string fileName) =>
        RomTestAssertions.AssertPassed(_roms.Results, fileName);
}
