// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Unit.RomTesting.Utils.ResultObservers;

/// <summary>
/// Observes one result-reporting channel of a running test ROM.
/// </summary>
internal interface IRomResultObserver
{
    /// <summary>
    /// Reads the channel's current state.
    /// </summary>
    /// <returns>A terminal observation, or <see langword="null"/> while the ROM is running.</returns>
    RomTestObservation? Observe();

    /// <summary>
    /// Gets the latest state available for timeout and failure diagnostics.
    /// </summary>
    RomTestObservation Snapshot { get; }
}
