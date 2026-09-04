// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Unit.RomTesting;

/// <summary>
/// Captures the latest output from one ROM result channel.
/// </summary>
/// <param name="Source">The serial, memory, or register channel that produced the observation.</param>
/// <param name="Status">The terminal status, or <see langword="null"/> while the ROM is running.</param>
/// <param name="Output">Human-readable protocol output.</param>
/// <param name="StatusCode">An optional raw protocol status byte.</param>
internal sealed record RomTestObservation(
    string Source,
    RomTestStatus? Status = null,
    string Output = "",
    byte? StatusCode = null
);
