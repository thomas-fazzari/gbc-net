// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Unit.RomTesting;

internal static class RomTestAssertions
{
    public static void AssertPassed(RomTestResult result)
    {
        (result.Status is RomTestStatus.Passed).Should().BeTrue(result.ToFailureMessage());
    }

    public static void AssertPassed(
        IReadOnlyDictionary<string, RomTestResult> results,
        string relativePath
    )
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(relativePath);

        var result = results.Should().ContainKey(relativePath).WhoseValue;

        (result.Status is RomTestStatus.Passed)
            .Should()
            .BeTrue(relativePath + Environment.NewLine + result.ToFailureMessage());
    }
}
