// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.Core.Ppu;

namespace GbcNet.Tests.Unit.RomTesting;

internal sealed record VisualRomTestResult(LcdFrame? Frame, int CompletedFrames, int MachineCycles)
    : IDisposable
{
    public void Dispose() => Frame?.Dispose();
}
