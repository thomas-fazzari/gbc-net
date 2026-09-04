// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Ppu;

namespace GbcNet.Tests.Unit.RomTesting;

/// <summary>
/// Holds the latest frame and progress counters from a visual ROM run.
/// </summary>
/// <param name="Frame">The latest completed frame, or <see langword="null"/> if none completed.</param>
/// <param name="CompletedFrames">The number of completed frames.</param>
/// <param name="MachineCycles">The number of emulated M-cycles completed.</param>
internal sealed record VisualRomTestResult(LcdFrame? Frame, int CompletedFrames, int MachineCycles)
    : IDisposable
{
    /// <summary>
    /// Disposes the retained frame when one was completed.
    /// </summary>
    public void Dispose() => Frame?.Dispose();
}
