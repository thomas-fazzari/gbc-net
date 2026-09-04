// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Unit.RomTesting;

/// <summary>
/// Describes the terminal outcome of an emulated test ROM.
/// </summary>
internal enum RomTestStatus
{
    /// <summary>The ROM reported success.</summary>
    Passed = 0,

    /// <summary>The ROM reported failure or its result channels disagreed.</summary>
    Failed = 1,

    /// <summary>The ROM did not finish within its M-cycle budget.</summary>
    TimedOut = 2,
}
