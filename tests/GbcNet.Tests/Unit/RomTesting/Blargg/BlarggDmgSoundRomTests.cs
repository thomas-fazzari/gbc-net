// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Tests.Unit.RomTesting.Utils;

namespace GbcNet.Tests.Unit.RomTesting.Blargg;

public sealed class BlarggDmgSoundRomTests
{
    private const string RomDirectory = "RomTesting/Resources/Blargg/dmg_sound";
    private const int MaxMachineCycles = 100_000_000;

    public static TheoryData<string> RomFileNameRows =>
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

    [Theory]
    [MemberData(nameof(RomFileNameRows))]
    public void DmgSoundRomPasses(string fileName)
    {
        var rom = File.ReadAllBytes(Path.Combine(RomDirectory, fileName));
        var result = RomTestRunner.Run(rom, MaxMachineCycles);

        RomTestAssertions.AssertPassed(result);
    }
}
