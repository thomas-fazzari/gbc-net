// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Unit.RomTesting.Utils;

internal sealed class RomSuite
{
    private readonly Lazy<IReadOnlyDictionary<string, RomTestResult>> _results;

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

    public TheoryData<string> Rows { get; }

    public IReadOnlyDictionary<string, RomTestResult> Results => _results.Value;
}
