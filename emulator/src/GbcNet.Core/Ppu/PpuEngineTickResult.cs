// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu;

/// <summary>
/// PPU engine outputs: requested interrupts, completed frame, and visible HBlank entry.
/// </summary>
internal readonly record struct PpuEngineTickResult(
    PpuInterruptRequests Interrupts,
    LcdFrame? CompletedFrame,
    bool EnteredVisibleHBlank
);
