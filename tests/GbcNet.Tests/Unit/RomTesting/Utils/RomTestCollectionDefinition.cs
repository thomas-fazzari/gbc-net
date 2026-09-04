// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Unit.RomTesting.Utils;

/// <summary>
/// Groups ROM test classes so they do not run in parallel with one another.
/// </summary>
[CollectionDefinition]
public sealed class RomTestCollectionDefinition
{
    private RomTestCollectionDefinition() { }
}
