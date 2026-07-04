// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

namespace GbcNet.Core.Ppu;

/// <summary>
/// PPU controller outputs consumed by the clock: completed frame and visible HBlank entry.
/// </summary>
internal readonly record struct PpuTickResult(LcdFrame? CompletedFrame, bool EnteredVisibleHBlank);
