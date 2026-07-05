// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Blargg;

[Collection<RomTestSuite>]
public sealed class BlarggCpuInstructionRomTests
{
    private const string RomDirectory = "RomTesting/Resources/Blargg/cpu_instrs/individual";
    private const int MaxMachineCycles = 20_000_000;

    private static readonly string[] _romFileNames =
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
    public void CpuInstructionRomPasses(string fileName) =>
        RomTestAssertions.AssertPassed(_results.Value, fileName);
}
