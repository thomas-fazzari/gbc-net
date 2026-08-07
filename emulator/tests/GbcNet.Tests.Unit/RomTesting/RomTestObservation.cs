// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.Unit.RomTesting;

internal sealed record RomTestObservation(
    string Source,
    // Null until a terminal result is observed.
    // RomTestResult.Statusis the final aggregated verdict.
    RomTestStatus? Status = null,
    string Output = "",
    byte? StatusCode = null
);
