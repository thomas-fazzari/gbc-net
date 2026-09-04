// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Unit.RomTesting.Utils;

/// <summary>
/// Selects the result channels used to detect a test ROM's terminal state.
/// </summary>
internal enum RomTestProtocol
{
    /// <summary>Observe Blargg serial text and external RAM status.</summary>
    Blargg = 0,

    /// <summary>Observe Mooneye register reports and serial bytes.</summary>
    Mooneye = 1,
}
