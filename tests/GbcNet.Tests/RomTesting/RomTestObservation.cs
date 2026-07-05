// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Tests.RomTesting;

internal sealed record RomTestObservation(
    string Source,
    RomTestStatus? Status = null,
    string Output = "",
    byte? StatusCode = null
);
