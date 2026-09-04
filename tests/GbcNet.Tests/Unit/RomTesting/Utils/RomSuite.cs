// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Unit.RomTesting.Utils;

/// <summary>
/// Pairs xUnit theory rows with lazily computed results for the same ROM paths.
/// </summary>
internal sealed class RomSuite
{
    private readonly Lazy<IReadOnlyDictionary<string, RomTestResult>> _results;

    /// <summary>
    /// Creates a suite whose expensive ROM runs start only when results are read.
    /// </summary>
    internal RomSuite(
        TheoryData<string> rows,
        Lazy<IReadOnlyDictionary<string, RomTestResult>> results
    )
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(results);

        Rows = rows;
        _results = results;
    }

    /// <summary>
    /// Gets the relative ROM paths exposed as xUnit theory data.
    /// </summary>
    public TheoryData<string> Rows { get; }

    /// <summary>
    /// Gets the cached result for each relative ROM path, running the suite on first access.
    /// </summary>
    public IReadOnlyDictionary<string, RomTestResult> Results => _results.Value;
}
