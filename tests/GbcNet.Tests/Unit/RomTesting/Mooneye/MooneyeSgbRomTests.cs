// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Hardware;
using GbcNet.Tests.Unit.RomTesting.Utils;

namespace GbcNet.Tests.Unit.RomTesting.Mooneye;

public sealed class MooneyeSgbRomTests
{
    private const string RomDirectory = "RomTesting/Resources/Mooneye/acceptance";
    private const int MaxMachineCycles = 20_000_000;

    public static TheoryData<string> RomRelativePathRows =>
        ["boot_div-S.gb", "boot_div2-S.gb", "boot_hwio-S.gb", "boot_regs-sgb.gb"];

    [Theory]
    [MemberData(nameof(RomRelativePathRows))]
    public void SgbRomPasses(string relativePath)
    {
        var rom = File.ReadAllBytes(Path.Combine(RomDirectory, relativePath));
        var result = RomTestRunner.Run(
            rom,
            MaxMachineCycles,
            RomTestProtocol.Mooneye,
            HardwareModel.Sgb
        );

        RomTestAssertions.AssertPassed(result);
    }
}
