// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu;

/// <summary>
/// PPU engine outputs: requested interrupts, completed frame, and visible HBlank entry.
/// </summary>
internal readonly record struct PpuEngineTickResult(
    PpuInterruptRequest Interrupts,
    LcdFrame? CompletedFrame,
    bool EnteredVisibleHBlank
);
