// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Blargg;

[Collection<RomTestSuite>]
public sealed class BlarggInstructionTimingRomTests
{
    private const string RomPath = "RomTesting/Resources/Blargg/instr_timing/instr_timing.gb";
    private const int MaxMachineCycles = 50_000_000;

    [Fact]
    public void InstructionTimingRomPasses()
    {
        var rom = File.ReadAllBytes(RomPath);

        var result = RomTestRunner.Run(rom, MaxMachineCycles);

        RomTestAssertions.AssertPassed(result);
    }
}
