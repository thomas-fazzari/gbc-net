// Copyright (C) 2026 GBC.Net Contributors
// SPDX-License-Identifier: GPL-3.0-only

using GbcNet.App.Emulation;
using GbcNet.Core.Apu;

namespace GbcNet.Tests.Fakes;

/// <summary>
/// Ignores audio and volume updates while recording how often buffered audio is cleared.
/// </summary>
internal sealed class TestAudioOutput : IAudioOutput
{
    /// <summary>
    /// Gets the number of calls made to <see cref="Clear"/>.
    /// </summary>
    public int ClearCount { get; private set; }

    /// <summary>
    /// Ignores the supplied stereo sample frames.
    /// </summary>
    public void EnqueueSamples(ReadOnlySpan<ApuStereoSample> samples) { }

    /// <summary>
    /// Ignores the supplied volume percentage and mute state.
    /// </summary>
    public void SetVolume(int volumePercent, bool muted) { }

    /// <summary>
    /// Increments <see cref="ClearCount"/>.
    /// </summary>
    public void Clear() => ClearCount++;

    /// <summary>
    /// Performs no cleanup.
    /// </summary>
    public void Dispose() { }
}
