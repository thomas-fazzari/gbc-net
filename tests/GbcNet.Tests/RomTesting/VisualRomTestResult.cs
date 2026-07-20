// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Ppu;

namespace GbcNet.Tests.RomTesting;

internal sealed record VisualRomTestResult(LcdFrame? Frame, int CompletedFrames, int MachineCycles)
    : IDisposable
{
    public void Dispose() => Frame?.Dispose();
}
