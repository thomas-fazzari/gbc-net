// Copyright (C) 2026 thomas-fazzari
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Audio;
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
