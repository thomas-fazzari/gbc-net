// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Hardware;
using GbcNet.Tests.RomTesting.Utils;

namespace GbcNet.Tests.RomTesting.Mooneye;

[Collection<RomTestSuite>]
public sealed class MooneyeSgbRomTests
{
    private const string RomDirectory = "RomTesting/Resources/Mooneye/acceptance";
    private const int MaxMachineCycles = 20_000_000;

    private static readonly string[] _romRelativePaths =
    [
        "boot_div-S.gb",
        "boot_div2-S.gb",
        "boot_hwio-S.gb",
        "boot_regs-sgb.gb",
    ];

    private static readonly RomTestRunner.RomSuite _roms = RomTestRunner.CreateSuite(
        _romRelativePaths,
        RomDirectory,
        MaxMachineCycles,
        RomTestProtocol.Mooneye,
        HardwareModel.Sgb
    );

    public static TheoryData<string> RomRelativePathRows => _roms.Rows;

    [Theory]
    [MemberData(nameof(RomRelativePathRows))]
    public void SgbRomPasses(string relativePath) =>
        RomTestAssertions.AssertPassed(_roms.Results, relativePath);
}
