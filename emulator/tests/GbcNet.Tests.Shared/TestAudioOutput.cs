// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Emulation;
using GbcNet.Core.Apu;

namespace GbcNet.Tests.Shared;

internal sealed class TestAudioOutput : IAudioOutput
{
    public int ClearCount { get; private set; }

    public void EnqueueSamples(ReadOnlySpan<ApuStereoSample> samples) { }

    public void SetVolume(int volumePercent, bool muted) { }

    public void Clear() => ClearCount++;

    public void Dispose() { }
}
