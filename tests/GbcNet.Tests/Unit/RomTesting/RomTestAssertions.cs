// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Unit.RomTesting;

/// <summary>
/// Converts ROM protocol results into assertion failures with captured diagnostics.
/// </summary>
internal static class RomTestAssertions
{
    /// <summary>
    /// Asserts that one ROM run reached its passing terminal state.
    /// </summary>
    public static void AssertPassed(RomTestResult result)
    {
        (result.Status is RomTestStatus.Passed).Should().BeTrue(result.ToFailureMessage());
    }

    /// <summary>
    /// Finds a ROM result by relative path and asserts that it passed.
    /// </summary>
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
