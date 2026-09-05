// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Tests.Unit.RomTesting.Utils;

namespace GbcNet.Tests.Unit.RomTesting.Blargg;

public sealed class BlarggCpuInstructionRomTests
{
    private const string RomDirectory = "RomTesting/Resources/Blargg/cpu_instrs/individual";
    private const int MaxMachineCycles = 20_000_000;

    public static TheoryData<string> RomFileNameRows =>
        [
            "01-special.gb",
            "02-interrupts.gb",
            "03-op sp,hl.gb",
            "04-op r,imm.gb",
            "05-op rp.gb",
            "06-ld r,r.gb",
            "07-jr,jp,call,ret,rst.gb",
            "08-misc instrs.gb",
            "09-op r,r.gb",
            "10-bit ops.gb",
            "11-op a,(hl).gb",
        ];

    [Theory]
    [MemberData(nameof(RomFileNameRows))]
    public void CpuInstructionRomPasses(string fileName)
    {
        var rom = File.ReadAllBytes(Path.Combine(RomDirectory, fileName));
        var result = RomTestRunner.Run(rom, MaxMachineCycles);

        RomTestAssertions.AssertPassed(result);
    }
}
